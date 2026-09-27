using System.Diagnostics;

namespace Quentra.Etabs;

public enum EtabsSupport { TestedSupported, Untested }

public sealed record EtabsInstallation(string Directory, string ExePath, string Version, string? ApiAssembly, string? ApiVersion)
{
    public EtabsSupport Support => EtabsDiscovery.Classify(Version);
}

public sealed record EtabsInstance(int ProcessId, string? ExePath, string? Version, string WindowTitle)
{
    public EtabsSupport Support => EtabsDiscovery.Classify(Version);
}

// Finds installed and running ETABS copies. ETABS 22.7 is the only tested version; others are reported but
// never chosen silently. Several versions can be installed side by side, and the COM registration names only the
// last one registered, so Quentra attaches by process id and loads ETABSv1.dll from the chosen version's folder.
public static class EtabsDiscovery
{
    public const string TestedVersionPrefix = "22.7.";
    public const string TestedVersionLabel = "22.7";

    public static EtabsSupport Classify(string? version) =>
        version is not null && version.StartsWith(TestedVersionPrefix, StringComparison.Ordinal) ? EtabsSupport.TestedSupported : EtabsSupport.Untested;

    public static string Describe(EtabsSupport support) => support == EtabsSupport.TestedSupported ? "tested and supported" : "untested";

    public static IReadOnlyList<EtabsInstallation> Installations()
    {
        var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetEnvironmentVariable("ProgramW6432") }
            .Where(x => !string.IsNullOrEmpty(x)).Select(x => Path.Combine(x!, "Computers and Structures")).Distinct(StringComparer.OrdinalIgnoreCase);
        var found = new List<EtabsInstallation>();
        foreach (var root in roots.Where(Directory.Exists))
            foreach (var dir in Directory.GetDirectories(root, "ETABS*").Order(StringComparer.OrdinalIgnoreCase))
            {
                var exe = Path.Combine(dir, "ETABS.exe");
                if (!File.Exists(exe)) continue;
                var api = Path.Combine(dir, "ETABSv1.dll");
                found.Add(new(dir, exe, FileVersionInfo.GetVersionInfo(exe).FileVersion ?? "unknown",
                    File.Exists(api) ? api : null, File.Exists(api) ? FileVersionInfo.GetVersionInfo(api).FileVersion : null));
            }
        return found;
    }

    public static IReadOnlyList<EtabsInstance> RunningInstances()
    {
        var list = new List<EtabsInstance>();
        foreach (var process in Process.GetProcessesByName("ETABS").OrderBy(x => x.Id))
        {
            string? path = null, version = null;
            // An ETABS running elevated cannot be inspected from a normal process; it is still listed.
            try { path = process.MainModule?.FileName; version = path is null ? null : FileVersionInfo.GetVersionInfo(path).FileVersion; }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) { }
            list.Add(new(process.Id, path, version, process.MainWindowTitle));
            process.Dispose();
        }
        return list;
    }

    // Picks the instance to attach to, or explains why none can be chosen. Only ETABS 22.7 is chosen automatically;
    // another version needs both --pid and the explicit untested-version switch.
    public static EtabsInstance Choose(IReadOnlyList<EtabsInstance> running, int? pid, bool allowUntested)
    {
        if (running.Count == 0)
            throw new EtabsUnavailableException($"ETABS is not running. Start ETABS {TestedVersionLabel}, open the model, and try again.");
        EtabsInstance chosen;
        if (pid is { } id)
            chosen = running.FirstOrDefault(x => x.ProcessId == id) ?? throw new EtabsUnavailableException(
                $"No ETABS process has id {id}. Running: {string.Join("; ", running.Select(Line))}.");
        else
        {
            var tested = running.Where(x => x.Support == EtabsSupport.TestedSupported).ToArray();
            if (tested.Length > 1)
                throw new EtabsUnavailableException($"Several ETABS {TestedVersionLabel} instances are running: {string.Join("; ", tested.Select(Line))}. Choose one with --pid.");
            if (tested.Length == 0)
                throw new EtabsUnavailableException($"No running ETABS is version {TestedVersionLabel}, the tested version. Running: {string.Join("; ", running.Select(Line))}. " +
                    $"Open the model in ETABS {TestedVersionLabel}. To try another version anyway, give --pid and --allow-untested-version; the snapshot will be marked untested.");
            chosen = tested[0];
        }
        if (chosen.ExePath is null)
            throw new EtabsUnavailableException($"ETABS process {chosen.ProcessId} cannot be inspected (it may be running as administrator). Run Quentra the same way as ETABS.");
        if (chosen.Support != EtabsSupport.TestedSupported && !allowUntested)
            throw new EtabsUnavailableException($"ETABS process {chosen.ProcessId} is version {chosen.Version}, which is untested; Quentra is tested with ETABS {TestedVersionLabel}. " +
                "Open the model in ETABS 22.7, or add --allow-untested-version to extract anyway (the snapshot and every report will say so).");
        return chosen;
    }

    public static string Line(EtabsInstance x) =>
        $"process {x.ProcessId}, ETABS {x.Version ?? "version unknown"}{(x.WindowTitle.Length > 0 ? $" '{x.WindowTitle.Trim()}'" : "")}";
}
