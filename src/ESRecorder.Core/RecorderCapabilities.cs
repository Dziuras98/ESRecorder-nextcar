namespace ESRecorder.Core;

public sealed record RecorderBackendCapability(
    string Id,
    string Status,
    string[] SourceFamilies,
    string[] Features,
    string Notes);

public sealed record RecorderCapabilitiesReport(
    int SchemaVersion,
    string Product,
    string CapabilityContract,
    RecorderBackendCapability[] Backends,
    string[] Guarantees,
    string[] PlannedSourceFamilies);

public static class RecorderCapabilityCatalog
{
    public static RecorderCapabilitiesReport Create() => new(
        SchemaVersion: 1,
        Product: "ESRecorder-nextcar",
        CapabilityContract: "nextcar-recorder-capabilities-v1",
        Backends:
        [
            new RecorderBackendCapability(
                Id: "legacy-engine-sim-0.1.11a",
                Status: "supported-compatibility-backend",
                SourceFamilies: ["classic-piston-engine"],
                Features:
                [
                    "engine-sim-script-compile",
                    "rpm-hold",
                    "throttle-control",
                    "wav-recording"
                ],
                Notes:
                    "Preserves the historical esrecord-lib.dll compatibility path. " +
                    "It must not be used to claim support for topologies absent from engine-sim 0.1.11a."),
            new RecorderBackendCapability(
                Id: "event-source-v1",
                Status: "supported-native-fork-backend",
                SourceFamilies:
                [
                    "generic-periodic-event-source",
                    "wankel"
                ],
                Features:
                [
                    "arbitrary-event-phases",
                    "multiple-shaft-ratios",
                    "periodic-combustion-pulses",
                    "harmonic-mechanical-layers",
                    "deterministic-noise",
                    "cross-platform-pcm16-wav-render"
                ],
                Notes:
                    "Native ESRecorder-nextcar event renderer. Wankel is represented directly as rotor " +
                    "power events and is not mapped onto a piston engine.")
        ],
        Guarantees:
        [
            "machine-readable-capabilities",
            "deterministic-render-for-identical-input",
            "no-silent-topology-fallback",
            "offline-authoring-only"
        ],
        PlannedSourceFamilies:
        [
            "two-stroke-piston",
            "opposed-piston",
            "multi-crank",
            "radial-cam-ring",
            "axial-piston",
            "free-piston",
            "electric-machine",
            "multi-source-composite"
        ]);
}
