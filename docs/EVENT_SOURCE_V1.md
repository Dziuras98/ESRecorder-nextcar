# event-source-v1 acoustic renderer

## Purpose

`event-source-v1` is the native ESRecorder-nextcar rendering path for powertrain
topologies that cannot be represented faithfully by the historical engine-sim
0.1.11a `esrecord-lib.dll`.

It is an offline acoustic authoring model, not a vehicle-physics simulation.

## Source graph

A source definition contains one or more:

- **periodic event trains** — discrete combustion/pressure events at arbitrary phases of a referenced shaft revolution;
- **harmonic layers** — continuous shaft-order tones used for mechanical, eccentric-gear or accessory components.

Every event train declares:

- shaft speed ratio relative to requested RPM;
- event phases in `[0, 1)`;
- pulse gain and decay;
- resonance base frequency and shaft order;
- deterministic broadband-noise mix;
- throttle response.

This provides a reusable representation for irregular or multi-crank pulse topologies without pretending they are ordinary inline/V piston engines.

## Wankel v1

`WankelSourceFactory` is the first topology factory.

The model uses the eccentric shaft as the reference shaft. Each rotor contributes one power event per eccentric-shaft revolution. A source with `N` rotors therefore contains `N` power events per shaft revolution, evenly phased in the initial v1 model.

For example:

- 1 rotor: phase `0.0`;
- 2 rotors: phases `0.0, 0.5`;
- 3 rotors: phases `0.0, 1/3, 2/3`;
- 4 rotors: phases `0.0, 0.25, 0.5, 0.75`.

The renderer adds pulse resonance, broadband event texture, primary/secondary pulse-order harmonics and an eccentric-gear harmonic. These are acoustic authoring parameters and are not claims of CFD or combustion fidelity.

## Determinism

For identical source definition, RPM, throttle, sample rate, length and seed, the renderer produces the same PCM samples. The default seed is a stable hash of the source id.

## CLI

Print capabilities:

```text
ESRecorder.Cli capabilities
```

Render Wankel:

```text
ESRecorder.Cli render-wankel --name four-rotor --rotors 4 --displacement 2.6 --redline 9500 --output recordings/four-rotor --rpm 1500:44100,4500:44100,8500:44100 --throttle 0,100 --length 5
```

Render a generic event source:

```text
ESRecorder.Cli render-event-source --source source.json --output recordings/custom --rpm 1500:44100,4500:44100 --throttle 0,100 --length 5
```

Each render bank writes:

- `event-source.json`;
- one mono PCM16 WAV per RPM/throttle point;
- `event-render-report.json` with peak/RMS and render metadata.

## Capability boundary

Current direct source families:

- classic piston engine through the legacy engine-sim 0.1.11a backend;
- generic periodic event source through event-source-v1;
- Wankel through event-source-v1;
- multi-crank compositions through event-source-v1;
- generic two-stroke piston engines through event-source-v1;
- opposed-piston two-stroke engines through event-source-v1.

The event backend now directly supports all source-family primitives required by the current PR #193 concept catalog: radial/cam-ring, axial-piston, free-piston, electric-machine and multi-source composition are implemented in addition to the earlier families.

A family is considered supported only when it has a topology factory, contract tests and render evidence. No generic fallback is permitted. The current catalog additionally uses explicit fixed-firing banked piston and split-single/twingle two-stroke primitives.


## Multi-crank v1

`MultiCrankSourceFactory` composes two to sixteen independent crank modules.
Each module has its own:

- power-event count per reference revolution;
- shaft-speed ratio;
- phase offset;
- gain;
- mechanical order.

The renderer preserves every module as its own event train. H, U, square and
other multi-crank concepts therefore retain inter-crank phasing instead of being
collapsed into one synthetic crankshaft.

CLI module syntax is:

```text
name:events:phase-degrees[:shaft-ratio[:gain[:mechanical-order]]]
```

Modules are separated with semicolons.

## Generic two-stroke piston v1

`TwoStrokePistonSourceFactory` schedules exactly one power event per cylinder
per crankshaft revolution. Cylinder count, layout and scavenging identity remain
explicit metadata. The initial renderer adds combustion-pulse, reciprocating and
scavenging-flow acoustic layers without claiming full port-flow simulation.

## Opposed-piston two-stroke v1

`OpposedPistonSourceFactory` models combustion by chamber rather than by piston.
A six-chamber / twelve-piston engine therefore contains six combustion events
per output revolution, not twelve.

One or more crankshafts are represented through separate mechanical harmonic
layers with explicit phase increments. This prevents the second crankshaft from
incorrectly doubling combustion events while preserving the multi-crank
mechanical signature.

## CLI examples

```text
ESRecorder.Cli render-multi-crank --name h8 --modules "upper:2:0:1;lower:2:90:1" --redline 7500 --output recordings/h8 --rpm 1500:44100,6500:44100 --throttle 0,100 --length 5

ESRecorder.Cli render-two-stroke --name v6-2t --cylinders 6 --displacement 2.0 --redline 12000 --layout V6 --scavenging loop --output recordings/v6-2t --rpm 2000:44100,11000:44100 --throttle 0,100 --length 5

ESRecorder.Cli render-opposed-piston --name op6 --chambers 6 --displacement 3.6 --redline 4500 --cranks 2 --crank-phase-degrees 12 --combustion diesel --output recordings/op6 --rpm 1000:44100,4000:44100 --throttle 0,100 --length 5
```


## Radial and cam-ring v1

`RadialCamRingSourceFactory` supports:

- `radial-piston`;
- `cam-ring`;
- `dual-cam-ring`.

The event graph explicitly supports combustion cycles spanning more than one
output-shaft revolution. This matters for odd-cylinder four-stroke radials: a
radial-7 source uses seven firing events over two crankshaft revolutions
(`shaft_ratio = 0.5`), which is 3.5 power events per crankshaft revolution.
The model does not invent an eighth cylinder or force a one-revolution repeat.

Cam-ring variants retain cam-ring count and lobe-order mechanical layers.

## Axial-piston v1

`AxialPistonSourceFactory` represents axial piston count, combustion-cycle
length and the mechanical conversion mechanism (for example a swashplate).
Combustion events can span multiple output-shaft revolutions while mechanical
orders remain referenced to the output shaft.

## Free-piston v1

`FreePistonSourceFactory` introduces no fictitious crankshaft. For this family,
the renderer's numerical RPM input is interpreted as **oscillation cycles per
minute**. Each module owns one combustion event train and independent
linear/generator harmonic layers.

The resulting source metadata records:

- `reference_rate_unit = cycles_per_minute`;
- module count;
- equivalent displacement;
- generator class;
- maximum oscillation rate.

## Electric-machine v1

`ElectricMachineSourceFactory` is harmonic-only: it creates no combustion
event train. Electrical fundamental order is derived from pole-pair count, with
additional slot and inverter-related orders.

Multiple physical machines use deterministic golden-angle acoustic phase
offsets. They are deliberately not treated as phase-locked emitters; this
prevents physically unrelated motors from cancelling to digital silence when
their signals are summed.

## Multi-source composite v1

`CompositeSourceFactory` merges two to sixteen existing event-source graphs.
Every component keeps its own:

- source family;
- event trains and harmonic layers;
- speed ratio relative to the composite reference speed;
- gain;
- phase offset.

Child source master gain is folded into the component gain. Component event and
harmonic shaft ratios are scaled independently, which permits ICE + e-axle,
range-extender, multi-engine and other hybrid authoring graphs without
flattening their identities.

CLI composite syntax uses semicolon-separated components:

```text
name|source-path[|speed-ratio[|gain[|phase-offset-revolutions]]]
```

Example:

```text
ESRecorder.Cli render-composite --name rotary-hybrid --sources "ice|rotary/event-source.json|1|1|0;front-motor|motor/event-source.json|2.5|0.55|0.125" --output recordings/rotary-hybrid --rpm 1500:44100,8500:44100 --throttle 0,100 --length 5
```


## Fixed-firing banked piston v1

`FixedFiringPistonSourceFactory` is the explicit firing-order primitive for
conventional one-crank piston engines whose identity depends on crank/firing
geometry rather than a new thermodynamic cycle.

Authoring input includes:

- cylinder count;
- acoustic bank count;
- one ignition angle per cylinder over the declared cycle;
- one bank assignment per cylinder;
- cycle length in crank degrees;
- layout and firing-pattern labels.

Every cylinder is materialized as its own event train. This intentionally allows
simultaneous/grouped combustion events without losing amplitude through
duplicate-phase collapse. Bank identity is retained in event names and
bank-specific exhaust harmonic layers.

The same primitive covers current PR #193 families such as:

- asymmetric odd-cylinder V3/V5/V7/V9/V11/V13/V15;
- 3–6 bank fan/Y/X/pentafan engines;
- narrow-angle VR and W layouts;
- 180/270/360-degree twins;
- flat/crossplane and odd/even-fire fixed crank arrangements;
- grouped/big-bang fixed firing orders.

`CreateEven` derives evenly spaced ignition angles when the catalog explicitly
declares an even-firing strategy. Uneven/grouped layouts should supply explicit
ignition-angle tables rather than silently falling back to even firing.

CLI:

```text
ESRecorder.Cli render-fixed-firing-piston --name twin270 --cylinders 2 --banks 1 --displacement 1.0 --max-rpm 9000 --cycle-degrees 720 --ignition-degrees 0,270 --bank-assignments 0,0 --layout inline-2 --firing-label 270/450 --output recordings/twin270 --rpm 1500:48000,8000:48000 --throttle 0,100 --length 5
```

## Split-single / twingle two-stroke v1

`SplitSingleSourceFactory` models combustion by chamber. Each chamber contains
two coordinated pistons:

- an exhaust-control piston;
- a transfer/scavenge piston.

The paired pistons use a fixed phase offset and contribute separate mechanical
harmonics. They do **not** duplicate the chamber's combustion event.

A six-chamber split-single therefore records:

- 6 combustion events per output-shaft revolution;
- 12 physical pistons;
- one explicit transfer-piston phase relationship;
- optional multiple acoustic banks for V/flat layouts.

This directly represents the accepted FREE Concept Car 006 family without
mapping it to ordinary two-stroke cylinders or opposed-piston combustion.

CLI:

```text
ESRecorder.Cli render-split-single --name split6 --chambers 6 --banks 2 --displacement 3.0 --max-rpm 9000 --transfer-piston-phase-degrees 15 --layout V-split-single --output recordings/split6 --rpm 1500:48000,8000:48000 --throttle 0,100 --length 5
```

## Single-crank OPOC

The existing `OpposedPistonSourceFactory` accepts `crankshaftCount = 1`, so
the CL1M16 single-crank OPOC family does not require another combustion source
type. Its chambers remain one-event-per-revolution two-stroke chambers while the
single central crank contributes the mechanical layer.


## Non-Wankel rotary-combustion v1

`RotaryCombustionSourceFactory` is the generic authoring primitive for rotary
combustion mechanisms that explicitly are **not** Wankel engines.

Inputs are explicit:

- mechanism identity;
- working-element/chamber/vane/lobe/piston count;
- power events per output-shaft revolution;
- maximum output-shaft RPM;
- combustion class.

The source derives event-train shaft ratio from:

```text
power_events_per_output_revolution / working_element_count
```

and records that value in metadata. The factory never infers Wankel rotor
kinematics.

This primitive covers current PR #193 FREE Concept Car 003 mechanisms such as:

- articulated multi-chamber rotary;
- inverse-trochoid rotary;
- vane rotary combustion;
- gerotor combustion;
- orbital-piston rotary combustion;
- nutating-disc combustion;
- toroidal opposed rotary combustion.

A dual articulated rotary power unit can be represented by two
`rotary-combustion` child graphs combined through `multi-source-composite`
once its explicit phase offset is present in source data.

CLI:

```text
ESRecorder.Cli render-rotary-combustion --name gerotor7 --mechanism "seven-lobe gerotor combustion" --elements 7 --power-events-per-output-revolution 7 --max-rpm 7500 --output recordings/gerotor7 --rpm 1200:48000,7000:48000 --throttle 0,100 --length 5
```

The recipe materializer must provide an explicit working-element count and event
rate. If the catalog text does not determine those values, the variant must
remain blocked rather than being mapped to Wankel or another generic rotary.
