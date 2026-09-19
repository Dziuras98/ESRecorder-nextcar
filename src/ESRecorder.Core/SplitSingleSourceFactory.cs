using System.Globalization;

namespace ESRecorder.Core;

public static class SplitSingleSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int chamberCount,
        int bankCount,
        double displacementLitres,
        int maxRpm,
        double transferPistonPhaseDegrees = 15.0,
        string layout = "split-single",
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (chamberCount is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(chamberCount), "Combustion chamber count must be between 1 and 64.");
        if (bankCount is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(bankCount), "Bank count must be between 1 and 16.");
        if (bankCount > chamberCount)
            throw new ArgumentException("Bank count cannot exceed combustion chamber count.", nameof(bankCount));
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 100] litres.");
        }
        if (maxRpm is < 300 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 300 and 30000.");
        if (!double.IsFinite(transferPistonPhaseDegrees) || Math.Abs(transferPistonPhaseDegrees) > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transferPistonPhaseDegrees),
                "Transfer-piston phase must be finite and within +/-180 degrees.");
        }

        var phases = Enumerable.Range(0, chamberCount)
            .Select(index => index / (double)chamberCount)
            .ToArray();
        var harmonicLayers = new List<HarmonicLayerDefinition>(3 + bankCount)
        {
            new()
            {
                Name = "exhaust-piston-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.070,
                PhaseRadians = 0.0,
                ThrottleResponse = 0.34
            },
            new()
            {
                Name = "transfer-piston-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.066,
                PhaseRadians = transferPistonPhaseDegrees * Math.PI / 180.0,
                ThrottleResponse = 0.34
            },
            new()
            {
                Name = "paired-piston-secondary-order",
                ShaftRatio = 1.0,
                Order = 2.0,
                Gain = 0.040,
                PhaseRadians = (transferPistonPhaseDegrees * Math.PI / 180.0) + (Math.PI / 6.0),
                ThrottleResponse = 0.30
            }
        };

        for (var bank = 0; bank < bankCount; bank++)
        {
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"bank-{bank + 1}-scavenging-exhaust-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, chamberCount / (double)bankCount),
                Gain = 0.050 / Math.Sqrt(bankCount),
                PhaseRadians = (2.0 * Math.PI * bank) / bankCount,
                ThrottleResponse = 0.58
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "split-single-two-stroke",
            MasterGain = 0.82,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "shared-chamber-combustion-events",
                    ShaftRatio = 1.0,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 4.8 + (0.14 * Math.Min(chamberCount, 16)),
                    ResonanceBaseHz = 112.0,
                    ResonanceOrder = Math.Max(1.0, chamberCount * 1.08),
                    NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.40 : 0.30,
                    ThrottleResponse = 0.70
                }
            ],
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "split-single-two-stroke-event-v1",
                ["cycle"] = "two-stroke",
                ["combustion_chamber_count"] = chamberCount.ToString(CultureInfo.InvariantCulture),
                ["piston_count"] = (chamberCount * 2).ToString(CultureInfo.InvariantCulture),
                ["bank_count"] = bankCount.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] = chamberCount.ToString(CultureInfo.InvariantCulture),
                ["transfer_piston_phase_degrees"] =
                    transferPistonPhaseDegrees.ToString("0.###", CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["layout"] = layout,
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "Each combustion chamber is bounded by an exhaust-control piston and a transfer/scavenge piston with a fixed phase relationship; paired pistons are mechanical contributors and do not duplicate combustion events."
            }
        };

        source.Validate();
        return source;
    }
}
