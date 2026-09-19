using System.Globalization;

namespace ESRecorder.Core;

public static class FixedFiringPistonSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        int cylinderCount,
        int bankCount,
        double displacementLitres,
        int maxRpm,
        IReadOnlyList<double> ignitionAnglesDegrees,
        IReadOnlyList<int> cylinderBankAssignments,
        int cycleDegrees = 720,
        string layout = "fixed-firing-piston",
        string firingLabel = "explicit",
        string combustionClass = "petrol")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(firingLabel);
        ArgumentException.ThrowIfNullOrWhiteSpace(combustionClass);
        ArgumentNullException.ThrowIfNull(ignitionAnglesDegrees);
        ArgumentNullException.ThrowIfNull(cylinderBankAssignments);

        if (cylinderCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(cylinderCount), "Cylinder count must be between 1 and 128.");
        if (bankCount is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(bankCount), "Bank count must be between 1 and 16.");
        if (!double.IsFinite(displacementLitres) || displacementLitres is <= 0.0 or > 100.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displacementLitres),
                "Displacement must be finite and in the range (0, 100] litres.");
        }
        if (maxRpm is < 300 or > 30000)
            throw new ArgumentOutOfRangeException(nameof(maxRpm), "Maximum RPM must be between 300 and 30000.");
        if (cycleDegrees is < 360 or > 2880 || cycleDegrees % 360 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cycleDegrees),
                "Cycle degrees must be a whole-number multiple of 360 between 360 and 2880.");
        }
        if (ignitionAnglesDegrees.Count != cylinderCount)
        {
            throw new ArgumentException(
                $"Ignition-angle count must equal cylinder count ({cylinderCount}).",
                nameof(ignitionAnglesDegrees));
        }
        if (cylinderBankAssignments.Count != cylinderCount)
        {
            throw new ArgumentException(
                $"Cylinder-bank assignment count must equal cylinder count ({cylinderCount}).",
                nameof(cylinderBankAssignments));
        }

        var cycleRevolutions = cycleDegrees / 360.0;
        var gainScale = 1.0 / Math.Sqrt(cylinderCount);
        var eventTrains = new List<PeriodicEventTrainDefinition>(cylinderCount);
        var normalizedAngles = new double[cylinderCount];
        var bankCylinderCounts = new int[bankCount];

        for (var cylinderIndex = 0; cylinderIndex < cylinderCount; cylinderIndex++)
        {
            var rawAngle = ignitionAnglesDegrees[cylinderIndex];
            if (!double.IsFinite(rawAngle))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(ignitionAnglesDegrees),
                    $"Cylinder {cylinderIndex + 1}: ignition angle must be finite.");
            }

            var bank = cylinderBankAssignments[cylinderIndex];
            if (bank < 0 || bank >= bankCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cylinderBankAssignments),
                    $"Cylinder {cylinderIndex + 1}: bank index {bank} is outside [0, {bankCount - 1}].");
            }

            var normalizedAngle = NormalizeDegrees(rawAngle, cycleDegrees);
            normalizedAngles[cylinderIndex] = normalizedAngle;
            bankCylinderCounts[bank]++;

            eventTrains.Add(new PeriodicEventTrainDefinition
            {
                Name = $"bank-{bank + 1}/cylinder-{cylinderIndex + 1}",
                ShaftRatio = 1.0 / cycleRevolutions,
                EventPhases = [normalizedAngle / cycleDegrees],
                Gain = gainScale,
                DecayMilliseconds = 4.6 + (0.08 * Math.Min(cylinderCount, 24)),
                ResonanceBaseHz = 120.0 + (9.0 * bank),
                ResonanceOrder = Math.Max(1.0, cylinderCount / cycleRevolutions),
                NoiseMix = combustionClass.Contains("diesel", StringComparison.OrdinalIgnoreCase) ? 0.38 : 0.27,
                ThrottleResponse = 0.72
            });
        }

        if (bankCylinderCounts.Any(static count => count == 0))
            throw new ArgumentException("Every declared bank must contain at least one cylinder.", nameof(bankCount));

        var harmonicLayers = new List<HarmonicLayerDefinition>(2 + bankCount)
        {
            new()
            {
                Name = "crankshaft-primary-order",
                ShaftRatio = 1.0,
                Order = 1.0,
                Gain = 0.060,
                PhaseRadians = 0.0,
                ThrottleResponse = 0.34
            },
            new()
            {
                Name = "crankshaft-secondary-order",
                ShaftRatio = 1.0,
                Order = 2.0,
                Gain = 0.034,
                PhaseRadians = Math.PI / 5.0,
                ThrottleResponse = 0.30
            }
        };

        for (var bank = 0; bank < bankCount; bank++)
        {
            harmonicLayers.Add(new HarmonicLayerDefinition
            {
                Name = $"bank-{bank + 1}-exhaust-order",
                ShaftRatio = 1.0,
                Order = Math.Max(1.0, bankCylinderCounts[bank] / cycleRevolutions),
                Gain = 0.055 / Math.Sqrt(bankCount),
                PhaseRadians = (2.0 * Math.PI * bank) / bankCount,
                ThrottleResponse = 0.58
            });
        }

        var source = new AcousticEventSourceDefinition
        {
            Id = id,
            Family = "fixed-firing-piston",
            MasterGain = 0.82,
            EventTrains = eventTrains.ToArray(),
            HarmonicLayers = harmonicLayers.ToArray(),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["native_model"] = "fixed-firing-piston-event-v1",
                ["cylinder_count"] = cylinderCount.ToString(CultureInfo.InvariantCulture),
                ["bank_count"] = bankCount.ToString(CultureInfo.InvariantCulture),
                ["cycle_degrees"] = cycleDegrees.ToString(CultureInfo.InvariantCulture),
                ["cycle_revolutions"] = cycleRevolutions.ToString("0.###", CultureInfo.InvariantCulture),
                ["power_events_per_output_revolution"] =
                    (cylinderCount / cycleRevolutions).ToString("0.###", CultureInfo.InvariantCulture),
                ["displacement_litres"] =
                    displacementLitres.ToString("0.###", CultureInfo.InvariantCulture),
                ["max_rpm"] = maxRpm.ToString(CultureInfo.InvariantCulture),
                ["layout"] = layout,
                ["firing_label"] = firingLabel,
                ["combustion_class"] = combustionClass,
                ["ignition_angles_degrees"] =
                    string.Join(",", normalizedAngles.Select(static angle =>
                        angle.ToString("0.###", CultureInfo.InvariantCulture))),
                ["cylinder_bank_assignments"] =
                    string.Join(",", cylinderBankAssignments.Select(static bank =>
                        bank.ToString(CultureInfo.InvariantCulture))),
                ["kinematic_note"] =
                    "Each cylinder owns one explicit ignition event over the declared cycle; bank assignment is preserved for acoustic grouping and simultaneous events remain representable across independent cylinder trains."
            }
        };

        source.Validate();
        return source;
    }

    public static AcousticEventSourceDefinition CreateEven(
        string id,
        int cylinderCount,
        int bankCount,
        double displacementLitres,
        int maxRpm,
        IReadOnlyList<int> cylinderBankAssignments,
        int cycleDegrees = 720,
        string layout = "fixed-firing-piston",
        string firingLabel = "EVEN",
        string combustionClass = "petrol")
    {
        if (cylinderCount is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(cylinderCount), "Cylinder count must be between 1 and 128.");

        var angles = Enumerable.Range(0, cylinderCount)
            .Select(index => index * (cycleDegrees / (double)cylinderCount))
            .ToArray();

        return Create(
            id,
            cylinderCount,
            bankCount,
            displacementLitres,
            maxRpm,
            angles,
            cylinderBankAssignments,
            cycleDegrees,
            layout,
            firingLabel,
            combustionClass);
    }

    private static double NormalizeDegrees(double angle, int cycleDegrees)
    {
        var result = angle % cycleDegrees;
        if (result < 0.0)
            result += cycleDegrees;
        return result;
    }
}
