using System.Globalization;
using System.Text;
using System.Text.Json;
using ESRecorder.Core;

namespace ESRecorder.BeamNG;

public sealed record BeamNgExportOptions(
    string OutputDirectory,
    string EventName,
    string StarterEventName,
    int IdleRpm,
    int MaximumRpm,
    float StaticFriction,
    float DynamicFriction);

public static class BeamNgExporter
{
    public static async Task ExportAsync(
        RecordingSession session,
        BeamNgExportOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new ArgumentException("BeamNG output directory is required.", nameof(options));
        if (options.IdleRpm <= 0 || options.MaximumRpm <= options.IdleRpm)
            throw new ArgumentException("BeamNG idle and maximum RPM values are invalid.", nameof(options));

        var measurements = session.Measurements;
        var throttleLevels = measurements
            .Select(static measurement => measurement.Sample.Throttle)
            .Distinct()
            .OrderBy(static throttle => throttle)
            .ToArray();

        if (!throttleLevels.Contains(0) || !throttleLevels.Contains(100))
        {
            throw new InvalidOperationException(
                "BeamNG export requires recorded throttle levels 0 and 100. " +
                "The neutral headless recorder does not impose this restriction.");
        }

        var outputRoot = Path.GetFullPath(options.OutputDirectory);
        var sampleRoot = Path.Combine(outputRoot, "samples");
        Directory.CreateDirectory(sampleRoot);

        var engineStem = RecordingPlanBuilder.SanitizeFileName(session.Engine.Name)
            .Replace(' ', '_');

        foreach (var measurement in measurements.Where(static item =>
                     item.Sample.Throttle is 0 or 100))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(measurement.Sample.OutputPath))
            {
                throw new FileNotFoundException(
                    "Recorded WAV required for BeamNG export was not found.",
                    measurement.Sample.OutputPath);
            }

            var targetName = $"{engineStem}_{measurement.Sample.Rpm}_{measurement.Sample.Throttle}.wav";
            File.Copy(
                measurement.Sample.OutputPath,
                Path.Combine(sampleRoot, targetName),
                overwrite: true);
        }

        var torqueCurve = BuildTorqueCurve(session, options);
        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, $"{engineStem}.jbeam.fragment"),
            BuildJBeamFragment(torqueCurve, options),
            new UTF8Encoding(false),
            cancellationToken).ConfigureAwait(false);

        var idleSamples = BuildSampleRows(measurements, engineStem, 0);
        var loadSamples = BuildSampleRows(measurements, engineStem, 100);
        var blendDocument = new
        {
            header = new { version = 1 },
            eventName = options.EventName,
            samples = new[] { idleSamples, loadSamples }
        };

        await File.WriteAllTextAsync(
            Path.Combine(outputRoot, $"{engineStem}.sfxBlend2D.json"),
            JsonSerializer.Serialize(blendDocument, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false),
            cancellationToken).ConfigureAwait(false);
    }

    internal static SortedDictionary<int, float> BuildTorqueCurve(
        RecordingSession session,
        BeamNgExportOptions options)
    {
        var fullLoad = session.Measurements
            .Where(static measurement => measurement.Sample.Throttle == 100)
            .OrderBy(static measurement => measurement.Sample.Rpm)
            .ToArray();
        if (fullLoad.Length == 0)
            throw new InvalidOperationException("No full-load measurements are available.");

        var torque = new SortedDictionary<int, float>(fullLoad.ToDictionary(
            static measurement => measurement.Sample.Rpm,
            static measurement => measurement.TorqueNewtonMetres));

        var minimumRpm = torque.Keys.Min();
        var minimumTorque = torque[minimumRpm];
        while (minimumRpm > 200)
        {
            minimumRpm = Math.Max(200, minimumRpm - 500);
            minimumTorque /= 1.1f;
            torque[minimumRpm] = minimumTorque;
        }

        var maximumRecordedRpm = torque.Keys.Max();
        var trailingTorque = torque[maximumRecordedRpm];
        while (maximumRecordedRpm < options.MaximumRpm + 500)
        {
            maximumRecordedRpm += 500;
            trailingTorque /= 1.1f;
            torque[maximumRecordedRpm] = trailingTorque;
        }

        foreach (var rpm in torque.Keys.ToArray())
        {
            torque[rpm] -= options.StaticFriction;
            torque[rpm] -= (rpm / 9.55f) * options.DynamicFriction;
        }

        return torque;
    }

    private static string BuildJBeamFragment(
        SortedDictionary<int, float> torqueCurve,
        BeamNgExportOptions options)
    {
        var builder = new StringBuilder();
        builder.AppendLine("\"torque\": [");
        builder.AppendLine("    [\"rpm\", \"torque\"],");
        foreach (var entry in torqueCurve)
        {
            builder.Append("    [")
                .Append(entry.Key.ToString(CultureInfo.InvariantCulture))
                .Append(", ")
                .Append(entry.Value.ToString("0.###", CultureInfo.InvariantCulture))
                .AppendLine("],");
        }
        builder.AppendLine("],");
        builder.AppendLine($"\"idleRPM\": {options.IdleRpm.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"\"maxRPM\": {options.MaximumRpm.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"\"friction\": {options.StaticFriction.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"\"dynamicFriction\": {options.DynamicFriction.ToString(CultureInfo.InvariantCulture)},");
        builder.AppendLine($"\"starterSample\": \"event:>Engine>Starter>{options.StarterEventName}_eng\",");
        builder.AppendLine($"\"starterSampleExhaust\": \"event:>Engine>Starter>{options.StarterEventName}_exh\",");
        builder.AppendLine($"\"shutOffSampleEngine\": \"event:>Engine>Shutoff>{options.StarterEventName}_eng\",");
        builder.AppendLine($"\"shutOffSampleExhaust\": \"event:>Engine>Shutoff>{options.StarterEventName}_exh\",");
        return builder.ToString();
    }

    private static object[][] BuildSampleRows(
        IEnumerable<SampleMeasurement> measurements,
        string engineStem,
        int throttle) => measurements
        .Where(measurement => measurement.Sample.Throttle == throttle)
        .OrderBy(static measurement => measurement.Sample.Rpm)
        .Select(measurement => new object[]
        {
            $"art/sound/engine/{engineStem}/{engineStem}_{measurement.Sample.Rpm}_{throttle}.wav",
            measurement.Sample.Rpm
        })
        .ToArray();
}
