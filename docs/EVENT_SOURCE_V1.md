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

A family is considered supported only when it has a topology factory, contract tests and render evidence. No generic fallback is permitted.


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
