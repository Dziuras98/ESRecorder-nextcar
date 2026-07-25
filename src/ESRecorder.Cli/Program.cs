using System.Globalization;
using ESRecorder.BeamNG;
using ESRecorder.Core;
using ESRecorder.Native;

namespace ESRecorder.Cli;

internal static class Program
{
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
            return args[0] switch
            {
                "record" => await RecordAsync(options, cancellation.Token).ConfigureAwait(false),
                "export-beamng" => await ExportBeamNgAsync(options, cancellation.Token).ConfigureAwait(false),
                _ => throw new ArgumentException($"Unknown command: {args[0]}")
            };
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

            Record neutral sample-bank source data:
              ESRecorder.Cli record \
                --engine-script es/assets/main.mr \
                --output recordings/example \
                --name example \
                --rpm 1000:44100,2000:44100,3000:44100 \
                --throttle 0,50,100 \
                --length 5 \
                --warmup 1 \
                --instances 4

            Export an existing neutral manifest to BeamNG files:
              ESRecorder.Cli export-beamng \
                --manifest recordings/example/recording-manifest.json \
                --output exports/example \
                --starter-event i4 \
                --idle-rpm 800 \
                --max-rpm 7500 \
                --static-friction 12 \
                --dynamic-friction 0.01
            """);
    }
}
