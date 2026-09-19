using System.Globalization;

namespace ESRecorder.Core;

public sealed record CompositeSourceComponent(
    string Name,
    AcousticEventSourceDefinition Source,
    double SpeedRatio = 1.0,
    double Gain = 1.0,
    double PhaseOffsetRevolutions = 0.0);

public static class CompositeSourceFactory
{
    public static AcousticEventSourceDefinition Create(
        string id,
        IReadOnlyList<CompositeSourceComponent> components,
        string family = "multi-source-composite",
        double masterGain = 0.82)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentNullException.ThrowIfNull(components);

        if (components.Count is < 2 or > 16)
        {
            throw new ArgumentOutOfRangeException(
                nameof(components),
                "Composite sources require between 2 and 16 components.");
        }
        if (!double.IsFinite(masterGain) || masterGain is <= 0.0 or > 4.0)
            throw new ArgumentOutOfRangeException(nameof(masterGain), "Master gain must be finite and in (0, 4].");

        var names = new HashSet<string>(StringComparer.Ordinal);
        var eventTrains = new List<PeriodicEventTrainDefinition>();
        var harmonicLayers = new List<HarmonicLayerDefinition>();
        var componentMetadata = new List<string>(components.Count);

        foreach (var component in components)
        {
            ValidateComponent(component, names);
            component.Source.Validate();

            var componentGain = component.Gain * component.Source.MasterGain;
            var phaseOffset = Fractional(component.PhaseOffsetRevolutions);

            foreach (var train in component.Source.EventTrains)
            {
                var scaledShaftRatio = train.ShaftRatio * component.SpeedRatio;
                if (scaledShaftRatio > 64.0)
                {
                    throw new InvalidDataException(
                        $"Composite component {component.Name}/{train.Name} exceeds the event-source shaft-ratio limit after scaling: {scaledShaftRatio}.");
                }

                eventTrains.Add(new PeriodicEventTrainDefinition
                {
                    Name = $"{component.Name}/{train.Name}",
                    ShaftRatio = scaledShaftRatio,
                    EventPhases = train.EventPhases
                        .Select(phase => Fractional(phase + phaseOffset))
                        .ToArray(),
                    Gain = train.Gain * componentGain,
                    DecayMilliseconds = train.DecayMilliseconds,
                    ResonanceBaseHz = train.ResonanceBaseHz,
                    ResonanceOrder = train.ResonanceOrder,
                    NoiseMix = train.NoiseMix,
                    ThrottleResponse = train.ThrottleResponse
                });
            }

            foreach (var layer in component.Source.HarmonicLayers)
            {
                var scaledShaftRatio = layer.ShaftRatio * component.SpeedRatio;
                if (scaledShaftRatio > 64.0)
                {
                    throw new InvalidDataException(
                        $"Composite component {component.Name}/{layer.Name} exceeds the harmonic shaft-ratio limit after scaling: {scaledShaftRatio}.");
                }

                harmonicLayers.Add(new HarmonicLayerDefinition
                {
                    Name = $"{component.Name}/{layer.Name}",
                    ShaftRatio = scaledShaftRatio,
                    Order = layer.Order,
                    Gain = layer.Gain * componentGain,
                    PhaseRadians =
                        layer.PhaseRadians +
                        (2.0 * Math.PI * phaseOffset * layer.Order),
                    ThrottleResponse = layer.ThrottleResponse
                });
            }

            componentMetadata.Add(
                string.Join(
                    "|",
                    component.Name,
                    component.Source.Family,
                    component.SpeedRatio.ToString("0.###", CultureInfo.InvariantCulture),
                    component.Gain.ToString("0.###", CultureInfo.InvariantCulture),
                    phaseOffset.ToString("0.###", CultureInfo.InvariantCulture)));
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
                ["native_model"] = "multi-source-composite-event-v1",
                ["component_count"] = components.Count.ToString(CultureInfo.InvariantCulture),
                ["component_names"] = string.Join(",", components.Select(static component => component.Name)),
                ["component_families"] = string.Join(",", components.Select(static component => component.Source.Family)),
                ["component_spec_format"] = "name|family|speed_ratio|gain|phase_offset_revolutions",
                ["components"] = string.Join(";", componentMetadata),
                ["kinematic_note"] =
                    "Each component retains its own event/harmonic graph. Component speed ratios scale shaft rate, component gains include the child source master gain, and phase offsets are applied before rendering."
            }
        };

        source.Validate();
        return source;
    }

    private static void ValidateComponent(
        CompositeSourceComponent component,
        HashSet<string> names)
    {
        if (string.IsNullOrWhiteSpace(component.Name))
            throw new ArgumentException("Every composite component requires a name.", nameof(component));
        if (!names.Add(component.Name))
            throw new ArgumentException($"Duplicate composite component name: {component.Name}.", nameof(component));
        ArgumentNullException.ThrowIfNull(component.Source);
        if (!double.IsFinite(component.SpeedRatio) || component.SpeedRatio is <= 0.0 or > 64.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                $"Component {component.Name}: speed ratio must be finite and in (0, 64].");
        }
        if (!double.IsFinite(component.Gain) || component.Gain is <= 0.0 or > 4.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                $"Component {component.Name}: gain must be finite and in (0, 4].");
        }
        if (!double.IsFinite(component.PhaseOffsetRevolutions))
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                $"Component {component.Name}: phase offset must be finite.");
        }
    }

    private static double Fractional(double value)
    {
        var result = value - Math.Floor(value);
        return result < 0.0 ? result + 1.0 : result;
    }
}
