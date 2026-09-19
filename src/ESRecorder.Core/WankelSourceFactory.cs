using System.Globalization;

namespace ESRecorder.Core;

public static class WankelSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int rotorCount,
        double displacementLitres,
        int redlineRpm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (rotorCount is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(rotorCount), "Rotor count must be between 1 and 32.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 20.0)
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 20] litres.");
        if (redlineRpm is < 1000 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(redlineRpm), "Redline must be between 1000 and 30000 RPM.");

        var phases = Enumerable.Range(0, rotorCount)
            .Select(index => index / (double)rotorCount)
            .ToArray();

        var pulseOrder = (double)rotorCount;
        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "wankel",
            MasterGain = 0.82,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "rotor-combustion-events",
                    ShaftRatio = 1.0,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 3.6 + (0.35 * Math.Min(rotorCount, 8)),
                    ResonanceBaseHz = 165.0 + (18.0 * Math.Min(rotorCount, 8)),
                    ResonanceOrder = pulseOrder * 1.5,
                    NoiseMix = 0.34,
                    ThrottleResponse = 0.72
                }
            ],
            HarmonicLayers =
            [
                new HarmonicLayerDefinition
                {
                    Name = "primary-pulse-order",
                    ShaftRatio = 1.0,
                    Order = pulseOrder,
                    Gain = 0.22,
                    PhaseRadians = 0.0,
                    ThrottleResponse = 0.8
                },
                new HarmonicLayerDefinition
                {
                    Name = "secondary-pulse-order",
                    ShaftRatio = 1.0,
                    Order = pulseOrder * 2.0,
                    Gain = 0.11,
                    PhaseRadians = Math.PI / 5.0,
                    ThrottleResponse = 0.9
                },
                new HarmonicLayerDefinition
                {
                    Name = "eccentric-rotor-gear-whine",
                    ShaftRatio = 1.0,
                    Order = 3.0,
                    Gain = 0.055,
                    PhaseRadians = Math.PI / 3.0,
                    ThrottleResponse = 0.35
                }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "wankel-event-v1",
                ["rotor_count"] = rotorCount.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_eccentric_shaft_revolution"] =
                    rotorCount.ToString(CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["redline_rpm"] = redlineRpm.ToString(CultureInfo.InvariantCulture),
                ["kinematic_note"] =
                    "Each rotor contributes one power event per eccentric-shaft revolution; rotor phases are evenly distributed."
            }
        };

        source.Validate();
        return source;
    }
}
