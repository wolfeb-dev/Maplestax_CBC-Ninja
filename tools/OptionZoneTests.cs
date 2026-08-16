// Tests for the option-zone geometry lifted out of MapleStaxCBC.cs.
// Run via tools/run-zone-tests.ps1, which extracts the <OptionZoneGeometry> region
// from the real indicator source so these assertions exercise the shipping bytes.
using System;

internal static class OptionZoneTests
{
    private static int failures;

    private static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what);
        if (!ok) failures++;
    }

    private static void Near(double got, double want, string what)
    {
        bool ok = Math.Abs(got - want) < 1e-9;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what + " (got " + got + ", want " + want + ")");
        if (!ok) failures++;
    }

    // A prior bar of 21520..21540, so range 20, and the symmetric shipping multipliers.
    private const double PrevHigh = 21540.0;
    private const double PrevLow = 21520.0;
    private const double LowMult = 0.45;
    private const double HighMult = 0.55;

    // ATR 8 at the shipping 0.5 multiplier gives a zone thickness of 4 points. Well under
    // the cap of rng*(low+high) = 20, so these cases exercise the unclamped path.
    private const double Atr = 8.0;
    private const double AtrMult = 0.5;

    // Placement and thickness cases, with the cloud-side gate switched off so they are not
    // testing two things at once. The gate has its own block at the end.
    private static bool C(bool biasBull, bool ltfBull, double prevHigh, double prevLow,
                          double htfEmaSlow, double lowMult, double highMult,
                          double atr, double atrMult, out OptionZoneGeometry.Zones z)
    {
        return OptionZoneGeometry.Compute(biasBull, ltfBull, prevHigh, prevLow,
                                          htfEmaSlow, htfEmaSlow, double.NaN, false,
                                          lowMult, highMult, atr, atrMult, out z);
    }

    private static int Main()
    {
        OptionZoneGeometry.Zones z;

        // Agreement is not a three-option situation: bearish bias with a bearish LTF CBC
        // leaves exactly one trade, so nothing should be drawn.
        Check(!C(false, false, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, AtrMult, out z),
              "agreement (both bearish): no zones");
        Check(!C(true, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, AtrMult, out z),
              "agreement (both bullish): no zones");

        // Degenerate inputs must not draw.
        Check(!C(false, true, 21530, 21530, 21545, LowMult, HighMult, Atr, AtrMult, out z),
              "zero-range prior bar: no zones");
        Check(!C(false, true, PrevHigh, PrevLow, double.NaN, LowMult, HighMult, Atr, AtrMult, out z),
              "EMA not yet formed: no zones");

        // The case from Maple's chart: EMAs above, so a bearish bias, and the LTF CBC has
        // just flipped long. Thickness is ATR-driven now: 8 * 0.5 = 4, so half is 2.
        Check(C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, AtrMult, out z),
              "bearish bias + long CBC: zones drawn");
        Near(z.Lo1, 21528.0, "bearish: zone 1 centred on the BRSG span, ATR thick");
        Near(z.Hi1, 21532.0, "bearish: zone 1 high");
        Near(z.Lo2, 21543.0, "bearish: zone 2 EMA20 low  = ema - half");
        Near(z.Hi2, 21547.0, "bearish: zone 2 EMA20 high = ema + half");
        Near(z.Lo3, 21516.0, "bearish: zone 3 trigger low  = prevLow - thickness");
        Near(z.Hi3, 21520.0, "bearish: zone 3 trigger high = prevLow (the flip level)");
        Check(z.Long1 && !z.Long2 && !z.Long3, "bearish: only zone 1 is long");

        // Exact mirror: EMAs below, so a bullish bias, and the LTF CBC has flipped short.
        Check(C(true, false, PrevHigh, PrevLow, 21515, LowMult, HighMult, Atr, AtrMult, out z),
              "bullish bias + short CBC: zones drawn");
        Near(z.Lo1, 21528.0, "bullish: zone 1 centred on the SGCR span, ATR thick");
        Near(z.Hi1, 21532.0, "bullish: zone 1 high");
        Near(z.Lo2, 21513.0, "bullish: zone 2 EMA20 low");
        Near(z.Hi2, 21517.0, "bullish: zone 2 EMA20 high");
        Near(z.Lo3, 21540.0, "bullish: zone 3 trigger low  = prevHigh (the flip level)");
        Near(z.Hi3, 21544.0, "bullish: zone 3 trigger high = prevHigh + thickness");
        Check(!z.Long1 && z.Long2 && z.Long3, "bullish: only zone 1 is short");

        // All three zones share one thickness. That is the whole point of driving height
        // from ATR: a row of bands that read as one system rather than three sizes.
        C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, AtrMult, out z);
        double t1 = z.Hi1 - z.Lo1, t2 = z.Hi2 - z.Lo2, t3 = z.Hi3 - z.Lo3;
        Near(t1, 4.0, "uniform thickness: zone 1 = atr * mult");
        Near(t2, 4.0, "uniform thickness: zone 2 = atr * mult");
        Near(t3, 4.0, "uniform thickness: zone 3 = atr * mult");

        // Thickness must track ATR, or the knob does nothing.
        C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, 4.0, AtrMult, out z);
        Near(z.Hi1 - z.Lo1, 2.0, "half the ATR gives half the thickness");
        C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, 1.0, out z);
        Near(z.Hi1 - z.Lo1, 8.0, "double the multiplier gives double the thickness");

        // Before ATR forms there is still a chart to draw on, so an absent ATR falls back to
        // the original prior-bar-range thickness rather than drawing nothing.
        foreach (double dead in new[] { 0.0, double.NaN })
        {
            OptionZoneGeometry.Zones fz;
            Check(C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, dead, AtrMult, out fz),
                  "ATR absent (" + dead + "): still draws");
            Near(fz.Hi1 - fz.Lo1, 2.0, "ATR absent (" + dead + "): falls back to rng * |high-low|");
        }

        // The cap. Zone 1 must never spill across the flip level that zone 3 marks, so a
        // huge ATR is clamped rather than allowed to swallow the trigger.
        C(false, true, PrevHigh, PrevLow, 21530, LowMult, HighMult, 1000.0, AtrMult, out z);
        Near(z.Hi1 - z.Lo1, 20.0, "runaway ATR clamps to rng * (low + high)");
        Check(z.Hi3 <= z.Lo1, "clamped: zone 3 still clear of zone 1");

        // The invariant that makes the display mean anything: zone 1 always opposes the
        // bias, zones 2 and 3 always run with it.
        foreach (bool bias in new[] { true, false })
        {
            C(bias, !bias, PrevHigh, PrevLow, 21530, LowMult, HighMult, Atr, AtrMult, out z);
            Check(z.Long1 == !bias && z.Long2 == bias && z.Long3 == bias,
                  "invariant (bias bull=" + bias + "): 1 against, 2 and 3 with");
        }

        // Zone 3 must never land on zone 1, which is the whole reason it is a trigger band
        // and not an entry band. Swept across ATRs so the cap is what enforces it, not luck.
        foreach (double atr in new[] { 0.5, 4.0, 8.0, 40.0, 1000.0 })
        {
            OptionZoneGeometry.Zones sz;
            C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, atr, AtrMult, out sz);
            Check(sz.Hi3 <= sz.Lo1, "atr " + atr + ": zone 3 clear of zone 1 (bearish)");
            C(true, false, PrevHigh, PrevLow, 21515, LowMult, HighMult, atr, AtrMult, out sz);
            Check(sz.Lo3 >= sz.Hi1, "atr " + atr + ": zone 3 clear of zone 1 (bullish)");
        }

        // Asymmetric multipliers must keep zone 1 a true mirror rather than reusing one span.
        OptionZoneGeometry.Zones zb, zl;
        C(false, true, PrevHigh, PrevLow, 21530, 0.20, 0.40, Atr, AtrMult, out zb);
        C(true, false, PrevHigh, PrevLow, 21530, 0.20, 0.40, Atr, AtrMult, out zl);
        Near(zb.Lo1, 21524.0, "asymmetric: bearish zone 1 low");
        Near(zb.Hi1, 21528.0, "asymmetric: bearish zone 1 high");
        Near(zl.Lo1, 21532.0, "asymmetric: bullish zone 1 low");
        Near(zl.Hi1, 21536.0, "asymmetric: bullish zone 1 high");
        Check(Math.Abs(zb.Lo1 - zl.Lo1) > 1e-9, "asymmetric: the two zone 1 spans separate");

        // Every zone must be a real rectangle, not an inverted or zero-height one.
        C(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, Atr, AtrMult, out z);
        Check(z.Hi1 > z.Lo1 && z.Hi2 > z.Lo2 && z.Hi3 > z.Lo3, "all three zones have positive height");

        // The precondition Preview mode stands on. Preview draws by handing Compute a bias
        // that is the opposite of the LTF CBC, so disagreement must be SUFFICIENT on its own:
        // given a real prior bar and a formed EMA, an opposed bias always yields three zones.
        // If a future gate makes Compute reject some opposed cases, Preview silently stops
        // drawing on those charts and this assertion is what catches it.
        bool previewAlwaysDraws = true;
        foreach (bool ltfBull in new[] { true, false })
            foreach (double ema in new[] { 21500.0, 21530.0, 21560.0 })
                foreach (double high in new[] { 21540.0, 21600.0 })
                    foreach (double lo in new[] { 0.45, 0.20, 0.0 })
                        foreach (double atr in new[] { 0.0, 8.0, 1000.0 })
                        {
                            OptionZoneGeometry.Zones pz;
                            // Preview's rule verbatim: biasBull = !ltfBull.
                            if (!C(!ltfBull, ltfBull, high, PrevLow, ema, lo, HighMult, atr, AtrMult, out pz)
                                || !(pz.Hi1 > pz.Lo1 && pz.Hi2 > pz.Lo2 && pz.Hi3 > pz.Lo3))
                                previewAlwaysDraws = false;
                        }
        Check(previewAlwaysDraws, "preview invariant: an opposed bias always draws three real zones");

        // ---- The cloud-side gate ------------------------------------------------------
        // The setup is a CBC flip running INTO the higher-timeframe EMAs, so the EMAs have to
        // be on the far side of price. Bearish stack (slow over fast) means the cloud is
        // overhead and price must still be underneath it; bullish is the mirror. Without this
        // the zones fire on a flip that happens with price already through the cloud, and
        // option 2 becomes a fade at a level price has left behind.
        const double CloudFastBear = 21570.0;   // bearish stack: slow ABOVE fast
        const double CloudSlowBear = 21590.0;
        const double CloudFastBull = 21590.0;   // bullish stack: fast above slow
        const double CloudSlowBull = 21570.0;

        Check(OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, CloudSlowBear, 21540.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bearish cloud overhead, price below it: the setup is on");
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, CloudSlowBear, 21575.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bearish cloud, price already inside it: no setup");
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, CloudSlowBear, 21600.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bearish cloud, price above it entirely: no setup");

        Check(OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow,
                  CloudFastBull, CloudSlowBull, 21620.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bullish cloud below, price above it: the setup is on");
        Check(!OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow,
                  CloudFastBull, CloudSlowBull, 21580.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bullish cloud, price already inside it: no setup");
        Check(!OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow,
                  CloudFastBull, CloudSlowBull, 21500.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "bullish cloud, price below it entirely: no setup");

        // Zone 2 is the trade AT the 20, so it must sit on the slow EMA and nowhere else.
        OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
            CloudFastBear, CloudSlowBear, 21540.0, true, LowMult, HighMult, Atr, AtrMult, out z);
        Near((z.Lo2 + z.Hi2) * 0.5, CloudSlowBear, "zone 2 is centred on the HTF slow EMA, not the fast one");
        Check(!z.Long2, "bearish stack: zone 2 is the short into the 20");

        OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow,
            CloudFastBull, CloudSlowBull, 21620.0, true, LowMult, HighMult, Atr, AtrMult, out z);
        Near((z.Lo2 + z.Hi2) * 0.5, CloudSlowBull, "mirror: zone 2 is still centred on the HTF slow EMA");
        Check(z.Long2, "bullish stack: zone 2 is the long into the 20");

        // With the gate off, price is irrelevant - that is what lets Preview draw out of hours.
        Check(OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, CloudSlowBear, 21600.0, false, LowMult, HighMult, Atr, AtrMult, out z),
              "gate off: a price that would fail the gate still draws");
        Check(OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  double.NaN, CloudSlowBear, double.NaN, false, LowMult, HighMult, Atr, AtrMult, out z),
              "gate off: an absent fast EMA and close still draw");

        // A missing input must fail the gate closed, never open.
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  double.NaN, CloudSlowBear, 21540.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "gate on: an absent fast EMA draws nothing");
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, CloudSlowBear, double.NaN, true, LowMult, HighMult, Atr, AtrMult, out z),
              "gate on: an absent close draws nothing");
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow,
                  CloudFastBear, double.NaN, 21540.0, true, LowMult, HighMult, Atr, AtrMult, out z),
              "an absent slow EMA draws nothing, gate or no gate");

        // ---- Close-through, the rule that truncates a zone ----------------------------
        // A zone dies when a bar CLOSES on the far side of it: below a zone you would be
        // buying into, above one you would be selling into. Wicks through do not count,
        // which is the whole point of testing the close.
        const double zLo = 21528.0, zHi = 21532.0;

        Check(OptionZoneGeometry.ClosedThrough(true, zLo, zHi, 21527.75),
              "long zone: close below the low is closed through");
        Check(!OptionZoneGeometry.ClosedThrough(true, zLo, zHi, zLo),
              "long zone: close exactly on the low still holds");
        Check(!OptionZoneGeometry.ClosedThrough(true, zLo, zHi, 21530.0),
              "long zone: close inside holds");
        Check(!OptionZoneGeometry.ClosedThrough(true, zLo, zHi, 21600.0),
              "long zone: close far above holds (that is the trade working)");

        Check(OptionZoneGeometry.ClosedThrough(false, zLo, zHi, 21532.25),
              "short zone: close above the high is closed through");
        Check(!OptionZoneGeometry.ClosedThrough(false, zLo, zHi, zHi),
              "short zone: close exactly on the high still holds");
        Check(!OptionZoneGeometry.ClosedThrough(false, zLo, zHi, 21530.0),
              "short zone: close inside holds");
        Check(!OptionZoneGeometry.ClosedThrough(false, zLo, zHi, 21400.0),
              "short zone: close far below holds (that is the trade working)");

        // The two directions must be genuine mirrors, not one rule with a sign bug: a close
        // that kills the long zone must leave the short zone alone, and vice versa.
        Check(OptionZoneGeometry.ClosedThrough(true, zLo, zHi, 21500.0)
              && !OptionZoneGeometry.ClosedThrough(false, zLo, zHi, 21500.0),
              "mirror: a close below kills long only");
        Check(OptionZoneGeometry.ClosedThrough(false, zLo, zHi, 21560.0)
              && !OptionZoneGeometry.ClosedThrough(true, zLo, zHi, 21560.0),
              "mirror: a close above kills short only");

        // A NaN close must never be read as an invalidation.
        Check(!OptionZoneGeometry.ClosedThrough(true, zLo, zHi, double.NaN),
              "NaN close does not close through a long zone");
        Check(!OptionZoneGeometry.ClosedThrough(false, zLo, zHi, double.NaN),
              "NaN close does not close through a short zone");

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURE(S)"));
        return failures == 0 ? 0 : 1;
    }
}
