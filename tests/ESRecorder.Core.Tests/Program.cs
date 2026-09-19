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
            TestCapabilityContract();
            TestNativeWankelEventModel();
            TestMultiCrankTopology();
            TestTwoStrokeTopology();
            TestOpposedPistonTopology();
            TestNewTopologyRenderers();
            TestEventSourceRoundTripAndRender();
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

    private static void TestCapabilityContract()
    {
        var capabilities = RecorderCapabilityCatalog.Create();
        AssertEqual(1, capabilities.SchemaVersion, "capability schema");
        AssertEqual(
            "nextcar-recorder-capabilities-v1",
            capabilities.CapabilityContract,
            "capability contract id");

        var eventBackend = capabilities.Backends.Single(backend => backend.Id == "event-source-v1");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("wankel", StringComparer.Ordinal),
            "event backend exposes Wankel");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("multi-crank", StringComparer.Ordinal),
            "event backend exposes multi-crank");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("two-stroke-piston", StringComparer.Ordinal),
            "event backend exposes two-stroke piston");
        AssertTrue(
            eventBackend.SourceFamilies.Contains("opposed-piston", StringComparer.Ordinal),
            "event backend exposes opposed-piston");
        AssertTrue(
            capabilities.Guarantees.Contains("no-silent-topology-fallback", StringComparer.Ordinal),
            "capability contract forbids fallback");
    }

    private static void TestNativeWankelEventModel()
    {
        var source = WankelSourceFactory.Create("test-four-rotor", 4, 2.6, 9500);
        AssertEqual("wankel", source.Family, "Wankel family");
        AssertEqual(1, source.EventTrains.Length, "Wankel event train count");
        AssertEqual(4, source.EventTrains[0].EventPhases.Length, "Wankel power event count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "rotor phase 0");
        AssertNear(0.25, source.EventTrains[0].EventPhases[1], 1e-9, "rotor phase 1");
        AssertNear(0.50, source.EventTrains[0].EventPhases[2], 1e-9, "rotor phase 2");
        AssertNear(0.75, source.EventTrains[0].EventPhases[3], 1e-9, "rotor phase 3");
        AssertEqual(
            "4",
            source.Metadata["power_events_per_eccentric_shaft_revolution"],
            "Wankel power events metadata");
    }

    private static void TestMultiCrankTopology()
    {
        var source = MultiCrankSourceFactory.Create(
            "test-h8",
            new[]
            {
                new MultiCrankModuleSpec("upper", 2, 1.0, 0.0),
                new MultiCrankModuleSpec("lower", 2, 1.0, 0.25)
            },
            redlineRpm: 7500,
            family: "h-layout");

        AssertEqual("h-layout", source.Family, "multi-crank family");
        AssertEqual(2, source.EventTrains.Length, "multi-crank event train count");
        AssertEqual(4, source.HarmonicLayers.Length, "multi-crank harmonic layer count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "upper crank first phase");
        AssertNear(0.25, source.EventTrains[1].EventPhases[0], 1e-9, "lower crank first phase");
        AssertEqual("2", source.Metadata["module_count"], "multi-crank module metadata");
        AssertEqual(
            "4",
            source.Metadata["effective_power_events_per_reference_revolution"],
            "multi-crank effective event count");
    }

    private static void TestTwoStrokeTopology()
    {
        var source = TwoStrokePistonSourceFactory.Create(
            "test-i3-2t",
            3,
            1.5,
            8500,
            "inline-3",
            "uniflow");

        AssertEqual("two-stroke-piston", source.Family, "two-stroke family");
        AssertEqual(1, source.EventTrains.Length, "two-stroke event train count");
        AssertEqual(3, source.EventTrains[0].EventPhases.Length, "two-stroke power event count");
        AssertNear(0.0, source.EventTrains[0].EventPhases[0], 1e-9, "two-stroke phase 0");
        AssertNear(1.0 / 3.0, source.EventTrains[0].EventPhases[1], 1e-9, "two-stroke phase 1");
        AssertNear(2.0 / 3.0, source.EventTrains[0].EventPhases[2], 1e-9, "two-stroke phase 2");
        AssertEqual(
            "3",
            source.Metadata["power_events_per_crankshaft_revolution"],
            "two-stroke event metadata");
        AssertEqual("uniflow", source.Metadata["scavenging"], "two-stroke scavenging metadata");
    }

    private static void TestOpposedPistonTopology()
    {
        var source = OpposedPistonSourceFactory.Create(
            "test-op6",
            chamberCount: 6,
            displacementLitres: 3.6,
            redlineRpm: 4500,
            crankshaftCount: 2,
            crankPhaseDegrees: 12.0,
            combustionClass: "diesel");

        AssertEqual("opposed-piston", source.Family, "opposed-piston family");
        AssertEqual(1, source.EventTrains.Length, "opposed-piston combustion train count");
        AssertEqual(6, source.EventTrains[0].EventPhases.Length, "opposed-piston combustion event count");
        AssertEqual(5, source.HarmonicLayers.Length, "opposed-piston mechanical layer count");
        AssertEqual("6", source.Metadata["chamber_count"], "opposed-piston chamber metadata");
        AssertEqual("12", source.Metadata["piston_count"], "opposed-piston piston metadata");
        AssertEqual("2", source.Metadata["crankshaft_count"], "opposed-piston crank metadata");
        AssertEqual(
            "6",
            source.Metadata["power_events_per_output_revolution"],
            "opposed-piston power event metadata");
    }

    private static void TestNewTopologyRenderers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-topology-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var sources = new[]
            {
                MultiCrankSourceFactory.Create(
                    "render-h8",
                    new[]
                    {
                        new MultiCrankModuleSpec("upper", 2, 1.0, 0.0),
                        new MultiCrankModuleSpec("lower", 2, 1.0, 0.25)
                    },
                    7500),
                TwoStrokePistonSourceFactory.Create("render-2t", 4, 2.0, 8000, "inline-4", "loop"),
                OpposedPistonSourceFactory.Create("render-op", 4, 2.4, 5000, 2, 10.0, "diesel")
            };

            foreach (var source in sources)
            {
                var output = Path.Combine(root, $"{source.Id}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        output,
                        Rpm: 3000,
                        Throttle: 75,
                        SampleRate: 8000,
                        LengthSeconds: 1));

                AssertTrue(File.Exists(output), $"{source.Id} render WAV exists");
                AssertTrue(new FileInfo(output).Length > 44, $"{source.Id} render WAV contains PCM");
                AssertTrue(measurement.PeakAbsolute > 0.01, $"{source.Id} render peak is non-zero");
                AssertTrue(measurement.RootMeanSquare > 0.001, $"{source.Id} render RMS is non-zero");
                AssertTrue(double.IsFinite(measurement.PeakAbsolute), $"{source.Id} render peak is finite");
                AssertTrue(double.IsFinite(measurement.RootMeanSquare), $"{source.Id} render RMS is finite");
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void TestEventSourceRoundTripAndRender()
    {
        var root = Path.Combine(Path.GetTempPath(), $"esrecorder-event-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var source = WankelSourceFactory.Create("test-two-rotor", 2, 1.3, 9000);
            var sourcePath = Path.Combine(root, "source.json");
            AcousticEventSourceSerializer.Write(source, sourcePath);
            var loaded = AcousticEventSourceSerializer.Read(sourcePath);

            AssertEqual(source.Id, loaded.Id, "event source round-trip id");
            AssertEqual(2, loaded.EventTrains[0].EventPhases.Length, "event source round-trip phases");

            var output = Path.Combine(root, "sample.wav");
            var measurement = EventAudioRenderer.Render(
                loaded,
                new EventRenderRequest(
                    output,
                    Rpm: 6000,
                    Throttle: 100,
                    SampleRate: 8000,
                    LengthSeconds: 1));

            AssertEqual(8000, measurement.SampleCount, "event render sample count");
            AssertTrue(File.Exists(output), "event render WAV exists");
            AssertTrue(new FileInfo(output).Length > 44, "event render WAV contains PCM");
            AssertTrue(measurement.PeakAbsolute > 0.01, "event render peak is non-zero");
            AssertTrue(measurement.RootMeanSquare > 0.001, "event render RMS is non-zero");
            AssertTrue(double.IsFinite(measurement.PeakAbsolute), "event render peak is finite");
            AssertTrue(double.IsFinite(measurement.RootMeanSquare), "event render RMS is finite");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
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

    private static void AssertNear(double expected, double actual, double tolerance, string name)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
        {
            throw new InvalidOperationException(
                $"{name}: expected {expected} +/- {tolerance}, observed {actual}");
        }
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
