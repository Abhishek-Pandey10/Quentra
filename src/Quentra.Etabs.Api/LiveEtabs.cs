using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Quentra.Etabs.Api;

// Entry point for everything that talks to a running ETABS. Callers never see CSI types.
public static class LiveEtabs
{
#if ETABS_API
    public static bool ApiCompiled => true;
#else
    public static bool ApiCompiled => false;
#endif

    public static string? LoadedApiAssembly => EtabsApiLoader.Loaded?.Location;
    // The ETABSv1 type library Windows uses to marshal calls to any ETABS (the last version registered).
    public static string? RegisteredApi => ComRegistration.RegisteredTypeLibrary();

    // Attaches to a running ETABS by process id. Read-only: nothing in the model is changed.
    public static IEtabsConnection Connect(EtabsInstance instance)
    {
        RequireApi();
        if (!OperatingSystem.IsWindows()) throw new EtabsUnavailableException("ETABS runs on Windows only.");
        var directory = Path.GetDirectoryName(instance.ExePath) ?? throw new EtabsUnavailableException($"ETABS process {instance.ProcessId} has no known program folder.");
        EtabsApiLoader.Ensure(directory);
        if (ComRegistration.Problem(directory) is { } problem) throw new EtabsUnavailableException(problem);
        return Attach(instance);
    }

    // Starts a new ETABS from the given ETABS.exe and returns a connection that owns it; disposing the connection
    // closes that ETABS without saving. Used by the controlled-model tools and integration tests, never on a user's model.
    public static IEtabsConnection Launch(string exePath)
    {
        RequireApi();
        if (!OperatingSystem.IsWindows()) throw new EtabsUnavailableException("ETABS runs on Windows only.");
        EtabsApiLoader.Ensure(Path.GetDirectoryName(exePath)!);
        if (ComRegistration.Problem(Path.GetDirectoryName(exePath)!) is { } problem) throw new EtabsUnavailableException(problem);
        return Start(exePath);
    }

    // Loads ETABSv1.dll from an ETABS installation folder before any other use of the API (tools that call CSI types directly).
    public static void LoadApi(string installDirectory)
    {
        RequireApi();
        EtabsApiLoader.Ensure(installDirectory);
    }

    private static void RequireApi()
    {
        if (!ApiCompiled)
            throw new EtabsUnavailableException("This Quentra build was compiled without the ETABS API (ETABS 22.7 was not installed on the build machine). Use a Windows build made with ETABS 22.7 installed.");
    }

    // The methods below reference ETABSv1 types, so they are kept out of line: the runtime loads ETABSv1.dll when it
    // compiles them, which must happen only after EtabsApiLoader has chosen the folder to load it from.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEtabsConnection Attach(EtabsInstance instance)
#if ETABS_API
        => LiveConnection.Attach(instance);
#else
        => throw new EtabsUnavailableException("ETABS API unavailable.");
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IEtabsConnection Start(string exePath)
#if ETABS_API
        => LiveConnection.Launch(exePath);
#else
        => throw new EtabsUnavailableException("ETABS API unavailable.");
#endif
}

// Loads ETABSv1.dll from one ETABS installation folder, and refuses to switch to another in the same process.
internal static class EtabsApiLoader
{
    private static readonly Lock Gate = new();
    private static string? directory;

    // The copy in the default context is the one API calls use (ComRegistration loads others only to compare layouts).
    public static Assembly? Loaded => AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "ETABSv1");

    public static void Ensure(string installDirectory)
    {
        var full = Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar);
        lock (Gate)
        {
            if (directory is not null)
            {
                if (!string.Equals(directory, full, StringComparison.OrdinalIgnoreCase))
                    throw new EtabsUnavailableException($"This Quentra process already uses the ETABS API from {directory}. Restart Quentra to connect to the ETABS in {full}.");
                return;
            }
            if (!File.Exists(Path.Combine(full, "ETABSv1.dll")))
                throw new EtabsUnavailableException($"{full} has no ETABSv1.dll, so its ETABS API cannot be used.");
            directory = full;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
                name.Name is "ETABSv1" && File.Exists(Path.Combine(full, "ETABSv1.dll")) ? context.LoadFromAssemblyPath(Path.Combine(full, "ETABSv1.dll")) : null;
        }
    }

    public static (string Path, string Version) Describe()
    {
        var assembly = Loaded ?? throw new EtabsUnavailableException("The ETABS API assembly is not loaded.");
        return (assembly.Location, FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion ?? "unknown");
    }
}
