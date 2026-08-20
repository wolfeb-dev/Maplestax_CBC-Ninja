# MapleStaxCBCAdvanced - design

2026-08-20. Design for a second, uncoupled indicator in this repo. Approved
shape: the original MapleStaxCBC status table, plus the additional readouts the
2026-08-20 research produced.

## Why it exists

The research behind it lives in the Nunchi-NQ repo
(`maplestax_trades/out/level_sweep_report.md`, `option_frame_report.md`,
`docs/next-build-review.md`). Three findings drive this indicator, measured on
2,292 reconstructed MapleStax trades:

1. **The 3m read decays as the move ages away from a significant level.** 3m CBC
   win rate runs 64.5 / 57.1 / 59.9 / 52.2% across sweep ages of <=15 min,
   16-30, 31-90 and >90, while a 15m EMA20 touch holds 65.8 / 64.3 / 63.1 /
   69.3%.
2. **The clock says the same thing independently.** From 09:30-10:30 the two are
   level (3m CBC 62.8% / PF 2.50 vs 15m touch 64.7% / PF 2.47); from 10:30 the
   3m collapses to 54.5% / PF 1.42 while the 15m holds 64.1% / PF 2.59. Holding
   the window fixed leaves the sweep-age pattern intact and vice versa, so the
   two effects are real and are not each other.
3. **Being at the higher-timeframe 20 is the strongest single state in the
   record**: 68.3% win / PF 3.31 within 0.5 ATR of the 15m EMA20 (n=180),
   grading down monotonically to 61.5% / 2.40 beyond 1.5 ATR.

One finding is negative and constrains the build: **requiring both triggers is
worse than either alone** (9.47 pts / PF 2.08 where both fire in the fresh
state, against 11.54 / 2.40 for the 3m alone). So the new state changes
**emphasis only**. It must never gate, suppress or veto an existing signal.

**None of it is validated.** Descriptive statistics on his posted calls, not
pre-registered, never through backtest-skeptic. Every threshold below is a
property with the researched value as its default, and every one of them says so
in its description.

## Uncoupling

`advanced/` is self-contained. Nothing outside it changes.

```
MapleStaxCBC.cs          unchanged
README.md                unchanged - base only, no pointer added
MapleStaxCBC.zip         unchanged
tools/                   unchanged - base zone tests
advanced/
  MapleStaxCBCAdvanced.cs
  README.md              its own, self-contained
  DESIGN.md              this file
  tools/AdvancedStateTests.cs
  tools/run-advanced-tests.ps1
  MapleStaxCBCAdvanced.zip   NOT built here - see Deployment
```

`MapleStaxCBCAdvanced.cs` begins as a copy of `MapleStaxCBC.cs` that differs
only in the class name, the indicator name and the drawing-tag prefixes, then
gains the additions below. Keeping the untouched parts byte-identical is
deliberate: it makes a future `diff` against the base readable when the base
gets a fix, which is the only defence this arrangement has against drift.

**Distinct drawing-tag prefixes are required, not cosmetic.** Both indicators
will be on one chart while they are compared, and NinjaScript draw objects are
keyed by tag: identical tags mean the two fight over the same objects.

## Additions

### 1. Overnight container (ONH / ONL)

The 18:00-ET-prior-day to 09:30 window, matching the definition the research
used and the one `LiquiditySweepMap` uses in the other repo. The base indicator
has Globex PDH/PDL but no overnight container, so this is new here.

- Forms through the container and **locks at 09:30**.
- Marked partial (`ONH*`) when loaded history never reached 18:00, rather than
  quietly reporting a short overnight.
- Optional lines, off by default.

### 2. Significant-level sweep clock

Watches the six levels the research measured: **PMH, PML, ONH, ONL, and prior-
RTH high/low**.

**Prior-RTH H/L is computed here rather than reusing the base's PDH/PDL.** The
base's previous-day levels come from the 23-hour Globex session; the research
measured the prior **RTH** high/low. They are different levels, and silently
substituting one for the other would make the chart's clock disagree with the
numbers that justify it. The base's PDH/PDL display is untouched.

- A level is **taken** when an RTH bar trades through it. Bar extreme, not
  close: "takes out a level" is about the trade through it.
- Each level latches once per session; the first crossing minute is what is
  kept.
- `sweep age` = minutes since the most recent crossing among the six.

### 3. LTF trust state

Two inputs, sweep age and the clock:

| state | rule |
|---|---|
| LIVE | a level has been taken, sweep age <= `FreshMinutes` (15), **and** ET clock < `LtfCutoffEt` (1030) |
| MUTED | sweep age > `StaleMinutes` (30) **or** ET clock >= `LtfCutoffEt` |
| NO SWEEP | no significant level taken yet today |
| FADING | anything between |

**NO SWEEP is a real state, not a warm-up.** Before any level is taken the 3m
has nothing to be fresh from, and the research says that state does not favour
it: inside 09:30-12:00 with nothing taken yet, the 3m CBC ran 6.84 pts against
the 15m touch's 10.72, the widest gap in favour of the 15m anywhere in the
morning. It therefore renders as FADING rather than LIVE, and carries its own
label so the reason on screen is the true one.

Rendering, using the existing table's per-row brushes:

- The `CBC (chart TF)` row's value cell dims in MUTED only. FADING and NO SWEEP
  do not dim. A three-way dim is a distinction a glance cannot make reliably, so
  the colour channel carries the binary (trust it / do not) and the new row
  carries the detail.
- A new `LTF trust` row states `LIVE`, `FADING`, `MUTED` or `NO SWEEP` with the
  reason (`fresh 6m`, `stale 47m`, `after 10:30`, `no level taken`).
- **It never suppresses anything.** Option zones, CBC flip levels, BRSG and bar
  colouring are untouched in every state. Emphasis only, per the negative
  confluence result.

### 4. 15m EMA20 proximity

- Distance from the **HTF** EMA20 in HTF ATR(14) units.
- `<= ProximityAtr` (0.5) reads `AT`, `<= 1.5` reads `near`, beyond that `far`.
- A new row: `15m EMA20   0.3 ATR  AT`.
- Optional translucent band around the HTF EMA20 at +/- `ProximityAtr`, painted
  in `OnRender` in the same visual language as the option zones, off by default.
- In MUTED the row takes the emphasis the dimmed LTF row gives up.
- `HtfTimeframe` defaults to **15m** here, against the base's 10m, because that
  is the timeframe the bias question was answered on.

## Properties

All under a new `Advanced` group, all display-only.

| property | default | note |
|---|---|---|
| `ShowAdvancedRows` | true | master toggle for the four additions |
| `ShowOvernightLines` | false | ONH/ONL on the chart |
| `OnStartHour` / `OnStartMinute` | 18 / 0 | overnight container start, ET |
| `FreshMinutes` | 15 | chosen after the fact on his posted calls |
| `StaleMinutes` | 30 | as above |
| `LtfCutoffEt` | 1030 | HHMM; 0 disables the clock half of the state |
| `ProximityAtr` | 0.5 | the AT band |
| `ProximityAtrPeriod` | 14 | ATR period on the HTF |
| `ShowProximityBand` | false | the translucent band |
| `HtfTimeframe` | **15m** | inherited property, default changed from the base's 10m |

Every description ends with the same sentence: chosen from a descriptive,
non-pre-registered look at MapleStax's posted calls; display-only, not
validated.

## Testing

- Pure state logic lives in a marked `// <AdvancedState>` ... `//
  </AdvancedState>` region with **no NinjaTrader type crossing the boundary**,
  mirroring `OptionZoneGeometry`. `tools/run-advanced-tests.ps1` extracts that
  exact region from the shipping file and compiles it against
  `tools/AdvancedStateTests.cs`, following `tools/run-zone-tests.ps1` so the
  tests cannot drift from the indicator.
- Cases that must be covered: level latches once per session; a level taken
  before 09:30 does not count; sweep age is measured from the most recent of
  several crossings; the three trust states at their exact boundaries; the
  clock half disabled at 0; proximity banding including a zero and NaN ATR.
- Headless compile to **0 errors** via
  `python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py`.

## Deployment

The zip is **exported from NinjaTrader**, not built from this repo. There is no
packaging script and none should be added.

1. Copy `advanced/MapleStaxCBCAdvanced.cs` into
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
2. Compile in the NinjaScript editor (F5). The repo copy omits the trailing
   `#region NinjaScript generated code` block on purpose; NT regenerates it.
3. Tools > Export > NinjaScript Add-On, select the indicator, and save the
   resulting zip as `advanced/MapleStaxCBCAdvanced.zip`.

## Non-goals

- No orders, no account access, no automation. Display only.
- No change to `MapleStaxCBC.cs`, its README or its zip.
- No shared code file between the two indicators. The duplication is the
  accepted cost of uncoupling.
- No gating of one signal on another, in any state.
