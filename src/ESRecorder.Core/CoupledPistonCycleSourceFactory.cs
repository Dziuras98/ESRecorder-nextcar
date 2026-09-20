using System.Globalization;

namespace ESRecorder.Core;

public static class CoupledPistonCycleSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        string cycleClass,
        int combustionCylinderCount,
        int secondaryCylinderCount,
        int secondaryPressureEventsPerCycle,
        double displacementLitres,
        int maxRpm,
        double cycleRevolutions = 2.0,
        double secondaryPhaseDegrees = 180.0,
        bool pneumaticAccumulator = false,
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(cycleClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (combustionCylinderCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(
                nameof(combustionCylinderCount),
                "Combustion-cylinder count must be between 1 and 128.");
        if (secondaryCylinderCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(
                nameof(secondaryCylinderCount),
                "Secondary-cylinder count must be between 1 and 128.");
        if (secondaryPressureEventsPerCycle is < 1 or > 256)
            throw new ArgumentOutOfRangeException(
                nameof(secondaryPressureEventsPerCycle),
                "Secondary pressure-event count must be between 1 and 256.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 100.0)
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 100] litres.");
        if (maxRpm is < 200 or > 30000)
            throw new ArgumentOutOfRangeException(
                nameof(maxRpm),
                "Maximum RPM must be between 200 and 30000.");
        if (!double.IsFinite(cycleRevolutions) || cycleRevolutions is < 1.0 or > 8.0)
            throw new ArgumentOutOfRangeException(
                nameof(cycleRevolutions),
                "Cycle revolutions must be finite and in [1, 8].");
        if (!double.IsFinite(secondaryPhaseDegrees) || Math.Abs(secondaryPhaseDegrees) > 2880.0)
            throw new ArgumentOutOfRangeException(
                nameof(secondaryPhaseDegrees),
                "Secondary phase must be finite and within +/-2880 degrees.");

        var cycleDegrees = cycleRevolutions * 360.0;
        var secondaryPhase = NormalizePhase(secondaryPhaseDegrees / cycleDegrees);

        var combustionPhases = Enumerable.Range(0, combustionCylinderCount)
            .Select(index => index / (double)combustionCylinderCount)
            .ToArray();
        var secondaryPhases = Enumerable.Range(0, secondaryPressureEventsPerCycle)
            .Select(index => NormalizePhase(
                secondaryPhase + (index / (double)secondaryPressureEventsPerCycle)))
            .ToArray();

        var combustionGain = 1.0 / Math.Sqrt(combustionCylinderCount);
        var secondaryGain = 0.48 / Math.Sqrt(secondaryPressureEventsPerCycle);

        var harmonicLayers = new List<HarmonicLayerDefinition>
        {
            new()
            {
                Name = "shared-crank-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.060,
                PhaseRadians = 0.0,
                ThrottleResponse = 0.34
            },
            new()
            {
                Name = "combustion-group-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, combustionCylinderCount / cycleRevolutions),
                Gain = 0.060,
                PhaseRadians = 0.0,
                ThrottleResponse = 0.55
            },
            new()
            {
                Name = "secondary-cylinder-mechanical-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, secondaryCylinderCount / cycleRevolutions),
                Gain = 0.050,
                PhaseRadians = secondaryPhase * 2.0 * Math.PI,
                ThrottleResponse = 0.45
            }
        };

        if (pneumaticAccumulator)
        {
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = "pneumatic-accumulator-flow-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, secondaryPressureEventsPerCycle / cycleRevolutions),
                Gain = 0.050,
                PhaseRadians = (secondaryPhase * 2.0 * Math.PI) + (Math.PI / 7.0),
                ThrottleResponse = 0.62
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "coupled-piston-cycle",
            MasterGain = 0.80,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "combustion-events",
                    ShaftRatio = 1.0 / cycleRevolutions,
                    EventPhases = combustionPhases,
                    Gain = combustionGain,
                    DecayMilliseconds = 5.0 + (0.10 * Math.Min(combustionCylinderCount, 16)),
                    ResonanceBaseHz = 115.0,
                    ResonanceOrder = Math.Max(
                        1.0,
                        combustionCylinderCount / cycleRevolutions),
                    NoiseMix = combustionClass.Contains(
                            "diesel",
                            StringComparison.OrdinalIgnoreCase)
                        ? 0.38
                        : 0.27,
                    ThrottleResponse = 0.72
                },
                new PeriodicEventTrainDefinition
                {
                    Name = "secondary-pressure-events",
                    ShaftRatio = 1.0 / cycleRevolutions,
                    EventPhases = secondaryPhases,
                    Gain = secondaryGain,
                    DecayMilliseconds = 8.0,
                    ResonanceBaseHz = 82.0,
                    ResonanceOrder = Math.Max(
                        1.0,
                        secondaryPressureEventsPerCycle / cycleRevolutions),
                    NoiseMix = 0.18,
                    ThrottleResponse = 0.52
                }
            ],
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "coupled-piston-cycle-event-v1",
                ["cycle_class"] = cycleClass,
                ["combustion_cylinder_count"] =
                    combustionCylinderCount.ToString(CultureInfo.InvariantCulture),
                ["secondary_cylinder_count"] =
                    secondaryCylinderCount.ToString(CultureInfo.InvariantCulture),
                ["secondary_pressure_events_per_cycle"] =
                    secondaryPressureEventsPerCycle.ToString(CultureInfo.InvariantCulture),
                ["cycle_revolutions"] =
                    cycleRevolutions.ToString("0.###", CultureInfo.InvariantCulture),
                ["secondary_phase_degrees"] =
                    secondaryPhaseDegrees.ToString("0.###", CultureInfo.InvariantCulture),
                ["pneumatic_accumulator"] =
                    pneumaticAccumulator ? "true" : "false",
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "Combustion and secondary compression/expansion pressure events are represented " +
                    "as separate trains on a shared reference cycle. Secondary events are acoustic " +
                    "authoring events, not additional combustion cylinders."
            }
        };

        source.Validate();
        return source;
    }

    private static double NormalizePhase(double phase)
    {
        var normalized = phase - Math.Floor(phase);
        return normalized < 0.0 ? normalized + 1.0 : normalized;
    }
}
