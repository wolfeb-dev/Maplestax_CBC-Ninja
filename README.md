# MapleStax CBC for NinjaTrader 8

A NinjaScript port of the [MapleStax CBC](https://www.tradingview.com/) PineScript indicator suite. Discretionary trading framework built around **Confirmed Body Closes** (CBC) for trend identification, plus a stack of supporting reference levels: previous-day H/L, premarket H/L, opening-range breakouts across three sessions, dual-EMA cloud, VWAP, EMA(200) trend filter, Bill Breaker grid levels, BRSG zone, CBC flip levels, and an at-a-glance status table.

Original PineScript by **MapleStax / AsiaRoo**, enhanced by **WildWex**. C# / NinjaScript port by -E maintained here.

## What the indicator does

### CBC (Confirmed Body Close)
The core signal. A **bullish flip** prints when `Close > High[1]`; a **bearish flip** when `Close < Low[1]`. Strict prior-bar breaks only — no `<=`/`>=` touch flips. Operates simultaneously on the chart timeframe (LTF) and an optional higher timeframe (HTF), producing the "CBC agreement" state used by the bar coloring and status panel.

- **FOBO** (Failure-Of-Break-Out): two consecutive flips in opposite directions are highlighted in yellow as a fade signal.
- **Bar coloring**: bars repaint when LTF and HTF are both bullish (long color), both bearish (short color), or in conflict (neutral).
- **CBC flip levels**: horizontal lines at the prior-bar high/low that the next flip would have to clear.
- **BRSG zone**: a fractional band of the previous bar's range, used as an entry filter.

### Reference levels
- **PDH / PDL** — Previous-day high/low from the **23-hour Globex futures session (17:00 ET → 16:00 ET)**, drawn from the candle that printed the H/L. Locks at the next 16:00 ET close; rolls over to the new session at 17:00 ET.
- **PMH / PML** — Premarket high/low from the **04:00 → 09:30 ET** window, drawn from the candle that printed the H/L. Lines are visible during today's RTH session and freeze at 16:00 ET.
- **YDH/YDL backfill** — On chart load, the prior fully-completed Globex session is scanned from loaded bars so PDH/PDL render on the first bar at the live edge (no warmup needed when chart history covers the prior session).
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
Compact dashboard rendered via SharpDX. Rows: Market session, CBC LTF, CBC HTF, Opening range, EMA cloud, VWAP, EMA(200) vs price. Configurable position (9 anchors), text size, and per-row colors. Left column shows row labels in a blue-gray slab; right column flips text to black on yellow backgrounds for contrast, otherwise white. Single cell borders plus a double-line frame.

### Status table color palette
- Bullish row bg / Bearish row bg / Neutral row bg / Inside-range bg / Header bg — all configurable.

## Installation

1. Download `MapleStaxCBC.cs` from this repository.
2. Drop it into your NinjaTrader 8 custom indicators folder:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\MapleStaxCBC.cs`
3. In NinjaTrader, open **Tools → NinjaScript Editor**, find `MapleStaxCBC` under Indicators, and click **Compile** (F5). Resolve any reference issues NT prompts for.
4. Apply to a chart via **Indicators → MapleStaxCBC**.

## Important: Time Zone setting

All session windows (PDH/PDL, PMH/PML, OR sessions, Market session label, premarket detection) are anchored in **America/New_York (Eastern Time)**. The indicator needs to know what timezone the chart's bar timestamps are in so it can convert correctly.

In the indicator's properties panel, the first setting is **`Time zone → Chart TZ`**:

- `AutoDetect` *(default)* — assumes chart TZ = your Windows local TZ. Correct for most users.
- `Eastern` / `Central` / `Mountain` / `Pacific` / `Utc` / `Local` — explicit override.

**Example:** If your machine is in Pacific Time but you've set the chart's display timezone to ET (NinjaTrader → Right-click chart → Properties → "Time zone" set to Eastern), pick **Eastern** in this dropdown. Otherwise the bars will be misinterpreted as Pacific and every session window will fire 3 hours off (or 1 hour during DST mismatches).

## Settings groups

The properties panel groups settings as follows (alphabetical in NinjaTrader's UI):

- **CBC flip levels** — LTF and HTF flip-line colors, widths, toggles.
- **HTF Signals** — HTF timeframe selector, EMA cloud toggle, LONG/SHORT label settings, HTF pivot settings.
- **LTF Signals** — FOBO toggle, EMA20 touch dots, VWAP toggle, BRSG zone, LTF pivot settings.
- **Opening Range (Sessions)** — Per-session toggles (NY/London/Asian), OR length in minutes.
- **Reference levels** — PDH, PDL, PMH, PML toggles plus per-level color/width/style. Show level name / show level price toggles.
- **Status table** — Show toggle, position, text size, per-row backgrounds, text colors.
- **Time zone** — Chart TZ override (described above).

## Credits

- Original concept and PineScript: **MapleStax** / **AsiaRoo**
- PineScript enhancements: **WildWex**
- C# / NinjaScript port: **-E** this repository

## License

No license is currently attached. If you intend to distribute, modify, or relicense, please obtain permission from the original PineScript authors first. Do not assume MIT/BSD; this port is a derivative work of the original PineScript.
