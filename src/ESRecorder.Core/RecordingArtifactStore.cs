using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ESRecorder.Core;

public static class RecordingArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task WriteAsync(
        RecordingSession session,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var directory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(directory);

        var manifestPath = Path.Combine(directory, "recording-manifest.json");
        await using (var manifest = File.Create(manifestPath))
        {
            await JsonSerializer.SerializeAsync(
                manifest,
                session,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        var csv = new StringBuilder();
        csv.AppendLine("rpm,throttle,power_hp,torque_nm,realtime_ratio,elapsed_ms,wav");
        foreach (var measurement in session.Measurements)
        {
            csv.Append(measurement.Sample.Rpm.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(measurement.Sample.Throttle.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(measurement.PowerHorsepower.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(measurement.TorqueNewtonMetres.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(measurement.RealtimeRatio.ToString("0.###", CultureInfo.InvariantCulture)).Append(',')
                .Append(measurement.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(Path.GetRelativePath(directory, measurement.Sample.OutputPath)))
                .AppendLine();
        }

        await File.WriteAllTextAsync(
            Path.Combine(directory, "dyno.csv"),
            csv.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<RecordingSession> ReadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        await using var manifest = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<RecordingSession>(
            manifest,
            JsonOptions,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Recording manifest is empty or invalid.");
    }

    private static string EscapeCsv(string value)
    {
        if (!value.ContainsAny(',', '"', '\r', '\n'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

internal static class StringExtensions
{
    public static bool ContainsAny(this string value, params char[] characters) =>
        value.IndexOfAny(characters) >= 0;
}
