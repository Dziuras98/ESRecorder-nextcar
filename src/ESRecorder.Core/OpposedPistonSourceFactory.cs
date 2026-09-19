using System.Globalization;

namespace ESRecorder.Core;

public static class OpposedPistonSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int chamberCount,
        double displacementLitres,
        int redlineRpm,
        int crankshaftCount = 2,
        double crankPhaseDegrees = 12.0,
        string combustionClass = "generic")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (chamberCount is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(chamberCount), "Chamber count must be between 1 and 64.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 100.0)
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 100] litres.");
        if (redlineRpm is < 300 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(redlineRpm), "Redline must be between 300 and 20000 RPM.");
        if (crankshaftCount is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(crankshaftCount), "Crankshaft count must be between 1 and 8.");
        if (!double.IsFinite(crankPhaseDegrees) || Math.Abs(crankPhaseDegrees) > 180.0)
            throw new ArgumentOutOfRangeException(
                nameof(crankPhaseDegrees),
                "Crank phase increment must be finite and within +/-180 degrees.");

        var phases = Enumerable.Range(0, chamberCount)
            .Select(index => index / (double)chamberCount)
            .ToArray();

        var harmonicLayers = new List<HarmonicLayerDefinition>((crankshaftCount * 2) + 1);
        var perCrankGain = 0.070 / Math.Sqrt(crankshaftCount);

        for (var crankIndex = 0; crankIndex < crankshaftCount; crankIndex++)
        {
            var phaseRadians = crankIndex * crankPhaseDegrees * Math.PI / 180.0;
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"crankshaft-{crankIndex + 1}-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = perCrankGain,
                PhaseRadians = phaseRadians,
                ThrottleResponse = 0.34
            });
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"crankshaft-{crankIndex + 1}-secondary-order",
                ShaftRatio = 1.0,
                Order = 2.0,
                Gain = perCrankGain * 0.55,
                PhaseRadians = phaseRadians + (Math.PI / 5.0),
                ThrottleResponse = 0.32
            });
        }

        harmonicLayers.Add(new HarmonicLayerDefinition
        {
            Name = "opposed-piston-scavenging-flow",
            ShaftRatio = 1.0,
            Order = Math.Max(1.0, chamberCount),
            Gain = 0.070,
            PhaseRadians = Math.PI / 3.0,
            ThrottleResponse = 0.58
        });

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "opposed-piston",
            MasterGain = 0.82,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "shared-chamber-combustion-events",
                    ShaftRatio = 1.0,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 5.2 + (0.16 * Math.Min(chamberCount, 16)),
                    ResonanceBaseHz = 100.0,
                    ResonanceOrder = Math.Max(1.0, chamberCount * 1.05),
                    NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.40 : 0.31,
                    ThrottleResponse = 0.68
                }
            ],
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "opposed-piston-two-stroke-event-v1",
                ["cycle"] = "two-stroke",
                ["chamber_count"] = chamberCount.ToString(CultureInfo.InvariantCulture),
                ["piston_count"] = (chamberCount * 2).ToString(CultureInfo.InvariantCulture),
                ["crankshaft_count"] = crankshaftCount.ToString(CultureInfo.InvariantCulture),
                ["crank_phase_increment_degrees"] =
                    crankPhaseDegrees.ToString("0.###", CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] =
                    chamberCount.ToString(CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["redline_rpm"] = redlineRpm.ToString(CultureInfo.InvariantCulture),
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "Each opposed-piston combustion chamber contributes one power event per output revolution; paired pistons and crankshafts are represented as mechanical layers, not duplicate combustion events."
            }
        };

        source.Validate();
        return source;
    }
}
