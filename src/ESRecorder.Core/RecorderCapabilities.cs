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
                    "wankel",
                    "multi-crank",
                    "two-stroke-piston",
                    "opposed-piston",
                    "radial-cam-ring",
                    "axial-piston",
                    "free-piston",
                    "electric-machine",
                    "multi-source-composite",
                    "fixed-firing-piston",
                    "split-single-two-stroke",
                    "rotary-combustion"
                ],
                Features:
                [
                    "arbitrary-event-phases",
                    "multiple-shaft-ratios",
                    "multi-event-train-composition",
                    "periodic-combustion-pulses",
                    "two-stroke-event-scheduling",
                    "opposed-piston-chamber-model",
                    "multi-revolution-event-cycles",
                    "non-rotating-reference-cycle",
                    "electric-shaft-order-harmonics",
                    "source-graph-composition",
                    "explicit-ignition-angle-table",
                    "cylinder-bank-assignment",
                    "paired-piston-phase-model",
                    "non-wankel-rotary-combustion",
                    "harmonic-mechanical-layers",
                    "deterministic-noise",
                    "cross-platform-pcm16-wav-render"
                ],
                Notes:
                    "Native ESRecorder-nextcar event renderer. Wankel, multi-crank, two-stroke, opposed-piston, " +
                    "radial/cam-ring, axial-piston, free-piston, electric-machine, composite, explicit fixed-firing " +
                    "banked piston, split-single and non-Wankel rotary-combustion sources are represented by explicit event/harmonic topology.")
        ],
        Guarantees:
        [
            "machine-readable-capabilities",
            "deterministic-render-for-identical-input",
            "no-silent-topology-fallback",
            "offline-authoring-only"
        ],
        PlannedSourceFamilies: []);
}
