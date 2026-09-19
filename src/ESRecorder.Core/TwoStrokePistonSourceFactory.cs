using System.Globalization;

namespace ESRecorder.Core;

public static class TwoStrokePistonSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int cylinderCount,
        double displacementLitres,
        int redlineRpm,
        string layout = "unspecified",
        string scavenging = "generic")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(scavenging);

        if (cylinderCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(cylinderCount), "Cylinder count must be between 1 and 128.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 50.0)
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 50] litres.");
        if (redlineRpm is < 500 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(redlineRpm), "Redline must be between 500 and 30000 RPM.");

        var phases = Enumerable.Range(0, cylinderCount)
            .Select(index => index / (double)cylinderCount)
            .ToArray();
        var pulseOrder = (double)cylinderCount;

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "two-stroke-piston",
            MasterGain = 0.80,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "two-stroke-combustion-events",
                    ShaftRatio = 1.0,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 4.0 + (0.12 * Math.Min(cylinderCount, 16)),
                    ResonanceBaseHz = 125.0,
                    ResonanceOrder = Math.Max(1.0, pulseOrder * 1.15),
                    NoiseMix = 0.30,
                    ThrottleResponse = 0.70
                }
            ],
            HarmonicLayers =
            [
                new HarmonicLayerDefinition
                {
                    Name = "two-stroke-primary-order",
                    ShaftRatio = 1.0,
                    Order = pulseOrder,
                    Gain = 0.18,
                    PhaseRadians = 0.0,
                    ThrottleResponse = 0.75
                },
                new HarmonicLayerDefinition
                {
                    Name = "reciprocating-secondary-order",
                    ShaftRatio = 1.0,
                    Order = 2.0,
                    Gain = 0.060,
                    PhaseRadians = Math.PI / 6.0,
                    ThrottleResponse = 0.38
                },
                new HarmonicLayerDefinition
                {
                    Name = "scavenging-flow-order",
                    ShaftRatio = 1.0,
                    Order = Math.Max(1.0, pulseOrder),
                    Gain = 0.050,
                    PhaseRadians = Math.PI / 4.0,
                    ThrottleResponse = 0.55
                }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "two-stroke-piston-event-v1",
                ["cycle"] = "two-stroke",
                ["cylinder_count"] = cylinderCount.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_crankshaft_revolution"] =
                    cylinderCount.ToString(CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["redline_rpm"] = redlineRpm.ToString(CultureInfo.InvariantCulture),
                ["layout"] = layout,
                ["scavenging"] = scavenging,
                ["kinematic_note"] =
                    "The generic two-stroke model schedules one power event per cylinder per crankshaft revolution."
            }
        };

        source.Validate();
        return source;
    }
}
