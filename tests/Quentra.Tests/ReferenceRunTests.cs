using Quentra.Infrastructure;

namespace Quentra.Tests;

// Runs in fixtures/reference were calculated on macOS arm64 and are committed. Replaying them
// on each CI platform proves that calculation hashes do not depend on the operating system or CPU.
// Regenerate them with 'quentra calculate' only after an intentional calculation change.
public class ReferenceRunTests
{
    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "reference", name));

    [Fact]
    public void FrameReferenceRunReplaysOnThisPlatform()
    {
        var json = Read("frames-run.json");
        var saved = SnapshotJson.Replay(json);
        Assert.Equal(json.ReplaceLineEndings("\n"), SnapshotJson.Serialize(saved));
    }

    [Theory]
    [InlineData("takeoff-run.json")]
    [InlineData("E-bay-run.json")]
    public void TakeoffReferenceRunReplaysOnThisPlatform(string name)
    {
        var json = Read(name);
        var replayed = TakeoffJson.Replay(json);
        Assert.Equal(json.ReplaceLineEndings("\n"), TakeoffJson.Serialize(replayed));
    }
}
