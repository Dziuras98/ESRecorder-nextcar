using System.Globalization;

namespace ESRecorder.Core;

public static class ElectricMachineSourceFactory
{
    private sealed record TimbreProfile(
        string Id,
        double FundamentalGain,
        double SecondHarmonicGain,
        double SlotGain,
        double InverterGain,
        double CharacterOrderMultiplier,
        double CharacterGain,
        double CharacterPhaseRadians,
        double CharacterThrottleResponse);

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

        var profile = ResolveTimbreProfile(machineType);
        var layers = new List<HarmonicLayerDefinition>(machineCount * 5);
        var gainScale = 1.0 / Math.Sqrt(machineCount);
        var characterOrder = Math.Min(
            256.0,
            Math.Max(1.0, polePairs * profile.CharacterOrderMultiplier));

        const double GoldenAngleRadians = 2.39996322972865332;
        for (var machineIndex = 0; machineIndex < machineCount; machineIndex++)
        {
            // Separate physical machines must not be treated as phase-locked acoustic emitters.
            // A deterministic golden-angle offset avoids artificial coherent cancellation while
            // preserving repeatable output for identical authoring inputs.
            var phase = machineIndex * GoldenAngleRadians;

            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-electrical-fundamental",
                ShaftRatio = 1.0,
                Order = polePairs,
                Gain = profile.FundamentalGain * gainScale,
                PhaseRadians = phase,
                ThrottleResponse = 0.82
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-electrical-second-harmonic",
                ShaftRatio = 1.0,
                Order = Math.Min(256.0, polePairs * 2.0),
                Gain = profile.SecondHarmonicGain * gainScale,
                PhaseRadians = phase + (Math.PI / 9.0),
                ThrottleResponse = 0.86
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-slot-order",
                ShaftRatio = 1.0,
                Order = slotOrder,
                Gain = profile.SlotGain * gainScale,
                PhaseRadians = phase + (Math.PI / 4.0),
                ThrottleResponse = 0.62
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-inverter-order",
                ShaftRatio = 1.0,
                Order = inverterOrder,
                Gain = profile.InverterGain * gainScale,
                PhaseRadians = phase + (Math.PI / 2.0),
                ThrottleResponse = 0.92
            });
            layers.Add(new HarmonicLayerDefinition
            {
                Name = $"machine-{machineIndex + 1}-machine-type-character-order",
                ShaftRatio = 1.0,
                Order = characterOrder,
                Gain = profile.CharacterGain * gainScale,
                PhaseRadians = phase + profile.CharacterPhaseRadians,
                ThrottleResponse = profile.CharacterThrottleResponse
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
                ["native_model"] = "electric-machine-event-v2",
                ["machine_type"] = machineType,
                ["machine_timbre_profile"] = profile.Id,
                ["machine_timbre_policy"] = "electric-machine-timbre-v1",
                ["machine_count"] = machineCount.ToString(CultureInfo.InvariantCulture),
                ["pole_pairs"] = polePairs.ToString(CultureInfo.InvariantCulture),
                ["electrical_fundamental_order"] = polePairs.ToString(CultureInfo.InvariantCulture),
                ["slot_order"] = slotOrder.ToString(CultureInfo.InvariantCulture),
                ["inverter_order"] = inverterOrder.ToString(CultureInfo.InvariantCulture),
                ["machine_type_character_order"] =
                    characterOrder.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["acoustic_phase_policy"] = "deterministic_golden_angle_per_machine",
                ["kinematic_note"] =
                    "Electrical whine uses pole-pair, slot and inverter shaft-order harmonics plus a versioned machine-type timbre layer. The timbre profile is an offline acoustic-authoring policy, not a finite-element electromagnetic model. Separate physical machines use deterministic non-coherent acoustic phase offsets to avoid artificial cancellation."
            }
        };

        source.Validate();
        return source;
    }

    private static TimbreProfile ResolveTimbreProfile(string machineType)
    {
        var normalized = machineType.Trim().ToLowerInvariant();

        if (normalized.Contains("switched-reluctance", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "switched-reluctance-v1",
                FundamentalGain: 0.112,
                SecondHarmonicGain: 0.088,
                SlotGain: 0.071,
                InverterGain: 0.028,
                CharacterOrderMultiplier: 3.0,
                CharacterGain: 0.078,
                CharacterPhaseRadians: Math.PI / 8.0,
                CharacterThrottleResponse: 0.90);
        }

        if (normalized.Contains("synchronous-reluctance", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "synchronous-reluctance-v1",
                FundamentalGain: 0.142,
                SecondHarmonicGain: 0.050,
                SlotGain: 0.052,
                InverterGain: 0.030,
                CharacterOrderMultiplier: 1.5,
                CharacterGain: 0.046,
                CharacterPhaseRadians: Math.PI / 5.0,
                CharacterThrottleResponse: 0.76);
        }

        if (normalized.Contains("induction", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "induction-v1",
                FundamentalGain: 0.104,
                SecondHarmonicGain: 0.038,
                SlotGain: 0.058,
                InverterGain: 0.041,
                CharacterOrderMultiplier: 1.125,
                CharacterGain: 0.040,
                CharacterPhaseRadians: Math.PI / 7.0,
                CharacterThrottleResponse: 0.72);
        }

        if (normalized.Contains("wound-field", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "wound-field-synchronous-v1",
                FundamentalGain: 0.148,
                SecondHarmonicGain: 0.070,
                SlotGain: 0.039,
                InverterGain: 0.025,
                CharacterOrderMultiplier: 0.5,
                CharacterGain: 0.038,
                CharacterPhaseRadians: Math.PI / 3.0,
                CharacterThrottleResponse: 0.68);
        }

        if (normalized.Contains("axial-flux", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "axial-flux-permanent-magnet-v1",
                FundamentalGain: 0.165,
                SecondHarmonicGain: 0.080,
                SlotGain: 0.034,
                InverterGain: 0.027,
                CharacterOrderMultiplier: 3.0,
                CharacterGain: 0.044,
                CharacterPhaseRadians: Math.PI / 10.0,
                CharacterThrottleResponse: 0.80);
        }

        if (normalized.Contains("transverse-flux", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "transverse-flux-v1",
                FundamentalGain: 0.121,
                SecondHarmonicGain: 0.096,
                SlotGain: 0.076,
                InverterGain: 0.035,
                CharacterOrderMultiplier: 2.5,
                CharacterGain: 0.062,
                CharacterPhaseRadians: Math.PI / 12.0,
                CharacterThrottleResponse: 0.88);
        }

        if (normalized.Contains("permanent-magnet", StringComparison.Ordinal))
        {
            return new TimbreProfile(
                "permanent-magnet-v1",
                FundamentalGain: 0.150,
                SecondHarmonicGain: 0.065,
                SlotGain: 0.045,
                InverterGain: 0.032,
                CharacterOrderMultiplier: 3.0,
                CharacterGain: 0.035,
                CharacterPhaseRadians: Math.PI / 6.0,
                CharacterThrottleResponse: 0.78);
        }

        return new TimbreProfile(
            "generic-electric-v1",
            FundamentalGain: 0.135,
            SecondHarmonicGain: 0.060,
            SlotGain: 0.050,
            InverterGain: 0.033,
            CharacterOrderMultiplier: 1.75,
            CharacterGain: 0.032,
            CharacterPhaseRadians: Math.PI / 6.0,
            CharacterThrottleResponse: 0.76);
    }
}
