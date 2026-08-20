# MapleStaxCBCAdvanced Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a second, uncoupled NinjaTrader indicator that shows everything MapleStaxCBC shows plus four research-driven readouts: the overnight container, a significant-level sweep clock, an LTF trust state, and 15m EMA20 proximity.

**Architecture:** `advanced/MapleStaxCBCAdvanced.cs` is a copy of `MapleStaxCBC.cs` differing only in class name, indicator name and drawing-tag prefixes, then extended. All new decision logic lives in one pure `AdvancedState` static class inside a marked region with no NinjaTrader type crossing the boundary, so a PowerShell harness can extract and unit-test the exact shipping bytes. Nothing outside `advanced/` changes.

**Tech Stack:** C# / NinjaScript for NinjaTrader 8, .NET Framework 4.8, `csc.exe` for the standalone test harness, PowerShell 5.

**Spec:** `advanced/DESIGN.md`

## Global Constraints

- **Display-only.** No orders, no account access, no automation, in any task.
- **No file outside `advanced/` may be modified.** Not `MapleStaxCBC.cs`, not the root `README.md`, not `MapleStaxCBC.zip`, not `tools/`.
- **The new state changes emphasis only.** It must never gate, suppress or veto an existing signal. This follows a negative research result (requiring both triggers scored worse than either alone) and is not a stylistic preference.
- **No packaging script.** The zip is produced by NinjaTrader's Tools > Export, never by the repo.
- **Every new threshold ships as a property** whose description ends: "Chosen from a descriptive, non-pre-registered look at MapleStax's posted calls; display-only, not validated."
- **No NinjaTrader type may appear inside the `<AdvancedState>` region.** The harness compiles that region alone; a `Brush`, `Series` or `Bars` reference breaks the tests.
- **Inside `<AdvancedState>`, fully qualify BCL calls** as `System.Math.Abs(...)`. The extracted region is compiled without a `using System;` line, exactly as `OptionZoneGeometry` is.
- **Working directory for every task:** `D:/Maplestax_CBC-Ninja-wt-advanced` on branch `feat/maplestax-cbc-advanced`.
- **Headless compile command** (the whole-file gate, used at the end of several tasks):
  `python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs`
  Judge on `0 Error(s)`.

---

### Task 1: The copy that loads beside the original

Produces a working indicator identical in behaviour to MapleStaxCBC that can sit on the same chart without fighting it. No new features yet, which is the point: if this task is wrong, every later task inherits the fault.

**Files:**
- Create: `advanced/MapleStaxCBCAdvanced.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: class `MapleStaxCBCAdvanced` in namespace `NinjaTrader.NinjaScript.Indicators`, indicator display name `MapleStaxCBCAdvanced`, all drawing tags prefixed `ADV`.

- [ ] **Step 1: Copy the body, dropping the generated region**

The base file is 2,509 lines and lines 2454-2509 are `#region NinjaScript generated code`, which declares `cacheMapleStaxCBC` and the `MapleStaxCBC(...)` factory overloads on the shared `partial class Indicator`. Copying that region would declare those members a second time and the assembly would not build. NinjaTrader regenerates the region for the new class on first compile in the editor.

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
sed -n '1,2453p' MapleStaxCBC.cs > advanced/MapleStaxCBCAdvanced.cs
tail -3 advanced/MapleStaxCBCAdvanced.cs
```

Expected: the file ends with the closing braces of the class and namespace, and no `#region NinjaScript generated code` line.

- [ ] **Step 2: Verify the generated region is gone**

```bash
grep -c "NinjaScript generated code" advanced/MapleStaxCBCAdvanced.cs
```

Expected: `0`.

- [ ] **Step 3: Rename the class and the indicator**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
python - <<'PY'
import io
p = 'advanced/MapleStaxCBCAdvanced.cs'
s = io.open(p, encoding='utf-8').read()
assert 'public class MapleStaxCBC : Indicator' in s
s = s.replace('public class MapleStaxCBC : Indicator',
              'public class MapleStaxCBCAdvanced : Indicator', 1)
assert 'Name = "MapleStaxCBC";' in s
s = s.replace('Name = "MapleStaxCBC";', 'Name = "MapleStaxCBCAdvanced";', 1)
io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('renamed')
PY
grep -n "public class MapleStax\|Name = \"MapleStax" advanced/MapleStaxCBCAdvanced.cs
```

Expected: exactly two lines, both naming `MapleStaxCBCAdvanced`.

- [ ] **Step 4: Prefix every drawing tag with ADV**

NinjaScript draw objects are keyed by tag at chart level, so two indicators using the same tag overwrite each other's objects. Three literal families cover all 30 draw calls and the single `RemoveDrawObject`: the first argument of every `Draw.*` call, the `string tag = "PREFIX_"` builders, and the shared `tagBase` composition.

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
python - <<'PY'
import io, re
p = 'advanced/MapleStaxCBCAdvanced.cs'
s = io.open(p, encoding='utf-8').read()

s, n1 = re.subn(r'(Draw\.[A-Za-z]+\(this, ")', r'\1ADV', s)
s, n2 = re.subn(r'(string tag = ")', r'\1ADV', s)
old = 'string tag = tagBase + "_" + dateKey;'
new = 'string tag = "ADV" + tagBase + "_" + dateKey;'
n3 = 1 if old in s else 0
s = s.replace(old, new, 1)

io.open(p, 'w', encoding='utf-8', newline='').write(s)
print('draw calls', n1, 'tag builders', n2, 'tagBase', n3)
PY
```

Expected: `draw calls 30 tag builders 8 tagBase 1`.

- [ ] **Step 5: Verify no unprefixed tag survives**

```bash
grep -o 'Draw\.[A-Za-z]*(this, "[^"]*"' advanced/MapleStaxCBCAdvanced.cs | grep -v '"ADV' | wc -l
grep -o 'string tag = "[^"]*"' advanced/MapleStaxCBCAdvanced.cs | grep -v '"ADV' | wc -l
```

Expected: `0` from both.

- [ ] **Step 6: Compile headlessly**

```bash
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs
```

Expected: `0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git add advanced/MapleStaxCBCAdvanced.cs
git commit -m "feat(advanced): MapleStaxCBCAdvanced, a copy that loads beside the base"
```

---

### Task 2: The pure state logic, test-first

All four readouts reduce to four pure functions. They go in one region the harness can extract, and the tests are written before the region exists.

**Files:**
- Create: `advanced/tools/AdvancedStateTests.cs`
- Create: `advanced/tools/run-advanced-tests.ps1`
- Modify: `advanced/MapleStaxCBCAdvanced.cs` (add the `<AdvancedState>` region)

**Interfaces:**
- Consumes: nothing.
- Produces, all `internal static` on `AdvancedState`:
  - `enum Trust { Live, Fading, Muted, NoSweep }`
  - `enum Proximity { At, Near, Far, Unknown }`
  - `bool Taken(bool isHigh, double level, double barHigh, double barLow)`
  - `int Age(int etNowMin, int lastSweepMin)`, returning `-1` for "no sweep"
  - `Trust Evaluate(int sweepAgeMin, int etHhmm, int freshMin, int staleMin, int cutoffHhmm)`
  - `Proximity Near(double price, double ema20, double atr, double atMult, double nearMult)`

- [ ] **Step 1: Write the failing tests**

Create `advanced/tools/AdvancedStateTests.cs`:

```csharp
// Tests for the advanced state logic lifted out of MapleStaxCBCAdvanced.cs.
// Run via advanced/tools/run-advanced-tests.ps1, which extracts the <AdvancedState>
// region from the real indicator source so these assertions exercise the shipping bytes.
using System;

internal static class AdvancedStateTests
{
    private static int failures;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) failures++;
    }

    private const int Fresh = 15;
    private const int Stale = 30;
    private const int Cutoff = 1030;

    private static int Main()
    {
        // --- Taken: a level is taken when a bar TRADES THROUGH it, extreme not close.
        Check(AdvancedState.Taken(true, 100.0, 100.5, 99.0), "high level taken by a higher high");
        Check(!AdvancedState.Taken(true, 100.0, 100.0, 99.0), "touching a high level is not taking it");
        Check(AdvancedState.Taken(false, 100.0, 101.0, 99.5), "low level taken by a lower low");
        Check(!AdvancedState.Taken(false, 100.0, 101.0, 100.0), "touching a low level is not taking it");
        Check(!AdvancedState.Taken(true, double.NaN, 100.5, 99.0), "an unformed level is never taken");

        // --- Age: -1 means NO SWEEP and must never be read as zero minutes.
        Check(AdvancedState.Age(600, -1) == -1, "no sweep yet reads -1");
        Check(AdvancedState.Age(600, 570) == 30, "age is the minute difference");
        Check(AdvancedState.Age(570, 600) == -1, "a sweep in the future is refused");

        // --- Evaluate: the whole rule.
        Check(AdvancedState.Evaluate(-1, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.NoSweep,
              "nothing taken yet reads NoSweep, never Live");
        Check(AdvancedState.Evaluate(5, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Live,
              "fresh sweep before the cutoff is Live");
        Check(AdvancedState.Evaluate(15, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Live,
              "the fresh boundary is inclusive");
        Check(AdvancedState.Evaluate(16, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Fading,
              "past fresh but not past stale is Fading");
        Check(AdvancedState.Evaluate(30, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Fading,
              "the stale boundary is still Fading");
        Check(AdvancedState.Evaluate(31, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Muted,
              "past stale is Muted");
        Check(AdvancedState.Evaluate(5, 1030, Fresh, Stale, Cutoff) == AdvancedState.Trust.Muted,
              "the cutoff mutes even a fresh sweep");
        Check(AdvancedState.Evaluate(5, 1029, Fresh, Stale, Cutoff) == AdvancedState.Trust.Live,
              "one minute before the cutoff is still Live");
        Check(AdvancedState.Evaluate(5, 1500, Fresh, Stale, 0) == AdvancedState.Trust.Live,
              "cutoff 0 disables the clock half of the rule");
        Check(AdvancedState.Evaluate(-1, 900, Fresh, Stale, Cutoff) == AdvancedState.Trust.NoSweep,
              "NoSweep outranks the clock");

        // --- Near: unknown rather than a confident AT when it cannot measure.
        Check(AdvancedState.Near(100.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.At,
              "sitting on the EMA is AT");
        Check(AdvancedState.Near(105.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.At,
              "exactly 0.5 ATR is AT");
        Check(AdvancedState.Near(94.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Near,
              "0.6 ATR below is near, and direction does not matter");
        Check(AdvancedState.Near(116.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Far,
              "past 1.5 ATR is far");
        Check(AdvancedState.Near(100.0, 100.0, 0.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a zero ATR cannot measure, so Unknown not At");
        Check(AdvancedState.Near(100.0, 100.0, double.NaN, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a NaN ATR is Unknown");
        Check(AdvancedState.Near(100.0, double.NaN, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "an unformed EMA is Unknown");

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURE(S)"));
        return failures == 0 ? 0 : 1;
    }
}
```

- [ ] **Step 2: Write the harness**

Create `advanced/tools/run-advanced-tests.ps1`:

```powershell
# Extracts the AdvancedState region from the real MapleStaxCBCAdvanced.cs and compiles it
# together with AdvancedStateTests.cs, so the tests exercise the exact bytes that ship
# rather than a copy that can drift.
$ErrorActionPreference = 'Stop'

$advanced = Split-Path -Parent $PSScriptRoot
$src  = Join-Path $advanced 'MapleStaxCBCAdvanced.cs'
$out  = Join-Path $env:TEMP 'maplestax-advanced-tests'
New-Item -ItemType Directory -Force -Path $out | Out-Null

$text = Get-Content -Raw $src
$m = [regex]::Match($text, '(?s)//\s*<AdvancedState>(.*?)//\s*</AdvancedState>')
if (-not $m.Success) { throw 'AdvancedState region markers not found in MapleStaxCBCAdvanced.cs' }
Set-Content -Path (Join-Path $out 'AdvancedState.g.cs') -Value $m.Groups[1].Value -Encoding UTF8

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found at $csc" }

& $csc /nologo /target:exe /out:"$out\advancedtests.exe" "$out\AdvancedState.g.cs" (Join-Path $PSScriptRoot 'AdvancedStateTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'advanced-test harness compile failed' }

& "$out\advancedtests.exe"
exit $LASTEXITCODE
```

- [ ] **Step 3: Run the tests to verify they fail**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Maplestax_CBC-Ninja-wt-advanced\advanced\tools\run-advanced-tests.ps1"
```

Expected: FAIL with `AdvancedState region markers not found in MapleStaxCBCAdvanced.cs`.

- [ ] **Step 4: Add the region**

Insert immediately before the `// <OptionZoneGeometry>` marker in `advanced/MapleStaxCBCAdvanced.cs`:

```csharp
        // <AdvancedState>
        // Pure state logic for the advanced readouts. No NinjaTrader type crosses this
        // boundary, which is what lets advanced/tools/run-advanced-tests.ps1 extract this
        // exact region and compile it standalone against AdvancedStateTests.cs. Keep it
        // that way, and keep BCL calls fully qualified: the extracted file has no usings.
        //
        // Every threshold that reaches this code arrives from a property whose default came
        // from a DESCRIPTIVE, non-pre-registered look at MapleStax's posted calls. Nothing
        // here is validated, and nothing here gates a signal: these states change emphasis
        // only. Requiring two triggers to agree scored WORSE than either alone in that
        // research, which is why there is no confluence rule anywhere in this file.
        internal static class AdvancedState
        {
            internal enum Trust { Live, Fading, Muted, NoSweep }
            internal enum Proximity { At, Near, Far, Unknown }

            /// <summary>
            /// A level is TAKEN when a bar trades through it. Bar extreme, not close:
            /// "takes out a level" is about the trade through it, and a close-only test
            /// would miss the wick sweep that is the whole point of watching the level.
            /// An unformed level (NaN) is never taken.
            /// </summary>
            internal static bool Taken(bool isHigh, double level, double barHigh, double barLow)
            {
                if (double.IsNaN(level)) return false;
                return isHigh ? barHigh > level : barLow < level;
            }

            /// <summary>
            /// Minutes since a sweep, from ET minutes-of-day. Returns -1 when there is
            /// nothing to measure from, which callers must treat as "no sweep" and never
            /// as zero minutes: zero would read as maximally fresh, the exact inversion of
            /// what the research found for that state.
            /// </summary>
            internal static int Age(int etNowMin, int lastSweepMin)
            {
                if (lastSweepMin < 0 || etNowMin < lastSweepMin) return -1;
                return etNowMin - lastSweepMin;
            }

            /// <summary>
            /// The whole trust rule in one place. LIVE only while a level is freshly taken
            /// AND the clock is early. MUTED once the sweep goes stale OR the cutoff passes.
            /// NoSweep when nothing has been taken yet, which is NOT a warm-up state: with
            /// no level taken the 3m read was the weakest thing measured, so it must never
            /// read LIVE. A cutoff of 0 disables the clock half of the rule.
            /// </summary>
            internal static Trust Evaluate(int sweepAgeMin, int etHhmm, int freshMin, int staleMin, int cutoffHhmm)
            {
                if (sweepAgeMin < 0) return Trust.NoSweep;
                bool lateOnClock = cutoffHhmm > 0 && etHhmm >= cutoffHhmm;
                if (sweepAgeMin > staleMin || lateOnClock) return Trust.Muted;
                if (sweepAgeMin <= freshMin) return Trust.Live;
                return Trust.Fading;
            }

            /// <summary>
            /// Distance from the higher-timeframe EMA20 in HTF ATR units. Unknown on a
            /// missing or non-positive ATR rather than a confident AT: a band that claims
            /// certainty when it cannot measure is worse than one that says nothing.
            /// </summary>
            internal static Proximity Near(double price, double ema20, double atr,
                                           double atMult, double nearMult)
            {
                if (double.IsNaN(price) || double.IsNaN(ema20) || double.IsNaN(atr) || atr <= 0)
                    return Proximity.Unknown;
                double d = System.Math.Abs(price - ema20) / atr;
                if (d <= atMult) return Proximity.At;
                if (d <= nearMult) return Proximity.Near;
                return Proximity.Far;
            }
        }
        // </AdvancedState>
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Maplestax_CBC-Ninja-wt-advanced\advanced\tools\run-advanced-tests.ps1"
```

Expected: every line `PASS`, final line `ALL PASS`, exit code 0.

- [ ] **Step 6: Compile the whole indicator**

```bash
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs
```

Expected: `0 Error(s)`.

- [ ] **Step 7: Commit**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git add advanced/MapleStaxCBCAdvanced.cs advanced/tools/
git commit -m "feat(advanced): pure AdvancedState logic + extraction test harness"
```

---

### Task 3: Feed the state from real bars

Tracks the overnight container, prior-RTH high/low, and the six-level sweep clock, then computes the trust state each bar.

**Files:**
- Modify: `advanced/MapleStaxCBCAdvanced.cs`

**Interfaces:**
- Consumes: `AdvancedState.Taken`, `AdvancedState.Age`, `AdvancedState.Evaluate` from Task 2; `ConvertToEastern(DateTime)` from the base file.
- Produces, as private fields the panel task reads:
  - `double advOnHigh, advOnLow`, the locked overnight container, NaN until locked
  - `bool advOnPartial`, true when the container never saw an 18:00 bar
  - `double advPrevRthHigh, advPrevRthLow`, prior RTH extremes, NaN until a session completes
  - `int advLastSweepMin`, ET minute-of-day of the most recent sweep, -1 for none
  - `string advLastSweepLabel`, for example `PMH`
  - `AdvancedState.Trust advTrust`
  - `int advSweepAge`

- [ ] **Step 1: Add the fields**

Add beside the existing private state fields (near `private int htfSeriesIndex = -1;`):

```csharp
        // --- Advanced: overnight container, prior-RTH levels, sweep clock -----
        private double advOnHigh = double.NaN;
        private double advOnLow = double.NaN;
        private double advOnFormHigh = double.NaN;
        private double advOnFormLow = double.NaN;
        private bool   advOnSawEvening;
        private bool   advOnPartial;
        private bool   advOnLocked;
        private double advPrevRthHigh = double.NaN;
        private double advPrevRthLow = double.NaN;
        private double advRthHigh = double.NaN;
        private double advRthLow = double.NaN;
        private string advSessionKey = string.Empty;
        private readonly bool[] advSweptLatch = new bool[6];
        private int    advLastSweepMin = -1;
        private string advLastSweepLabel = string.Empty;
        private int    advSweepAge = -1;
        private AdvancedState.Trust advTrust = AdvancedState.Trust.NoSweep;
        private AdvancedState.Proximity advProximity = AdvancedState.Proximity.Unknown;
        private double advProximityAtr = double.NaN;
```

- [ ] **Step 2: Add the per-bar update**

Add this method beside the other `Update*` methods:

```csharp
        /// <summary>
        /// Overnight container, prior-RTH levels and the significant-level sweep clock.
        /// Session-scoped: every latch resets on the ET date roll, so a level taken
        /// yesterday cannot make today's 3m read look fresh.
        /// </summary>
        private void UpdateAdvancedState()
        {
            DateTime et = ConvertToEastern(Time[0]);
            int mod = et.Hour * 60 + et.Minute;
            string key = et.ToString("yyyyMMdd");
            int onStartMin = OnStartHour * 60 + OnStartMinute;
            bool inRth = mod >= 570 && mod < 960;

            if (key != advSessionKey)
            {
                // Yesterday's RTH becomes today's prior-RTH reference before anything
                // is measured against it.
                if (!double.IsNaN(advRthHigh)) { advPrevRthHigh = advRthHigh; advPrevRthLow = advRthLow; }
                advRthHigh = double.NaN;
                advRthLow = double.NaN;
                advSessionKey = key;
                advOnLocked = false;
                advLastSweepMin = -1;
                advLastSweepLabel = string.Empty;
                for (int i = 0; i < advSweptLatch.Length; i++) advSweptLatch[i] = false;
            }

            // Overnight container: 18:00 the prior evening through the 09:30 open.
            bool inContainer = mod >= onStartMin || mod < 570;
            if (inContainer)
            {
                if (mod >= onStartMin && !advOnSawEvening)
                {
                    advOnSawEvening = true;
                    advOnFormHigh = High[0];
                    advOnFormLow = Low[0];
                }
                else
                {
                    if (double.IsNaN(advOnFormHigh) || High[0] > advOnFormHigh) advOnFormHigh = High[0];
                    if (double.IsNaN(advOnFormLow) || Low[0] < advOnFormLow) advOnFormLow = Low[0];
                }
            }
            else if (!advOnLocked && mod >= 570)
            {
                advOnHigh = advOnFormHigh;
                advOnLow = advOnFormLow;
                advOnPartial = !advOnSawEvening;   // history never reached 18:00
                advOnLocked = true;
                advOnSawEvening = false;
                advOnFormHigh = double.NaN;
                advOnFormLow = double.NaN;
            }

            if (inRth)
            {
                if (double.IsNaN(advRthHigh) || High[0] > advRthHigh) advRthHigh = High[0];
                if (double.IsNaN(advRthLow) || Low[0] < advRthLow) advRthLow = Low[0];

                // The six levels the research measured, in the order the latch array holds.
                double[] lv = { pmHigh, pmLow, advOnHigh, advOnLow, advPrevRthHigh, advPrevRthLow };
                bool[] isHigh = { true, false, true, false, true, false };
                string[] name = { "PMH", "PML", "ONH", "ONL", "YDH", "YDL" };
                for (int i = 0; i < lv.Length; i++)
                {
                    if (advSweptLatch[i]) continue;
                    if (!AdvancedState.Taken(isHigh[i], lv[i], High[0], Low[0])) continue;
                    advSweptLatch[i] = true;
                    advLastSweepMin = mod;
                    advLastSweepLabel = name[i];
                }
            }

            advSweepAge = AdvancedState.Age(mod, advLastSweepMin);
            advTrust = AdvancedState.Evaluate(advSweepAge, et.Hour * 100 + et.Minute,
                                              FreshMinutes, StaleMinutes, LtfCutoffEt);
        }
```

- [ ] **Step 3: Confirm the premarket field names**

The method above reads `pmHigh` and `pmLow`. Verify the base's actual premarket field names and correct the two references if they differ:

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
grep -n "private double pm" advanced/MapleStaxCBCAdvanced.cs
```

Expected: field declarations for the premarket high and low. Use whatever names appear here.

- [ ] **Step 4: Call it from OnBarUpdate**

In the `BarsInProgress == 0` block, add the call after `UpdatePremarketLevels();` so the premarket extremes are current before the sweep clock reads them:

```csharp
                UpdatePremarketLevels();
                UpdateAdvancedState();
```

- [ ] **Step 5: Compile**

```bash
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs
```

Expected: `0 Error(s)`. The new properties do not exist yet, so if the compiler reports `FreshMinutes`, `StaleMinutes`, `LtfCutoffEt`, `OnStartHour` or `OnStartMinute` as undefined, do Task 4 Step 1 first and then re-run this step.

- [ ] **Step 6: Commit**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git add advanced/MapleStaxCBCAdvanced.cs
git commit -m "feat(advanced): overnight container, prior-RTH levels, sweep clock"
```

---

### Task 4: Properties, panel rows, and the proximity band

**Files:**
- Modify: `advanced/MapleStaxCBCAdvanced.cs`

**Interfaces:**
- Consumes: `advTrust`, `advSweepAge`, `advLastSweepLabel`, `advOnHigh`, `advOnLow`, `advOnPartial` from Task 3; `AdvancedState.Near` from Task 2.
- Produces: the `Advanced` property group, two new plots at `Values[6]` and `Values[7]`, and four new status rows.

- [ ] **Step 1: Add the properties**

Add before the `#endregion` that closes the property block. Every description ends with the same sentence, which is the honesty constraint made mechanical:

```csharp
        // ---- Advanced (display-only research overlay) -----------------------
        private const string AdvCaveat = " Chosen from a descriptive, non-pre-registered "
            + "look at MapleStax's posted calls; display-only, not validated.";

        [NinjaScriptProperty]
        [Display(Name = "Show advanced rows", GroupName = "Advanced", Order = 1,
            Description = "Adds the overnight container, the significant-level sweep clock, the "
                        + "LTF trust state and the higher-timeframe EMA20 proximity to the status "
                        + "table. Changes emphasis only: it never gates, suppresses or vetoes a signal.")]
        public bool ShowAdvancedRows { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Overnight start hour (ET)", GroupName = "Advanced", Order = 2,
            Description = "Overnight container runs from this hour the prior evening to the RTH "
                        + "open. CME reopen is 18:00 ET.")]
        public int OnStartHour { get; set; } = 18;

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Overnight start minute (ET)", GroupName = "Advanced", Order = 3)]
        public int OnStartMinute { get; set; } = 0;

        [NinjaScriptProperty]
        [Display(Name = "Draw ONH / ONL lines", GroupName = "Advanced", Order = 4)]
        public bool ShowOvernightLines { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, 240)]
        [Display(Name = "Fresh sweep (minutes)", GroupName = "Advanced", Order = 5,
            Description = "A level taken within this many minutes leaves the LTF read LIVE." + AdvCaveat)]
        public int FreshMinutes { get; set; } = 15;

        [NinjaScriptProperty]
        [Range(1, 480)]
        [Display(Name = "Stale sweep (minutes)", GroupName = "Advanced", Order = 6,
            Description = "Past this many minutes since the last level was taken the LTF read is "
                        + "MUTED." + AdvCaveat)]
        public int StaleMinutes { get; set; } = 30;

        [NinjaScriptProperty]
        [Range(0, 2359)]
        [Display(Name = "LTF cutoff (ET, HHMM)", GroupName = "Advanced", Order = 7,
            Description = "From this ET time the LTF read is MUTED regardless of sweep age. "
                        + "1030 is 10:30. Set 0 to disable the clock half of the rule." + AdvCaveat)]
        public int LtfCutoffEt { get; set; } = 1030;

        [NinjaScriptProperty]
        [Range(0.05, 5.0)]
        [Display(Name = "AT band (x HTF ATR)", GroupName = "Advanced", Order = 8,
            Description = "Distance from the higher-timeframe EMA20, in HTF ATR units, that counts "
                        + "as being AT it." + AdvCaveat)]
        public double ProximityAtr { get; set; } = 0.5;

        [NinjaScriptProperty]
        [Range(0.1, 10.0)]
        [Display(Name = "Near band (x HTF ATR)", GroupName = "Advanced", Order = 9,
            Description = "Beyond this multiple the reading is far." + AdvCaveat)]
        public double ProximityNearAtr { get; set; } = 1.5;

        [NinjaScriptProperty]
        [Range(2, 200)]
        [Display(Name = "HTF ATR period", GroupName = "Advanced", Order = 10)]
        public int ProximityAtrPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "Show proximity band", GroupName = "Advanced", Order = 11,
            Description = "Shades the AT band around the higher-timeframe EMA20.")]
        public bool ShowProximityBand { get; set; } = false;

        [XmlIgnore]
        [Display(Name = "Muted LTF cell", GroupName = "Advanced", Order = 12,
            Description = "Background for the CBC row while the LTF read is MUTED.")]
        public Brush AdvMutedBg { get; set; } = new SolidColorBrush(Color.FromRgb(60, 60, 60));

        [Browsable(false)]
        public string AdvMutedBgSerializable
        {
            get { return Serialize.BrushToString(AdvMutedBg); }
            set { AdvMutedBg = Serialize.StringToBrush(value); }
        }
```

- [ ] **Step 2: Default the HTF to 15m and add the band plots**

In `OnStateChange`, `State.SetDefaults`, change the inherited default and append two transparent plots after the existing six:

```csharp
                HtfTimeframe = "15m";
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "AdvBandUpper");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "AdvBandLower");
```

- [ ] **Step 3: Add the HTF ATR read and the band**

Add this method and call it from the `BarsInProgress == 0` block immediately after `UpdateAdvancedState();`:

```csharp
        /// <summary>
        /// Proximity to the higher-timeframe EMA20, in HTF ATR units, plus the optional
        /// shaded band. The band is drawn as a Region between two transparent plots,
        /// which is how this file already draws the EMA clouds.
        /// </summary>
        private void UpdateAdvancedProximity()
        {
            double ema20 = Values[5][0];
            double atrVal = htfSeriesIndex >= 0 && CurrentBars[htfSeriesIndex] >= ProximityAtrPeriod
                ? ATR(BarsArray[htfSeriesIndex], ProximityAtrPeriod)[0]
                : double.NaN;

            advProximity = AdvancedState.Near(Close[0], ema20, atrVal, ProximityAtr, ProximityNearAtr);
            advProximityAtr = (double.IsNaN(ema20) || double.IsNaN(atrVal) || atrVal <= 0)
                ? double.NaN
                : Math.Abs(Close[0] - ema20) / atrVal;

            if (ShowProximityBand && !double.IsNaN(ema20) && !double.IsNaN(atrVal) && atrVal > 0)
            {
                Values[6][0] = ema20 + ProximityAtr * atrVal;
                Values[7][0] = ema20 - ProximityAtr * atrVal;
                Draw.Region(this, "ADVProxBand_" + CurrentBar, 1, 0, Values[6], Values[7],
                            null, Brushes.SlateGray, 20);
            }
        }
```

- [ ] **Step 4: Add the rows and the dim**

In the method that builds `statusRowData`, after the array is assigned, append the advanced rows and dim the LTF CBC row. The base builds a fixed array, so this appends rather than editing the literal:

```csharp
            if (ShowAdvancedRows)
            {
                string trustMsg;
                switch (advTrust)
                {
                    case AdvancedState.Trust.Live:    trustMsg = "LIVE (fresh " + advSweepAge + "m)"; break;
                    case AdvancedState.Trust.Fading:  trustMsg = "FADING (" + advSweepAge + "m)"; break;
                    case AdvancedState.Trust.NoSweep: trustMsg = "NO SWEEP (no level taken)"; break;
                    default:
                        trustMsg = (LtfCutoffEt > 0 && (et.Hour * 100 + et.Minute) >= LtfCutoffEt)
                            ? "MUTED (after " + (LtfCutoffEt / 100) + ":" + (LtfCutoffEt % 100).ToString("00") + ")"
                            : "MUTED (stale " + advSweepAge + "m)";
                        break;
                }

                string sweepMsg = advLastSweepMin < 0
                    ? "none taken"
                    : advLastSweepLabel + " " + (advLastSweepMin / 60).ToString("00") + ":"
                      + (advLastSweepMin % 60).ToString("00") + "  " + advSweepAge + "m ago";

                string onMsg = double.IsNaN(advOnHigh)
                    ? "forming"
                    : advOnHigh.ToString("0.##") + " / " + advOnLow.ToString("0.##")
                      + (advOnPartial ? "  (partial)" : string.Empty);

                string proxMsg;
                switch (advProximity)
                {
                    case AdvancedState.Proximity.At:   proxMsg = advProximityAtr.ToString("0.0") + " ATR  AT"; break;
                    case AdvancedState.Proximity.Near: proxMsg = advProximityAtr.ToString("0.0") + " ATR  near"; break;
                    case AdvancedState.Proximity.Far:  proxMsg = advProximityAtr.ToString("0.0") + " ATR  far"; break;
                    default: proxMsg = "no reading"; break;
                }

                List<StatusRow> extra = new List<StatusRow>(statusRowData);
                extra.Add(new StatusRow { Label = "LTF trust",  Value = trustMsg, LeftBg = StatusLabelBg, RightBg = StatusNeutralBg, LeftFg = StatusLabelText, RightFg = PickRightFg(StatusNeutralBg) });
                extra.Add(new StatusRow { Label = "Last sweep", Value = sweepMsg, LeftBg = StatusLabelBg, RightBg = StatusNeutralBg, LeftFg = StatusLabelText, RightFg = PickRightFg(StatusNeutralBg) });
                extra.Add(new StatusRow { Label = "ONH / ONL",  Value = onMsg,    LeftBg = StatusLabelBg, RightBg = StatusNeutralBg, LeftFg = StatusLabelText, RightFg = PickRightFg(StatusNeutralBg) });
                extra.Add(new StatusRow { Label = htfTf + " EMA20", Value = proxMsg, LeftBg = StatusLabelBg, RightBg = StatusNeutralBg, LeftFg = StatusLabelText, RightFg = PickRightFg(StatusNeutralBg) });

                // MUTED dims the LTF CBC row and nothing else. A three-way dim is not a
                // distinction a glance can make, so the colour channel carries the binary
                // and the LTF trust row carries the detail. It dims, it never hides: the
                // signal underneath is unchanged in every state.
                if (advTrust == AdvancedState.Trust.Muted)
                {
                    for (int i = 0; i < extra.Count; i++)
                    {
                        if (extra[i].Label != null && extra[i].Label.StartsWith("CBC (" + chartTf))
                        {
                            StatusRow r = extra[i];
                            r.RightBg = AdvMutedBg;
                            r.RightFg = PickRightFg(AdvMutedBg);
                            extra[i] = r;
                        }
                    }
                }

                statusRowData = extra.ToArray();
            }
```

- [ ] **Step 5: Compile**

```bash
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs
```

Expected: `0 Error(s)`. If `List<StatusRow>` is undefined, add `using System.Collections.Generic;` to the using block.

- [ ] **Step 6: Re-run the state tests**

The region must still extract and pass unchanged:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Maplestax_CBC-Ninja-wt-advanced\advanced\tools\run-advanced-tests.ps1"
```

Expected: `ALL PASS`.

- [ ] **Step 7: Commit**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git add advanced/MapleStaxCBCAdvanced.cs
git commit -m "feat(advanced): properties, status rows, proximity band"
```

---

### Task 5: The README and the final gate

**Files:**
- Create: `advanced/README.md`

- [ ] **Step 1: Write the README**

Self-contained, since the point of `advanced/` is that it stands alone. It must cover: what the indicator adds over the base, the exact rule behind each readout with its default, the standing caveat that none of it is validated, the deployment path through NT's export rather than a repo build, and the note that both indicators can run on one chart because the tags are prefixed.

- [ ] **Step 2: Verify nothing outside advanced/ changed**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git diff --stat main -- . ':!advanced'
```

Expected: no output.

- [ ] **Step 3: Final compile and tests**

```bash
python C:\Users\wolfe\.claude\scripts\test_ninjascript_compile.py D:\Maplestax_CBC-Ninja-wt-advanced\advanced\MapleStaxCBCAdvanced.cs
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "D:\Maplestax_CBC-Ninja-wt-advanced\advanced\tools\run-advanced-tests.ps1"
```

Expected: `0 Error(s)` and `ALL PASS`.

- [ ] **Step 4: Commit and push**

```bash
cd /d/Maplestax_CBC-Ninja-wt-advanced
git add advanced/README.md
git commit -m "docs(advanced): self-contained README for MapleStaxCBCAdvanced"
git push -u origin feat/maplestax-cbc-advanced
```

---

## Not in this plan, deliberately

- **`advanced/MapleStaxCBCAdvanced.zip`.** Produced by NinjaTrader's Tools > Export after the file compiles in the editor. No repo step creates it and no packaging script should be added.
- **Live-bar verification.** Nothing here runs inside NinjaTrader. The compile gate and the extracted-region tests are correctness by construction, not observation, and the README must say so.
