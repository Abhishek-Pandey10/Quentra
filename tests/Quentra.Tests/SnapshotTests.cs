using System.Collections.Immutable;
using System.Text.Json;
using Quentra.Core;
using Quentra.Infrastructure;

namespace Quentra.Tests;

public class SnapshotTests
{
    [Fact]
    public void RunRoundTripReproducesQuantitiesHashesAndSourceUnits()
    {
        var original = SnapshotJson.Calculate(FrameCalculationTests.Fixture());
        var saved = SnapshotJson.Serialize(original);
        var replayed = SnapshotJson.Replay(saved);
        Assert.Equal(saved, SnapshotJson.Serialize(replayed));
        Assert.Equal(LengthUnit.Millimetre, replayed.Snapshot.Frames[0].Section!.Width!.Unit);
        Assert.Equal(300, replayed.Snapshot.Frames[0].Section!.Width!.Value);
        Assert.Null(replayed.Result.Frames[0].SteelMassKg);
    }

    [Fact]
    public void InventoryOrderDoesNotChangeHashes()
    {
        var source = FrameCalculationTests.Fixture();
        var a = SnapshotJson.Calculate(source);
        var b = SnapshotJson.Calculate(source with { Frames = source.Frames.Reverse().ToImmutableArray() });
        Assert.Equal(a.SnapshotSha256, b.SnapshotSha256);
        Assert.Equal(a.CalculationSha256, b.CalculationSha256);
    }

    [Theory]
    [InlineData("\"value\": 300", "\"value\": 350")]
    [InlineData("\"reviewStatus\": \"Draft\"", "\"reviewStatus\": \"Accepted\"")]
    [InlineData("\"cubicMetres\": 1.08", "\"cubicMetres\": 2.08")]
    public void ModifiedSourcesAndResultsFailIntegrityChecks(string before, string after)
    {
        var json = SnapshotJson.Serialize(SnapshotJson.Calculate(FrameCalculationTests.Fixture()));
        Assert.Contains(before, json);
        Assert.Throws<InvalidDataException>(() => SnapshotJson.Replay(json.Replace(before, after)));
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":2}")]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("null")]
    public void MalformedSnapshotsAreRejected(string json) =>
        Assert.Throws<JsonException>(() => SnapshotJson.ParseSnapshot(json));

    [Theory]
    [InlineData("\"x\": 0,", "")]
    [InlineData("\"isStraight\": true,", "")]
    [InlineData("\"coordinateUnit\": \"Metre\"", "\"coordinateUnit\": 0")]
    [InlineData("\"coordinateUnit\": \"Metre\"", "\"coordinateUnit\": \"Furlong\"")]
    public void MissingCoordinatesFlagsAndInvalidUnitsDoNotDefaultSilently(string before, string after)
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "frames.json"));
        Assert.Contains(before, json);
        Assert.Throws<JsonException>(() => SnapshotJson.ParseSnapshot(json.Replace(before, after)));
    }

    [Fact]
    public void UnsupportedSchemaFailsBeforeCalculation()
    {
        var source = FrameCalculationTests.Fixture() with { SchemaVersion = 2 };
        Assert.Throws<ArgumentException>(() => SnapshotJson.Calculate(source));
    }

    [Fact]
    public async Task AtomicExportPreservesExistingReportAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quentra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "run.json");
            var package = SnapshotJson.Calculate(FrameCalculationTests.Fixture());
            await SnapshotJson.WriteAsync(path, package);
            var first = await File.ReadAllTextAsync(path);
            await Assert.ThrowsAsync<IOException>(() => SnapshotJson.WriteAsync(path, package));
            Assert.Equal(first, await File.ReadAllTextAsync(path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Equal(package.CalculationSha256, SnapshotJson.Replay(await SnapshotJson.ReadAsync(path)).CalculationSha256);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task CancelledExportDoesNotPublishReport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "quentra-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "run.json");
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SnapshotJson.WriteAsync(path,
                SnapshotJson.Calculate(FrameCalculationTests.Fixture()), cancel.Token));
            Assert.False(File.Exists(path));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
}
