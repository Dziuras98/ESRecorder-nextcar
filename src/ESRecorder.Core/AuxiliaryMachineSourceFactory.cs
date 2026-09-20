using System.Globalization;

namespace ESRecorder.Core;

public static class AuxiliaryMachineSourceFactory
{
    private static readonly HashSet<string> SupportedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "flywheel",
        "hydraulic-machine",
        "mechanical-accessory"
    };

    public static AcousticEventSourceDefinition Create(
        string id,
        string machineClass,
        int workingElementCount,
        int maxRpm,
        double primaryOrder,
        double rippleEventsPerRevolution = 0.0,
        double noiseMix = 0.08)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineClass);

        if (!SupportedClasses.Contains(machineClass))
            throw new ArgumentException($"Unsupported auxiliary machine class: {machineClass}.", nameof(machineClass));
        if (workingElementCount is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(workingElementCount), "Working-element count must be between 1 and 256.");
        if (maxRpm is < 100 or > 200000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 100 and 200000.");
        if (!double.IsFinite(primaryOrder) || primaryOrder is <= 0.0 or > 256.0)
            throw new ArgumentOutOfRangeException(nameof(primaryOrder), "Primary order must be finite and in (0, 256].");
        if (!double.IsFinite(rippleEventsPerRevolution) || rippleEventsPerRevolution is < 0.0 or > 256.0)
            throw new ArgumentOutOfRangeException(nameof(rippleEventsPerRevolution), "Ripple-event rate must be finite and in [0, 256].");
        if (!double.IsFinite(noiseMix) || noiseMix is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(noiseMix), "Noise mix must be finite and in [0, 1].");

        var eventTrains = new List<PeriodicEventTrainDefinition>();
        if (rippleEventsPerRevolution > 0.0)
        {
            var eventRatio = rippleEventsPerRevolution / workingElementCount;
            if (eventRatio is <= 0.0 or > 64.0)
                throw new ArgumentOutOfRangeException(
                    nameof(rippleEventsPerRevolution),
                    $"Derived ripple-event shaft ratio must be in (0, 64], observed {eventRatio}.");

            eventTrains.Add(new PeriodicEventTrainDefinition
            {
                Name = "auxiliary-ripple-events",
                ShaftRatio = eventRatio,
                EventPhases = Enumerable.Range(0, workingElementCount)
                    .Select(index => index / (double)workingElementCount)
                    .ToArray(),
                Gain = machineClass.Equals("hydraulic-machine", StringComparison.OrdinalIgnoreCase) ? 0.42 : 0.20,
                DecayMilliseconds = machineClass.Equals("hydraulic-machine", StringComparison.OrdinalIgnoreCase) ? 2.8 : 2.0,
                ResonanceBaseHz = machineClass.Equals("hydraulic-machine", StringComparison.OrdinalIgnoreCase) ? 180.0 : 240.0,
                ResonanceOrder = Math.Min(128.0, Math.Max(1.0, rippleEventsPerRevolution)),
                NoiseMix = noiseMix,
                ThrottleResponse = 0.55
            });
        }

        var primaryGain = machineClass.Equals("flywheel", StringComparison.OrdinalIgnoreCase) ? 0.14 : 0.10;
        var secondaryGain = primaryGain * 0.46;

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "auxiliary-machine",
            MasterGain = 0.72,
            EventTrains = eventTrains.ToArray(),
            HarmonicLayers =
            [
                new HarmonicLayerDefinition
                {
                    Name = "auxiliary-primary-order",
                    ShaftRatio = 1.0,
                    Order = primaryOrder,
                    Gain = primaryGain,
                    PhaseRadians = 0.0,
                    ThrottleResponse = 0.72
                },
                new HarmonicLayerDefinition
                {
                    Name = "auxiliary-secondary-order",
                    ShaftRatio = 1.0,
                    Order = Math.Min(256.0, primaryOrder * 2.0),
                    Gain = secondaryGain,
                    PhaseRadians = Math.PI / 5.0,
                    ThrottleResponse = 0.64
                },
                new HarmonicLayerDefinition
                {
                    Name = machineClass.Equals("hydraulic-machine", StringComparison.OrdinalIgnoreCase)
                        ? "hydraulic-flow-order"
                        : "bearing-structure-order",
                    ShaftRatio = 1.0,
                    Order = Math.Min(256.0, Math.Max(1.0, workingElementCount)),
                    Gain = machineClass.Equals("hydraulic-machine", StringComparison.OrdinalIgnoreCase) ? 0.075 : 0.040,
                    PhaseRadians = Math.PI / 3.0,
                    ThrottleResponse = 0.50
                }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "auxiliary-machine-event-v1",
                ["machine_class"] = machineClass,
                ["working_element_count"] = workingElementCount.ToString(CultureInfo.InvariantCulture),
                ["primary_order"] = primaryOrder.ToString("0.###", CultureInfo.InvariantCulture),
                ["ripple_events_per_revolution"] =
                    rippleEventsPerRevolution.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["acoustic_note"] =
                    "Offline auxiliary-machine authoring source. Flywheel and hydraulic profiles are acoustic abstractions and do not claim exact OEM construction."
            }
        };

        source.Validate();
        return source;
    }
}
