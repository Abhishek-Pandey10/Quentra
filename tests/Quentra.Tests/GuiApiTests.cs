using System.IO.Compression;
using System.Text;
using Quentra.Gui;

namespace Quentra.Tests;

public class GuiApiTests
{
    private readonly GuiSession session = new();
    private RunResponse Template() => session.Calculate(GuiSession.Template());

    [Fact]
    public void TemplateCalculatesWithTheCliRounding()
    {
        var view = Template().View;
        Assert.Equal("18.408 m³", view.Totals.KnownGross);
        Assert.Equal("unknown", view.Totals.CompleteSteel);
        Assert.Equal("Draft", view.ReviewStatus);
        Assert.Contains(view.Acknowledgements, x => x.Token == "PARTIAL_STEEL" && x.Meaning.Length > 0);
        Assert.Contains(view.Elements.Single(x => x.ObjectId == "B1").Fields, x => x.Field == "FrameDepthM" && x.CurrentMm == "600 mm");
    }

    [Theory]
    [InlineData("{ \"schemaVersion\": 2,", "Snapshot: not valid JSON")]
    [InlineData("{ \"schemaVersion\": 1 }", "schema 1")]
    [InlineData("", "empty")]
    public void BadSnapshotsAreExplained(string snapshot, string expected)
    {
        var e = Assert.Throws<ArgumentException>(() => session.Calculate(snapshot));
        Assert.Contains(expected, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DownloadedRunsReopenAndAreNotConfusedWithSnapshots()
    {
        var run = Encoding.UTF8.GetString(session.Download(Template().RunId).Json);
        Assert.Equal("18.408 m³", session.Open(run).View.Totals.KnownGross);
        Assert.Contains("Open run", Assert.Throws<ArgumentException>(() => session.Calculate(run)).Message, StringComparison.Ordinal);
        Assert.Contains("Open snapshot", Assert.Throws<ArgumentException>(() => session.Open(GuiSession.Template())).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => session.Open(run.Replace("\"Synthetic Reviewer\"", "\"Someone Else\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void UnknownRunIdsAreExplained() =>
        Assert.Contains("no longer held", Assert.Throws<ArgumentException>(() => session.Download("missing")).Message, StringComparison.Ordinal);

    [Fact]
    public void OverridesAndAcceptanceProduceNewRuns()
    {
        var run = Template().RunId;
        var changed = session.Override(new(run, "B1", "set", "FrameDepthM", 700, "mm", "Revised drawing", "A. Engineer"));
        Assert.Equal("depth 600 mm → 700 mm", Assert.Single(changed.View.Overrides).Change);
        Assert.Equal("18.588 m³", changed.View.Totals.KnownGross);

        var excluded = session.Override(new(changed.RunId, "W1", "exclude", null, null, null, "Not in this package", "A. Engineer"));
        Assert.True(excluded.View.Elements.Single(x => x.ObjectId == "W1").Excluded);
        Assert.Throws<ArgumentException>(() => session.Override(new(run, "B1", "set", "FrameDepthM", 700, "mm", " ", "A. Engineer")));

        var tokens = excluded.View.Acknowledgements.Select(x => x.Token).ToArray();
        Assert.Throws<ArgumentException>(() => session.Accept(new(excluded.RunId, "R. Reviewer", "Checked", false, tokens)));
        var accepted = session.Accept(new(excluded.RunId, "R. Reviewer", "Checked", true, tokens));
        Assert.Equal("AcceptedPartial", accepted.View.ReviewStatus);
        Assert.True(Assert.Single(accepted.View.Reviews).Current);
    }

    [Fact]
    public async Task ExportIsAZipOfTheReportPackage()
    {
        var (zip, name) = await session.Export(Template().RunId);
        Assert.EndsWith("-report-DRAFT.zip", name, StringComparison.Ordinal);
        using var archive = new ZipArchive(new MemoryStream(zip));
        Assert.Contains(archive.Entries, x => x.FullName == "manifest.json");
        Assert.Contains(archive.Entries, x => x.FullName == "report-DRAFT.xlsx");
    }

    [Fact]
    public void AcceptedRunsAreKeptUntilDownloadedAndDropsAreReported()
    {
        var template = Template();
        var accepted = session.Accept(new(template.RunId, "R. Reviewer", "Checked", true, [.. template.View.Acknowledgements.Select(x => x.Token)]));
        var latest = accepted;
        var dropped = new List<string>();
        for (var i = 0; i < GuiSession.Capacity + 2; i++)
        {
            latest = session.Override(new(latest.RunId, "B1", "set", "FrameDepthM", 610 + i, "mm", "Revised drawing", "A. Engineer"));
            dropped.AddRange(latest.Dropped);
        }
        // The undownloaded draft calculation was dropped and reported; the acceptance is still held.
        Assert.Contains(dropped, x => x.Contains("#1 Draft", StringComparison.Ordinal));
        Assert.True(Assert.Single(latest.Held, x => x.RunId == accepted.RunId).Pinned);
        Assert.Equal("AcceptedPartial", session.Show(accepted.RunId).View.ReviewStatus);

        session.Download(accepted.RunId);
        Assert.False(session.HeldRuns().Single(x => x.RunId == accepted.RunId).Pinned);
        for (var i = 0; i < GuiSession.Capacity; i++)
            latest = session.Override(new(latest.RunId, "B1", "set", "FrameDepthM", 700 + i, "mm", "Revised drawing", "A. Engineer"));
        // Once downloaded it is released like any other run, without a warning.
        Assert.DoesNotContain(session.HeldRuns(), x => x.RunId == accepted.RunId);
    }

    [Fact]
    public void ExcludedRunsShowAReducedScopeLabel()
    {
        var excluded = session.Override(new(Template().RunId, "W1", "exclude", null, null, null, "Not in this package", "A. Engineer"));
        Assert.Equal("complete in scope reduced by override (excludes W1 (o1))", excluded.View.CompleteLabel);
        Assert.Equal(["W1 (o1)"], excluded.View.Excluded);
    }

    // The page's script must parse, and every element it looks up by id must exist: either failure would silently
    // disable the GUI in a browser, and nothing else in the test suite runs the page.
    [Fact]
    public void PageScriptParsesAndItsElementIdsExist()
    {
        using var stream = typeof(GuiSession).Assembly.GetManifestResourceStream("Quentra.Gui.index.html")!;
        var page = new StreamReader(stream).ReadToEnd();
        var script = System.Text.RegularExpressions.Regex.Match(page, "(?s)<script>(.*)</script>").Groups[1].Value;
        new Acornima.Parser(new Acornima.ParserOptions { EcmaVersion = Acornima.EcmaVersion.ES2022 }).ParseScript(script);
        var ids = System.Text.RegularExpressions.Regex.Matches(page, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var used = System.Text.RegularExpressions.Regex.Matches(script, "\\$\\(\"([^\"]+)\"\\)").Select(m => m.Groups[1].Value).Distinct().ToArray();
        Assert.NotEmpty(used);
        Assert.DoesNotContain(used, id => !ids.Contains(id));
    }

    [Fact]
    public void PageHasNoBulkAcknowledgementAndMarksEditedSnapshotsStale()
    {
        using var stream = typeof(GuiSession).Assembly.GetManifestResourceStream("Quentra.Gui.index.html")!;
        var page = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("btn-ack-all", page, StringComparison.Ordinal);
        Assert.Contains("$(\"snapshot-text\").oninput", page, StringComparison.Ordinal);
        Assert.Contains("id=\"stale\"", page, StringComparison.Ordinal);
    }
}
