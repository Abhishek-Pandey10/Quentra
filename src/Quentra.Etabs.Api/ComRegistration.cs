using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Quentra.Etabs.Api;

// Calls into a running ETABS cross a process boundary, and Windows marshals them using the ETABSv1 type library
// registered on the machine, which is the last ETABS version registered (not necessarily the one Quentra talks to).
// That is only correct when every interface Quentra calls has the same method layout in the registered version, or
// the registered version only appends methods. Checked on ETABS 22.7 with 23.1.1 registered: all 135 interfaces
// match or append. This check repeats the comparison at connection time for the interfaces Quentra uses.
internal static class ComRegistration
{
    private const string TypeLibrary = "{542F7A9D-3A7D-4061-97B3-3A1276FF83BD}";
    private static readonly string[] UsedInterfaces =
        ["cOAPI", "cSapModel", "cFrameObj", "cAreaObj", "cPropFrame", "cPropArea", "cPropMaterial", "cPropRebar", "cStory", "cTower", "cDesignConcrete"];

    public static string? RegisteredTypeLibrary()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return Read();

        [SupportedOSPlatform("windows")]
        static string? Read()
        {
            using var key = Registry.ClassesRoot.OpenSubKey($@"TypeLib\{TypeLibrary}\1.0\0\win64");
            return key?.GetValue(null) as string;
        }
    }

    public static string? Problem(string installDirectory)
    {
        var tlb = RegisteredTypeLibrary();
        if (tlb is null)
            return "The ETABS API is not registered on this computer (no ETABSv1 type library). Register it by running ETABS once as administrator, or with RegisterETABS.exe in the ETABS folder.";
        var registered = Path.Combine(Path.GetDirectoryName(tlb)!, "ETABSv1.dll");
        var local = Path.Combine(installDirectory, "ETABSv1.dll");
        if (!File.Exists(registered))
            return $"The registered ETABS API type library ({tlb}) has no ETABSv1.dll beside it, so Quentra cannot confirm that calls to this ETABS are marshalled correctly.";
        try
        {
            var mine = Layouts(local); var theirs = Layouts(registered);
            foreach (var name in UsedInterfaces)
            {
                if (!mine.TryGetValue(name, out var a) || !theirs.TryGetValue(name, out var b) || a.Length > b.Length || !a.SequenceEqual(b.Take(a.Length)))
                    return $"The ETABS API registered on this computer ({tlb}) lays out interface {name} differently from the API of the ETABS being used ({local}). " +
                        "Calls could reach the wrong method, so Quentra will not connect. Re-register the ETABS version you are using (RegisterETABS.exe in its folder, as administrator).";
            }
            return null;
        }
        catch (Exception e) when (e is IOException or BadImageFormatException or ReflectionTypeLoadException)
        {
            return $"Quentra could not compare the registered ETABS API ({registered}) with {local}: {e.Message}";
        }
    }

    private static Dictionary<string, string[]> Layouts(string path)
    {
        var context = new AssemblyLoadContext("layout:" + path, isCollectible: true);
        try
        {
            using var stream = File.OpenRead(path);
            var assembly = context.LoadFromStream(stream);
            return UsedInterfaces.Select(n => assembly.GetType("ETABSv1." + n)).Where(t => t is not null)
                .ToDictionary(t => t!.Name, t => t!.GetMethods().Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}):{m.ReturnType.Name}").ToArray());
        }
        finally { context.Unload(); }
    }
}
