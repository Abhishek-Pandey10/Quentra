using System.Text.RegularExpressions;
using Quentra.Application;

namespace Quentra.Tests;

public class WarningCatalogueTests
{
    // 'quentra codes' must explain every warning the engines can raise.
    [Fact]
    public void EveryWarningCodeInTheSourceIsCatalogued()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Quentra.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var catalogued = WarningCatalogue.Entries.Select(x => x.Code).ToHashSet(StringComparer.Ordinal);
        var raised = Directory.GetFiles(Path.Combine(root.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !x.EndsWith("WarningCatalogue.cs", StringComparison.Ordinal))
            .SelectMany(x => Regex.Matches(File.ReadAllText(x), "(?:new(?: CalculationWarning)?\\(|Warn\\([^,()]*,\\s*)\"([A-Z]+(?:_[A-Z]+)+)\"").Select(m => m.Groups[1].Value))
            .Append("AREA_UNSUPPORTED").Append("AREA_INVALID") // built as "AREA_" + status
            .ToHashSet(StringComparer.Ordinal);
        Assert.Empty(raised.Except(catalogued));
        Assert.Equal(catalogued.Count, WarningCatalogue.Entries.Count);
    }
}
