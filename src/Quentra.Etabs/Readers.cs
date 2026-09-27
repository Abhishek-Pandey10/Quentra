using System.Collections.Immutable;
using System.Diagnostics;

namespace Quentra.Etabs;

// The seam between ETABS and Quentra. The live implementation (Quentra.Etabs.Api) wraps the CSI API; tests use
// in-memory readers. Readers return raw source values only; interpretation happens in EtabsSnapshotBuilder.
public interface IEtabsStoryReader { RawStories Read(); }
public interface IEtabsMaterialReader { ImmutableArray<RawMaterial> Read(); }
public interface IEtabsSectionReader
{
    ImmutableArray<RawFrameSection> ReadFrameSections();
    ImmutableArray<RawAreaProperty> ReadAreaProperties();
}
public interface IEtabsFrameReader { ImmutableArray<RawFrame> Read(CancellationToken token); }
public interface IEtabsAreaReader { ImmutableArray<RawArea> Read(CancellationToken token); }
public interface IEtabsDesignResultReader { RawDesign Read(IReadOnlyList<RawFrame> frames, CancellationToken token); }

public interface IEtabsModelReader
{
    RawModelInfo ReadModelInfo();
    RawUnits ReadPresentUnits();
    IEtabsStoryReader Stories { get; }
    IEtabsMaterialReader Materials { get; }
    IEtabsSectionReader Sections { get; }
    IEtabsFrameReader Frames { get; }
    IEtabsAreaReader Areas { get; }
    IEtabsDesignResultReader Design { get; }
    // Failed calls recorded while reading; they never throw unless ETABS itself is gone.
    IReadOnlyList<ApiIssue> Issues { get; }
}

public interface IEtabsConnection : IDisposable
{
    EtabsInstance Instance { get; }
    IEtabsModelReader Reader { get; }
}

public interface IEtabsSnapshotBuilder
{
    EtabsBuildResult Build(EtabsRawModel raw, EtabsBuildOptions options);
}

// ETABS stopped responding, closed, or has no model: extraction cannot continue and nothing is written.
public sealed class EtabsUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public sealed record StageTiming(string Stage, long Milliseconds);

public static class EtabsExtractor
{
    public static string Version => typeof(EtabsExtractor).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    // Verified on ETABS 22.7: a freshly started ETABS reports "" and any new, unsaved model reports "(Untitled)".
    public static bool IsSavedModel(string? path) => !string.IsNullOrWhiteSpace(path) && path.Trim() != "(Untitled)";

    // Reads everything in a fixed order. Present units are read before and after: ETABS reports values in its
    // present units, so a change during extraction (the user switching units) would mix units, and the capture is refused.
    public static (EtabsRawModel Raw, IReadOnlyList<StageTiming> Timings) Extract(IEtabsModelReader reader, DateTimeOffset extractedAt, CancellationToken token = default)
    {
        var timings = new List<StageTiming>();
        var clock = Stopwatch.StartNew();
        T Stage<T>(string name, Func<T> read)
        {
            token.ThrowIfCancellationRequested();
            var start = clock.ElapsedMilliseconds;
            var value = read();
            timings.Add(new(name, clock.ElapsedMilliseconds - start));
            return value;
        }
        var model = Stage("model information", reader.ReadModelInfo);
        if (!IsSavedModel(model.ModelPath))
            throw new EtabsUnavailableException($"ETABS (process {model.ProcessId}) has no saved model open{(model.ModelPath.Length > 0 ? $" (it reports '{model.ModelPath}')" : "")}. Open or save the model in ETABS, then extract again.");
        var stories = Stage("stories", reader.Stories.Read);
        var materials = Stage("materials", reader.Materials.Read);
        var sections = Stage("frame sections", reader.Sections.ReadFrameSections);
        var areaProperties = Stage("area properties", reader.Sections.ReadAreaProperties);
        var frames = Stage("frames", () => reader.Frames.Read(token));
        var areas = Stage("areas and openings", () => reader.Areas.Read(token));
        var design = Stage("design results", () => reader.Design.Read(frames, token));
        var after = Stage("unit check", reader.ReadPresentUnits);
        if (after != model.PresentUnits)
            throw new EtabsUnavailableException($"ETABS present units changed during extraction ({model.PresentUnits} to {after}). Nothing was written; do not change units while Quentra extracts, then extract again.");
        var raw = new EtabsRawModel
        {
            Format = EtabsRawModel.CurrentFormat, ExtractorVersion = Version, ExtractedAt = extractedAt, Model = model, Stories = stories,
            Materials = [.. materials.OrderBy(x => x.Name, StringComparer.Ordinal)],
            FrameSections = [.. sections.OrderBy(x => x.Name, StringComparer.Ordinal)],
            AreaProperties = [.. areaProperties.OrderBy(x => x.Name, StringComparer.Ordinal)],
            Frames = [.. frames.OrderBy(x => x.Name, StringComparer.Ordinal)],
            Areas = [.. areas.OrderBy(x => x.Name, StringComparer.Ordinal)],
            Design = design with
            {
                Beams = [.. design.Beams.OrderBy(x => x.Frame, StringComparer.Ordinal)],
                Columns = [.. design.Columns.OrderBy(x => x.Frame, StringComparer.Ordinal)]
            },
            Issues = [.. reader.Issues]
        };
        return (raw, timings);
    }
}

// SI factors for the ETABS eForce and eLength names. Every value that enters the snapshot is converted here, once.
public static class EtabsUnits
{
    public static double MetresPer(string length) => length switch
    {
        "inch" => 0.0254, "ft" => 0.3048, "micron" => 1e-6, "mm" => 0.001, "cm" => 0.01, "m" => 1,
        _ => throw new NotSupportedException($"ETABS length unit '{length}' is not recognised; the model cannot be normalised.")
    };

    public static double NewtonsPer(string force) => force switch
    {
        "lb" => 4.4482216152605, "kip" => 4448.2216152605, "N" => 1, "kN" => 1000, "kgf" => 9.80665, "tonf" => 9806.65,
        _ => throw new NotSupportedException($"ETABS force unit '{force}' is not recognised; the model cannot be normalised.")
    };
}
