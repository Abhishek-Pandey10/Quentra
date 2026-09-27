using Quentra.Etabs;
using Quentra.Etabs.Api;

namespace Quentra.Gui;

public sealed record EtabsInstallView(string Version, string Support, string Directory);
public sealed record EtabsInstanceView(int ProcessId, string? Version, string Support, string Title);
public sealed record EtabsConnectionView(int ProcessId, string Version, string Build, string Model, bool ModelSaved, string Units, bool Locked, int Stories, string Api);
public sealed record EtabsStatusView(bool ApiAvailable, string Target, EtabsInstallView[] Installed, EtabsInstanceView[] Running, EtabsConnectionView? Connection, string? Problem);

// The GUI's access to ETABS. The live gateway attaches to a running ETABS 22.7; tests supply captured models.
public interface IEtabsGateway
{
    EtabsStatusView Status(int? pid);
    EtabsRawModel Extract(int? pid, bool allowUntested, CancellationToken token);
}

public sealed class LiveEtabsGateway : IEtabsGateway
{
    public EtabsStatusView Status(int? pid)
    {
        var installed = EtabsDiscovery.Installations().Select(x => new EtabsInstallView(x.Version, EtabsDiscovery.Describe(x.Support), x.Directory)).ToArray();
        var running = EtabsDiscovery.RunningInstances();
        var runningViews = running.Select(x => new EtabsInstanceView(x.ProcessId, x.Version, EtabsDiscovery.Describe(x.Support), x.WindowTitle.Trim())).ToArray();
        var target = $"ETABS {EtabsDiscovery.TestedVersionLabel}";
        if (!LiveEtabs.ApiCompiled) return new(false, target, installed, runningViews, null, "This Quentra build has no ETABS API. Use a Windows build made with ETABS 22.7 installed.");
        try
        {
            var instance = EtabsDiscovery.Choose(running, pid, allowUntested: false);
            using var connection = LiveEtabs.Connect(instance);
            var info = connection.Reader.ReadModelInfo();
            var stories = connection.Reader.Stories.Read();
            return new(true, target, installed, runningViews, new(info.ProcessId, info.ProgramVersion, info.ProgramBuild, info.ModelPath,
                EtabsExtractor.IsSavedModel(info.ModelPath), info.PresentUnits.ToString(), info.ModelLocked, stories.Stories.Length,
                $"{info.ApiAssembly} {info.ApiAssemblyVersion}"), null);
        }
        catch (EtabsUnavailableException e) { return new(true, target, installed, runningViews, null, e.Message); }
    }

    public EtabsRawModel Extract(int? pid, bool allowUntested, CancellationToken token)
    {
        var instance = EtabsDiscovery.Choose(EtabsDiscovery.RunningInstances(), pid, allowUntested);
        using var connection = LiveEtabs.Connect(instance);
        return EtabsExtractor.Extract(connection.Reader, DateTimeOffset.UtcNow, token).Raw;
    }
}
