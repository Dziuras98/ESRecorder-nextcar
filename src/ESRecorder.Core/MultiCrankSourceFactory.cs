using System.Globalization;

namespace ESRecorder.Core;

public sealed record MultiCrankModuleSpec(
    string Name,
    int PowerEventsPerReferenceRevolution,
    double ShaftRatio = 1.0,
    double PhaseOffsetRevolutions = 0.0,
    double Gain = 1.0,
    double MechanicalOrder = 1.0);

public static class MultiCrankSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        IReadOnlyList<MultiCrankModuleSpec> modules,
        int redlineRpm,
        string family = "multi-crank",
        double masterGain = 0.78)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(modules);

        if (modules.Count is < 2 or > 16)
            throw new ArgumentOutOfRangeException(nameof(modules), "Multi-crank sources require 2 to 16 modules.");
        if (redlineRpm is < 500 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(redlineRpm), "Redline must be between 500 and 30000 RPM.");
        if (!double.IsFinite(masterGain) || masterGain is <= 0.0 or > 4.0)
            throw new ArgumentOutOfRangeException(nameof(masterGain), "Master gain must be finite and in (0, 4].");

        var eventTrains = new List<PeriodicEventTrainDefinition>(modules.Count);
        var harmonicLayers = new List<HarmonicLayerDefinition>(modules.Count * 2);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var gainScale = 1.0 / Math.Sqrt(modules.Count);
        var effectivePowerEvents = 0.0;

        for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            var module = modules[moduleIndex];
            ValidateModule(module, names);

            var phaseOffset = Fractional(module.PhaseOffsetRevolutions);
            var phases = Enumerable.Range(0, module.PowerEventsPerReferenceRevolution)
                .Select(index => Fractional(
                    phaseOffset + (index / (double)module.PowerEventsPerReferenceRevolution)))
                .ToArray();

            effectivePowerEvents += module.PowerEventsPerReferenceRevolution * module.ShaftRatio;

            eventTrains.Add(new PeriodicEventTrainDefinition
            {
                Name = $"{module.Name}-power-events",
                ShaftRatio = module.ShaftRatio,
                EventPhases = phases,
                Gain = module.Gain * gainScale,
                DecayMilliseconds = 4.2 + (0.15 * Math.Min(module.PowerEventsPerReferenceRevolution, 16)),
                ResonanceBaseHz = 115.0 + (12.0 * moduleIndex),
                ResonanceOrder = Math.Max(1.0, module.PowerEventsPerReferenceRevolution * 1.25),
                NoiseMix = 0.26,
                ThrottleResponse = 0.78
            });

            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"{module.Name}-crank-order",
                ShaftRatio = module.ShaftRatio,
                Order = module.MechanicalOrder,
                Gain = 0.075 * gainScale,
                PhaseRadians = phaseOffset * 2.0 * Math.PI,
                ThrottleResponse = 0.42
            });

            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"{module.Name}-secondary-order",
                ShaftRatio = module.ShaftRatio,
                Order = Math.Max(2.0, module.MechanicalOrder * 2.0),
                Gain = 0.035 * gainScale,
                PhaseRadians = (phaseOffset * 2.0 * Math.PI) + (Math.PI / 7.0),
                ThrottleResponse = 0.35
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = family,
            MasterGain = masterGain,
            EventTrains = eventTrains.ToArray(),
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "multi-crank-event-v1",
                ["module_count"] = modules.Count.ToString(CultureInfo.InvariantCulture),
                ["redline_rpm"] = redlineRpm.ToString(CultureInfo.InvariantCulture),
                ["effective_power_events_per_reference_revolution"] =
                    effectivePowerEvents.ToString("0.###", CultureInfo.InvariantCulture),
                ["module_names"] = string.Join(",", modules.Select(static module => module.Name)),
                ["kinematic_note"] =
                    "Each crank module preserves its own shaft ratio, phase offset and periodic power-event train."
            }
        };

        source.Validate();
        return source;
    }

    private static void ValidateModule(MultiCrankModuleSpec module, HashSet<string> names)
    {
        if (string.IsNullOrWhiteSpace(module.Name))
            throw new ArgumentException("Every multi-crank module requires a name.", nameof(module));
        if (!names.Add(module.Name))
            throw new ArgumentException($"Duplicate multi-crank module name: {module.Name}.", nameof(module));
        if (module.PowerEventsPerReferenceRevolution is < 1 or > 128)
            throw new ArgumentOutOfRangeException(
                nameof(module),
                $"Module {module.Name}: power-event count must be between 1 and 128.");
        if (!double.IsFinite(module.ShaftRatio) || module.ShaftRatio is <= 0.0 or > 64.0)
            throw new ArgumentOutOfRangeException(
                nameof(module),
                $"Module {module.Name}: shaft ratio must be finite and in (0, 64].");
        if (!double.IsFinite(module.PhaseOffsetRevolutions))
            throw new ArgumentOutOfRangeException(
                nameof(module),
                $"Module {module.Name}: phase offset must be finite.");
        if (!double.IsFinite(module.Gain) || module.Gain is <= 0.0 or > 4.0)
            throw new ArgumentOutOfRangeException(
                nameof(module),
                $"Module {module.Name}: gain must be finite and in (0, 4].");
        if (!double.IsFinite(module.MechanicalOrder) || module.MechanicalOrder is <= 0.0 or > 256.0)
            throw new ArgumentOutOfRangeException(
                nameof(module),
                $"Module {module.Name}: mechanical order must be finite and in (0, 256].");
    }

    private static double Fractional(double value)
    {
        var result = value - Math.Floor(value);
        return result < 0.0 ? result + 1.0 : result;
    }
}
