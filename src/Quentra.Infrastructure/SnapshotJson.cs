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
    public const long MaximumInputBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions Compact = CreateOptions(false);
    private static readonly JsonSerializerOptions Indented = CreateOptions(true);

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = indented,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 32
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
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

    public static async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumInputBytes)
            throw new InvalidDataException("Input exceeds the initial 16 MiB snapshot limit.");
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > MaximumInputBytes)
                throw new InvalidDataException("Input exceeds the initial 16 MiB snapshot limit.");
            buffer.Write(chunk, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    // The completed file appears only after a successful write. Existing reports are
    // deliberately preserved; callers choose a new destination for another run.
    public static async Task WriteAsync(string path, RunPackage package, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".quentra-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, Serialize(package), new UTF8Encoding(false), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

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
