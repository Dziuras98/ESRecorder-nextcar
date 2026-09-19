using System.Globalization;

namespace ESRecorder.Core;

public static class FreePistonSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int moduleCount,
        double displacementEquivalentLitres,
        int maxCyclesPerMinute,
        string generatorClass = "linear-generator",
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (moduleCount is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(moduleCount), "Module count must be between 1 and 32.");
        if (!double.IsFinite(displacementEquivalentLitres) ||
            displacementEquivalentLitres is <= 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementEquivalentLitres),
                "Equivalent displacement must be finite and in the range (0, 100] litres.");
        }
        if (maxCyclesPerMinute is < 100 or > 30000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCyclesPerMinute),
                "Maximum oscillation rate must be between 100 and 30000 cycles/min.");
        }

        var eventTrains = new List<PeriodicEventTrainDefinition>(moduleCount);
        var harmonicLayers = new List<HarmonicLayerDefinition>(moduleCount * 2);
        var gainScale = 1.0 / Math.Sqrt(moduleCount);

        for (var moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
        {
            var phase = moduleIndex / (double)moduleCount;
            eventTrains.Add(new PeriodicEventTrainDefinition
            {
                Name = $"module-{moduleIndex + 1}-combustion-event",
                ShaftRatio = 1.0,
                EventPhases = [phase],
                Gain = gainScale,
                DecayMilliseconds = 6.5,
                ResonanceBaseHz = 85.0 + (4.0 * moduleIndex),
                ResonanceOrder = 1.0,
                NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.42 : 0.32,
                ThrottleResponse = 0.65
            });

            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"module-{moduleIndex + 1}-linear-oscillation-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.060 * gainScale,
                PhaseRadians = phase * 2.0 * Math.PI,
                ThrottleResponse = 0.32
            });
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"module-{moduleIndex + 1}-{generatorClass}-order",
                ShaftRatio = 1.0,
                Order = 2.0,
                Gain = 0.040 * gainScale,
                PhaseRadians = (phase * 2.0 * Math.PI) + (Math.PI / 5.0),
                ThrottleResponse = 0.45
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "free-piston",
            MasterGain = 0.82,
            EventTrains = eventTrains.ToArray(),
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "free-piston-event-v1",
                ["module_count"] = moduleCount.ToString(CultureInfo.InvariantCulture),
                ["combustion_events_per_reference_cycle"] =
                    moduleCount.ToString(CultureInfo.InvariantCulture),
                ["reference_rate_unit"] = "cycles_per_minute",
                ["max_reference_rate"] = maxCyclesPerMinute.ToString(CultureInfo.InvariantCulture),
                ["displacement_equivalent_litres"] =
                    displacementEquivalentLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["generator_class"] = generatorClass,
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "The renderer RPM input is interpreted as free-piston oscillation cycles per minute for this family; no fictitious crankshaft is introduced."
            }
        };

        source.Validate();
        return source;
    }
}
