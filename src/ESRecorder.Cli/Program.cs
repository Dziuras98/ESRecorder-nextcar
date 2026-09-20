using System.Globalization;
using System.Text.Json;
using ESRecorder.BeamNG;
using ESRecorder.Core;
using ESRecorder.Native;

namespace ESRecorder.Cli;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                PrintUsage();
                return 0;
            }

            var options = ParseOptions(args.Skip(1).ToArray());
            switch (args[0])
            {
                case "record":
                    return await RecordAsync(options, cancellation.Token).ConfigureAwait(false);
                case "export-beamng":
                    return await ExportBeamNgAsync(options, cancellation.Token).ConfigureAwait(false);
                case "capabilities":
                    return PrintCapabilities();
                case "render-wankel":
                    return RenderWankel(options, cancellation.Token);
                case "render-multi-crank":
                    return RenderMultiCrank(options, cancellation.Token);
                case "render-two-stroke":
                    return RenderTwoStroke(options, cancellation.Token);
                case "render-opposed-piston":
                    return RenderOpposedPiston(options, cancellation.Token);
                case "render-fixed-firing-piston":
                    return RenderFixedFiringPiston(options, cancellation.Token);
                case "render-split-single":
                    return RenderSplitSingle(options, cancellation.Token);
                case "render-rotary-combustion":
                    return RenderRotaryCombustion(options, cancellation.Token);
                case "render-thermal-fluid":
                    return RenderThermalFluid(options, cancellation.Token);
                case "render-radial-cam-ring":
                    return RenderRadialCamRing(options, cancellation.Token);
                case "render-axial-piston":
                    return RenderAxialPiston(options, cancellation.Token);
                case "render-free-piston":
                    return RenderFreePiston(options, cancellation.Token);
                case "render-electric":
                    return RenderElectricMachine(options, cancellation.Token);
                case "render-composite":
                    return RenderComposite(options, cancellation.Token);
                case "render-event-source":
                    return RenderEventSource(options, cancellation.Token);
                default:
                    throw new ArgumentException($"Unknown command: {args[0]}");
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Recording cancelled.");
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RecordAsync(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Native recording requires Windows x64.");

        var engineScript = Get(options, "engine-script", "es/assets/main.mr");
        var output = Path.GetFullPath(Get(options, "output", "recordings"));
        var request = new RecordingRequest(
            engineScript,
            output,
            Get(options, "name", "engine"),
            ParseRpmPoints(GetRequired(options, "rpm")),
            ParseIntegers(GetRequired(options, "throttle"), "throttle"),
            ParsePositiveInt(Get(options, "length", "5"), "length"),
            ParseNonNegativeInt(Get(options, "warmup", "1"), "warmup"),
            ParseRangeInt(Get(options, "instances", "4"), "instances", 1, 8));

        var progress = new Progress<RecordingProgress>(item =>
        {
            var sample = item.Sample is null
                ? "engine setup"
                : $"{item.Sample.Rpm} RPM / {item.Sample.Throttle}%";
            Console.WriteLine(
                $"[{item.CompletedSamples}/{item.TotalSamples}] " +
                $"instance {item.InstanceId}: {sample} — " +
                $"{item.Status.State} {item.Status.ProgressPercent}%");
        });

        var coordinator = new RecordingCoordinator(new NativeRecorderBackend());
        var session = await coordinator.RecordAsync(
            request,
            progress,
            cancellationToken).ConfigureAwait(false);
        await RecordingArtifactStore.WriteAsync(
            session,
            output,
            cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"Recorded {session.Measurements.Count} samples.");
        Console.WriteLine($"Engine: {session.Engine.Name}");
        Console.WriteLine($"Manifest: {Path.Combine(output, "recording-manifest.json")}");
        return 0;
    }

    private static async Task<int> ExportBeamNgAsync(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var manifest = Path.GetFullPath(GetRequired(options, "manifest"));
        var output = Path.GetFullPath(GetRequired(options, "output"));
        var session = await RecordingArtifactStore.ReadAsync(
            manifest,
            cancellationToken).ConfigureAwait(false);

        var exportOptions = new BeamNgExportOptions(
            output,
            Get(options, "event-name", "event:>Engine>default"),
            GetRequired(options, "starter-event"),
            ParsePositiveInt(GetRequired(options, "idle-rpm"), "idle-rpm"),
            ParsePositiveInt(GetRequired(options, "max-rpm"), "max-rpm"),
            ParseFloat(Get(options, "static-friction", "0"), "static-friction"),
            ParseFloat(Get(options, "dynamic-friction", "0"), "dynamic-friction"));

        await BeamNgExporter.ExportAsync(
            session,
            exportOptions,
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"BeamNG export written to {output}");
        return 0;
    }

    private static int PrintCapabilities()
    {
        Console.WriteLine(JsonSerializer.Serialize(RecorderCapabilityCatalog.Create(), JsonOptions));
        return 0;
    }

    private static int RenderWankel(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = WankelSourceFactory.Create(
            Get(options, "name", "wankel"),
            ParseRangeInt(GetRequired(options, "rotors"), "rotors", 1, 32),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "redline"), "redline", 1000, 30000));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderMultiCrank(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = MultiCrankSourceFactory.Create(
            Get(options, "name", "multi-crank"),
            ParseMultiCrankModules(GetRequired(options, "modules")),
            ParseRangeInt(GetRequired(options, "redline"), "redline", 500, 30000),
            Get(options, "family", "multi-crank"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderTwoStroke(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = TwoStrokePistonSourceFactory.Create(
            Get(options, "name", "two-stroke"),
            ParseRangeInt(GetRequired(options, "cylinders"), "cylinders", 1, 128),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "redline"), "redline", 500, 30000),
            Get(options, "layout", "unspecified"),
            Get(options, "scavenging", "generic"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderOpposedPiston(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = OpposedPistonSourceFactory.Create(
            Get(options, "name", "opposed-piston"),
            ParseRangeInt(GetRequired(options, "chambers"), "chambers", 1, 64),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "redline"), "redline", 300, 20000),
            ParseRangeInt(Get(options, "cranks", "2"), "cranks", 1, 8),
            ParseFiniteDouble(Get(options, "crank-phase-degrees", "12"), "crank-phase-degrees"),
            Get(options, "combustion", "generic"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderFixedFiringPiston(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var cylinderCount = ParseRangeInt(GetRequired(options, "cylinders"), "cylinders", 1, 128);
        var bankCount = ParseRangeInt(GetRequired(options, "banks"), "banks", 1, 16);
        var source = FixedFiringPistonSourceFactory.Create(
            Get(options, "name", "fixed-firing-piston"),
            cylinderCount,
            bankCount,
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 300, 30000),
            ParseDoubleList(GetRequired(options, "ignition-degrees"), "ignition-degrees"),
            ParseIntegerList(
                GetRequired(options, "bank-assignments"),
                "bank-assignments",
                0,
                bankCount - 1),
            ParseRangeInt(Get(options, "cycle-degrees", "720"), "cycle-degrees", 360, 2880),
            Get(options, "layout", "fixed-firing-piston"),
            Get(options, "firing-label", "explicit"),
            Get(options, "combustion", "petrol"),
            Get(options, "combustion-profile", "default"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderSplitSingle(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = SplitSingleSourceFactory.Create(
            Get(options, "name", "split-single"),
            ParseRangeInt(GetRequired(options, "chambers"), "chambers", 1, 64),
            ParseRangeInt(Get(options, "banks", "1"), "banks", 1, 16),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 300, 30000),
            ParseFiniteDouble(
                Get(options, "transfer-piston-phase-degrees", "15"),
                "transfer-piston-phase-degrees"),
            Get(options, "layout", "split-single"),
            Get(options, "combustion", "petrol"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderRotaryCombustion(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = RotaryCombustionSourceFactory.Create(
            Get(options, "name", "rotary-combustion"),
            GetRequired(options, "mechanism"),
            ParseRangeInt(GetRequired(options, "elements"), "elements", 1, 128),
            ParsePositiveDouble(
                GetRequired(options, "power-events-per-output-revolution"),
                "power-events-per-output-revolution"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 100, 100000),
            Get(options, "combustion", "petrol"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderThermalFluid(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = ThermalFluidMachineSourceFactory.Create(
            Get(options, "name", "thermal-fluid-machine"),
            GetRequired(options, "machine-class"),
            GetRequired(options, "mechanism"),
            ParseRangeInt(GetRequired(options, "elements"), "elements", 1, 128),
            ParseNonNegativeDouble(
                Get(options, "pressure-events-per-reference-revolution", "0"),
                "pressure-events-per-reference-revolution"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 50, 200000),
            ParseRangeInt(GetRequired(options, "blade-lobe-order"), "blade-lobe-order", 1, 256),
            Get(options, "working-fluid", "generic"),
            Get(options, "thermal-response", "thermal-lag"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderRadialCamRing(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var topology = GetRequired(options, "topology");
        var defaultCamRings = string.Equals(topology, "dual-cam-ring", StringComparison.OrdinalIgnoreCase)
            ? "2"
            : "1";

        var source = RadialCamRingSourceFactory.Create(
            Get(options, "name", topology),
            topology,
            ParseRangeInt(GetRequired(options, "elements"), "elements", 1, 128),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 200, 30000),
            ParseRangeInt(Get(options, "cycle-revolutions", "2"), "cycle-revolutions", 1, 8),
            ParseRangeInt(Get(options, "cam-rings", defaultCamRings), "cam-rings", 1, 8),
            ParseRangeInt(Get(options, "cam-lobes", "1"), "cam-lobes", 1, 64),
            Get(options, "combustion", "petrol"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderAxialPiston(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = AxialPistonSourceFactory.Create(
            Get(options, "name", "axial-piston"),
            ParseRangeInt(GetRequired(options, "pistons"), "pistons", 1, 128),
            ParsePositiveDouble(GetRequired(options, "displacement"), "displacement"),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 300, 30000),
            ParseRangeInt(Get(options, "cycle-revolutions", "2"), "cycle-revolutions", 1, 8),
            Get(options, "mechanism", "swashplate"),
            Get(options, "combustion", "petrol"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderFreePiston(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = FreePistonSourceFactory.Create(
            Get(options, "name", "free-piston"),
            ParseRangeInt(GetRequired(options, "modules"), "modules", 1, 32),
            ParsePositiveDouble(GetRequired(options, "displacement-equivalent"), "displacement-equivalent"),
            ParseRangeInt(GetRequired(options, "max-rate"), "max-rate", 100, 30000),
            Get(options, "generator", "linear-generator"),
            Get(options, "combustion", "petrol"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderElectricMachine(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = ElectricMachineSourceFactory.Create(
            Get(options, "name", "electric-machine"),
            ParseRangeInt(GetRequired(options, "pole-pairs"), "pole-pairs", 1, 64),
            ParseRangeInt(GetRequired(options, "max-rpm"), "max-rpm", 100, 100000),
            ParseRangeInt(Get(options, "machines", "1"), "machines", 1, 16),
            Get(options, "machine-type", "permanent-magnet"),
            ParseRangeInt(Get(options, "slot-order", "12"), "slot-order", 1, 256),
            ParseRangeInt(Get(options, "inverter-order", "24"), "inverter-order", 1, 256));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderComposite(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = CompositeSourceFactory.Create(
            Get(options, "name", "composite"),
            ParseCompositeComponents(GetRequired(options, "sources")),
            Get(options, "family", "multi-source-composite"));

        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderEventSource(
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        var source = AcousticEventSourceSerializer.Read(
            Path.GetFullPath(GetRequired(options, "source")));
        return RenderEventBank(source, options, cancellationToken);
    }

    private static int RenderEventBank(
        AcousticEventSourceDefinition source,
        IReadOnlyDictionary<string, string> options,
        CancellationToken cancellationToken)
    {
        source.Validate();
        var output = Path.GetFullPath(Get(options, "output", "event-recordings"));
        var rpmPoints = ParseRpmPoints(GetRequired(options, "rpm"));
        var throttles = ParseIntegers(GetRequired(options, "throttle"), "throttle");
        var length = ParseRangeInt(Get(options, "length", "5"), "length", 1, 120);
        var outputStem = RecordingPlanBuilder.SanitizeFileName(Get(options, "name", source.Id));
        var measurements = new List<EventRenderMeasurement>();

        Directory.CreateDirectory(output);
        AcousticEventSourceSerializer.Write(source, Path.Combine(output, "event-source.json"));

        foreach (var rpmPoint in rpmPoints.OrderBy(static point => point.Rpm))
        {
            foreach (var throttle in throttles.OrderBy(static value => value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var wav = Path.Combine(output, $"{outputStem}_{rpmPoint.Rpm}_{throttle}.wav");
                var measurement = EventAudioRenderer.Render(
                    source,
                    new EventRenderRequest(
                        wav,
                        rpmPoint.Rpm,
                        throttle,
                        rpmPoint.Frequency,
                        length));
                measurements.Add(measurement);
                Console.WriteLine(
                    $"rendered {rpmPoint.Rpm} RPM / {throttle}% -> {Path.GetFileName(wav)} " +
                    $"peak={measurement.PeakAbsolute:0.000} rms={measurement.RootMeanSquare:0.000}");
            }
        }

        var reportPath = Path.Combine(output, "event-render-report.json");
        File.WriteAllText(
            reportPath,
            JsonSerializer.Serialize(
                new
                {
                    schema_version = 1,
                    backend = "event-source-v1",
                    source,
                    measurements
                },
                JsonOptions) + Environment.NewLine);

        Console.WriteLine($"Event source: {Path.Combine(output, "event-source.json")}");
        Console.WriteLine($"Report: {reportPath}");
        return 0;
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || key.Length == 2)
                throw new ArgumentException($"Expected option name, observed: {key}");
            if (index + 1 >= args.Length)
                throw new ArgumentException($"Option {key} requires a value.");

            key = key[2..];
            if (!options.TryAdd(key, args[index + 1]))
                throw new ArgumentException($"Option --{key} was supplied more than once.");
        }
        return options;
    }

    private static IReadOnlyList<RpmPoint> ParseRpmPoints(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(entry =>
        {
            var components = entry.Split(':', StringSplitOptions.TrimEntries);
            if (components.Length != 2)
                throw new ArgumentException($"RPM point must use rpm:frequency syntax: {entry}");
            return new RpmPoint(
                ParsePositiveInt(components[0], "rpm"),
                ParsePositiveInt(components[1], "frequency"));
        })
        .ToArray();

    private static IReadOnlyList<double> ParseDoubleList(string value, string name)
    {
        var values = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ParseFiniteDouble(item, name))
            .ToArray();

        if (values.Length == 0)
            throw new ArgumentException($"--{name} must contain at least one value.");

        return values;
    }

    private static IReadOnlyList<int> ParseIntegerList(
        string value,
        string name,
        int minimum,
        int maximum)
    {
        var values = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ParseRangeInt(item, name, minimum, maximum))
            .ToArray();

        if (values.Length == 0)
            throw new ArgumentException($"--{name} must contain at least one value.");

        return values;
    }

    private static IReadOnlyList<CompositeSourceComponent> ParseCompositeComponents(string value)
    {
        var components = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(entry =>
            {
                var fields = entry.Split('|', StringSplitOptions.TrimEntries);
                if (fields.Length is < 2 or > 5)
                {
                    throw new ArgumentException(
                        $"Composite source must use name|source-path[|speed-ratio[|gain[|phase-offset-revolutions]]] syntax: {entry}");
                }

                var name = fields[0];
                var source = AcousticEventSourceSerializer.Read(Path.GetFullPath(fields[1]));
                var speedRatio = fields.Length >= 3
                    ? ParsePositiveDouble(fields[2], "component-speed-ratio")
                    : 1.0;
                var gain = fields.Length >= 4
                    ? ParsePositiveDouble(fields[3], "component-gain")
                    : 1.0;
                var phase = fields.Length >= 5
                    ? ParseFiniteDouble(fields[4], "component-phase-offset")
                    : 0.0;

                return new CompositeSourceComponent(name, source, speedRatio, gain, phase);
            })
            .ToArray();

        if (components.Length is < 2 or > 16)
            throw new ArgumentException("--sources must define between 2 and 16 source components.");

        return components;
    }

    private static IReadOnlyList<MultiCrankModuleSpec> ParseMultiCrankModules(string value)
    {
        var modules = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((entry, index) =>
            {
                var components = entry.Split(':', StringSplitOptions.TrimEntries);
                if (components.Length is < 3 or > 6)
                {
                    throw new ArgumentException(
                        $"Multi-crank module must use name:events:phase-degrees[:shaft-ratio[:gain[:mechanical-order]]] syntax: {entry}");
                }

                var phaseDegrees = ParseFiniteDouble(components[2], "module-phase-degrees");
                return new MultiCrankModuleSpec(
                    components[0],
                    ParseRangeInt(components[1], "module-events", 1, 128),
                    components.Length >= 4
                        ? ParsePositiveDouble(components[3], "module-shaft-ratio")
                        : 1.0,
                    phaseDegrees / 360.0,
                    components.Length >= 5
                        ? ParsePositiveDouble(components[4], "module-gain")
                        : 1.0,
                    components.Length >= 6
                        ? ParsePositiveDouble(components[5], "module-mechanical-order")
                        : 1.0);
            })
            .ToArray();

        if (modules.Length is < 2 or > 16)
            throw new ArgumentException("--modules must define between 2 and 16 crank modules.");

        return modules;
    }

    private static IReadOnlyList<int> ParseIntegers(string value, string name) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => ParseRangeInt(item, name, 0, 100))
        .ToArray();

    private static string Get(
        IReadOnlyDictionary<string, string> options,
        string name,
        string fallback) => options.TryGetValue(name, out var value) ? value : fallback;

    private static string GetRequired(
        IReadOnlyDictionary<string, string> options,
        string name) => options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException($"Required option --{name} is missing.");

    private static int ParsePositiveInt(string value, string name) =>
        ParseRangeInt(value, name, 1, int.MaxValue);

    private static int ParseNonNegativeInt(string value, string name) =>
        ParseRangeInt(value, name, 0, int.MaxValue);

    private static int ParseRangeInt(string value, string name, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ||
            result < minimum || result > maximum)
        {
            throw new ArgumentException(
                $"--{name} must be an integer between {minimum} and {maximum}.");
        }
        return result;
    }

    private static double ParseNonNegativeDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
            !double.IsFinite(result) ||
            result < 0.0)
        {
            throw new ArgumentException($"--{name} must be a non-negative finite number.");
        }
        return result;
    }

    private static double ParsePositiveDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
            !double.IsFinite(result) ||
            result <= 0.0)
        {
            throw new ArgumentException($"--{name} must be a positive finite number.");
        }
        return result;
    }

    private static double ParseFiniteDouble(string value, string name)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ||
            !double.IsFinite(result))
        {
            throw new ArgumentException($"--{name} must be a finite number.");
        }
        return result;
    }

    private static float ParseFloat(string value, string name)
    {
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"--{name} must be a number.");
        return result;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            ESRecorder headless host

            Print machine-readable recorder capabilities:
              ESRecorder.Cli capabilities

            Render a native ESRecorder-nextcar Wankel source:
              ESRecorder.Cli render-wankel
                --name four-rotor
                --rotors 4
                --displacement 2.6
                --redline 9500
                --output recordings/four-rotor
                --rpm 1500:44100,4500:44100,8500:44100
                --throttle 0,100
                --length 5

            Render a multi-crank source:
              ESRecorder.Cli render-multi-crank
                --name h8
                --modules "left:2:0:1;right:2:90:1"
                --redline 7500
                --output recordings/h8
                --rpm 1500:44100,6500:44100
                --throttle 0,100
                --length 5

            Render a generic two-stroke piston source:
              ESRecorder.Cli render-two-stroke
                --name inline-three-2t
                --cylinders 3
                --displacement 1.5
                --redline 8000
                --layout inline-3
                --scavenging uniflow
                --output recordings/inline-three-2t
                --rpm 1500:44100,7000:44100
                --throttle 0,100
                --length 5

            Render an opposed-piston two-stroke source:
              ESRecorder.Cli render-opposed-piston
                --name op6
                --chambers 6
                --displacement 3.6
                --redline 4500
                --cranks 2
                --crank-phase-degrees 12
                --combustion diesel
                --output recordings/op6
                --rpm 1000:44100,4000:44100
                --throttle 0,100
                --length 5

            Render an explicit fixed-firing piston source:
              ESRecorder.Cli render-fixed-firing-piston
                --name odd-fire-v6
                --cylinders 6
                --banks 2
                --displacement 3.0
                --max-rpm 8000
                --cycle-degrees 720
                --ignition-degrees 0,90,240,330,480,570
                --bank-assignments 0,1,0,1,0,1
                --layout V6
                --firing-label odd-fire
                --combustion-profile turbulent-jet-ignition
                --output recordings/odd-fire-v6
                --rpm 1200:48000,7000:48000
                --throttle 0,100
                --length 5

            Render a split-single/twingle two-stroke source:
              ESRecorder.Cli render-split-single
                --name split-single-six
                --chambers 6
                --banks 2
                --displacement 3.0
                --max-rpm 9000
                --transfer-piston-phase-degrees 15
                --layout V-split-single
                --output recordings/split-single-six
                --rpm 1500:48000,8000:48000
                --throttle 0,100
                --length 5

            Render a non-Wankel rotary-combustion source:
              ESRecorder.Cli render-rotary-combustion
                --name gerotor-7
                --mechanism "seven-lobe gerotor combustion"
                --elements 7
                --power-events-per-output-revolution 7
                --max-rpm 7500
                --output recordings/gerotor-7
                --rpm 1200:48000,7000:48000
                --throttle 0,100
                --length 5

            Render a thermal-fluid / external-combustion source:
              ESRecorder.Cli render-thermal-fluid
                --name steam-turbine
                --machine-class turbine
                --mechanism "two-stage steam turbine"
                --elements 2
                --pressure-events-per-reference-revolution 0
                --max-rpm 18000
                --blade-lobe-order 32
                --working-fluid steam
                --thermal-response "slow-thermal"
                --output recordings/steam-turbine
                --rpm 3000:48000,16000:48000
                --throttle 0,100
                --length 5

            Render radial or cam-ring topology:
              ESRecorder.Cli render-radial-cam-ring
                --name radial-7
                --topology radial-piston
                --elements 7
                --displacement 7.0
                --max-rpm 3200
                --cycle-revolutions 2
                --output recordings/radial-7
                --rpm 800:44100,2800:44100
                --throttle 0,100
                --length 5

            Render an axial-piston source:
              ESRecorder.Cli render-axial-piston
                --name axial-12
                --pistons 12
                --displacement 2.0
                --max-rpm 9500
                --cycle-revolutions 2
                --mechanism swashplate
                --output recordings/axial-12
                --rpm 1500:44100,8500:44100
                --throttle 0,100
                --length 5

            Render a free-piston generator source:
              ESRecorder.Cli render-free-piston
                --name fp4
                --modules 4
                --displacement-equivalent 4.0
                --max-rate 3600
                --generator linear-generator
                --output recordings/fp4
                --rpm 800:44100,3200:44100
                --throttle 0,100
                --length 5

            Render an electric machine source:
              ESRecorder.Cli render-electric
                --name dual-motor
                --pole-pairs 4
                --max-rpm 18000
                --machines 2
                --slot-order 24
                --inverter-order 48
                --output recordings/dual-motor
                --rpm 1000:44100,16000:44100
                --throttle 0,100
                --length 5

            Compose existing event-source JSON files:
              ESRecorder.Cli render-composite
                --name rotary-hybrid
                --sources "ice|recordings/rotary/event-source.json|1|1|0;motor|recordings/motor/event-source.json|2.5|0.55|0.125"
                --output recordings/rotary-hybrid
                --rpm 1500:44100,8500:44100
                --throttle 0,100
                --length 5

            Render an arbitrary event-source-v1 definition:
              ESRecorder.Cli render-event-source
                --source source.json
                --output recordings/custom
                --rpm 1500:44100,4500:44100
                --throttle 0,100
                --length 5

            Record neutral sample-bank source data through the legacy engine-sim backend:
              ESRecorder.Cli record
                --engine-script es/assets/main.mr
                --output recordings/example
                --name example
                --rpm 1000:44100,2000:44100,3000:44100
                --throttle 0,50,100
                --length 5
                --warmup 1
                --instances 4

            Export an existing neutral manifest to BeamNG files:
              ESRecorder.Cli export-beamng
                --manifest recordings/example/recording-manifest.json
                --output exports/example
                --starter-event i4
                --idle-rpm 800
                --max-rpm 7500
                --static-friction 12
                --dynamic-friction 0.01
            """);
    }
}
