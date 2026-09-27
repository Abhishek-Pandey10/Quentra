using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Quentra.Application;
using Quentra.Core;

namespace Quentra.Infrastructure;

public sealed record RunPackage(int SchemaVersion, string SnapshotSha256,
    string CalculationSha256, ModelSnapshot Snapshot, CalculationResult Result);

public static class SnapshotJson
{
    // Sized for tall buildings: a pretty-printed 70-storey, 84k-element snapshot is about 144 MB. The limit guards
    // against opening the wrong file, not against real models.
    public const long MaximumSnapshotBytes = 192 * 1024 * 1024;
    // A run embeds its snapshot plus per-element results and is written indented: up to about four times a compact
    // snapshot. A snapshot at its limit must still produce a run every later command can open, and a run is read
    // as one string, which .NET caps just under 1 GiB of characters.
    public const long MaximumRunBytes = 1000L * 1024 * 1024;
    private static readonly JsonSerializerOptions Compact = CreateOptions(false);
    private static readonly JsonSerializerOptions Indented = CreateOptions(true);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            NewLine = "\n", // Identical file bytes on every platform.
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 32
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        options.Converters.Add(new UtcDateTimeConverter());
        return options;
    }

    public static ModelSnapshot ParseSnapshot(string json)
    {
        RejectDuplicateProperties(json);
        var snapshot = JsonSerializer.Deserialize<ModelSnapshot>(json, Compact)
            ?? throw new JsonException("Snapshot cannot be null.");
        CalculateSnapshot.Validate(snapshot);
        return CanonicalSnapshot(snapshot);
    }

    public static RunPackage Calculate(ModelSnapshot snapshot)
    {
        CalculateSnapshot.Validate(snapshot);
        snapshot = CanonicalSnapshot(snapshot);
        var result = CalculateSnapshot.Run(snapshot);
        var sourceHash = Hash(JsonSerializer.Serialize(snapshot, Compact));
        return new RunPackage(1, sourceHash, ResultHash(sourceHash, result), snapshot, result);
    }

    public static RunPackage Replay(string json)
    {
        RejectDuplicateProperties(json);
        var package = JsonSerializer.Deserialize<RunPackage>(json, Compact)
            ?? throw new JsonException("Run package cannot be null.");
        if (package.SchemaVersion != 1 || package.Snapshot is null || package.Result is null)
            throw new InvalidDataException("Unsupported or incomplete run package.");
        if (package.Result.CalculationVersion != CalculateSnapshot.Version)
            throw new InvalidDataException("This calculation version cannot be replayed by the current engine.");
        var actual = Calculate(package.Snapshot);
        if (package.SnapshotSha256 != actual.SnapshotSha256 ||
            package.CalculationSha256 != ResultHash(package.SnapshotSha256, package.Result))
            throw new InvalidDataException("Run package integrity check failed: snapshot or result changed.");
        if (actual.CalculationSha256 != package.CalculationSha256)
            throw new InvalidDataException("Saved quantities do not reproduce with the declared calculation version.");
        return actual;
    }

    public static string Serialize(RunPackage package) => JsonSerializer.Serialize(package, Indented) + "\n";

    public static async Task<string> ReadAsync(string path, long maximumBytes, CancellationToken cancellationToken = default)
    {
        var tooLarge = $"{Path.GetFileName(path)} is larger than the {maximumBytes / (1024 * 1024)} MiB limit for this input.";
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes)
            throw new InvalidDataException(tooLarge);
        using var buffer = new MemoryStream((int)stream.Length); // One allocation, not repeated doubling.
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > maximumBytes)
                throw new InvalidDataException(tooLarge);
            buffer.Write(chunk, 0, count);
        }
        try { return new UTF8Encoding(false, true).GetString(buffer.GetBuffer(), 0, (int)buffer.Length).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException) { throw new InvalidDataException($"{Path.GetFileName(path)} is not UTF-8 text. Save it as a UTF-8 JSON file."); }
    }

    // The completed file appears only after a successful write. Existing reports are
    // deliberately preserved; callers choose a new destination for another run.
    public static async Task WriteAsync(string path, RunPackage package, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        try { Directory.CreateDirectory(directory); }
        catch (IOException e) { throw FolderFailed(directory, e); }
        var temporary = Path.Combine(directory, $".quentra-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, Serialize(package), new UTF8Encoding(false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: false);
        }
        catch (IOException e) { throw WriteFailed(fullPath, e); }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    // The temporary file's name means nothing to the user; name the file they asked for.
    public static IOException WriteFailed(string path, Exception inner) => new(File.Exists(path)
        ? $"{path} already exists. Choose a new path; existing runs and reports are never overwritten."
        : $"Could not write {path}. Check that the folder is writable and has free space.", inner);

    // The OS message ("Read-only file system : '/x'") does not say what Quentra was trying to do.
    public static IOException FolderFailed(string folder, Exception inner) =>
        new($"Could not create the folder {folder}. Choose a location you can write to.", inner);

    private static ModelSnapshot CanonicalSnapshot(ModelSnapshot snapshot) => snapshot with
    {
        Frames = snapshot.Frames.OrderBy(x => x.ObjectId, StringComparer.Ordinal).ToImmutableArray()
    };

    private static string ResultHash(string sourceHash, CalculationResult result) =>
        Hash(sourceHash + "\n" + JsonSerializer.Serialize(result, Compact));

    private static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static void RejectDuplicateProperties(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        Check(document.RootElement);
        static void Check(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException($"Duplicate property: {property.Name}.");
                    Check(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Check(item);
        }
    }
}
