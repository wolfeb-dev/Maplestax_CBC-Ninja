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

    private static int Main()
    {
        OptionZoneGeometry.Zones z;

        // Agreement is not a three-option situation: bearish bias with a bearish LTF CBC
        // leaves exactly one trade, so nothing should be drawn.
        Check(!OptionZoneGeometry.Compute(false, false, PrevHigh, PrevLow, 21545, LowMult, HighMult, out z),
              "agreement (both bearish): no zones");
        Check(!OptionZoneGeometry.Compute(true, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, out z),
              "agreement (both bullish): no zones");

        // Degenerate inputs must not draw.
        Check(!OptionZoneGeometry.Compute(false, true, 21530, 21530, 21545, LowMult, HighMult, out z),
              "zero-range prior bar: no zones");
        Check(!OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow, double.NaN, LowMult, HighMult, out z),
              "EMA not yet formed: no zones");

        // The case from Maple's chart: EMAs above, so a bearish bias, and the LTF CBC has
        // just flipped long. rng 20, band thickness 20 * 0.10 = 2.0, half 1.0.
        Check(OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, out z),
              "bearish bias + long CBC: zones drawn");
        Near(z.Lo1, 21529.0, "bearish: zone 1 BRSG low  = prevLow + rng*0.45");
        Near(z.Hi1, 21531.0, "bearish: zone 1 BRSG high = prevLow + rng*0.55");
        Near(z.Lo2, 21544.0, "bearish: zone 2 EMA20 low  = ema - half");
        Near(z.Hi2, 21546.0, "bearish: zone 2 EMA20 high = ema + half");
        Near(z.Lo3, 21518.0, "bearish: zone 3 trigger low  = prevLow - band");
        Near(z.Hi3, 21520.0, "bearish: zone 3 trigger high = prevLow");
        Check(z.Long1 && !z.Long2 && !z.Long3, "bearish: only zone 1 is long");

        // Exact mirror: EMAs below, so a bullish bias, and the LTF CBC has flipped short.
        Check(OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow, 21515, LowMult, HighMult, out z),
              "bullish bias + short CBC: zones drawn");
        Near(z.Lo1, 21529.0, "bullish: zone 1 SGCR low  = prevHigh - rng*0.55");
        Near(z.Hi1, 21531.0, "bullish: zone 1 SGCR high = prevHigh - rng*0.45");
        Near(z.Lo2, 21514.0, "bullish: zone 2 EMA20 low");
        Near(z.Hi2, 21516.0, "bullish: zone 2 EMA20 high");
        Near(z.Lo3, 21540.0, "bullish: zone 3 trigger low  = prevHigh");
        Near(z.Hi3, 21542.0, "bullish: zone 3 trigger high = prevHigh + band");
        Check(!z.Long1 && z.Long2 && z.Long3, "bullish: only zone 1 is short");

        // The invariant that makes the display mean anything: zone 1 always opposes the
        // bias, zones 2 and 3 always run with it.
        foreach (bool bias in new[] { true, false })
        {
            OptionZoneGeometry.Compute(bias, !bias, PrevHigh, PrevLow, 21530, LowMult, HighMult, out z);
            Check(z.Long1 == !bias && z.Long2 == bias && z.Long3 == bias,
                  "invariant (bias bull=" + bias + "): 1 against, 2 and 3 with");
        }

        // Zone 3 must never land on zone 1, which is the whole reason it is a trigger band
        // and not an entry band: at the symmetric defaults the BRSG and SGCR entry spans
        // are literally the same rectangle.
        OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, out z);
        Check(z.Hi3 <= z.Lo1, "zone 3 sits clear of zone 1 (bearish)");
        OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow, 21515, LowMult, HighMult, out z);
        Check(z.Lo3 >= z.Hi1, "zone 3 sits clear of zone 1 (bullish)");

        // Asymmetric multipliers must keep zone 1 a true mirror rather than reusing one span.
        OptionZoneGeometry.Zones zb, zl;
        OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow, 21530, 0.20, 0.40, out zb);
        OptionZoneGeometry.Compute(true, false, PrevHigh, PrevLow, 21530, 0.20, 0.40, out zl);
        Near(zb.Lo1, 21524.0, "asymmetric: bearish zone 1 low  = prevLow + rng*0.20");
        Near(zb.Hi1, 21528.0, "asymmetric: bearish zone 1 high = prevLow + rng*0.40");
        Near(zl.Lo1, 21532.0, "asymmetric: bullish zone 1 low  = prevHigh - rng*0.40");
        Near(zl.Hi1, 21536.0, "asymmetric: bullish zone 1 high = prevHigh - rng*0.20");
        Check(Math.Abs(zb.Lo1 - zl.Lo1) > 1e-9, "asymmetric: the two zone 1 spans separate");

        // Every zone must be a real rectangle, not an inverted or zero-height one.
        OptionZoneGeometry.Compute(false, true, PrevHigh, PrevLow, 21545, LowMult, HighMult, out z);
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
                    {
                        OptionZoneGeometry.Zones pz;
                        // Preview's rule verbatim: biasBull = !ltfBull.
                        if (!OptionZoneGeometry.Compute(!ltfBull, ltfBull, high, PrevLow, ema, lo, HighMult, out pz)
                            || !(pz.Hi1 > pz.Lo1 && pz.Hi2 > pz.Lo2 && pz.Hi3 > pz.Lo3))
                            previewAlwaysDraws = false;
                    }
        Check(previewAlwaysDraws, "preview invariant: an opposed bias always draws three real zones");

        Console.WriteLine(failures == 0 ? "ALL PASS" : (failures + " FAILURE(S)"));
        return failures == 0 ? 0 : 1;
    }
}
