using System.Globalization;

namespace ESRecorder.Core;

public static class AxialPistonSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int pistonCount,
        double displacementLitres,
        int maxRpm,
        int cycleRevolutions = 2,
        string mechanism = "swashplate",
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (pistonCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(pistonCount), "Piston count must be between 1 and 128.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 50.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 50] litres.");
        }
        if (maxRpm is < 300 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 300 and 30000.");
        if (cycleRevolutions is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cycleRevolutions),
                "Event cycle must span between 1 and 8 output-shaft revolutions.");
        }

        var phases = Enumerable.Range(0, pistonCount)
            .Select(index => index / (double)pistonCount)
            .ToArray();
        var eventRate = pistonCount / (double)cycleRevolutions;

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "axial-piston",
            MasterGain = 0.79,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "axial-piston-combustion-events",
                    ShaftRatio = 1.0 / cycleRevolutions,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 4.2 + (0.10 * Math.Min(pistonCount, 24)),
                    ResonanceBaseHz = 130.0,
                    ResonanceOrder = Math.Max(1.0, Math.Min(128.0, eventRate * 1.25)),
                    NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.37 : 0.27,
                    ThrottleResponse = 0.72
                }
            ],
            HarmonicLayers =
            [
                new HarmonicLayerDefinition
                {
                    Name = "output-shaft-order",
                    ShaftRatio = 1.0,
                    Order = 1.0,
                    Gain = 0.050,
                    PhaseRadians = 0.0,
                    ThrottleResponse = 0.30
                },
                new HarmonicLayerDefinition
                {
                    Name = $"{mechanism}-mechanical-order",
                    ShaftRatio = 1.0,
                    Order = Math.Min(128.0, Math.Max(1.0, pistonCount)),
                    Gain = 0.080,
                    PhaseRadians = Math.PI / 7.0,
                    ThrottleResponse = 0.42
                },
                new HarmonicLayerDefinition
                {
                    Name = $"{mechanism}-secondary-order",
                    ShaftRatio = 1.0,
                    Order = Math.Min(128.0, Math.Max(2.0, pistonCount * 2.0)),
                    Gain = 0.036,
                    PhaseRadians = Math.PI / 3.0,
                    ThrottleResponse = 0.36
                }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "axial-piston-event-v1",
                ["piston_count"] = pistonCount.ToString(CultureInfo.InvariantCulture),
                ["event_cycle_revolutions"] = cycleRevolutions.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] =
                    eventRate.ToString("0.###", CultureInfo.InvariantCulture),
                ["mechanism"] = mechanism,
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["combustion_class"] = combustionClass
            }
        };

        source.Validate();
        return source;
    }
}
