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
        Check(!AdvancedState.Taken(false, double.NaN, 100.5, 99.0), "an unformed low level is never taken");

        // --- Age: -1 means NO SWEEP and must never be read as zero minutes.
        Check(AdvancedState.Age(600, -1) == -1, "no sweep yet reads -1");
        Check(AdvancedState.Age(600, 570) == 30, "age is the minute difference");
        Check(AdvancedState.Age(600, 600) == 0, "a sweep on this very bar is zero minutes old");
        Check(AdvancedState.Age(570, 600) == -1, "a sweep in the future is refused");

        // --- Evaluate: the whole rule, checked at every boundary.
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
        Check(AdvancedState.Evaluate(0, 1000, Fresh, Stale, Cutoff) == AdvancedState.Trust.Live,
              "a sweep on this bar is Live, which is why Age must not return 0 for no-sweep");

        // --- Near: unknown rather than a confident AT when it cannot measure.
        Check(AdvancedState.Near(100.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.At,
              "sitting on the EMA is AT");
        Check(AdvancedState.Near(105.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.At,
              "exactly 0.5 ATR is AT");
        Check(AdvancedState.Near(94.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Near,
              "0.6 ATR below is near, and direction does not matter");
        Check(AdvancedState.Near(115.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Near,
              "exactly 1.5 ATR is still near");
        Check(AdvancedState.Near(116.0, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Far,
              "past 1.5 ATR is far");
        Check(AdvancedState.Near(100.0, 100.0, 0.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a zero ATR cannot measure, so Unknown not At");
        Check(AdvancedState.Near(100.0, 100.0, -1.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a negative ATR is Unknown");
        Check(AdvancedState.Near(100.0, 100.0, double.NaN, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a NaN ATR is Unknown");
        Check(AdvancedState.Near(100.0, double.NaN, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "an unformed EMA is Unknown");
        Check(AdvancedState.Near(double.NaN, 100.0, 10.0, 0.5, 1.5) == AdvancedState.Proximity.Unknown,
              "a NaN price is Unknown");

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURE(S)"));
        return failures == 0 ? 0 : 1;
    }
}
