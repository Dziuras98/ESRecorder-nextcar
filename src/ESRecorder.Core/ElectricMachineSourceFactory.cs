using System.Globalization;

namespace ESRecorder.Core;

public static class ElectricMachineSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int polePairs,
        int maxRpm,
        int machineCount = 1,
        string machineType = "permanent-magnet",
        int slotOrder = 12,
        int inverterOrder = 24)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(machineType);

        if (polePairs is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(polePairs), "Pole-pair count must be between 1 and 64.");
        if (maxRpm is < 100 or > 100000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 100 and 100000.");
        if (machineCount is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(machineCount), "Machine count must be between 1 and 16.");
        if (slotOrder is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(slotOrder), "Slot order must be between 1 and 256.");
        if (inverterOrder is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(inverterOrder), "Inverter order must be between 1 and 256.");

        var layers = new List<HarmonicLayerDefinition>(machineCount * 4);
        var gainScale = 1.0 / Math.Sqrt(machineCount);

        for (var machineIndex = 0; machineIndex < machineCount; machineIndex++)
        {
            var phase = (2.0 * Math.PI * machineIndex) / machineCount;
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-electrical-fundamental",
                ShaftRatio = 1.0,
                Order = polePairs,
                Gain = 0.150 * gainScale,
                PhaseRadians = phase,
                ThrottleResponse = 0.82
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-electrical-second-harmonic",
                ShaftRatio = 1.0,
                Order = Math.Min(256.0, polePairs * 2.0),
                Gain = 0.065 * gainScale,
                PhaseRadians = phase + (Math.PI / 9.0),
                ThrottleResponse = 0.86
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-slot-order",
                ShaftRatio = 1.0,
                Order = slotOrder,
                Gain = 0.045 * gainScale,
                PhaseRadians = phase + (Math.PI / 4.0),
                ThrottleResponse = 0.62
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-inverter-order",
                ShaftRatio = 1.0,
                Order = inverterOrder,
                Gain = 0.032 * gainScale,
                PhaseRadians = phase + (Math.PI / 2.0),
                ThrottleResponse = 0.92
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "electric-machine",
            MasterGain = 0.74,
            EventTrains = [],
            HarmonicLayers = layers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "electric-machine-event-v1",
                ["machine_type"] = machineType,
                ["machine_count"] = machineCount.ToString(CultureInfo.InvariantCulture),
                ["pole_pairs"] = polePairs.ToString(CultureInfo.InvariantCulture),
                ["electrical_fundamental_order"] = polePairs.ToString(CultureInfo.InvariantCulture),
                ["slot_order"] = slotOrder.ToString(CultureInfo.InvariantCulture),
                ["inverter_order"] = inverterOrder.ToString(CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["kinematic_note"] =
                    "Electrical whine is represented with shaft-order harmonics derived from pole-pair count plus slot and inverter-related orders; no combustion event train is created."
            }
        };

        source.Validate();
        return source;
    }
}
