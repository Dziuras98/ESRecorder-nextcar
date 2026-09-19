using System.Globalization;

namespace ESRecorder.Core;

public static class RadialCamRingSourceFactory
{
    private static readonly HashSet<string> SupportedTopologies = new(StringComparer.OrdinalIgnoreCase)
    {
        "radial-piston",
        "cam-ring",
        "dual-cam-ring"
    };

    public static AcousticEventSourceDefinition Create(
        string id,
        string topology,
        int workingElementCount,
        double displacementLitres,
        int maxRpm,
        int cycleRevolutions = 2,
        int camRingCount = 1,
        int camLobesPerRing = 1,
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(topology);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);

        if (!SupportedTopologies.Contains(topology))
        {
            throw new ArgumentException(
                $"Unsupported radial/cam-ring topology: {topology}.",
                nameof(topology));
        }
        if (workingElementCount is < 1 or > 128)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workingElementCount),
                "Working-element count must be between 1 and 128.");
        }
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 100] litres.");
        }
        if (maxRpm is < 200 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 200 and 30000.");
        if (cycleRevolutions is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cycleRevolutions),
                "Event cycle must span between 1 and 8 output-shaft revolutions.");
        }
        if (camRingCount is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(camRingCount), "Cam-ring count must be between 1 and 8.");
        if (camLobesPerRing is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(camLobesPerRing),
                "Cam lobes per ring must be between 1 and 64.");
        }
        if (string.Equals(topology, "dual-cam-ring", StringComparison.OrdinalIgnoreCase) && camRingCount < 2)
        {
            throw new ArgumentException(
                "dual-cam-ring topology requires at least two cam rings.",
                nameof(camRingCount));
        }

        var phases = Enumerable.Range(0, workingElementCount)
            .Select(index => index / (double)workingElementCount)
            .ToArray();
        var eventShaftRatio = 1.0 / cycleRevolutions;
        var eventRatePerOutputRevolution = workingElementCount / (double)cycleRevolutions;
        var harmonicLayers = new List<HarmonicLayerDefinition>();

        harmonicLayers.Add(new HarmonicLayerDefinition
        {
            Name = "output-shaft-primary-order",
            ShaftRatio = 1.0,
            Order = 1.0,
            Gain = 0.055,
            PhaseRadians = 0.0,
            ThrottleResponse = 0.32
        });

        if (string.Equals(topology, "radial-piston", StringComparison.OrdinalIgnoreCase))
        {
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = "radial-master-slave-rod-order",
                ShaftRatio = 1.0,
                Order = Math.Min(128.0, Math.Max(1.0, workingElementCount)),
                Gain = 0.070,
                PhaseRadians = Math.PI / 8.0,
                ThrottleResponse = 0.44
            });
        }
        else
        {
            for (var ringIndex = 0; ringIndex < camRingCount; ringIndex++)
            {
                harmonicLayers.Add(new HarmonicLayerDefinition
                {
                    Name = $"cam-ring-{ringIndex + 1}-lobe-order",
                    ShaftRatio = 1.0,
                    Order = camLobesPerRing,
                    Gain = 0.060 / Math.Sqrt(camRingCount),
                    PhaseRadians = (2.0 * Math.PI * ringIndex) / camRingCount,
                    ThrottleResponse = 0.40
                });
            }
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "radial-cam-ring",
            MasterGain = 0.80,
            EventTrains =
            [
                new PeriodicEventTrainDefinition
                {
                    Name = "working-element-combustion-events",
                    ShaftRatio = eventShaftRatio,
                    EventPhases = phases,
                    Gain = 1.0,
                    DecayMilliseconds = 5.0 + (0.12 * Math.Min(workingElementCount, 20)),
                    ResonanceBaseHz = 105.0,
                    ResonanceOrder = Math.Max(1.0, Math.Min(128.0, eventRatePerOutputRevolution * 1.2)),
                    NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.38 : 0.28,
                    ThrottleResponse = 0.72
                }
            ],
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "radial-cam-ring-event-v1",
                ["topology"] = topology,
                ["working_element_count"] = workingElementCount.ToString(CultureInfo.InvariantCulture),
                ["event_cycle_revolutions"] = cycleRevolutions.ToString(CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] =
                    eventRatePerOutputRevolution.ToString("0.###", CultureInfo.InvariantCulture),
                ["cam_ring_count"] = camRingCount.ToString(CultureInfo.InvariantCulture),
                ["cam_lobes_per_ring"] = camLobesPerRing.ToString(CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["combustion_class"] = combustionClass,
                ["kinematic_note"] =
                    "Combustion events may span multiple output-shaft revolutions. Four-stroke odd-cylinder radials use a two-revolution event cycle instead of forcing a fractional event count into one revolution."
            }
        };

        source.Validate();
        return source;
    }
}
