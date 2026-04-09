# Profiling the MTG Simulator

## Prerequisites

Install `dotnet-trace` globally (one-time setup):

```bash
dotnet tool install --global dotnet-trace
```

Install the **Speedscope** extension in VS Code:

- Open Extensions (`Ctrl+Shift+X`)
- Search for `speedscope`
- Install it

---

## Step-by-Step

### 1. Run the simulator in Release mode

Always use Release for profiling — Debug builds have extra overhead that skews results.

```bash
cd MtgSimulator
dotnet run -c Release
```

The simulator prints its PID on startup:

```
MTG Simulator
  PID: 12345
```

Enter a large game count (e.g. 9999) so it runs long enough to capture meaningful data.

### 2. Attach the tracer in a second terminal

In a new terminal, run:

```bash
dotnet-trace collect --process-id <PID> --output simulator.nettrace --duration 00:00:30
```

Replace `<PID>` with the number shown in the simulator window.

- `--duration 00:00:30` captures for 30 seconds then stops automatically
- Adjust duration if needed — 30 seconds is usually enough

### 3. Convert to Speedscope format

```bash
dotnet-trace convert simulator.nettrace --format Speedscope
```

This produces `simulator.speedscope.json`.

### 4. Open in VS Code

```bash
code simulator.speedscope.json
```

The Speedscope extension opens it as a flame graph automatically.

---

## Reading the Flame Graph

- Use the **Left Heavy** view (top-left selector) — groups by total time, best for finding hotspots
- Wider bars = more time spent in that method
- Click a bar to zoom in
- The sandwich view shows which methods are most expensive across all call sites

---

## Tips

- Run with a high game count and short trace duration — you want the trace to capture steady-state behaviour, not startup
- Always profile Release builds (`-c Release`)
- Re-profile after optimizations to confirm improvements — the hotspot distribution shifts as you fix things
- If method names show as `???`, the symbols weren't included — make sure `<DebugSymbols>true</DebugSymbols>` is set in the `.csproj` for Release builds
