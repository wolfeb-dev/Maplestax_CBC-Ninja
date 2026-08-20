# MapleStaxCBCAdvanced

A second NinjaTrader 8 indicator, uncoupled from `MapleStaxCBC` in the repo root.
It shows everything the base indicator shows, on the same status table, plus four
readouts that came out of a 2026-08-20 study of MapleStax's own posted trades.

**Display only.** No orders, no account access, no automation.

## What it adds

### The LTF trust state

The question it answers: **should I be reading the 3m right now, or waiting for
the higher timeframe?**

| state | rule | on the table |
|---|---|---|
| LIVE | a level was taken within `Fresh sweep` (15 min) **and** the clock is before `LTF cutoff` (10:30 ET) | `LIVE (fresh 6m)` |
| FADING | between fresh and `Stale sweep` (30 min) | `FADING (22m)` |
| MUTED | past stale, **or** past the cutoff | `MUTED (stale 47m)` / `MUTED (after 10:30)` |
| NO SWEEP | no significant level taken yet today | `NO SWEEP (no level taken)` |

In MUTED, and only in MUTED, the `CBC (chart TF)` row dims. **It dims, it never
hides.** Nothing is suppressed, cancelled or vetoed in any state: the option
zones, CBC flip levels, BRSG and bar colouring behave exactly as they do in the
base indicator. That is not caution, it is a finding: in the study, requiring the
3m and the 15m to agree scored *worse* than either read alone.

**NO SWEEP is not a warm-up.** With no level taken yet, the 3m read was the
weakest thing measured, so it never reads LIVE.

### The significant-level sweep clock

Watches six levels and reports the most recent one taken, with its age:
`PMH 09:47  6m ago`.

- **PMH / PML**, today's premarket extremes.
- **ONH / ONL**, the overnight container (18:00 ET the prior evening to 09:30).
- **YDH / YDL**, the prior **RTH** high and low.

The prior-day pair is computed here rather than reused from the base's PDH/PDL,
which come from the 23-hour Globex session. Those are different levels, and
quietly substituting one for the other would make this chart disagree with the
numbers behind it.

A level counts as taken when a bar **trades through** it, extreme not close, and
latches once per session. Every latch resets on the ET date roll, so a level
taken yesterday cannot make today's read look fresh.

### The overnight container

`ONH / ONL` on the table, and optionally on the chart via **Draw ONH / ONL
lines**. It forms from the start hour and locks at 09:30.

If your loaded history never reached 18:00, the container is real but short, and
it says so: the labels carry a `*` and the row reads `(partial)`. **Load enough
history** if you want a true overnight.

### Higher-timeframe EMA20 proximity

`15m EMA20   0.3 ATR  AT`. Distance from the HTF EMA20 in HTF ATR units:

- `AT` within `AT band` (0.5 ATR)
- `near` within `Near band` (1.5 ATR)
- `far` beyond that
- `no reading` when the ATR or EMA is not formed. It says nothing rather than
  claiming a confident AT it cannot measure.

**Show proximity band** shades the AT band around the EMA.

`Timeframe` under HTF Signals defaults to **15** here, against the base's 10,
because 15m is the timeframe the higher-timeframe question was answered on.

## What the numbers behind it are, and are not

The defaults came from a descriptive study of 2,292 reconstructed MapleStax
trades. The findings, so you can judge the settings rather than trust them:

- The 3m CBC win rate ran 64.5 / 57.1 / 59.9 / 52.2% across sweep ages of <=15,
  16-30, 31-90 and >90 minutes, while a 15m EMA20 touch held 65.8 / 64.3 / 63.1
  / 69.3%.
- On the clock alone: 09:30-10:30 the two were level (3m 62.8% / PF 2.50 vs 15m
  64.7% / PF 2.47); from 10:30 the 3m fell to 54.5% / PF 1.42 while the 15m held
  64.1% / PF 2.59.
- Being within 0.5 ATR of the 15m EMA20 was the strongest single state measured,
  68.3% win / PF 3.31 on n=180.
- Requiring both reads to agree was **worse** than either alone.

**None of this is validated.** It is descriptive statistics of trades he chose to
post, so selection bias is in every number; it was not pre-registered; and it has
never been through an adversarial backtest review. Every threshold is a property
with the researched value as its default precisely so you can move it. Treat the
readouts as orientation, not permission.

**Nothing here has run on live bars.** The logic is verified by unit tests and a
headless compile, which is correctness by construction, not observation.

## Running it beside the base indicator

Both can sit on one chart. Every drawing tag in this file is prefixed `ADV`, so
the two do not fight over draw objects, which they would otherwise do because
NinjaScript keys draw objects by tag at chart level.

## Install

1. Copy `MapleStaxCBCAdvanced.cs` into
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
2. Compile in the NinjaScript editor (F5). The repo copy deliberately omits the
   trailing `#region NinjaScript generated code` block; NinjaTrader regenerates
   it for this class on the first compile.
3. Add **MapleStaxCBCAdvanced** to a chart. Load 30-plus sessions of history so
   the overnight container and the prior-RTH levels are real rather than partial.

## Sharing it

The distributable zip comes from **NinjaTrader's own export**, not from this
repo. There is no packaging script here and none should be added.

- Tools > Export > NinjaScript Add-On, select the indicator, save the zip as
  `advanced/MapleStaxCBCAdvanced.zip`.

## Tests

```
powershell -ExecutionPolicy Bypass -File advanced\tools\run-advanced-tests.ps1
```

The harness extracts the `<AdvancedState>` region out of the shipping `.cs` and
compiles it against the test file, so the assertions exercise the exact bytes
that ship rather than a copy that can drift. 31 assertions covering every state
boundary, the no-sweep case, and the unmeasurable-ATR case.

Headless compile of the whole indicator, no NinjaTrader required:

```
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py advanced\MapleStaxCBCAdvanced.cs
```

## Files

```
MapleStaxCBCAdvanced.cs   the indicator
README.md                 this file
DESIGN.md                 the approved design
PLAN.md                   the implementation plan it was built from
tools/AdvancedStateTests.cs
tools/run-advanced-tests.ps1
```
