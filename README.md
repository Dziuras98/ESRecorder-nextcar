# ESRecorder-nextcar

Fork of ES-Recorder used as the offline engine-audio renderer source for nEXTcAR.

The repository preserves the original WPF/BeamNG application while adding a
separate headless architecture:

- `src/ESRecorder.Core` — platform-neutral recording domain and orchestration;
- `src/ESRecorder.Native` — Windows adapter for `esrecord-lib.dll`;
- `src/ESRecorder.Cli` — non-interactive recording host;
- `src/ESRecorder.BeamNG` — optional BeamNG exporter;
- `tests/ESRecorder.Core.Tests` — executable contract tests.

See `docs/HEADLESS_ARCHITECTURE.md` for commands and architectural boundaries.

## Compatibility baseline

The imported native library and original application currently use engine-sim
0.1.11a. Compatibility with the nEXTcAR engine-sim baseline is a separate,
explicit porting task.

## Build the legacy WPF application

```powershell
dotnet build ESRecorder.csproj --configuration Release --runtime win-x64
```

## Build the headless host

```powershell
dotnet build src/ESRecorder.Cli/ESRecorder.Cli.csproj \
  --configuration Release \
  --runtime win-x64
```

## Run platform-neutral contract tests

```sh
dotnet run \
  --project tests/ESRecorder.Core.Tests/ESRecorder.Core.Tests.csproj \
  --configuration Release
```

## Original upstream purpose

The original ES-Recorder is an engine-sim recording application with BeamNG.drive
conversion. The conversion process is intended to assist BeamNG modding and does
not create a complete mod automatically.

Original upstream: `DDev247/ESRecorder`.
