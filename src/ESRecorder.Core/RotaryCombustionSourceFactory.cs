using System.Globalization;

namespace ESRecorder.Core;

public static class RotaryCombustionSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        string mechanism,
        int workingElementCount,
        double powerEventsPerOutputRevolution,
        int maxOutputRpm,
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (workingElementCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(workingElementCount), "Working-element count must be between 1 and 128.");
        if (!double.IsFinite(powerEventsPerOutputRevolution) ||
            powerEventsPerOutputRevolution is <= 0.0 or > 256.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(powerEventsPerOutputRevolution),
                "Power-event rate must be finite and in the range (0, 256].");
        }
        if (maxOutputRpm is < 100 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(maxOutputRpm), "Maximum output RPM must be between 100 and 100000.");

        var eventShaftRatio = powerEventsPerOutputRevolution / workingElementCount;
        if (eventShaftRatio is <= 0.0 or > 64.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(powerEventsPerOutputRevolution),
                $"Derived event-train shaft ratio must be in (0, 64], observed {eventShaftRatio}.");
        }

        var phases = Enumerable.Range(0, workingElementCount)
            .Select(index => index / (double)workingElementCount)
            .ToArray();
        var elementOrder = Math.Min(128.0, Math.Max(1.0, workingElementCount));
        var eventOrder = Math.Min(128.0, Math.Max(1.0, powerEventsPerOutputRevolution));

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "rotary-combustion",
            MasterGain = 0.80,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "rotary-combustion-events",
                    ShaftRatio = eventShaftRatio,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 3.8 + (0.10 * Math.Min(workingElementCount, 24)),
                    ResonanceBaseHz = 145.0,
                    ResonanceOrder = eventOrder * 1.15,
                    NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.38 : 0.29,
                    ThrottleResponse = 0.72
                }
            ],
            HarmonicLayers =
            [
                new HarmonicLayerDefinition
                {
                    Name = "output-shaft-primary-order",
                    ShaftRatio = 1.0,
                    Order = 1.0,
                    Gain = 0.050,
                    PhaseRadians = 0.0,
                    ThrottleResponse = 0.30
                },
                new HarmonicLayerDefinition
                {
                    Name = "rotary-working-element-order",
                    ShaftRatio = 1.0,
                    Order = elementOrder,
                    Gain = 0.075,
                    PhaseRadians = Math.PI / 7.0,
                    ThrottleResponse = 0.46
                },
                new HarmonicLayerDefinition
                {
                    Name = "rotary-seal-contact-secondary-order",
                    ShaftRatio = 1.0,
                    Order = Math.Min(128.0, Math.Max(2.0, elementOrder * 2.0)),
                    Gain = 0.036,
                    PhaseRadians = Math.PI / 3.0,
                    ThrottleResponse = 0.38
                }
            ],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "rotary-combustion-event-v1",
                ["mechanism"] = mechanism,
                ["working_element_count"] = workingElementCount.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] =
                    powerEventsPerOutputRevolution.ToString("0.###", CultureInfo.InvariantCulture),
                ["event_train_shaft_ratio"] =
                    eventShaftRatio.ToString("0.######", CultureInfo.InvariantCulture),
                ["max_output_rpm"] = maxOutputRpm.ToString(CultureInfo.InvariantCulture),
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "Generic non-Wankel rotary-combustion source. Working-element count and event rate are explicit authoring inputs; the model must not be used as an implicit Wankel fallback."
            }
        };

        source.Validate();
        return source;
    }
}
