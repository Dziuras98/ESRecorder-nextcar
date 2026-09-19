using System.Diagnostics;
using System.Text;

namespace ESRecorder.Core;

public static class EventAudioRenderer
{
    public static EventRenderMeasurement Render(
        AcousticEventSourceDefinition source,
        EventRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        source.Validate();
        ValidateRequest(request);

        var stopwatch = Stopwatch.StartNew();
        var sampleCount = checked(request.SampleRate * request.LengthSeconds);
        var samples = new short[sampleCount];
        var rpmHz = request.Rpm / 60.0;
        var throttle = request.Throttle / 100.0;
        var seed = request.Seed == 0 ? StableHash(source.Id) : request.Seed;
        var sumSquares = 0.0;
        var peak = 0.0;
        var fadeSamples = Math.Min(request.SampleRate / 200, sampleCount / 2);

        for (var index = 0; index < sampleCount; index++)
        {
            var time = index / (double)request.SampleRate;
            var value = 0.0;

            for (var trainIndex = 0; trainIndex < source.EventTrains.Length; trainIndex++)
            {
                var train = source.EventTrains[trainIndex];
                var shaftHz = rpmHz * train.ShaftRatio;
                var phase = Fractional(time * shaftHz);
                var decaySeconds = train.DecayMilliseconds / 1000.0;
                var envelope = 0.0;

                foreach (var eventPhase in train.EventPhases)
                {
                    var delta = phase - eventPhase;
                    if (delta < 0.0)
                        delta += 1.0;

                    var secondsSinceEvent = delta / shaftHz;
                    envelope = Math.Max(envelope, Math.Exp(-secondsSinceEvent / decaySeconds));
                }

                var response = ThrottleScale(throttle, train.ThrottleResponse);
                var resonanceHz = train.ResonanceBaseHz + (shaftHz * train.ResonanceOrder);
                var carrier = Math.Sin((2.0 * Math.PI * resonanceHz * time) + (trainIndex * 0.371));
                var noise = DeterministicNoise(index, seed + (trainIndex * 7919));
                var texture = (carrier * (1.0 - train.NoiseMix)) + (noise * train.NoiseMix);
                value += train.Gain * response * envelope * texture;
            }

            foreach (var layer in source.HarmonicLayers)
            {
                var frequency = rpmHz * layer.ShaftRatio * layer.Order;
                var response = ThrottleScale(throttle, layer.ThrottleResponse);
                value += layer.Gain * response *
                    Math.Sin((2.0 * Math.PI * frequency * time) + layer.PhaseRadians);
            }

            value *= source.MasterGain;

            if (fadeSamples > 0)
            {
                if (index < fadeSamples)
                    value *= index / (double)fadeSamples;
                else if (index >= sampleCount - fadeSamples)
                    value *= (sampleCount - 1 - index) / (double)fadeSamples;
            }

            var limited = Math.Tanh(value);
            if (!double.IsFinite(limited))
                throw new InvalidOperationException($"Non-finite renderer sample at index {index}.");

            var absolute = Math.Abs(limited);
            peak = Math.Max(peak, absolute);
            sumSquares += limited * limited;
            samples[index] = (short)Math.Clamp(
                Math.Round(limited * short.MaxValue),
                short.MinValue,
                short.MaxValue);
        }

        var outputPath = Path.GetFullPath(request.OutputPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Event renderer output directory is invalid."));
        WritePcm16MonoWave(outputPath, request.SampleRate, samples);

        stopwatch.Stop();
        return new EventRenderMeasurement(
            source.Id,
            outputPath,
            request.Rpm,
            request.Throttle,
            request.SampleRate,
            sampleCount,
            peak,
            Math.Sqrt(sumSquares / sampleCount),
            stopwatch.ElapsedMilliseconds);
    }

    private static void ValidateRequest(EventRenderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OutputPath))
            throw new ArgumentException("Event renderer output path is required.", nameof(request));
        if (request.Rpm is < 50 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(request), "RPM must be between 50 and 100000.");
        if (request.Throttle is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request), "Throttle must be between 0 and 100.");
        if (request.SampleRate is < 8000 or > 192000)
            throw new ArgumentOutOfRangeException(nameof(request), "Sample rate must be between 8000 and 192000.");
        if (request.LengthSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(request), "Length must be between 1 and 120 seconds.");
    }

    private static double ThrottleScale(double throttle, double response)
    {
        var shaped = response == 0.0 ? 1.0 : Math.Pow(throttle, response);
        return 0.2 + (0.8 * shaped);
    }

    private static double Fractional(double value) => value - Math.Floor(value);

    private static double DeterministicNoise(int sampleIndex, int seed)
    {
        unchecked
        {
            var value = (uint)(sampleIndex + seed);
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return ((value & 0x00ffffff) / 8388607.5) - 1.0;
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
                hash = (hash * 31) + character;
            return hash;
        }
    }

    private static void WritePcm16MonoWave(string path, int sampleRate, short[] samples)
    {
        var dataBytes = checked(samples.Length * sizeof(short));
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((short)sizeof(short));
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);

        foreach (var sample in samples)
            writer.Write(sample);
    }
}
