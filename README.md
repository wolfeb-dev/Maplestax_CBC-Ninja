# MapleStax CBC for NinjaTrader 8

A NinjaScript port of the [MapleStax CBC](https://www.tradingview.com/) PineScript indicator suite. Discretionary trading framework built around **Candle By Candle** (CBC) method for trend identification, plus a stack of supporting reference levels: previous-day H/L, premarket H/L, opening-range breakouts across three sessions, dual-EMA cloud, VWAP, EMA(200) trend filter, Bill Breaker grid levels, BRSG zone, CBC flip levels, and an at-a-glance status table.

Original PineScript by **MapleStax / AsiaRoo**, enhanced by **WildWex**. C# / NinjaScript port by **-E** maintained here.

## What the indicator does

### CBC (Candle By Candle)
The core signal. A **bullish flip** prints when a candle closes above the prior candle's high; a **bearish flip** when a candle closes below the prior candle's low. The prior-bar break must be strict — equal closes do not flip. Operates simultaneously on the chart timeframe (LTF) and an optional higher timeframe (HTF), producing the "CBC agreement" state used by the bar coloring and status panel.

- **FOBO** (Fake-Out-Break-Out): two consecutive flips in opposite directions are highlighted in yellow as a fade signal.
- **Bar coloring**: bars repaint when LTF and HTF are both bullish (long color), both bearish (short color), or in conflict (neutral).
- **CBC flip levels**: horizontal lines at the prior-bar high/low that the next flip would have to clear.
- **BRSG zone** (Buy-Red-Sell-Green): a fractional band of the previous bar's range, used as an entry filter.

### Option zones
When the LTF CBC and the higher-timeframe bias point in opposite directions, the method gives you three choices rather than one. This draws all three at price so you can see where each one lives. **Ships off.**

Worked example, a bearish bias with the LTF CBC freshly flipped long:

1. **BRSG LONG** - take the CBC trade now, against the bias. The band is the usual fractional span of the previous bar's range.
2. **EMA20 SHORT (small)** - fade the retrace back into the 20 EMA, with the bias, at smaller size because the CBC has not confirmed. The band is the 20 EMA given the same thickness as the other two.
3. **SGCR SHORT on flip** - wait for the CBC to confirm, then enter. This zone marks the *confirmation trigger*, not an entry: the LTF CBC flips bearish only on a close below the prior bar's low, so the zone sits just under that level. The entry band for that trade depends on a bar that has not printed yet, so drawing one would be invention.

Every one of those mirrors when the bias is bullish and the CBC has flipped short (SGCR short, EMA20 long, BRSG long on flip). Zone 1 always runs against the bias; zones 2 and 3 always run with it.

When the two reads **agree** there is only one trade, not three, so nothing is drawn. The existing BRSG zone and CBC flip lines already cover that case.

- **Bias source** picks which higher-timeframe read is the bias: the HTF CBC state, or the HTF EMA cloud (HTF fast EMA against HTF slow EMA).
- At the default 0.45 / 0.55 multipliers the BRSG and SGCR *entry* bands are the same span, measured from opposite ends of the prior bar. That is expected, and it is why zone 3 is a trigger rather than an entry band: otherwise two of the three zones would land on one rectangle.
- The geometry is covered by `tools/run-zone-tests.ps1`, which extracts the shipping code out of `MapleStaxCBC.cs` and asserts against it, so the tests cannot drift from the indicator.

### Reference levels
- **PDH / PDL** — Previous-day high/low from the **23-hour Globex futures session (17:00 ET → 16:00 ET)**, drawn from the candle that printed the H/L. Locks at the next 16:00 ET close; rolls over to the new session at 17:00 ET.
- **PMH / PML** — Premarket high/low from the **04:00 → 09:30 ET** window, drawn from the candle that printed the H/L. Lines are visible during today's RTH session and freeze at 16:00 ET.
- **PDH/PDL backfill** — On chart load, the prior fully-completed Globex session is scanned so PDH/PDL render immediately at the live edge with no warmup, as long as your chart history covers the prior session.
- Per-level color, width (1-4), and style (Solid / Dashed / Dotted) configurable in `Reference levels`.

### Opening Range (Sessions)
Three independently togglable opening-range trackers:
- **NY** — anchor 09:30 ET, default 30 minutes.
- **London** — anchor 03:00 ET.
- **Asian** — anchor 20:00 ET, with rollback so bars before 03:00 ET use the previous calendar day's 20:00.

Each session's OR line:
- **Live-updates during the OR window** — origin and price track new highs/lows as they form.
- **Freezes at OR close** (e.g., 10:00 ET for default 30-min NY OR), then extends through the next 16:00 ET RTH close where it locks.
- Anchored at the candle that printed the OR-H or OR-L.
- Each session uses a fixed color (NY yellow, London purple, Asian orange) for the line and the matching fill region.

### EMA cloud
Two EMAs (defaults 9 / 20) painted as a colored region — green when fast ≥ slow, red otherwise. Optional HTF cloud overlay. Optional EMA20 touch dots.

### VWAP & EMA(200)
Standard daily VWAP plot (orange dashed) and a long-term EMA used as a trend filter. Both feed the status table.

### Bill Breaker
A grid of `multiplier`-spaced horizontal lines centered around a base that auto-shifts when price breaks two multiples in either direction. Configurable multiplier and color.

### LTF/HTF Pivots
Optional bar-pivot overlays on both timeframes. `N`-bar lookback (default 1) for pivot confirmation; per-side colors, line width, and dash style.

### Status table
Compact on-chart dashboard. Rows: Market session, CBC LTF, CBC HTF, Opening range, EMA cloud, VWAP, EMA(200) vs price. Configurable position (9 anchors), text size, and per-row colors. Left column shows row labels in a blue-gray slab; right column flips text to black on yellow backgrounds for contrast, otherwise white.

### Status table color palette
- Bullish row bg / Bearish row bg / Neutral row bg / Inside-range bg / Header bg — all configurable.

## Installation

1. Download `MapleStaxCBC.zip` from this repository. Do not unzip it — NinjaTrader imports the archive directly.
2. Launch NinjaTrader 8 and open the **Control Center**.
3. Go to **Tools → Import → NinjaScript Add-On…**
4. In the file picker, browse to the downloaded `MapleStaxCBC.zip` and click **Open**. NinjaTrader will import and compile the indicator and confirm with a success dialog.
5. Open a chart, then **Indicators…** (or right-click the chart → Indicators). Select **MapleStaxCBC** from the list and click **Apply**.

To update later, simply repeat steps 3-4 with the newer `MapleStaxCBC.zip`; NinjaTrader will overwrite the existing install.

### Alternate: source-file install

If you prefer to install from source (for example, to review or modify the code before compiling):

1. Download `MapleStaxCBC.cs` from this repository.
2. Place it in your NinjaTrader 8 custom indicators folder:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\MapleStaxCBC.cs`
3. In NinjaTrader, open **Tools → NinjaScript Editor**, find `MapleStaxCBC` under **Indicators**, and click **Compile** (F5).
4. Apply to a chart via **Indicators → MapleStaxCBC**.

## Important: Time Zone setting

All session windows (PDH/PDL, PMH/PML, OR sessions, Market session label, premarket detection) are anchored in **America/New_York (Eastern Time)**. The indicator needs to know what timezone the chart's bar timestamps are in so it can convert correctly.

In the indicator's properties panel, the first setting is **`Time zone → Chart TZ`**:

- `Eastern` *(default)* — correct if your chart's display time zone is set to Eastern Time, which is the most common futures setup.
- `Central` / `Mountain` / `Pacific` / `Utc` / `Local` — explicit override for charts displayed in a different time zone.
- `AutoDetect` — falls back to your Windows local time zone.

**Example:** If your machine is in Pacific Time but you've set the chart's display timezone to ET (NinjaTrader → Right-click chart → Properties → "Time zone" set to Eastern), pick **Eastern** in this dropdown. Otherwise the bars will be misinterpreted as Pacific and every session window will fire 3 hours off (or 1 hour during DST mismatches).

## Settings groups

The properties panel groups settings as follows (alphabetical in NinjaTrader's UI):

- **CBC flip levels** — LTF and HTF flip-line colors, widths, toggles.
- **HTF Signals** — HTF timeframe selector, EMA cloud toggle, LONG/SHORT label settings, HTF pivot settings.
- **LTF Signals** — FOBO toggle, EMA20 touch dots, VWAP toggle, BRSG zone, LTF pivot settings.
- **Opening Range (Sessions)** — Per-session toggles (NY/London/Asian), OR length in minutes.
- **Option zones** - Show toggle, bias source (HTF CBC or HTF EMA cloud), labels toggle, fill opacity, long/short colors.
- **Reference levels** — PDH, PDL, PMH, PML toggles plus per-level color/width/style. Show level name / show level price toggles.
- **Status table** — Show toggle, position, text size, per-row backgrounds, text colors.
- **Time zone** — Chart TZ override (described above).

## Credits

- Original concept and PineScript: **MapleStax** / **AsiaRoo**
- PineScript enhancements: **WildWex**
- C# / NinjaScript port: **-E** this repository

## License

No license is currently attached. If you intend to distribute, modify, or relicense, please obtain permission from the original PineScript authors first. Do not assume MIT/BSD; this port is a derivative work of the original PineScript.
