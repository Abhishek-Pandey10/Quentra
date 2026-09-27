using System.IO.Compression;
using Quentra.Etabs;
using Quentra.Gui;
using Quentra.Infrastructure;

namespace Quentra.Tests;

// The GUI's ETABS workflow, driven by ETABS 22.7 captures instead of a live ETABS, and its saved runs.
public sealed class GuiEtabsTests : IDisposable
{
    private readonly string data = Path.Combine(Path.GetTempPath(), "quentra-gui-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(data)) Directory.Delete(data, recursive: true); }

    private static EtabsRawModel Capture(string name) =>
        EtabsRawJson.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "etabs", name + ".etabs-raw.json")));

    // Returns the captures in order, one per extraction (the last one repeats).
    private sealed class Captured(params string[] names) : IEtabsGateway
    {
        private int next;
        public EtabsStatusView Status(int? pid) => new(true, "ETABS 22.7", [], [], null, "captured");
        public EtabsRawModel Extract(int? pid, bool allowUntested, CancellationToken token) => Capture(names[Math.Min(next++, names.Length - 1)]);
    }

    private static EtabsExtractRequest Request(string? policy = "P. Engineer") => new(null, false, "S. Engineer", policy, null);

    [Fact]
    public void ExtractionCalculatesAndSavesTheCaptureAndSnapshot()
    {
        var session = new GuiSession(data, new Captured("08-one-story-frame"));
        var response = session.EtabsExtract(Request());
        Assert.Equal("17.240 m³", response.Run.View.Totals.KnownGross);
        Assert.Equal("Draft", response.Run.View.ReviewStatus);
        Assert.StartsWith("DRAFT", response.Run.View.StatusBanner, StringComparison.Ordinal);
        Assert.Equal("ETABS 22.7.0", response.Run.View.Source!.Program);
        Assert.True(response.Run.View.Source.Tested);
        Assert.Equal(4, response.Extraction.Counts.Beams);
        Assert.Contains(response.Extraction.Warnings, w => w.Code == "ETABS_SOURCE");
        Assert.StartsWith("Ready to calculate", response.Extraction.Status, StringComparison.Ordinal);
        var saved = Directory.GetFiles(response.Extraction.SavedTo).Select(Path.GetFileName).ToArray();
        Assert.Contains(saved, x => x!.EndsWith(".etabs-raw.json", StringComparison.Ordinal));
        Assert.Contains(saved, x => x!.EndsWith(".snapshot.json", StringComparison.Ordinal));
        Assert.All(response.Run.View.Elements, e => Assert.StartsWith("ETABS ", e.Source, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARunWhoseEtabsModelChangedCannotBeAcceptedOrExported()
    {
        // The second extraction is the same model after ETABS rewrote the wall around its door.
        var session = new GuiSession(data, new Captured("10-meshed", "10-meshed.after-mesh"));
        var run = session.EtabsExtract(Request()).Run;
        var checkedRun = session.EtabsCheck(new(run.RunId, null, false));
        Assert.False(checkedRun.View.SourceCheck!.Matches);
        Assert.Contains("changed", checkedRun.View.SourceCheck.Message, StringComparison.Ordinal);
        var tokens = checkedRun.View.Acknowledgements.Select(x => x.Token).ToArray();
        Assert.Contains("ETABS model has changed", Assert.Throws<ArgumentException>(() => session.Accept(new(run.RunId, "R. Reviewer", "Checked", true, tokens))).Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ArgumentException>(() => session.Export(run.RunId));
        // An override of a stale run is still stale: it has the same snapshot.
        var element = checkedRun.View.Elements.First(e => e.Category == "Slab").ObjectId;
        var overridden = session.Override(new(run.RunId, element, "exclude", null, null, null, "test", "A. Engineer"));
        Assert.False(overridden.View.SourceCheck!.Matches);
    }

    [Fact]
    public void AnUnchangedModelChecksAsMatching()
    {
        // The same model read in other ETABS units must not look changed.
        var session = new GuiSession(data, new Captured("11-units.kN-m", "11-units.lb-in"));
        var run = session.EtabsExtract(Request()).Run;
        Assert.True(session.EtabsCheck(new(run.RunId, null, false)).View.SourceCheck!.Matches);
    }

    [Fact]
    public async Task AcceptedRunsAreSavedWithIdentityAndReopenFromDisk()
    {
        var session = new GuiSession(data, new Captured("08-one-story-frame"));
        var run = session.EtabsExtract(Request()).Run;
        var accepted = session.Accept(new(run.RunId, "R. Reviewer", "Checked against drawings", true, [.. run.View.Acknowledgements.Select(x => x.Token)]));
        Assert.Equal("AcceptedPartial", accepted.View.ReviewStatus);
        Assert.NotNull(accepted.SavedPath);
        Assert.True(File.Exists(accepted.SavedPath));
        Assert.Contains("ACCEPTED-PARTIAL", Path.GetFileName(accepted.SavedPath), StringComparison.Ordinal);
        var review = Assert.Single(accepted.View.Reviews);
        Assert.Equal(Environment.UserName, review.Account);
        Assert.Equal(Environment.MachineName, review.Computer);
        // Saved to disk, so it is not pinned in memory waiting for a download.
        Assert.False(accepted.Held.Single(x => x.RunId == accepted.RunId).Pinned);

        var fresh = new GuiSession(data, null);
        var listed = Assert.Single(fresh.SavedRuns());
        var reopened = fresh.OpenSaved(listed.Name);
        Assert.Equal("AcceptedPartial", reopened.View.ReviewStatus);
        Assert.Throws<ArgumentException>(() => fresh.OpenSaved("..\\outside.run.json"));

        var (zip, name) = await fresh.Export(reopened.RunId);
        Assert.EndsWith("-report-ACCEPTED-PARTIAL.zip", name, StringComparison.Ordinal);
        using var archive = new ZipArchive(new MemoryStream(zip));
        Assert.Contains(archive.Entries, x => x.FullName == "report-ACCEPTED-PARTIAL.xlsx");
        var summary = new StreamReader(archive.Entries.Single(x => x.FullName == "Summary.csv").Open()).ReadToEnd();
        Assert.Contains("ACCEPTED AS PARTIAL SCOPE by R. Reviewer", summary, StringComparison.Ordinal);
        Assert.Contains("ETABS 22.7.0 (build 22.7.0.4095)", summary, StringComparison.Ordinal);
        var reviews = new StreamReader(archive.Entries.Single(x => x.FullName == "Review_History.csv").Open()).ReadToEnd();
        Assert.Contains("recorded, not verified", reviews, StringComparison.Ordinal);
        Assert.Contains(Environment.MachineName, reviews, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedExtractionIsExplainedAndNotCalculated()
    {
        // Two stories at the same elevation cannot form bands: the snapshot is saved for inspection, not calculated.
        var raw = Capture("09-multi-story");
        raw = raw with { Stories = raw.Stories with { Stories = [.. raw.Stories.Stories.Select(s => s.Name == "Story3" ? s with { Elevation = 7.2 } : s)] } };
        var session = new GuiSession(data, new Fixed(raw));
        var e = Assert.Throws<ArgumentException>(() => session.EtabsExtract(Request()));
        Assert.Contains("blocking error", e.Message, StringComparison.Ordinal);
        Assert.Contains("Story3", e.Message, StringComparison.Ordinal);
        Assert.Empty(session.HeldRuns());
    }

    private sealed class Fixed(EtabsRawModel raw) : IEtabsGateway
    {
        public EtabsStatusView Status(int? pid) => new(true, "ETABS 22.7", [], [], null, null);
        public EtabsRawModel Extract(int? pid, bool allowUntested, CancellationToken token) => raw;
    }

    [Fact]
    public void GuiWithoutEtabsSaysSo()
    {
        var session = new GuiSession(data, null);
        Assert.Contains("not available", Assert.Throws<ArgumentException>(() => session.EtabsExtract(Request())).Message, StringComparison.Ordinal);
    }
}
