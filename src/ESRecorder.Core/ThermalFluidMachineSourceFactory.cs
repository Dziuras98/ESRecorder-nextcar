using System.Globalization;

namespace ESRecorder.Core;

public static class ThermalFluidMachineSourceFactory
{
    private static readonly HashSet<string> SupportedClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "reciprocating-external-combustion",
        "rotary-expander",
        "turbine",
        "wave-rotor"
    };

    public static AcousticEventSourceDefinition Create(
        string id,
        string machineClass,
        string mechanism,
        int workingElementCount,
        double pressureEventsPerReferenceRevolution,
        int maxReferenceRpm,
        int bladeOrLobeOrder,
        string workingFluid = "generic",
        string thermalResponseClass = "thermal-lag")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineClass);
        ArgumentException.ThrowIfNullOrWhiteSpace(mechanism);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingFluid);
        ArgumentException.ThrowIfNullOrWhiteSpace(thermalResponseClass);

        if (!SupportedClasses.Contains(machineClass))
        {
            throw new ArgumentException(
                $"Unsupported thermal-fluid machine class: {machineClass}.",
                nameof(machineClass));
        }
        if (workingElementCount is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workingElementCount),
                "Working-element count must be between 1 and 128.");
        }
        if (!double.IsFinite(pressureEventsPerReferenceRevolution) ||
            pressureEventsPerReferenceRevolution is < 0.0 or > 256.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pressureEventsPerReferenceRevolution),
                "Pressure-event rate must be finite and in [0, 256].");
        }
        if (maxReferenceRpm is < 50 or > 200000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxReferenceRpm),
                "Maximum reference RPM must be between 50 and 200000.");
        }
        if (bladeOrLobeOrder is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bladeOrLobeOrder),
                "Blade/lobe order must be between 1 and 256.");
        }

        var eventTrains = new List<PeriodicEventTrainDefinition>();
        if (pressureEventsPerReferenceRevolution > 0.0)
        {
            var eventShaftRatio =
                pressureEventsPerReferenceRevolution / workingElementCount;
            if (eventShaftRatio is <= 0.0 or > 64.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pressureEventsPerReferenceRevolution),
                    $"Derived event-train shaft ratio must be in (0, 64], observed {eventShaftRatio}.");
            }

            var phases = Enumerable.Range(0, workingElementCount)
                .Select(index => index / (double)workingElementCount)
                .ToArray();

            eventTrains.Add(new PeriodicEventTrainDefinition
            {
                Name = "thermal-pressure-events",
                ShaftRatio = eventShaftRatio,
                EventPhases = phases,
                Gain = machineClass.Equals(
                        "reciprocating-external-combustion",
                        StringComparison.OrdinalIgnoreCase)
                    ? 0.82
                    : 0.52,
                DecayMilliseconds = machineClass.Equals(
                        "reciprocating-external-combustion",
                        StringComparison.OrdinalIgnoreCase)
                    ? 8.0
                    : 3.5,
                ResonanceBaseHz = 72.0,
                ResonanceOrder = Math.Min(
                    128.0,
                    Math.Max(1.0, pressureEventsPerReferenceRevolution)),
                NoiseMix = machineClass.Equals("rotary-expander", StringComparison.OrdinalIgnoreCase)
                    ? 0.34
                    : 0.22,
                ThrottleResponse = 0.58
            });
        }

        var mechanicalGain =
            machineClass.Equals("turbine", StringComparison.OrdinalIgnoreCase) ? 0.16 : 0.10;
        var flowGain =
            machineClass.Equals("wave-rotor", StringComparison.OrdinalIgnoreCase) ? 0.12 : 0.07;

        var harmonicLayers = new[]
        {
            new HarmonicLayerDefinition
            {
                Name = "reference-shaft-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.055,
                PhaseRadians = 0.0,
                ThrottleResponse = 0.28
            },
            new HarmonicLayerDefinition
            {
                Name = "blade-lobe-passage-order",
                ShaftRatio = 1.0,
                Order = bladeOrLobeOrder,
                Gain = mechanicalGain,
                PhaseRadians = Math.PI / 11.0,
                ThrottleResponse = 0.74
            },
            new HarmonicLayerDefinition
            {
                Name = "blade-lobe-secondary-order",
                ShaftRatio = 1.0,
                Order = Math.Min(256.0, bladeOrLobeOrder * 2.0),
                Gain = mechanicalGain * 0.42,
                PhaseRadians = Math.PI / 4.0,
                ThrottleResponse = 0.68
            },
            new HarmonicLayerDefinition
            {
                Name = "working-fluid-flow-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, Math.Min(128.0, workingElementCount)),
                Gain = flowGain,
                PhaseRadians = Math.PI / 6.0,
                ThrottleResponse = 0.64
            }
        };

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "thermal-fluid-machine",
            MasterGain = 0.76,
            EventTrains = eventTrains.ToArray(),
            HarmonicLayers = harmonicLayers,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "thermal-fluid-machine-event-v1",
                ["machine_class"] = machineClass,
                ["mechanism"] = mechanism,
                ["working_element_count"] =
                    workingElementCount.ToString(CultureInfo.InvariantCulture),
                ["pressure_events_per_reference_revolution"] =
                    pressureEventsPerReferenceRevolution.ToString(
                        "0.###",
                        CultureInfo.InvariantCulture),
                ["blade_or_lobe_order"] =
                    bladeOrLobeOrder.ToString(CultureInfo.InvariantCulture),
                ["max_reference_rpm"] =
                    maxReferenceRpm.ToString(CultureInfo.InvariantCulture),
                ["working_fluid"] = workingFluid,
                ["thermal_response_class"] = thermalResponseClass,
                ["kinematic_note"] =
                    "Generic external-combustion/thermal-fluid acoustic source. " +
                    "Reciprocating/rotary expanders may include explicit pressure events; " +
                    "continuous turbines and wave-rotor sources may use zero pressure events " +
                    "and remain harmonic/flow driven. Thermodynamic transient response remains " +
                    "authoring metadata rather than a runtime vehicle simulation."
            }
        };

        source.Validate();
        return source;
    }
}
