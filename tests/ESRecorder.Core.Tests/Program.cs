using System.Collections.Concurrent;
using ESRecorder.BeamNG;
using ESRecorder.Core;

namespace ESRecorder.Core.Tests;

internal static class Program
{
    private static async Task<int> Main()
    {
        try
        {
            TestDeterministicPlan();
            TestDuplicateThrottleRejected();
            await TestCoordinatorAndArtifactsAsync().ConfigureAwait(false);
            Console.WriteLine("PASS: headless recorder core contract tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
    }

    private static void TestDeterministicPlan()
    {
        var request = CreateRequest(Path.Combine(Path.GetTempPath(), "esrecorder-plan"));
        var plan = RecordingPlanBuilder.Build(request);

        AssertEqual(2, plan.WorkerAssignments.Count, "worker count");
        AssertEqual(3, plan.WorkerAssignments[0].Count, "worker 0 sample count");
        AssertEqual(3, plan.WorkerAssignments[1].Count, "worker 1 sample count");

        var ordered = plan.Samples;
        AssertEqual(6, ordered.Count, "sample count");
        AssertEqual(1000, ordered[0].Rpm, "first RPM");
        AssertEqual(0, ordered[0].Throttle, "first throttle");
        AssertEqual(2000, ordered[^1].Rpm, "last RPM");
        AssertEqual(100, ordered[^1].Throttle, "last throttle");
    }

    private static void TestDuplicateThrottleRejected()
    {
        var request = CreateRequest(Path.Combine(Path.GetTempPath(), "esrecorder-duplicates")) with
        {
            ThrottlePoints = new[] { 0, 100, 100 }
        };

        try
        {
            _ = RecordingPlanBuilder.Build(request);
            throw new InvalidOperationException("Duplicate throttle values were accepted.");
        }
        catch (ArgumentException exception) when (
            exception.Message.Contains("Duplicate throttle", StringComparison.Ordinal))
        {
        }
    }

    private static async Task TestCoordinatorAndArtifactsAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-core-tests-{Guid.NewGuid():N}");
        var recordingRoot = Path.Combine(root, "recording");
        var beamNgRoot = Path.Combine(root, "beamng");
        Directory.CreateDirectory(recordingRoot);

        try
        {
            var request = CreateRequest(recordingRoot);
            var backend = new FakeRecorderBackend();
            var coordinator = new RecordingCoordinator(backend);
            var session = await coordinator.RecordAsync(request).ConfigureAwait(false);

            AssertEqual(6, session.Measurements.Count, "coordinator result count");
            AssertEqual("Test Engine", session.Engine.Name, "engine name");
            AssertEqual(2, backend.InitialisedInstances.Count, "initialised instance count");
            AssertEqual(2, backend.CompiledInstances.Count, "compiled instance count");

            foreach (var measurement in session.Measurements)
            {
                await File.WriteAllBytesAsync(
                    measurement.Sample.OutputPath,
                    new byte[] { 82, 73, 70, 70 }).ConfigureAwait(false);
            }

            await RecordingArtifactStore.WriteAsync(session, recordingRoot).ConfigureAwait(false);
            var manifestPath = Path.Combine(recordingRoot, "recording-manifest.json");
            var loaded = await RecordingArtifactStore.ReadAsync(manifestPath).ConfigureAwait(false);
            AssertEqual(session.Measurements.Count, loaded.Measurements.Count, "manifest measurement count");
            AssertTrue(File.Exists(Path.Combine(recordingRoot, "dyno.csv")), "dyno CSV exists");

            await BeamNgExporter.ExportAsync(
                loaded,
                new BeamNgExportOptions(
                    beamNgRoot,
                    "event:>Engine>default",
                    "i4",
                    800,
                    7500,
                    10.0f,
                    0.01f)).ConfigureAwait(false);

            AssertTrue(
                Directory.EnumerateFiles(beamNgRoot, "*.jbeam.fragment").Any(),
                "BeamNG JBeam fragment exists");
            AssertTrue(
                Directory.EnumerateFiles(beamNgRoot, "*.sfxBlend2D.json").Any(),
                "BeamNG blend file exists");
            AssertEqual(
                4,
                Directory.EnumerateFiles(Path.Combine(beamNgRoot, "samples"), "*.wav").Count(),
                "BeamNG copied sample count");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static RecordingRequest CreateRequest(string outputDirectory) => new(
        "engine.mr",
        outputDirectory,
        "Test Engine",
        new[] { new RpmPoint(1000, 44100), new RpmPoint(2000, 48000) },
        new[] { 0, 50, 100 },
        SampleLength: 5,
        WarmupCount: 1,
        MaxInstances: 2);

    private static void AssertEqual<T>(T expected, T actual, string name)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected {expected}, observed {actual}");
    }

    private static void AssertTrue(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Assertion failed: {name}");
    }

    private sealed class FakeRecorderBackend : IRecorderBackend
    {
        public ConcurrentDictionary<int, byte> InitialisedInstances { get; } = new();
        public ConcurrentDictionary<int, byte> CompiledInstances { get; } = new();

        public int NativeLibraryVersion => 1011;

        public void Initialise(int instanceId) => InitialisedInstances.TryAdd(instanceId, 0);

        public void Compile(int instanceId, string engineScriptPath)
        {
            if (!InitialisedInstances.ContainsKey(instanceId))
                throw new InvalidOperationException("Instance was compiled before initialisation.");
            CompiledInstances.TryAdd(instanceId, 0);
        }

        public EngineMetadata GetEngineMetadata(int instanceId) =>
            new("Test Engine", 7500.0f, 2.0f, NativeLibraryVersion);

        public SampleMeasurement Record(int instanceId, RecordingSample sample)
        {
            if (!CompiledInstances.ContainsKey(instanceId))
                throw new InvalidOperationException("Instance was recorded before compilation.");

            return new SampleMeasurement(
                sample,
                sample.Rpm * 0.1f,
                sample.Rpm * (sample.Throttle / 100.0f),
                2.5f,
                10);
        }

        public RecorderStatus GetStatus(int instanceId) =>
            new(RecorderState.Idle, 100, SimulatorReady: true);
    }
}
