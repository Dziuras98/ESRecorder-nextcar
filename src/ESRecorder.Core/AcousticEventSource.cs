using System.Text.Json;

namespace ESRecorder.Core;

public sealed record AcousticEventSourceDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public string Id { get; init; } = string.Empty;

    public string Family { get; init; } = string.Empty;

    public double MasterGain { get; init; } = 0.8;

    public PeriodicEventTrainDefinition[] EventTrains { get; init; } = [];

    public HarmonicLayerDefinition[] HarmonicLayers { get; init; } = [];

    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.Ordinal);

    public void Validate()
    {
        if (SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported acoustic event source schema version: {SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidDataException("Acoustic event source id is required.");
        if (string.IsNullOrWhiteSpace(Family))
            throw new InvalidDataException("Acoustic event source family is required.");
        if (!double.IsFinite(MasterGain) || MasterGain is <= 0.0 or > 4.0)
            throw new InvalidDataException("Master gain must be finite and in the range (0, 4].");
        if (EventTrains.Length == 0 && HarmonicLayers.Length == 0)
            throw new InvalidDataException("At least one event train or harmonic layer is required.");

        foreach (var train in EventTrains)
            train.Validate();
        foreach (var layer in HarmonicLayers)
            layer.Validate();
    }
}

public sealed record PeriodicEventTrainDefinition
{
    public string Name { get; init; } = string.Empty;

    public double ShaftRatio { get; init; } = 1.0;

    public double[] EventPhases { get; init; } = [];

    public double Gain { get; init; } = 1.0;

    public double DecayMilliseconds { get; init; } = 5.0;

    public double ResonanceBaseHz { get; init; } = 120.0;

    public double ResonanceOrder { get; init; } = 1.0;

    public double NoiseMix { get; init; } = 0.25;

    public double ThrottleResponse { get; init; } = 1.0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("Event train name is required.");
        if (!double.IsFinite(ShaftRatio) || ShaftRatio is <= 0.0 or > 64.0)
            throw new InvalidDataException($"Event train {Name}: shaft ratio must be in the range (0, 64].");
        if (EventPhases.Length == 0)
            throw new InvalidDataException($"Event train {Name}: at least one event phase is required.");
        if (EventPhases.Any(static phase => !double.IsFinite(phase) || phase is < 0.0 or >= 1.0))
            throw new InvalidDataException($"Event train {Name}: event phases must be finite and in [0, 1).");
        if (EventPhases.Distinct().Count() != EventPhases.Length)
            throw new InvalidDataException($"Event train {Name}: event phases must be unique.");
        if (!double.IsFinite(Gain) || Gain is < 0.0 or > 4.0)
            throw new InvalidDataException($"Event train {Name}: gain must be in [0, 4].");
        if (!double.IsFinite(DecayMilliseconds) || DecayMilliseconds is < 0.1 or > 1000.0)
            throw new InvalidDataException($"Event train {Name}: decay must be in [0.1, 1000] ms.");
        if (!double.IsFinite(ResonanceBaseHz) || ResonanceBaseHz is < 0.0 or > 20000.0)
            throw new InvalidDataException($"Event train {Name}: resonance base must be in [0, 20000] Hz.");
        if (!double.IsFinite(ResonanceOrder) || ResonanceOrder is < 0.0 or > 128.0)
            throw new InvalidDataException($"Event train {Name}: resonance order must be in [0, 128].");
        if (!double.IsFinite(NoiseMix) || NoiseMix is < 0.0 or > 1.0)
            throw new InvalidDataException($"Event train {Name}: noise mix must be in [0, 1].");
        if (!double.IsFinite(ThrottleResponse) || ThrottleResponse is < 0.0 or > 4.0)
            throw new InvalidDataException($"Event train {Name}: throttle response must be in [0, 4].");
    }
}

public sealed record HarmonicLayerDefinition
{
    public string Name { get; init; } = string.Empty;

    public double ShaftRatio { get; init; } = 1.0;

    public double Order { get; init; } = 1.0;

    public double Gain { get; init; } = 0.1;

    public double PhaseRadians { get; init; }

    public double ThrottleResponse { get; init; } = 1.0;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("Harmonic layer name is required.");
        if (!double.IsFinite(ShaftRatio) || ShaftRatio is <= 0.0 or > 64.0)
            throw new InvalidDataException($"Harmonic layer {Name}: shaft ratio must be in the range (0, 64].");
        if (!double.IsFinite(Order) || Order is <= 0.0 or > 256.0)
            throw new InvalidDataException($"Harmonic layer {Name}: order must be in the range (0, 256].");
        if (!double.IsFinite(Gain) || Gain is < 0.0 or > 4.0)
            throw new InvalidDataException($"Harmonic layer {Name}: gain must be in [0, 4].");
        if (!double.IsFinite(PhaseRadians))
            throw new InvalidDataException($"Harmonic layer {Name}: phase must be finite.");
        if (!double.IsFinite(ThrottleResponse) || ThrottleResponse is < 0.0 or > 4.0)
            throw new InvalidDataException($"Harmonic layer {Name}: throttle response must be in [0, 4].");
    }
}

public sealed record EventRenderRequest(
    string OutputPath,
    int Rpm,
    int Throttle,
    int SampleRate,
    int LengthSeconds,
    int Seed = 0);

public sealed record EventRenderMeasurement(
    string SourceId,
    string OutputPath,
    int Rpm,
    int Throttle,
    int SampleRate,
    int SampleCount,
    double PeakAbsolute,
    double RootMeanSquare,
    long ElapsedMilliseconds);

public static class AcousticEventSourceSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static AcousticEventSourceDefinition Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var source = JsonSerializer.Deserialize<AcousticEventSourceDefinition>(
            File.ReadAllText(path),
            JsonOptions)
            ?? throw new InvalidDataException($"Unable to deserialize acoustic source: {path}");

        source.Validate();
        return source;
    }

    public static void Write(AcousticEventSourceDefinition source, string path)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        source.Validate();

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Acoustic source output directory is invalid."));
        File.WriteAllText(fullPath, JsonSerializer.Serialize(source, JsonOptions) + Environment.NewLine);
    }
}
