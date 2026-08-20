#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System.Windows;
using System.Windows.Media;
#endregion

public enum MapleStaxRefLineStyle
{
    Solid,
    Dashed,
    Dotted
}

public enum MapleStaxStatusTextSize
{
    Tiny,
    Small,
    Normal,
    Large,
    Huge
}

public enum MapleStaxStatusTablePosition
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

// Which higher-timeframe read sets the trading bias the three option zones hang off.
public enum MapleStaxOptionBias
{
    HtfCbc,
    HtfEmaCloud
}

public enum MapleStaxChartTimeZone
{
    AutoDetect,
    Eastern,
    Central,
    Mountain,
    Pacific,
    Utc,
    Local
}

namespace NinjaTrader.NinjaScript.Indicators
{
    public class MapleStaxCBCAdvanced : Indicator
    {
        [NinjaScriptProperty]
        [Display(Name = "Chart TZ", GroupName = "Time zone", Order = 1, Description = "Time zone of the chart's bar timestamps. NT8 has no public API to read the chart's Display Time Zone, so AutoDetect falls back to your Windows local TZ. If your chart's display TZ is different from your machine local (e.g., PT machine running an ET chart), pick the chart's TZ explicitly here (Eastern in that example) so session windows resolve correctly.")]
        public MapleStaxChartTimeZone ChartTz { get; set; } = MapleStaxChartTimeZone.Eastern;

        [NinjaScriptProperty]
        [Display(Name = "Show FOBO", GroupName = "LTF Signals", Order = 1)]
        public bool ShowFobo { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "EMA20 touch dots", GroupName = "LTF Signals", Order = 2)]
        public bool ShowEmaTouchDots { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Show VWAP", GroupName = "LTF Signals", Order = 13)]
        public bool ShowVwap { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "BRSG Zone", GroupName = "LTF Signals", Order = 3)]
        public bool ShowBrsgZone { get; set; } = false;

        [XmlIgnore]
        [Display(Name = "BRSG Color", GroupName = "LTF Signals", Order = 4)]
        public Brush BrsgColor { get; set; } = Brushes.Gray;

        [Browsable(false)]
        public string BrsgColorSerializable { get { return Serialize.BrushToString(BrsgColor); } set { BrsgColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "High", GroupName = "LTF Signals", Order = 5)]
        public double BrsgHighMult { get; set; } = 0.55;

        [NinjaScriptProperty]
        [Display(Name = "Low", GroupName = "LTF Signals", Order = 6)]
        public double BrsgLowMult { get; set; } = 0.45;

        [NinjaScriptProperty]
        [Display(Name = "Show", GroupName = "Option zones", Order = 1, Description = "Draw the three trading options at price whenever the LTF CBC and the higher-timeframe bias disagree. When they agree there is only one trade, so nothing is drawn.")]
        public bool ShowOptionZones { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Preview", GroupName = "Option zones", Order = 2, Description = "REVIEW AID, NOT A SIGNAL. Forces a bias opposite the LTF CBC so the three zones always draw, and skips the higher-timeframe warm-up, which lets you see them on a closed market or outside trading hours. Prices, the prior bar's range and the 20 EMA are all real; only the bias is invented. Labels are suffixed PREVIEW while this is on. Turn it off to trade.")]
        public bool PreviewOptionZones { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Bias source", GroupName = "Option zones", Order = 3, Description = "Which higher-timeframe read is the bias. HTF EMA cloud is the setup as traded: the HTF 9 against the HTF 20, and price must still be on the near side of both. HTF CBC uses the higher-timeframe CBC state instead and has no cloud to gate on, so it fires on the disagreement alone.")]
        public MapleStaxOptionBias OptionZoneBias { get; set; } = MapleStaxOptionBias.HtfEmaCloud;

        [NinjaScriptProperty]
        [Display(Name = "Width (x ATR)", GroupName = "Option zones", Order = 4, Description = "Height of each zone as a multiple of ATR. All three zones share this thickness. Capped so zone 1 can never spill across the flip level zone 3 marks.")]
        public double OptionZoneAtrMult { get; set; } = 0.5;

        [NinjaScriptProperty]
        [Display(Name = "Width ATR period", GroupName = "Option zones", Order = 5, Description = "ATR period behind the zone thickness.")]
        public int OptionZoneAtrPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "Window start (ET, HHMM)", GroupName = "Option zones", Order = 6, Description = "Earliest ET time an episode may start, as HHMM: 930 is 09:30. Zones outside the session you trade would otherwise take the kept-example slots away from the ones you want to study. Set 0 for no lower bound.")]
        public int OptionZoneWindowStartEt { get; set; } = 930;

        [NinjaScriptProperty]
        [Display(Name = "Window end (ET, HHMM)", GroupName = "Option zones", Order = 7, Description = "ET time zones stop at, as HHMM: 1200 is noon. Nothing opens on or after it, and any live episode is retired when the bar reaches it or the ET day changes. Set 0 to let them run until price closes through them.")]
        public int OptionZoneWindowEndEt { get; set; } = 1200;

        [NinjaScriptProperty]
        [Display(Name = "Examples kept per side", GroupName = "Option zones", Order = 8, Description = "How many finished episodes to leave on the chart for each bias side, drawn at half opacity and truncated where they died. 3 keeps three bullish-bias and three bearish-bias worked examples. Raise it to see every episode in a session rather than the most recent few. Set 0 to show only the live one.")]
        public int OptionZoneHistoryPerSide { get; set; } = 3;

        [NinjaScriptProperty]
        [Display(Name = "Labels", GroupName = "Option zones", Order = 9)]
        public bool ShowOptionZoneLabels { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Opacity", GroupName = "Option zones", Order = 10, Description = "Fill opacity of the three zones, 0 to 100.")]
        public int OptionZoneOpacity { get; set; } = 20;

        [XmlIgnore]
        [Display(Name = "Long color", GroupName = "Option zones", Order = 11)]
        public Brush OptionZoneLongColor { get; set; } = Brushes.MediumSeaGreen;

        [Browsable(false)]
        public string OptionZoneLongColorSerializable { get { return Serialize.BrushToString(OptionZoneLongColor); } set { OptionZoneLongColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Short color", GroupName = "Option zones", Order = 12)]
        public Brush OptionZoneShortColor { get; set; } = Brushes.IndianRed;

        [Browsable(false)]
        public string OptionZoneShortColorSerializable { get { return Serialize.BrushToString(OptionZoneShortColor); } set { OptionZoneShortColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "Pivot", GroupName = "LTF Signals", Order = 7)]
        public bool ShowLtfPivots { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Bar Count", GroupName = "LTF Signals", Order = 8)]
        public int LtfPivN { get; set; } = 1;

        [XmlIgnore]
        [Display(Name = "Resistance", GroupName = "LTF Signals", Order = 9)]
        public Brush LtfPivotResColor { get; set; } = Brushes.Red;

        [Browsable(false)]
        public string LtfPivotResColorSerializable { get { return Serialize.BrushToString(LtfPivotResColor); } set { LtfPivotResColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Support", GroupName = "LTF Signals", Order = 10)]
        public Brush LtfPivotSupColor { get; set; } = Brushes.Green;

        [Browsable(false)]
        public string LtfPivotSupColorSerializable { get { return Serialize.BrushToString(LtfPivotSupColor); } set { LtfPivotSupColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Width", GroupName = "LTF Signals", Order = 11)]
        public int LtfPivWidth { get; set; } = 1;

        [NinjaScriptProperty]
        [Display(Name = "Style", GroupName = "LTF Signals", Order = 12)]
        public MapleStaxRefLineStyle LtfPivStyle { get; set; } = MapleStaxRefLineStyle.Dashed;

        [NinjaScriptProperty]
        [Display(Name = "HTF EMA cloud", GroupName = "HTF Signals", Order = 1)]
        public bool ShowHtfEmaCloud { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Enable HTF", GroupName = "HTF Signals", Order = 2)]
        public bool HtfEnabled { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Timeframe", GroupName = "HTF Signals", Order = 3)]
        public string HtfTimeframe { get; set; } = "10";

        [NinjaScriptProperty]
        [Display(Name = "HTF LONG/SHORT labels", GroupName = "HTF Signals", Order = 5)]
        public bool ShowHtfCbcLabels { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Pivot", GroupName = "HTF Signals", Order = 6)]
        public bool ShowHtfPivots { get; set; } = true;

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Bar Count", GroupName = "HTF Signals", Order = 7)]
        public int HtfPivN { get; set; } = 1;

        [XmlIgnore]
        [Display(Name = "Resistance", GroupName = "HTF Signals", Order = 8)]
        public Brush HtfPivotResColor { get; set; } = Brushes.DarkRed;

        [Browsable(false)]
        public string HtfPivotResColorSerializable { get { return Serialize.BrushToString(HtfPivotResColor); } set { HtfPivotResColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Support", GroupName = "HTF Signals", Order = 9)]
        public Brush HtfPivotSupColor { get; set; } = Brushes.DarkGreen;

        [Browsable(false)]
        public string HtfPivotSupColorSerializable { get { return Serialize.BrushToString(HtfPivotSupColor); } set { HtfPivotSupColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Width", GroupName = "HTF Signals", Order = 10)]
        public int HtfPivWidth { get; set; } = 3;

        [NinjaScriptProperty]
        [Display(Name = "Style", GroupName = "HTF Signals", Order = 11)]
        public MapleStaxRefLineStyle HtfPivStyle { get; set; } = MapleStaxRefLineStyle.Dashed;

        [XmlIgnore]
        [Display(Name = "LONG label background", GroupName = "HTF Signals", Order = 12)]
        public Brush HtfLongLabelBg { get; set; } = Brushes.Green;

        [Browsable(false)]
        public string HtfLongLabelBgSerializable { get { return Serialize.BrushToString(HtfLongLabelBg); } set { HtfLongLabelBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "SHORT label background", GroupName = "HTF Signals", Order = 13)]
        public Brush HtfShortLabelBg { get; set; } = Brushes.Red;

        [Browsable(false)]
        public string HtfShortLabelBgSerializable { get { return Serialize.BrushToString(HtfShortLabelBg); } set { HtfShortLabelBg = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Label opacity (0-100)", GroupName = "HTF Signals", Order = 14)]
        public int HtfLabelOpacity { get; set; } = 50;

        [NinjaScriptProperty]
        [Display(Name = "CBC agreement bar colors", GroupName = "CBC bar colors", Order = 1)]
        public bool ShowBarColoring { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Fast period", GroupName = "EMA periods", Order = 1)]
        public int EmaFastPeriod { get; set; } = 9;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Slow period", GroupName = "EMA periods", Order = 2)]
        public int EmaSlowPeriod { get; set; } = 20;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA Trend period", GroupName = "EMA periods", Order = 3)]
        public int EmaTrendPeriod { get; set; } = 200;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "HTF EMA fast period", GroupName = "EMA periods", Order = 4)]
        public int HtfEmaFastPeriod { get; set; } = 9;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "HTF EMA slow period", GroupName = "EMA periods", Order = 5)]
        public int HtfEmaSlowPeriod { get; set; } = 20;

        [NinjaScriptProperty]
        [Display(Name = "New York", GroupName = "Opening Range (Sessions)", Order = 1)]
        public bool OrNy { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "London", GroupName = "Opening Range (Sessions)", Order = 2)]
        public bool OrLondon { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Asian", GroupName = "Opening Range (Sessions)", Order = 3)]
        public bool OrAsian { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, 390)]
        [Display(Name = "Minutes", GroupName = "Opening Range (Sessions)", Order = 4)]
        public int OrMinutes { get; set; } = 30;

        [NinjaScriptProperty]
        [Display(Name = "PDH", GroupName = "Reference levels", Order = 1)]
        public bool ShowPdh { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "PDH Color", GroupName = "Reference levels", Order = 2)]
        public Brush PdhColor { get; set; } = Brushes.Lime;

        [Browsable(false)]
        public string PdhColorSerializable { get { return Serialize.BrushToString(PdhColor); } set { PdhColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "PDH Width", GroupName = "Reference levels", Order = 3)]
        public int PdhWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "PDH Style", GroupName = "Reference levels", Order = 4)]
        public MapleStaxRefLineStyle PdhStyle { get; set; } = MapleStaxRefLineStyle.Solid;

        [NinjaScriptProperty]
        [Display(Name = "PDL", GroupName = "Reference levels", Order = 5)]
        public bool ShowPdl { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "PDL Color", GroupName = "Reference levels", Order = 6)]
        public Brush PdlColor { get; set; } = Brushes.Red;

        [Browsable(false)]
        public string PdlColorSerializable { get { return Serialize.BrushToString(PdlColor); } set { PdlColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "PDL Width", GroupName = "Reference levels", Order = 7)]
        public int PdlWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "PDL Style", GroupName = "Reference levels", Order = 8)]
        public MapleStaxRefLineStyle PdlStyle { get; set; } = MapleStaxRefLineStyle.Solid;

        [NinjaScriptProperty]
        [Display(Name = "PMH", GroupName = "Reference levels", Order = 9)]
        public bool ShowPmh { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "PMH Color", GroupName = "Reference levels", Order = 10)]
        public Brush PmhColor { get; set; } = new SolidColorBrush(Color.FromRgb(76, 175, 80));

        [Browsable(false)]
        public string PmhColorSerializable { get { return Serialize.BrushToString(PmhColor); } set { PmhColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "PMH Width", GroupName = "Reference levels", Order = 11)]
        public int PmhWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "PMH Style", GroupName = "Reference levels", Order = 12)]
        public MapleStaxRefLineStyle PmhStyle { get; set; } = MapleStaxRefLineStyle.Dotted;

        [NinjaScriptProperty]
        [Display(Name = "PML", GroupName = "Reference levels", Order = 13)]
        public bool ShowPml { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "PML Color", GroupName = "Reference levels", Order = 14)]
        public Brush PmlColor { get; set; } = new SolidColorBrush(Color.FromRgb(239, 83, 80));

        [Browsable(false)]
        public string PmlColorSerializable { get { return Serialize.BrushToString(PmlColor); } set { PmlColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "PML Width", GroupName = "Reference levels", Order = 15)]
        public int PmlWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "PML Style", GroupName = "Reference levels", Order = 16)]
        public MapleStaxRefLineStyle PmlStyle { get; set; } = MapleStaxRefLineStyle.Dotted;

        [NinjaScriptProperty]
        [Display(Name = "Show Level Name", GroupName = "Reference levels", Order = 17)]
        public bool ShowLevelName { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Level Price", GroupName = "Reference levels", Order = 18)]
        public bool ShowLevelPrice { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show Bill Breaker", GroupName = "Bill Breaker", Order = 1)]
        public bool ShowBillBreaker { get; set; } = false;

        [XmlIgnore]
        [Display(Name = "Level Color", GroupName = "Bill Breaker", Order = 2)]
        public Brush BillBreakerColor { get; set; } = new SolidColorBrush(Color.FromRgb(50, 50, 200));

        [Browsable(false)]
        public string BillBreakerColorSerializable { get { return Serialize.BrushToString(BillBreakerColor); } set { BillBreakerColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "multiplier", GroupName = "Bill Breaker", Order = 3)]
        public int BillBreakerMultiplier { get; set; } = 100;

        [NinjaScriptProperty]
        [Display(Name = "Show", GroupName = "Status table", Order = 1)]
        public bool ShowStatusTable { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Position", GroupName = "Status table", Order = 2)]
        public MapleStaxStatusTablePosition StatusTablePosition { get; set; } = MapleStaxStatusTablePosition.TopRight;

        [NinjaScriptProperty]
        [Display(Name = "Text", GroupName = "Status table", Order = 3)]
        public MapleStaxStatusTextSize StatusTableTextSize { get; set; } = MapleStaxStatusTextSize.Normal;

        [NinjaScriptProperty]
        [Range(0, 5)]
        [Display(Name = "Header ↓", GroupName = "Status table", Order = 4)]
        public int StatusTableHeaderNewlines { get; set; } = 0;

        [XmlIgnore]
        [Display(Name = "Bullish row bg", GroupName = "Status table", Order = 5)]
        public Brush StatusBullBg { get; set; } = new SolidColorBrush(Color.FromArgb(243, 4, 120, 87));

        [Browsable(false)]
        public string StatusBullBgSerializable { get { return Serialize.BrushToString(StatusBullBg); } set { StatusBullBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Bearish row bg", GroupName = "Status table", Order = 6)]
        public Brush StatusBearBg { get; set; } = new SolidColorBrush(Color.FromArgb(243, 155, 28, 28));

        [Browsable(false)]
        public string StatusBearBgSerializable { get { return Serialize.BrushToString(StatusBearBg); } set { StatusBearBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Neutral row bg", GroupName = "Status table", Order = 7)]
        public Brush StatusNeutralBg { get; set; } = new SolidColorBrush(Color.FromArgb(247, 55, 65, 81));

        [Browsable(false)]
        public string StatusNeutralBgSerializable { get { return Serialize.BrushToString(StatusNeutralBg); } set { StatusNeutralBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Inside-range row bg", GroupName = "Status table", Order = 8)]
        public Brush StatusInsideBg { get; set; } = new SolidColorBrush(Color.FromArgb(245, 87, 83, 78));

        [Browsable(false)]
        public string StatusInsideBgSerializable { get { return Serialize.BrushToString(StatusInsideBg); } set { StatusInsideBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Header bg", GroupName = "Status table", Order = 9)]
        public Brush StatusHeaderBg { get; set; } = new SolidColorBrush(Color.FromArgb(255, 12, 18, 34));

        [Browsable(false)]
        public string StatusHeaderBgSerializable { get { return Serialize.BrushToString(StatusHeaderBg); } set { StatusHeaderBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Label cell bg", GroupName = "Status table", Order = 10)]
        public Brush StatusLabelBg { get; set; } = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105));

        [Browsable(false)]
        public string StatusLabelBgSerializable { get { return Serialize.BrushToString(StatusLabelBg); } set { StatusLabelBg = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Header text color", GroupName = "Status table", Order = 11)]
        public Brush StatusHeaderText { get; set; } = new SolidColorBrush(Color.FromRgb(203, 213, 225));

        [Browsable(false)]
        public string StatusHeaderTextSerializable { get { return Serialize.BrushToString(StatusHeaderText); } set { StatusHeaderText = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Label text color", GroupName = "Status table", Order = 12)]
        public Brush StatusLabelText { get; set; } = new SolidColorBrush(Color.FromRgb(148, 163, 184));

        [Browsable(false)]
        public string StatusLabelTextSerializable { get { return Serialize.BrushToString(StatusLabelText); } set { StatusLabelText = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Display(Name = "LTF flip", GroupName = "CBC flip levels", Order = 1)]
        public bool ShowCbcFlipLtf { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "High", GroupName = "CBC flip levels", Order = 2)]
        public Brush CbcFlipLtfHColor { get; set; } = new SolidColorBrush(Color.FromRgb(21, 255, 0));

        [Browsable(false)]
        public string CbcFlipLtfHColorSerializable { get { return Serialize.BrushToString(CbcFlipLtfHColor); } set { CbcFlipLtfHColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Low", GroupName = "CBC flip levels", Order = 3)]
        public Brush CbcFlipLtfLColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 21, 0));

        [Browsable(false)]
        public string CbcFlipLtfLColorSerializable { get { return Serialize.BrushToString(CbcFlipLtfLColor); } set { CbcFlipLtfLColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Width", GroupName = "CBC flip levels", Order = 4)]
        public int CbcFlipLtfWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "HTF flip", GroupName = "CBC flip levels", Order = 5)]
        public bool ShowCbcFlipHtf { get; set; } = true;

        [XmlIgnore]
        [Display(Name = "High", GroupName = "CBC flip levels", Order = 6)]
        public Brush CbcFlipHtfHColor { get; set; } = new SolidColorBrush(Color.FromRgb(21, 255, 0));

        [Browsable(false)]
        public string CbcFlipHtfHColorSerializable { get { return Serialize.BrushToString(CbcFlipHtfHColor); } set { CbcFlipHtfHColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Low", GroupName = "CBC flip levels", Order = 7)]
        public Brush CbcFlipHtfLColor { get; set; } = new SolidColorBrush(Color.FromRgb(255, 21, 0));

        [Browsable(false)]
        public string CbcFlipHtfLColorSerializable { get { return Serialize.BrushToString(CbcFlipHtfLColor); } set { CbcFlipHtfLColor = Serialize.StringToBrush(value); } }

        [NinjaScriptProperty]
        [Range(1, 4)]
        [Display(Name = "Width", GroupName = "CBC flip levels", Order = 8)]
        public int CbcFlipHtfWidth { get; set; } = 4;

        private TimeZoneInfo etTimeZone;
        private TimeZoneInfo sourceTimeZone;
        private bool htfDataSeriesAdded;
        private BarsPeriod htfBarsPeriod;
        private int htfSeriesIndex = -1;

        private double lastHtfHigh1 = double.NaN;
        private double lastHtfLow1 = double.NaN;
        private bool previousHtfCbc = false;
        private bool pendingHtfBullLabel = false;
        private bool pendingHtfBearLabel = false;
        private double lastClosedHtfLow = double.NaN;
        private double lastClosedHtfHigh = double.NaN;
        private DateTime lastClosedHtfOpen = NinjaTrader.Core.Globals.MinDate;
        private DateTime lastClosedHtfClose = NinjaTrader.Core.Globals.MinDate;

        private double dailyPrevHigh = double.NaN;
        private double dailyPrevLow = double.NaN;
        private DateTime pdhOriginTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime pdlOriginTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime pdhLockTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime pdlLockTime = NinjaTrader.Core.Globals.MinDate;
        private double currentGlobexHigh = double.NaN;
        private double currentGlobexLow = double.NaN;
        private DateTime currentGlobexHighTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime currentGlobexLowTime = NinjaTrader.Core.Globals.MinDate;
        private bool isGlobexPrev;
        private double pendingDailyHigh = double.NaN;
        private double pendingDailyLow = double.NaN;
        private DateTime pendingDailyHighTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime pendingDailyLowTime = NinjaTrader.Core.Globals.MinDate;

        private double? orHighNy;
        private double? orLowNy;
        private double? orHighLondon;
        private double? orLowLondon;
        private double? orHighAsian;
        private double? orLowAsian;
        private bool nyOrActivePrev;
        private bool londonOrActivePrev;
        private bool asianOrActivePrev;
        private DateTime orHighNyTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime orLowNyTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime orHighLondonTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime orLowLondonTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime orHighAsianTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime orLowAsianTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime nyOrbLockTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime londonOrbLockTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime asianOrbLockTime = NinjaTrader.Core.Globals.MinDate;
        private double currentPmHigh = double.NaN;
        private double currentPmLow = double.NaN;
        private bool isPremarketPrev;
        private bool isRegularPrev;
        private bool isPremarket;
        private double pmh = double.NaN;
        private double pml = double.NaN;
        private DateTime pmhOriginTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime pmlOriginTime = NinjaTrader.Core.Globals.MinDate;

        // RTH close timestamp for the current trading day (chart time).
        private DateTime todayRthCloseChartTime = NinjaTrader.Core.Globals.MinDate;

        // ORB period end times (when the locked range starts being drawn).
        private DateTime nyOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime londonOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;
        private DateTime asianOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;

        private bool cbcState = false;
        private bool cbcStatePrev = false;

        // FOBO state
        private bool prevAnyFlip = false;
        private Brush colCbcFobo = new SolidColorBrush(Color.FromRgb(249, 203, 0));

        // HTF EMA equivalent-period multiplier (HTF_seconds / LTF_seconds).
        private int htfEma9Period = 9;
        private int htfEma20Period = 20;

        // HTF EMA bullish state
        private bool actualHtfEmaBullish = true;
        private double actualHtfEmaFastVal = double.NaN;
        private double actualHtfEmaSlowVal = double.NaN;


        // OR fill series + session brushes
        private Series<double> orHighNySeries;
        private Series<double> orLowNySeries;
        private Series<double> orHighLondonSeries;
        private Series<double> orLowLondonSeries;
        private Series<double> orHighAsianSeries;
        private Series<double> orLowAsianSeries;
        private Brush colOrbNy = new SolidColorBrush(Color.FromRgb(255, 235, 59));
        private Brush colOrbLondon = new SolidColorBrush(Color.FromRgb(156, 39, 176));
        private Brush colOrbAsian = new SolidColorBrush(Color.FromRgb(230, 81, 0));

        // LTF pivot state
        private double lastLtfPivotHigh = double.NaN;
        private int lastLtfPivotHighBar = -1;
        private double lastLtfPivotLow = double.NaN;
        private int lastLtfPivotLowBar = -1;

        // HTF pivot state
        private double lastHtfPivotHigh = double.NaN;
        private DateTime lastHtfPivotHighTime = NinjaTrader.Core.Globals.MinDate;
        private double lastHtfPivotLow = double.NaN;
        private DateTime lastHtfPivotLowTime = NinjaTrader.Core.Globals.MinDate;

        // Status table render state
        private struct StatusRow
        {
            public string Label;
            public string Value;
            public Brush LeftBg;
            public Brush RightBg;
            public Brush LeftFg;
            public Brush RightFg;
        }
        private string statusHeaderText = string.Empty;
        private StatusRow[] statusRowData = new StatusRow[0];

        private double bbBase = double.NaN;
        private bool bbBaseInitialized;

        private double vwapCumPv;
        private double vwapCumVol;

        private Brush colCbcLong = new SolidColorBrush(Color.FromRgb(21, 255, 0));
        private Brush colCbcShort = new SolidColorBrush(Color.FromRgb(255, 21, 0));
        private Brush colCbcBarLong = new SolidColorBrush(Color.FromRgb(0, 122, 138));
        private Brush colCbcBarShort = new SolidColorBrush(Color.FromRgb(179, 71, 0));
        private Brush colCbcBarNeutral = new SolidColorBrush(Color.FromRgb(90, 90, 90));

        // Suppress the parameter list NT8 appends to the chart's indicator label.
        public override string DisplayName { get { return Name; } }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "MapleStax CBC indicator suite";
                Name = "MapleStaxCBCAdvanced";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = true;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                ScaleJustification = ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                // Multi-bar Draw.Region calls (ORB fills span up to 500 bars, line ~1249)
                // invoke IsValidDataPoint across their range. The indicator default of
                // MaximumBarsLookBack.TwoHundredFiftySix throws "IsValidDataPoint cannot be
                // used with MaximumBarsLookBack.256" on every redraw once a region reaches
                // past 256 bars. Infinite keeps the full series available so the fill renders.
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

                AddPlot(new Stroke(Brushes.Green, 1), PlotStyle.Line, "EMA Fast");
                AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.Line, "EMA Slow");
                AddPlot(new Stroke(Brushes.Blue, 1), PlotStyle.Line, "EMA Trend");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.Line, "VWAP");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "HTFEMAFast");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "HTFEMASlow");

                FreezeIfNeeded(colCbcLong);
                FreezeIfNeeded(colCbcShort);
                FreezeIfNeeded(colCbcBarLong);
                FreezeIfNeeded(colCbcBarShort);
                FreezeIfNeeded(colCbcBarNeutral);
                FreezeIfNeeded(StatusHeaderBg);
                FreezeIfNeeded(StatusLabelBg);
                FreezeIfNeeded(StatusBullBg);
                FreezeIfNeeded(StatusBearBg);
                FreezeIfNeeded(StatusNeutralBg);
                FreezeIfNeeded(StatusInsideBg);
                FreezeIfNeeded(StatusLabelText);
                FreezeIfNeeded(StatusHeaderText);
                FreezeIfNeeded(BrsgColor);
                FreezeIfNeeded(OptionZoneLongColor);
                FreezeIfNeeded(OptionZoneShortColor);
                FreezeIfNeeded(BillBreakerColor);
                FreezeIfNeeded(PdhColor);
                FreezeIfNeeded(PdlColor);
                FreezeIfNeeded(PmhColor);
                FreezeIfNeeded(PmlColor);
                FreezeIfNeeded(LtfPivotResColor);
                FreezeIfNeeded(LtfPivotSupColor);
                FreezeIfNeeded(HtfPivotResColor);
                FreezeIfNeeded(HtfPivotSupColor);
                FreezeIfNeeded(HtfLongLabelBg);
                FreezeIfNeeded(HtfShortLabelBg);
                FreezeIfNeeded(CbcFlipLtfHColor);
                FreezeIfNeeded(CbcFlipLtfLColor);
                FreezeIfNeeded(CbcFlipHtfHColor);
                FreezeIfNeeded(CbcFlipHtfLColor);
                FreezeIfNeeded(colCbcFobo);
                FreezeIfNeeded(colOrbNy);
                FreezeIfNeeded(colOrbLondon);
                FreezeIfNeeded(colOrbAsian);
            }
            else if (State == State.Configure)
            {
                etTimeZone = GetEasternTimeZone();
                sourceTimeZone = GetSourceTimeZone();
                if (HtfEnabled && !string.IsNullOrWhiteSpace(HtfTimeframe))
                {
                    if (TryParseTimeframe(HtfTimeframe, out BarsPeriodType type, out int value))
                    {
                        AddDataSeries(type, value);
                        htfDataSeriesAdded = true;
                        htfBarsPeriod = new BarsPeriod { BarsPeriodType = type, Value = value };
                    }
                    else
                    {
                        AddDataSeries(BarsPeriodType.Minute, 10);
                        htfDataSeriesAdded = true;
                        htfBarsPeriod = new BarsPeriod { BarsPeriodType = BarsPeriodType.Minute, Value = 10 };
                    }
                    htfSeriesIndex = 1;
                }
            }
            else if (State == State.DataLoaded)
            {
                // These six feed Draw.Region calls that span up to 500 bars (see
                // regionStart in UpdateOpeningRange). A Series<double> you construct
                // does NOT inherit the indicator's MaximumBarsLookBack, so setting
                // that property alone is not enough: without the explicit overload
                // each series defaults to 256 bars and Draw.Region's IsValidDataPoint
                // throws on every redraw once the region reaches past that. This
                // mirrors NinjaTrader's own @ZigZag.cs, which passes Infinite to its
                // self-constructed series and never sets the indicator-level property.
                // Zone thickness. Built here rather than read on demand because a child
                // indicator has to be constructed in DataLoaded, and only when the zones are
                // switched on: this indicator runs twelve up on the MTF workspace and most of
                // those instances never draw a zone. Changing any property reloads the whole
                // indicator, so switching Show on later still gets an ATR.
                if (ShowOptionZones)
                    optionZoneAtrIndicator = ATR(Math.Max(1, OptionZoneAtrPeriod));

                orHighNySeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                orLowNySeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                orHighLondonSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                orLowLondonSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                orHighAsianSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);
                orLowAsianSeries = new Series<double>(this, MaximumBarsLookBack.Infinite);

                if (htfBarsPeriod != null)
                {
                    double ltfSec = Math.Max(1.0, BarsPeriodToTimeSpan(BarsPeriod).TotalSeconds);
                    double htfSec = BarsPeriodToTimeSpan(htfBarsPeriod).TotalSeconds;
                    double mult = htfSec / ltfSec;
                    if (mult < 1.0) mult = 1.0;
                    htfEma9Period = Math.Max(1, (int)Math.Round(HtfEmaFastPeriod * mult));
                    htfEma20Period = Math.Max(1, (int)Math.Round(HtfEmaSlowPeriod * mult));
                }
                BackfillPdhPdl();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < 1)
                return;

            if (BarsInProgress == 0)
            {
                UpdateLtfCbc();
                UpdateCbcShapes();
                UpdateEmaCloud();
                UpdateVwapEma200();
                UpdatePremarketLevels();
                UpdateReferenceLevels();
                UpdateOpeningRange();
                UpdateBillBreaker();
                UpdateFlipLevelsAndBrsg();
                // After UpdateLtfCbc and UpdateEmaCloud, so cbcState and Values[1][0] (the
                // 20 EMA) are both current for this bar.
                UpdateOptionZones();
                UpdateLtfPivots();
                DrawHtfPivotLines();
                UpdateStatusPanel();
                UpdateBarColoring();
                UpdateHtfLabelsOnLtf();
            }
            else if (BarsInProgress == htfSeriesIndex)
            {
                UpdateHtfSeries();
                UpdateHtfPivots();
            }
        }

        private void UpdateLtfCbc()
        {
            cbcStatePrev = cbcState;
            bool bearFlip = Close[0] < Low[1];
            bool bullFlip = Close[0] > High[1];
            if (cbcState && bearFlip)
                cbcState = false;
            else if (!cbcState && bullFlip)
                cbcState = true;
        }

        private void UpdateCbcShapes()
        {
            bool bullFlip = cbcState && !cbcStatePrev;
            bool bearFlip = !cbcState && cbcStatePrev;
            bool anyFlip = bullFlip || bearFlip;
            bool fobo = anyFlip && prevAnyFlip;

            bool bearPlain = bearFlip && (!fobo || !ShowFobo);
            bool bullPlain = bullFlip && (!fobo || !ShowFobo);
            bool bearFoboMk = ShowFobo && bearFlip && fobo;
            bool bullFoboMk = ShowFobo && bullFlip && fobo;

            string tagBase = "ADVCBCShape" + CurrentBar;
            if (bearPlain)
                Draw.TriangleDown(this, tagBase + "BearPlain", false, 0, High[0] + TickSize * 4, colCbcShort);
            if (bullPlain)
                Draw.TriangleUp(this, tagBase + "BullPlain", false, 0, Low[0] - TickSize * 4, colCbcLong);
            if (bearFoboMk)
                Draw.TriangleDown(this, tagBase + "BearFobo", false, 0, High[0] + TickSize * 4, colCbcFobo);
            if (bullFoboMk)
                Draw.TriangleUp(this, tagBase + "BullFobo", false, 0, Low[0] - TickSize * 4, colCbcFobo);

            prevAnyFlip = anyFlip;
        }

        private void UpdateLtfPivots()
        {
            if (!ShowLtfPivots) return;
            int n = LtfPivN;
            if (CurrentBar < 2 * n) return;

            double candidateHigh = High[n];
            double candidateLow = Low[n];
            bool isPivotHigh = true;
            bool isPivotLow = true;
            for (int off = 0; off <= 2 * n; off++)
            {
                if (off == n) continue;
                if (High[off] >= candidateHigh) isPivotHigh = false;
                if (Low[off] <= candidateLow) isPivotLow = false;
                if (!isPivotHigh && !isPivotLow) break;
            }

            if (isPivotHigh)
            {
                lastLtfPivotHigh = candidateHigh;
                lastLtfPivotHighBar = CurrentBar - n;
            }
            if (isPivotLow)
            {
                lastLtfPivotLow = candidateLow;
                lastLtfPivotLowBar = CurrentBar - n;
            }

            DrawLtfPivotLine("LTFPivHigh", lastLtfPivotHigh, lastLtfPivotHighBar, LtfPivotResColor);
            DrawLtfPivotLine("LTFPivLow", lastLtfPivotLow, lastLtfPivotLowBar, LtfPivotSupColor);
        }

        private void DrawLtfPivotLine(string tagBase, double price, int anchorBar, Brush color)
        {
            string tag = "ADV" + tagBase;
            if (double.IsNaN(price) || anchorBar < 0) return;
            int startAgo = CurrentBar - anchorBar;
            if (startAgo < 0) return;
            Draw.Line(this, tag, false, startAgo, price, -1, price, color, GetDashStyle(LtfPivStyle), LtfPivWidth);
        }

        private void DrawHtfPivotLines()
        {
            if (!ShowHtfPivots || !HtfEnabled) return;
            DateTime rightEdge = Time[0].Add(BarsPeriodToTimeSpan(BarsPeriod));
            if (!double.IsNaN(lastHtfPivotHigh) && lastHtfPivotHighTime != NinjaTrader.Core.Globals.MinDate)
                Draw.Line(this, "ADVHTFPivHigh", false, lastHtfPivotHighTime, lastHtfPivotHigh, rightEdge, lastHtfPivotHigh, HtfPivotResColor, GetDashStyle(HtfPivStyle), HtfPivWidth);
            if (!double.IsNaN(lastHtfPivotLow) && lastHtfPivotLowTime != NinjaTrader.Core.Globals.MinDate)
                Draw.Line(this, "ADVHTFPivLow", false, lastHtfPivotLowTime, lastHtfPivotLow, rightEdge, lastHtfPivotLow, HtfPivotSupColor, GetDashStyle(HtfPivStyle), HtfPivWidth);
        }

        private void UpdateEmaCloud()
        {
            double ema9 = EMA(EmaFastPeriod)[0];
            double ema20 = EMA(EmaSlowPeriod)[0];
            Values[0][0] = ema9;
            Values[1][0] = ema20;

            // HTF EMA on LTF-equivalent period — e.g., 10m HTF EMA(9) ≈ 1m EMA(90).
            // Smooth continuous line, not a step-function from HTF bar closes.
            double htfEma9Val = EMA(htfEma9Period)[0];
            double htfEma20Val = EMA(htfEma20Period)[0];
            Values[4][0] = htfEma9Val;
            Values[5][0] = htfEma20Val;

            // Per-bar segment fill: each bar emits its own Draw.Region (unique tag,
            // 2-bar-wide between previous and current EMA values) colored by THIS
            // bar's regime. Past regions stay drawn with their original colors so
            // historical regime changes are preserved on the chart.
            if (CurrentBar >= 1)
            {
                bool bullish = ema9 >= ema20;
                Brush ltfBrush = bullish ? Brushes.Green : Brushes.Red;
                Draw.Region(this, "ADVEMACloud_" + CurrentBar, 1, 0, Values[0], Values[1], null, ltfBrush, 30);

                if (ShowHtfEmaCloud)
                {
                    Brush htfBrush = actualHtfEmaBullish ? colCbcBarLong : colCbcBarShort;
                    Draw.Region(this, "ADVHTFEMACloud_" + CurrentBar, 1, 0, Values[4], Values[5], null, htfBrush, 25);
                }
            }

            if (ShowEmaTouchDots && Low[0] <= ema20 && High[0] >= ema20)
                Draw.Dot(this, "ADVEMA20Touch" + CurrentBar, false, 0, ema20, Brushes.White);
        }

        private void UpdateVwapEma200()
        {
            // Session-anchored cumulative VWAP matching VWAPx.cs: typical price = HLC/3,
            // volume = VOL()[0] (NT8 Volume indicator), reset on Bars.IsFirstBarOfSession.
            double hl3 = (High[0] + Low[0] + Close[0]) / 3.0;
            double vol = VOL()[0];
            if (Bars.IsFirstBarOfSession)
            {
                vwapCumPv = vol * hl3;
                vwapCumVol = vol;
            }
            else
            {
                vwapCumPv += vol * hl3;
                vwapCumVol += vol;
            }
            double vwapValue = vwapCumVol > 0 ? vwapCumPv / vwapCumVol : Close[0];

            double ema200 = EMA(EmaTrendPeriod)[0];
            Values[2][0] = ema200;
            Values[3][0] = ShowVwap ? vwapValue : double.NaN;
        }

        private DateTime LineEndT(DateTime lockTime)
        {
            return lockTime != NinjaTrader.Core.Globals.MinDate ? lockTime : Time[0];
        }

        private void UpdateReferenceLevels()
        {
            double pdhDisplay = dailyPrevHigh;
            double pdlDisplay = dailyPrevLow;
            double pmhDisplay = isPremarket ? currentPmHigh : pmh;
            double pmlDisplay = isPremarket ? currentPmLow : pml;

            string nowKey = ConvertToEastern(Time[0]).ToString("yyyyMMdd");

            DateTime pdhEnd = LineEndT(pdhLockTime);
            DateTime pdlEnd = LineEndT(pdlLockTime);
            if (ShowPdh && !double.IsNaN(pdhDisplay) && pdhOriginTime != NinjaTrader.Core.Globals.MinDate && pdhOriginTime <= Time[0])
            {
                string tag = "ADVPDH_" + ConvertToEastern(pdhOriginTime).ToString("yyyyMMdd");
                Draw.Line(this, tag, false, pdhOriginTime, pdhDisplay, pdhEnd, pdhDisplay, PdhColor, GetDashStyle(PdhStyle), PdhWidth);
            }
            if (ShowPdl && !double.IsNaN(pdlDisplay) && pdlOriginTime != NinjaTrader.Core.Globals.MinDate && pdlOriginTime <= Time[0])
            {
                string tag = "ADVPDL_" + ConvertToEastern(pdlOriginTime).ToString("yyyyMMdd");
                Draw.Line(this, tag, false, pdlOriginTime, pdlDisplay, pdlEnd, pdlDisplay, PdlColor, GetDashStyle(PdlStyle), PdlWidth);
            }

            DateTime pmEndT = todayRthCloseChartTime != NinjaTrader.Core.Globals.MinDate
                ? todayRthCloseChartTime
                : Time[0];
            if (ShowPmh && !double.IsNaN(pmhDisplay) && pmhOriginTime != NinjaTrader.Core.Globals.MinDate && pmhOriginTime <= Time[0])
            {
                DateTime endT = pmEndT < pmhOriginTime ? pmhOriginTime : pmEndT;
                string tag = "ADVPMH_" + ConvertToEastern(pmhOriginTime).ToString("yyyyMMdd");
                Draw.Line(this, tag, false, pmhOriginTime, pmhDisplay, endT, pmhDisplay, PmhColor, GetDashStyle(PmhStyle), PmhWidth);
            }
            if (ShowPml && !double.IsNaN(pmlDisplay) && pmlOriginTime != NinjaTrader.Core.Globals.MinDate && pmlOriginTime <= Time[0])
            {
                DateTime endT = pmEndT < pmlOriginTime ? pmlOriginTime : pmEndT;
                string tag = "ADVPML_" + ConvertToEastern(pmlOriginTime).ToString("yyyyMMdd");
                Draw.Line(this, tag, false, pmlOriginTime, pmlDisplay, endT, pmlDisplay, PmlColor, GetDashStyle(PmlStyle), PmlWidth);
            }

            UpdateReferenceLabels(nowKey, pdhEnd, pdlEnd, pmEndT, pdhDisplay, pdlDisplay, pmhDisplay, pmlDisplay);
        }

        private void UpdatePremarketLevels()
        {
            DateTime et = ConvertToEastern(Time[0]);
            bool premarket = IsPremarket(et);
            bool regular = IsRegular(et);
            bool globex = IsInGlobex(et);
            bool newDay = regular && !isRegularPrev;
            bool newGlobex = globex && !isGlobexPrev;
            bool globexEnded = !globex && isGlobexPrev;

            if (premarket && !isPremarketPrev)
            {
                currentPmHigh = High[0];
                currentPmLow = Low[0];
                pmhOriginTime = Time[0];
                pmlOriginTime = Time[0];
            }

            if (premarket)
            {
                if (double.IsNaN(currentPmHigh) || High[0] > currentPmHigh)
                {
                    currentPmHigh = High[0];
                    pmhOriginTime = Time[0];
                }
                if (double.IsNaN(currentPmLow) || Low[0] < currentPmLow)
                {
                    currentPmLow = Low[0];
                    pmlOriginTime = Time[0];
                }
            }

            if (newDay)
            {
                pmh = currentPmHigh;
                pml = currentPmLow;
                currentPmHigh = double.NaN;
                currentPmLow = double.NaN;
                todayRthCloseChartTime = NinjaTrader.Core.Globals.MinDate;
                CleanupOldDrawings(et);
            }

            if (!regular && isRegularPrev)
            {
                todayRthCloseChartTime = Time[0];
                if (orHighNyTime != NinjaTrader.Core.Globals.MinDate && nyOrbLockTime == NinjaTrader.Core.Globals.MinDate)
                    nyOrbLockTime = Time[0];
                if (orHighLondonTime != NinjaTrader.Core.Globals.MinDate && londonOrbLockTime == NinjaTrader.Core.Globals.MinDate)
                    londonOrbLockTime = Time[0];
                if (orHighAsianTime != NinjaTrader.Core.Globals.MinDate && asianOrbLockTime == NinjaTrader.Core.Globals.MinDate)
                    asianOrbLockTime = Time[0];
            }

            // Globex 23-hour session for PDH/PDL (17:00 ET prev -> 16:00 ET curr).
            if (newGlobex)
            {
                currentGlobexHigh = High[0];
                currentGlobexLow = Low[0];
                currentGlobexHighTime = Time[0];
                currentGlobexLowTime = Time[0];
                if (!double.IsNaN(pendingDailyHigh))
                {
                    dailyPrevHigh = pendingDailyHigh;
                    pdhOriginTime = pendingDailyHighTime;
                    pdhLockTime = NinjaTrader.Core.Globals.MinDate;
                }
                if (!double.IsNaN(pendingDailyLow))
                {
                    dailyPrevLow = pendingDailyLow;
                    pdlOriginTime = pendingDailyLowTime;
                    pdlLockTime = NinjaTrader.Core.Globals.MinDate;
                }
            }
            if (globex)
            {
                if (double.IsNaN(currentGlobexHigh) || High[0] > currentGlobexHigh)
                {
                    currentGlobexHigh = High[0];
                    currentGlobexHighTime = Time[0];
                }
                if (double.IsNaN(currentGlobexLow) || Low[0] < currentGlobexLow)
                {
                    currentGlobexLow = Low[0];
                    currentGlobexLowTime = Time[0];
                }
            }
            if (globexEnded)
            {
                if (!double.IsNaN(currentGlobexHigh))
                {
                    pendingDailyHigh = currentGlobexHigh;
                    pendingDailyHighTime = currentGlobexHighTime;
                }
                if (!double.IsNaN(currentGlobexLow))
                {
                    pendingDailyLow = currentGlobexLow;
                    pendingDailyLowTime = currentGlobexLowTime;
                }
                if (pdhOriginTime != NinjaTrader.Core.Globals.MinDate && pdhLockTime == NinjaTrader.Core.Globals.MinDate)
                    pdhLockTime = Time[0];
                if (pdlOriginTime != NinjaTrader.Core.Globals.MinDate && pdlLockTime == NinjaTrader.Core.Globals.MinDate)
                    pdlLockTime = Time[0];
            }

            isPremarketPrev = premarket;
            isRegularPrev = regular;
            isGlobexPrev = globex;
            isPremarket = premarket;
        }

        private void UpdateReferenceLabels(string dateKey, DateTime pdhEnd, DateTime pdlEnd, DateTime pmEndT, double pdhDisplay, double pdlDisplay, double pmhDisplay, double pmlDisplay)
        {
            if (!ShowLevelName && !ShowLevelPrice)
                return;

            if (ShowPdh && !double.IsNaN(pdhDisplay) && pdhOriginTime != NinjaTrader.Core.Globals.MinDate && pdhOriginTime <= Time[0])
            {
                string tag = "ADVPDHLabel_" + ConvertToEastern(pdhOriginTime).ToString("yyyyMMdd");
                DrawLevelLabel(tag, GetLevelLabelText("PDH", pdhDisplay), pdhEnd, pdhDisplay, PdhColor);
            }
            if (ShowPdl && !double.IsNaN(pdlDisplay) && pdlOriginTime != NinjaTrader.Core.Globals.MinDate && pdlOriginTime <= Time[0])
            {
                string tag = "ADVPDLLabel_" + ConvertToEastern(pdlOriginTime).ToString("yyyyMMdd");
                DrawLevelLabel(tag, GetLevelLabelText("PDL", pdlDisplay), pdlEnd, pdlDisplay, PdlColor);
            }
            if (ShowPmh && !double.IsNaN(pmhDisplay) && pmhOriginTime != NinjaTrader.Core.Globals.MinDate && pmhOriginTime <= Time[0])
            {
                string tag = "ADVPMHLabel_" + ConvertToEastern(pmhOriginTime).ToString("yyyyMMdd");
                DrawLevelLabel(tag, GetLevelLabelText("PMH", pmhDisplay), pmEndT, pmhDisplay, PmhColor);
            }
            if (ShowPml && !double.IsNaN(pmlDisplay) && pmlOriginTime != NinjaTrader.Core.Globals.MinDate && pmlOriginTime <= Time[0])
            {
                string tag = "ADVPMLLabel_" + ConvertToEastern(pmlOriginTime).ToString("yyyyMMdd");
                DrawLevelLabel(tag, GetLevelLabelText("PML", pmlDisplay), pmEndT, pmlDisplay, PmlColor);
            }
        }

        // Draws a level label just to the right of the line's terminus, offset upward
        // so the text sits above and beyond the end of the line rather than over it.
        private void DrawLevelLabel(string tag, string text, DateTime t, double y, Brush color)
        {
            int barIdx = Bars.GetBar(t);
            int barsAgo = barIdx < 0 ? 0 : CurrentBar - barIdx;
            // Shift two bars further right so the label clears the line's endpoint.
            barsAgo -= 2;
            SimpleFont font = new SimpleFont("Arial", 10) { Bold = true };
            Draw.Text(this, tag, false, text, barsAgo, y, -0, color, font, TextAlignment.Left, Brushes.Transparent, Brushes.Transparent, 0);
        }

        private string GetLevelLabelText(string levelName, double levelPrice)
        {
            if (ShowLevelName && ShowLevelPrice)
                return levelName + " " + levelPrice.ToString("#.##");
            if (ShowLevelName)
                return levelName;
            return levelPrice.ToString("#.##");
        }

        private void BackfillPdhPdl()
        {
            if (Bars == null || Bars.Count < 1) return;

            DateTime latestEt = ConvertToEastern(Bars.GetTime(Bars.Count - 1));

            // Determine the start of the current Globex session (latest 17:00 ET <= latestEt).
            DateTime today17 = new DateTime(latestEt.Year, latestEt.Month, latestEt.Day, 17, 0, 0);
            DateTime currentSessionStart = (latestEt >= today17) ? today17 : today17.AddDays(-1);

            // Previous Globex session: starts 1 day before currentSessionStart, ends at the 16:00 ET
            // immediately preceding currentSessionStart.
            DateTime prevSessionStart = currentSessionStart.AddDays(-1);
            DateTime prevSessionEnd = currentSessionStart.AddHours(-1);

            // Walk back through up to 5 candidate windows to skip weekend / holiday gaps.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                double sessHigh = double.NegativeInfinity;
                double sessLow = double.PositiveInfinity;
                DateTime sessHighTime = NinjaTrader.Core.Globals.MinDate;
                DateTime sessLowTime = NinjaTrader.Core.Globals.MinDate;
                bool sawAnyBar = false;

                for (int i = Bars.Count - 1; i >= 0; i--)
                {
                    DateTime barEt = ConvertToEastern(Bars.GetTime(i));
                    if (barEt >= prevSessionEnd) continue;
                    if (barEt < prevSessionStart) break;
                    sawAnyBar = true;
                    double h = Bars.GetHigh(i);
                    double l = Bars.GetLow(i);
                    if (h > sessHigh) { sessHigh = h; sessHighTime = Bars.GetTime(i); }
                    if (l < sessLow)  { sessLow  = l; sessLowTime  = Bars.GetTime(i); }
                }

                if (sawAnyBar)
                {
                    dailyPrevHigh = sessHigh;
                    pdhOriginTime = sessHighTime;
                    pdhLockTime = NinjaTrader.Core.Globals.MinDate;
                    dailyPrevLow = sessLow;
                    pdlOriginTime = sessLowTime;
                    pdlLockTime = NinjaTrader.Core.Globals.MinDate;
                    return;
                }

                prevSessionStart = prevSessionStart.AddDays(-1);
                prevSessionEnd = prevSessionEnd.AddDays(-1);
            }
        }

        // Removes drawings (PDH/PDL/PMH/PML/ORB lines and labels) tagged with a date older
        // than today - 2 days. Called once per day at RTH start.
        private void CleanupOldDrawings(DateTime nowEt)
        {
            DateTime cutoffDate = nowEt.Date.AddDays(-2);
            string cutoffKey = cutoffDate.ToString("yyyyMMdd");
            string[] prefixes = new string[] {
                "PDH_", "PDL_", "PMH_", "PML_",
                "PDHLabel_", "PDLLabel_", "PMHLabel_", "PMLLabel_",
                "ORNYH_", "ORNYL_", "ORLNH_", "ORLNL_", "ORASH_", "ORASL_"
            };
            var toRemove = new System.Collections.Generic.List<string>();
            // Snapshot NT's live draw-object collection before enumerating: with 12
            // instances hitting the new-day boundary near the same bar close, a sibling
            // instance or the render thread can mutate DrawObjects mid-enumeration and
            // throw "Collection was modified; enumeration operation may not execute".
            foreach (var obj in DrawObjects.ToList())
            {
                if (obj == null || string.IsNullOrEmpty(obj.Tag)) continue;
                foreach (var prefix in prefixes)
                {
                    if (!obj.Tag.StartsWith(prefix)) continue;
                    string rest = obj.Tag.Substring(prefix.Length);
                    if (rest.Length < 8) break;
                    string datePart = rest.Substring(0, 8);
                    if (string.Compare(datePart, cutoffKey, System.StringComparison.Ordinal) < 0)
                        toRemove.Add(obj.Tag);
                    break;
                }
            }
            foreach (var tag in toRemove)
                RemoveDrawObject(tag);
        }

        private void UpdateOpeningRange()
        {
            DateTime et = ConvertToEastern(Time[0]);
            bool orNyActive = OrNy && IsSessionActive(et, 9, 30, OrMinutes);
            bool orLondonActive = OrLondon && IsSessionActive(et, 3, 0, OrMinutes);
            bool orAsianActive = OrAsian && IsAsianSessionActive(et, OrMinutes);

            if (OrNy)
            {
                if (orNyActive && !nyOrActivePrev)
                {
                    orHighNy = High[0];
                    orLowNy = Low[0];
                    orHighNyTime = Time[0];
                    orLowNyTime = Time[0];
                    nyOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;
                    nyOrbLockTime = NinjaTrader.Core.Globals.MinDate;
                }
                if (orNyActive && orHighNy.HasValue)
                {
                    if (High[0] > orHighNy.Value) { orHighNy = High[0]; orHighNyTime = Time[0]; }
                    if (Low[0] < orLowNy.Value)   { orLowNy  = Low[0];  orLowNyTime  = Time[0]; }
                }
                if (!orNyActive && nyOrActivePrev && orHighNy.HasValue)
                    nyOrbEndChartTime = Time[0];
            }
            if (OrLondon)
            {
                if (orLondonActive && !londonOrActivePrev)
                {
                    orHighLondon = High[0];
                    orLowLondon = Low[0];
                    orHighLondonTime = Time[0];
                    orLowLondonTime = Time[0];
                    londonOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;
                    londonOrbLockTime = NinjaTrader.Core.Globals.MinDate;
                }
                if (orLondonActive && orHighLondon.HasValue)
                {
                    if (High[0] > orHighLondon.Value) { orHighLondon = High[0]; orHighLondonTime = Time[0]; }
                    if (Low[0] < orLowLondon.Value)   { orLowLondon  = Low[0];  orLowLondonTime  = Time[0]; }
                }
                if (!orLondonActive && londonOrActivePrev && orHighLondon.HasValue)
                    londonOrbEndChartTime = Time[0];
            }
            if (OrAsian)
            {
                if (orAsianActive && !asianOrActivePrev)
                {
                    orHighAsian = High[0];
                    orLowAsian = Low[0];
                    orHighAsianTime = Time[0];
                    orLowAsianTime = Time[0];
                    asianOrbEndChartTime = NinjaTrader.Core.Globals.MinDate;
                    asianOrbLockTime = NinjaTrader.Core.Globals.MinDate;
                }
                if (orAsianActive && orHighAsian.HasValue)
                {
                    if (High[0] > orHighAsian.Value) { orHighAsian = High[0]; orHighAsianTime = Time[0]; }
                    if (Low[0] < orLowAsian.Value)   { orLowAsian  = Low[0];  orLowAsianTime  = Time[0]; }
                }
                if (!orAsianActive && asianOrActivePrev && orHighAsian.HasValue)
                    asianOrbEndChartTime = Time[0];
            }

            nyOrActivePrev = orNyActive;
            londonOrActivePrev = orLondonActive;
            asianOrActivePrev = orAsianActive;

            // Draw locked ORB lines from ORB-period close through RTH close (or the
            // current bar while RTH is still open). Per-day tags so historical days
            // keep their own bounded lines.
            DrawOrbLine("ORNYH", "NY ORB-H", OrNy, orHighNy, orHighNyTime, nyOrbLockTime, colOrbNy);
            DrawOrbLine("ORNYL", "NY ORB-L", OrNy, orLowNy, orLowNyTime, nyOrbLockTime, colOrbNy);
            DrawOrbLine("ORLNH", "LN ORB-H", OrLondon, orHighLondon, orHighLondonTime, londonOrbLockTime, colOrbLondon);
            DrawOrbLine("ORLNL", "LN ORB-L", OrLondon, orLowLondon, orLowLondonTime, londonOrbLockTime, colOrbLondon);
            DrawOrbLine("ORASH", "AS ORB-H", OrAsian, orHighAsian, orHighAsianTime, asianOrbLockTime, colOrbAsian);
            DrawOrbLine("ORASL", "AS ORB-L", OrAsian, orLowAsian, orLowAsianTime, asianOrbLockTime, colOrbAsian);

            // Region fills only render where both series are non-NaN, so clamp them
            // to the same window as the lines (post-ORB and within RTH).
            bool nyVisible = OrNy && orHighNy.HasValue && orLowNy.HasValue && IsOrbLineVisible(nyOrbEndChartTime);
            bool londonVisible = OrLondon && orHighLondon.HasValue && orLowLondon.HasValue && IsOrbLineVisible(londonOrbEndChartTime);
            bool asianVisible = OrAsian && orHighAsian.HasValue && orLowAsian.HasValue && IsOrbLineVisible(asianOrbEndChartTime);

            orHighNySeries[0] = nyVisible ? orHighNy.Value : double.NaN;
            orLowNySeries[0] = nyVisible ? orLowNy.Value : double.NaN;
            orHighLondonSeries[0] = londonVisible ? orHighLondon.Value : double.NaN;
            orLowLondonSeries[0] = londonVisible ? orLowLondon.Value : double.NaN;
            orHighAsianSeries[0] = asianVisible ? orHighAsian.Value : double.NaN;
            orLowAsianSeries[0] = asianVisible ? orLowAsian.Value : double.NaN;

            int regionStart = Math.Min(CurrentBar, 500);
            if (nyVisible)
                Draw.Region(this, "ADVORNYFill", regionStart, 0, orHighNySeries, orLowNySeries, null, colOrbNy, 15);
            if (londonVisible)
                Draw.Region(this, "ADVORLondonFill", regionStart, 0, orHighLondonSeries, orLowLondonSeries, null, colOrbLondon, 15);
            if (asianVisible)
                Draw.Region(this, "ADVORAsianFill", regionStart, 0, orHighAsianSeries, orLowAsianSeries, null, colOrbAsian, 15);
        }

        private bool IsOrbLineVisible(DateTime orbEnd)
        {
            if (orbEnd == NinjaTrader.Core.Globals.MinDate) return false;
            if (Time[0] < orbEnd) return false;
            if (todayRthCloseChartTime != NinjaTrader.Core.Globals.MinDate && Time[0] > todayRthCloseChartTime) return false;
            return true;
        }

        private void DrawOrbLine(string tagBase, string labelName, bool enabled, double? price, DateTime originTime, DateTime lockTime, Brush color)
        {
            if (!enabled || !price.HasValue) return;
            if (originTime == NinjaTrader.Core.Globals.MinDate) return;

            DateTime endT = LineEndT(lockTime);
            if (endT < originTime) endT = originTime;

            string dateKey = ConvertToEastern(originTime).ToString("yyyyMMdd");
            string tag = "ADV" + tagBase + "_" + dateKey;
            Draw.Line(this, tag, false, originTime, price.Value, endT, price.Value, color, DashStyleHelper.Dot, 1);

            if (ShowLevelName || ShowLevelPrice)
                DrawLevelLabel(tag + "_lbl", GetLevelLabelText(labelName, price.Value), endT, price.Value, color);
        }

        private void UpdateBillBreaker()
        {
            if (!ShowBillBreaker)
                return;

            if (!bbBaseInitialized)
            {
                bbBase = Math.Round(Open[0] / BillBreakerMultiplier) * BillBreakerMultiplier;
                bbBaseInitialized = true;
            }
            else
            {
                double prevBase = bbBase;
                if (Close[0] >= prevBase + 2 * BillBreakerMultiplier)
                    bbBase = prevBase + BillBreakerMultiplier;
                else if (Close[0] <= prevBase - 2 * BillBreakerMultiplier)
                    bbBase = prevBase - BillBreakerMultiplier;
            }

            if (!double.IsNaN(bbBase))
            {
                Draw.HorizontalLine(this, "ADVBBL2", bbBase - 2 * BillBreakerMultiplier, BillBreakerColor, DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "ADVBBL1", bbBase - BillBreakerMultiplier, BillBreakerColor, DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "ADVBBM0", bbBase, BillBreakerColor, DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "ADVBBU1", bbBase + BillBreakerMultiplier, BillBreakerColor, DashStyleHelper.Dot, 1);
                Draw.HorizontalLine(this, "ADVBBU2", bbBase + 2 * BillBreakerMultiplier, BillBreakerColor, DashStyleHelper.Dot, 1);
            }
        }

        private void UpdateFlipLevelsAndBrsg()
        {
            if (CurrentBar < 1)
                return;

            if (ShowCbcFlipLtf)
            {
                double price = cbcStatePrev ? Low[1] : High[1];
                Brush color = cbcStatePrev ? CbcFlipLtfLColor : CbcFlipLtfHColor;
                Draw.Line(this, "ADVCBCFlipLtf", false, 1, price, 0, price, color, DashStyleHelper.Solid, CbcFlipLtfWidth);
            }

            if (HtfEnabled && ShowCbcFlipHtf && !double.IsNaN(lastHtfLow1) && !double.IsNaN(lastHtfHigh1))
            {
                double htfPrice = previousHtfCbc ? lastHtfLow1 : lastHtfHigh1;
                Brush color = previousHtfCbc ? CbcFlipHtfLColor : CbcFlipHtfHColor;
                Draw.Line(this, "ADVCBCFlipHtf", false, 1, htfPrice, 0, htfPrice, color, DashStyleHelper.Solid, CbcFlipHtfWidth);
            }

            if (ShowBrsgZone && CurrentBar > 0)
            {
                double rng = High[1] - Low[1];
                double top = Low[1] + rng * BrsgHighMult;
                double bot = Low[1] + rng * BrsgLowMult;
                Draw.Line(this, "ADVBRSGTop", false, 1, top, 0, top, BrsgColor, DashStyleHelper.Solid, CbcFlipLtfWidth);
                Draw.Line(this, "ADVBRSGBot", false, 1, bot, 0, bot, BrsgColor, DashStyleHelper.Solid, CbcFlipLtfWidth);
            }
        }

        // <OptionZoneGeometry>
        // Pure geometry for Maple's three trading options. No NinjaTrader type crosses this
        // boundary, which is what lets tools/run-zone-tests.ps1 extract this exact region and
        // compile it standalone against tools/OptionZoneTests.cs. Keep it that way.
        internal static class OptionZoneGeometry
        {
            internal struct Zones
            {
                public double Lo1, Hi1, Lo2, Hi2, Lo3, Hi3;
                public bool Long1, Long2, Long3;
            }

            // Returns false when there is nothing to draw: the two reads agree (that is one
            // trade, not three), the prior bar has no range, or the EMA has not formed.
            //
            // All three zones share ONE thickness, driven by ATR. Two of them never had a
            // thickness of their own - the 20 EMA is a line and the flip level is a price, so
            // both used to borrow zone 1's span for want of anything better - and a span
            // measured off a single prior bar collapses to nothing on a quiet bar, which is
            // how the whole display ended up unreadable. ATR is the right basis and the EMA
            // separation is not: these zones exist only while the two reads DISAGREE, and
            // when the bias is the EMA cloud that disagreement clusters around the EMAs
            // converging, so an EMA-distance thickness would go to zero exactly when the
            // zones are needed.
            // The setup, stated the way it is traded: the higher-timeframe EMAs are stacked
            // one way and sitting on the far side of price, and the LTF CBC flips INTO them.
            // Bearish stack (HTF 20 over HTF 9) with the cloud overhead and price beneath it,
            // CBC flipping long, gives: (1) take the CBC long on BRSG, against the HTF;
            // (2) short the HTF 20 as price reaches it, with the HTF but unconfirmed, so
            // smaller; (3) wait for the CBC to flip back short and take SGCR with both
            // timeframes aligned. Bullish stack is the exact mirror.
            //
            // Both EMAs are the HIGHER timeframe's. Anchoring zone 2 on the chart's own 20
            // was wrong: it sits on top of price, so "short the 20" became a fade at a level
            // price was already trading through, rather than the overhead EMA the rally is
            // running into.
            internal static bool Compute(bool biasBull, bool ltfBull, double prevHigh, double prevLow,
                                         double htfEmaFast, double htfEmaSlow, double close,
                                         bool requireCloudSide,
                                         double lowMult, double highMult,
                                         double atr, double atrMult, out Zones z)
            {
                z = default(Zones);

                if (ltfBull == biasBull)
                    return false;

                double rng = prevHigh - prevLow;
                if (!(rng > 0) || double.IsNaN(htfEmaSlow))
                    return false;

                // "Into the EMAs" is half the setup, so price has to still be on the near
                // side of the whole cloud. Fails closed on a missing input: a gate that opens
                // when it cannot see is not a gate.
                if (requireCloudSide)
                {
                    if (double.IsNaN(htfEmaFast) || double.IsNaN(close))
                        return false;

                    double cloudLo = System.Math.Min(htfEmaFast, htfEmaSlow);
                    double cloudHi = System.Math.Max(htfEmaFast, htfEmaSlow);

                    if (!biasBull && !(close < cloudLo)) return false;
                    if (biasBull && !(close > cloudHi)) return false;
                }

                // Fall back to the original prior-bar-range thickness while ATR warms up, so
                // the opening bars of a chart still draw something honest.
                double fallback = rng * System.Math.Abs(highMult - lowMult);
                double t = (!double.IsNaN(atr) && atr > 0 && atrMult > 0) ? atr * atrMult : fallback;

                // Cap. Zone 1 is centred on the entry span and zone 3 starts at the flip
                // level, so a thickness above rng*(low+high) would push zone 1 across that
                // level - it would advertise entries at prices where the CBC flips back, and
                // it would bury the trigger under zone 1, which is the exact collision zone 3
                // was designed around.
                double cap = rng * (lowMult + highMult);
                if (cap > 0 && t > cap) t = cap;
                if (!(t > 0)) t = fallback;
                if (!(t > 0)) return false;

                double half = t * 0.5;

                // Zone 1: take the CBC trade now, against the bias. Centred on the BRSG/SGCR
                // entry span - BRSG measures up from the prior bar's low, SGCR down from its
                // high, so the two stay correct mirrors under asymmetric multipliers. The
                // exact entry edges are not lost by widening this: the BRSG zone feature
                // already draws them precisely on the same chart.
                double mid1 = !biasBull
                    ? prevLow + rng * (lowMult + highMult) * 0.5
                    : prevHigh - rng * (lowMult + highMult) * 0.5;
                z.Lo1 = mid1 - half;
                z.Hi1 = mid1 + half;
                z.Long1 = !biasBull;

                // Zone 2: the trade AT the higher-timeframe 20, with the bias, smaller size
                // because the LTF CBC has not confirmed it. In a bearish stack the 20 is the
                // upper EMA, so this is the short price rallies into; in a bullish stack it is
                // the lower one, and the mirror holds.
                z.Lo2 = htfEmaSlow - half;
                z.Hi2 = htfEmaSlow + half;
                z.Long2 = biasBull;

                // Zone 3: wait for the CBC to confirm, then enter. That entry band depends on a
                // bar which has not printed, so drawing one would be fiction. What is knowable
                // now is the trigger: the LTF CBC flips bearish on Close < prior low and bullish
                // on Close > prior high. The band hangs off the far side of that level, which is
                // both the honest rendering and what keeps zone 3 clear of zone 1.
                z.Lo3 = !biasBull ? prevLow - t : prevHigh;
                z.Hi3 = !biasBull ? prevLow : prevHigh + t;
                z.Long3 = biasBull;

                return true;
            }

            // Has price CLOSED through this zone, killing it? A zone you would be buying into
            // dies on a close below its low; one you would be selling into dies on a close
            // above its high. Deliberately the close and not the high/low of the bar: a wick
            // through a zone is the zone doing its job, and truncating on wicks would erase
            // every zone that ever worked. A close beyond the zone in the direction the trade
            // wanted is not an invalidation either - that is the trade paying - so only the
            // one side counts.
            // Is this bar inside the session window an episode may open in? Times are ET as
            // HHMM (930 = 09:30). Either bound at 0 is "no bound", so 0/0 is always open.
            // The window is half-open on purpose: an episode must not open on the very bar
            // the cutoff retires it.
            internal static bool InWindow(int hhmm, int startHhmm, int endHhmm)
            {
                if (startHhmm > 0 && hhmm < startHhmm) return false;
                if (endHhmm > 0 && hhmm >= endHhmm) return false;
                return true;
            }

            internal static bool ClosedThrough(bool isLong, double lo, double hi, double close)
            {
                if (double.IsNaN(close))
                    return false;

                return isLong ? close < lo : close > hi;
            }
        }
        // </OptionZoneGeometry>

        // Zone state handed from OnBarUpdate to OnRender. These zones are PAINTED in
        // OnRender the way OrderFlowZones paints its OB/FVG zones, not created as draw
        // objects, which is why there are no tags here any more. Three consequences worth
        // knowing: the bands size themselves in pixels and always reach the chart's right
        // edge, so no barsAgo or time projection is involved; they render under this
        // indicator's own Draw.* output, which is the right layer for a translucent band
        // sitting behind the flip lines and BRSG; and they are no longer selectable or
        // listed under Drawing Objects, same as OrderFlowZones.
        // One mixed-signal episode: the three zones as they stood at the bar the disagreement
        // appeared, and how far right each of them is still valid.
        //
        // GEOMETRY IS FROZEN AT THE START BAR and never recomputed. That is not an
        // optimisation, it is what makes the display mean anything. A band drawn from the
        // start bar to now while its prices track the latest bar asserts that those levels
        // held for the whole span, which is false; and a level that moves every bar cannot be
        // "closed through" at all, so truncation would be meaningless. The three options are
        // decided at the moment the signals disagree, so that is when the prices are fixed.
        private sealed class OptionZoneEpisode
        {
            // Anchored by TIME, not by bar index. A bar index is only meaningful against the
            // series it was counted on, and the render pass resolves it against ChartBars;
            // any disagreement between those two bases puts the whole band at the wrong x,
            // which is a zone floating away from the candles that produced it. A bar's time
            // has no such ambiguity.
            public DateTime StartTime;
            public DateTime EndTime = DateTime.MinValue;        // MinValue while live
            public bool BiasBull;
            public DateTime OpenDateEt;      // ET calendar day the episode opened on
            public int EndHhmm;              // ET cutoff as HHMM, 0 = none
            public readonly double[] Lo = new double[3];
            public readonly double[] Hi = new double[3];
            public readonly bool[] IsLong = new bool[3];
            public readonly string[] Text = new string[3];
            public readonly DateTime[] ClosedTime =
                { DateTime.MinValue, DateTime.MinValue, DateTime.MinValue };
        }

        private readonly List<OptionZoneEpisode> optionZoneHistory = new List<OptionZoneEpisode>();
        private OptionZoneEpisode optionZoneLive;

        private ATR optionZoneAtrIndicator;
        private double optionZoneAtr = double.NaN;

        private void ClearOptionZones()
        {
            optionZoneLive = null;
            optionZoneHistory.Clear();
        }

        // Close the live episode at endTime and file it as an example.
        private void RetireOptionZoneEpisode(DateTime endTime)
        {
            if (optionZoneLive == null) return;

            optionZoneLive.EndTime = endTime;
            optionZoneHistory.Add(optionZoneLive);
            optionZoneLive = null;

            // Keep the last N per side, so the chart carries a comparable set of bullish-bias
            // and bearish-bias examples rather than whichever side happened to fire recently.
            int keep = Math.Max(0, OptionZoneHistoryPerSide);
            for (int pass = 0; pass < 2; pass++)
            {
                bool side = pass == 0;
                int seen = 0;
                for (int i = optionZoneHistory.Count - 1; i >= 0; i--)
                {
                    if (optionZoneHistory[i].BiasBull != side) continue;
                    seen++;
                    if (seen > keep) optionZoneHistory.RemoveAt(i);
                }
            }
        }

        // Maple's three trading options, drawn at price.
        //
        // They exist only while the LTF CBC and the higher-timeframe bias disagree. With a
        // bearish bias and the LTF CBC flipped long, the choice is: take the CBC long on a
        // BRSG entry (against the bias), fade the rally into the 20 EMA at smaller size
        // (with the bias, unconfirmed), or wait for the CBC to flip back short and take
        // SGCR (with the bias, confirmed). Every one of those mirrors under a bullish bias.
        //
        // When the two reads agree there is one trade, not three, so nothing is drawn.
        private void UpdateOptionZones()
        {
            if (!ShowOptionZones || CurrentBar < 1)
            {
                ClearOptionZones();
                return;
            }

            // Read ATR here, once per bar, so the render thread never touches a series.
            // Below the period it stays NaN and Compute falls back to the prior-bar range.
            if (optionZoneAtrIndicator != null && CurrentBar >= OptionZoneAtrPeriod)
                optionZoneAtr = optionZoneAtrIndicator[0];

            DateTime etNow = ConvertToEastern(Time[0]);

            // 1. Age the live episode. Its zones are frozen, so the ONLY things that can end
            //    it are price closing through them and the clock. Deliberately NOT the setup
            //    conditions: once the three options are on the chart they stand until they are
            //    invalidated or time out. Re-testing the entry conditions here is what made an
            //    episode blink out and back in as price oscillated over the cloud edge.
            if (optionZoneLive != null)
            {
                bool allClosed = true;
                for (int i = 0; i < 3; i++)
                {
                    if (optionZoneLive.ClosedTime[i] == DateTime.MinValue
                        && OptionZoneGeometry.ClosedThrough(optionZoneLive.IsLong[i],
                                                            optionZoneLive.Lo[i],
                                                            optionZoneLive.Hi[i], Close[0]))
                        optionZoneLive.ClosedTime[i] = Time[0];

                    if (optionZoneLive.ClosedTime[i] == DateTime.MinValue) allClosed = false;
                }

                // The ET-date test is not redundant with the HHMM one. If the session ends
                // before the cutoff time, the next bar is the following morning at an HHMM
                // still under the cutoff, and without the date check the episode would stretch
                // straight across the overnight gap.
                bool pastCutoff = etNow.Date != optionZoneLive.OpenDateEt
                    || (optionZoneLive.EndHhmm > 0
                        && etNow.Hour * 100 + etNow.Minute >= optionZoneLive.EndHhmm);

                if (allClosed || pastCutoff)
                    RetireOptionZoneEpisode(Time[0]);
            }

            if (optionZoneLive != null)
                return;   // one episode at a time; it continues, frozen where it was drawn

            // 2. THE TRIGGER IS THE FLIP, not the state. "CBC flips long into the EMAs" names
            //    an event on one bar. Opening on the standing condition instead meant every
            //    bar where the reads happened to disagree was a candidate, and because a
            //    failed cloud test re-armed the next one, price wobbling across the cloud edge
            //    spawned a fresh episode on each re-entry. One flip, one episode.
            if (cbcStatePrev == cbcState)
                return;

            bool biasBull;
            bool ltfBull = cbcState;
            bool requireCloudSide = OptionZoneBias == MapleStaxOptionBias.HtfEmaCloud;

            if (PreviewOptionZones)
            {
                // Review aid. It takes the bias from the REAL EMA stack and forces only the
                // LTF CBC to the opposite side, then drops the cloud-side test, so every CBC
                // flip in the window becomes a worked example. One synthetic input: zone 2
                // still sits on the genuine HTF 20 with the genuine stack direction, so what
                // is on screen is the geometry that ships. Labels carry PREVIEW.
                biasBull = actualHtfEmaBullish;
                ltfBull = !biasBull;
                requireCloudSide = false;
            }
            else
            {
                // Warm-up: until the HTF series has closed a bar there is no bias. previousHtfCbc
                // seeds bearish and actualHtfEmaBullish seeds bullish, so without this guard the
                // opening bars would draw zones off a bias nothing has measured yet.
                if (!HtfEnabled || !htfDataSeriesAdded || htfSeriesIndex < 0
                    || CurrentBars[htfSeriesIndex] < 2)
                    return;

                biasBull = OptionZoneBias == MapleStaxOptionBias.HtfEmaCloud
                    ? actualHtfEmaBullish
                    : previousHtfCbc;
            }

            // 3. The flip has to be AGAINST the higher-timeframe bias. A flip that agrees with
            //    it is just one trade, which the BRSG zone and flip lines already show.
            if (ltfBull == biasBull)
                return;

            // 4. Only inside the session window. This applies to Preview too: a preview run
            //    that opened episodes all day would refill the kept-example slots from the
            //    afternoon and evict the morning ones, which is the session worth studying.
            if (!OptionZoneGeometry.InWindow(etNow.Hour * 100 + etNow.Minute,
                                             OptionZoneWindowStartEt, OptionZoneWindowEndEt))
                return;

            // Preview can run before the HTF series has produced an EMA, so fall back to the
            // chart's own slow EMA there purely to have something on screen. Live zones never
            // take that path: the warm-up gate above has already required two closed HTF bars.
            double htfFast = actualHtfEmaFastVal;
            double htfSlow = actualHtfEmaSlowVal;
            if (PreviewOptionZones && double.IsNaN(htfSlow))
            {
                htfSlow = Values[1][0];
                htfFast = double.NaN;
            }

            OptionZoneGeometry.Zones z;
            if (!OptionZoneGeometry.Compute(biasBull, ltfBull, High[1], Low[1],
                                            htfFast, htfSlow, Close[0], requireCloudSide,
                                            BrsgLowMult, BrsgHighMult,
                                            optionZoneAtr, OptionZoneAtrMult, out z))
                return;

            var ep = new OptionZoneEpisode { StartTime = Time[0], BiasBull = biasBull };
            ep.Lo[0] = z.Lo1; ep.Hi[0] = z.Hi1; ep.IsLong[0] = z.Long1;
            ep.Lo[1] = z.Lo2; ep.Hi[1] = z.Hi2; ep.IsLong[1] = z.Long2;
            ep.Lo[2] = z.Lo3; ep.Hi[2] = z.Hi3; ep.IsLong[2] = z.Long3;

            string[] text = !biasBull
                ? new string[] { "1 BRSG LONG", "2 EMA20 SHORT (small)", "3 SGCR SHORT on flip" }
                : new string[] { "1 SGCR SHORT", "2 EMA20 LONG (small)", "3 BRSG LONG on flip" };
            for (int i = 0; i < 3; i++)
                ep.Text[i] = PreviewOptionZones ? text[i] + "  (PREVIEW)" : text[i];

            ep.OpenDateEt = etNow.Date;
            ep.EndHhmm = OptionZoneWindowEndEt;

            optionZoneLive = ep;
        }

        // Paint the three zones, in the same visual language OrderFlowZones uses for its OB
        // and FVG zones: a flat translucent fill, a lighter strip along the edge the zone is
        // defended from as a cheap stand-in for a gradient, a 1px border at a stronger alpha,
        // and an interior label. The volumetric split bar it draws inside each zone has no
        // counterpart here - there is no order-flow data on this chart - so that part is left
        // out rather than faked.
        private void RenderOptionZones(NinjaTrader.Gui.Chart.ChartControl chartControl,
                                       NinjaTrader.Gui.Chart.ChartScale chartScale)
        {
            if (chartControl == null || chartScale == null || ChartBars == null) return;
            if (optionZoneLive == null && optionZoneHistory.Count == 0) return;

            SharpDX.DirectWrite.TextFormat zoneFmt = null;
            try
            {
                if (ShowOptionZoneLabels)
                    zoneFmt = new SharpDX.DirectWrite.TextFormat(
                        NinjaTrader.Core.Globals.DirectWriteFactory, "Arial", optionZoneTextPx)
                    { TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading };

                // Retired episodes first, so the live one paints over them where they overlap.
                for (int e = 0; e < optionZoneHistory.Count; e++)
                    RenderOptionZoneEpisode(optionZoneHistory[e], chartControl, chartScale, zoneFmt);

                if (optionZoneLive != null)
                    RenderOptionZoneEpisode(optionZoneLive, chartControl, chartScale, zoneFmt);
            }
            finally
            {
                if (zoneFmt != null) zoneFmt.Dispose();
            }
        }

        private const float optionZoneTextPx = 11f;

        private void RenderOptionZoneEpisode(OptionZoneEpisode ep,
                                             NinjaTrader.Gui.Chart.ChartControl chartControl,
                                             NinjaTrader.Gui.Chart.ChartScale chartScale,
                                             SharpDX.DirectWrite.TextFormat zoneFmt)
        {
            const float interiorPad = 3f;

            float panelRight = (float)(chartScale.ChartPanel.X + chartScale.ChartPanel.W);
            float xLeft = GetXForTime(chartControl, ep.StartTime);

            // Where the episode as a whole stops: the right edge while it is live, otherwise
            // the bar it was retired on.
            float xEpisodeEnd = ep.EndTime != DateTime.MinValue
                ? GetXForTime(chartControl, ep.EndTime)
                : panelRight - 2f;
            if (xEpisodeEnd > panelRight - 2f) xEpisodeEnd = panelRight - 2f;

            float fillOpacity = Math.Max(0, Math.Min(100, OptionZoneOpacity)) / 100f;

            for (int i = 0; i < 3; i++)
            {
                // Each zone truncates on its own bar. A zone that was closed through stops
                // there even if the other two carried the episode on.
                float xRight = ep.ClosedTime[i] != DateTime.MinValue
                    ? Math.Min(xEpisodeEnd, GetXForTime(chartControl, ep.ClosedTime[i]))
                    : xEpisodeEnd;
                if (xLeft >= xRight) continue;
                float zoneW = xRight - xLeft;
                if (zoneW < 1f) continue;

                bool spent = ep.EndTime != DateTime.MinValue || ep.ClosedTime[i] != DateTime.MinValue;

                Brush baseBrush = ep.IsLong[i] ? OptionZoneLongColor : OptionZoneShortColor;
                SharpDX.Color4 baseColor = ToD2DColor(baseBrush);
                float alpha = fillOpacity * baseColor.Alpha * (spent ? 0.5f : 1f);
                if (alpha < 0.001f) continue;

                float yTop = (float)chartScale.GetYByValue(ep.Hi[i]);
                float yBot = (float)chartScale.GetYByValue(ep.Lo[i]);
                if (yTop > yBot) { float tmp = yTop; yTop = yBot; yBot = tmp; }
                float zoneH = yBot - yTop;
                if (zoneH < 1f) zoneH = 1f;

                var rect = new SharpDX.RectangleF(xLeft, yTop, zoneW, zoneH);

                using (var fill = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                           new SharpDX.Color4(baseColor.Red, baseColor.Green, baseColor.Blue, alpha)))
                    RenderTarget.FillRectangle(rect, fill);

                // The lighter strip sits on the side price has to come from: along the
                // bottom of a zone you are buying into, the top of one you are selling
                // into. Same trick and same numbers as OrderFlowZones.
                float stripH = Math.Min(zoneH * 0.35f, 6f);
                float stripY = ep.IsLong[i] ? yBot - stripH : yTop;
                var stripColor = new SharpDX.Color4(
                    Math.Min(baseColor.Red + 0.15f, 1f),
                    Math.Min(baseColor.Green + 0.15f, 1f),
                    Math.Min(baseColor.Blue + 0.15f, 1f),
                    alpha * 0.5f);
                using (var strip = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, stripColor))
                    RenderTarget.FillRectangle(new SharpDX.RectangleF(xLeft, stripY, zoneW, stripH), strip);

                using (var border = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                           new SharpDX.Color4(baseColor.Red, baseColor.Green, baseColor.Blue,
                                              Math.Min(alpha * 2.5f, 0.9f))))
                    RenderTarget.DrawRectangle(rect, border, 1f);

                if (zoneFmt == null) continue;

                // Full text on the live episode. Retired examples carry the zone number only:
                // six worked examples at three lines each is eighteen strings of chart noise,
                // and the number plus the colour already says which option it was.
                string label = ep.EndTime != DateTime.MinValue
                    ? (string.IsNullOrEmpty(ep.Text[i]) ? string.Empty : ep.Text[i].Substring(0, 1))
                    : (ep.Text[i] ?? string.Empty);

                float lblY = ep.IsLong[i] ? yTop + interiorPad : yBot - optionZoneTextPx - interiorPad;
                float lblW = Math.Max(zoneW - interiorPad * 2f, 20f);
                var lblRect = new SharpDX.RectangleF(xLeft + interiorPad, lblY, lblW, optionZoneTextPx + 2f);
                using (var txt = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget,
                           new SharpDX.Color4(baseColor.Red, baseColor.Green, baseColor.Blue,
                                              spent ? 0.75f : 1f)))
                    RenderTarget.DrawText(label, zoneFmt, lblRect, txt);
            }
        }

        // Bar time to an X pixel. By time rather than by bar index on purpose: an index only
        // means anything against the series that counted it, and resolving it against
        // ChartBars puts the band wherever those two bases disagree. Guarded because a time
        // outside what the chart currently holds must not take the whole render pass down.
        private float GetXForTime(NinjaTrader.Gui.Chart.ChartControl chartControl, DateTime t)
        {
            try { return (float)chartControl.GetXByTime(t); }
            catch { return (float)chartControl.CanvasLeft; }
        }

        private void UpdateStatusPanel()
        {
            if (!ShowStatusTable)
            {
                statusRowData = new StatusRow[0];
                return;
            }

            DateTime et = ConvertToEastern(Time[0]);
            string market = MarketSessionLabel(et);
            string chartTf = BarsPeriodDescription(BarsPeriod);
            string htfTf = htfDataSeriesAdded ? BarsPeriodDescription(htfBarsPeriod) : HtfTimeframe;
            string ltfMsg = cbcState ? "Bullish" : "Bearish";
            string htfMsg = !HtfEnabled ? "Off" : previousHtfCbc ? "Bullish" : "Bearish";
            string orMsg = OpeningRangeStatus(et);
            string cloudMsg = Values[0][0] >= Values[1][0] ? "Bullish" : "Bearish";
            string vwapMsg = !ShowVwap || double.IsNaN(Values[3][0]) ? "Off" : (Close[0] > Values[3][0] ? "Above" : "Below");
            string ema200Msg = Close[0] > Values[2][0] ? "Above" : "Below";

            Brush mktBg = market == "Asian" ? colOrbAsian : market == "London" ? colOrbLondon : colOrbNy;
            Brush ltfBg = cbcState ? StatusBullBg : StatusBearBg;
            Brush htfBg = !HtfEnabled ? StatusNeutralBg : previousHtfCbc ? StatusBullBg : StatusBearBg;
            Brush orBg = orMsg == "Inside range" ? StatusInsideBg : (orMsg == "Above range" ? StatusBullBg : orMsg == "Below range" ? StatusBearBg : StatusNeutralBg);
            Brush cloudBg = cloudMsg == "Bullish" ? StatusBullBg : StatusBearBg;
            Brush vwapBg = vwapMsg == "Off" ? StatusNeutralBg : (vwapMsg == "Above" ? StatusBullBg : StatusBearBg);
            Brush e200Bg = ema200Msg == "Above" ? StatusBullBg : StatusBearBg;

            statusHeaderText = new string('\n', StatusTableHeaderNewlines) + "MapleStax CBC  ·  " + Instrument.MasterInstrument.Name;
            statusRowData = new StatusRow[]
            {
                new StatusRow { Label = "Market",                   Value = market,    LeftBg = StatusLabelBg, RightBg = mktBg,   LeftFg = StatusLabelText, RightFg = PickRightFg(mktBg) },
                new StatusRow { Label = "CBC (" + chartTf + ")",    Value = ltfMsg,    LeftBg = StatusLabelBg, RightBg = ltfBg,   LeftFg = StatusLabelText, RightFg = PickRightFg(ltfBg) },
                new StatusRow { Label = "CBC (" + htfTf + ")",      Value = htfMsg,    LeftBg = StatusLabelBg, RightBg = htfBg,   LeftFg = StatusLabelText, RightFg = PickRightFg(htfBg) },
                new StatusRow { Label = "Opening range",            Value = orMsg,     LeftBg = StatusLabelBg, RightBg = orBg,    LeftFg = StatusLabelText, RightFg = PickRightFg(orBg) },
                new StatusRow { Label = "EMA cloud",                Value = cloudMsg,  LeftBg = StatusLabelBg, RightBg = cloudBg, LeftFg = StatusLabelText, RightFg = PickRightFg(cloudBg) },
                new StatusRow { Label = "VWAP",                     Value = vwapMsg,   LeftBg = StatusLabelBg, RightBg = vwapBg,  LeftFg = StatusLabelText, RightFg = PickRightFg(vwapBg) },
                new StatusRow { Label = "EMA " + EmaTrendPeriod,    Value = ema200Msg, LeftBg = StatusLabelBg, RightBg = e200Bg,  LeftFg = StatusLabelText, RightFg = PickRightFg(e200Bg) },
            };
        }

        private void UpdateBarColoring()
        {
            if (!ShowBarColoring || CurrentBar < 1)
                return;

            if (State != State.Historical && CurrentBar == Bars.Count - 1 && !IsFirstTickOfBar)
                return;

            bool bothLong = cbcState && previousHtfCbc;
            bool bothShort = !cbcState && !previousHtfCbc;

            Brush paint;
            if (bothLong)
                paint = colCbcBarLong;
            else if (bothShort)
                paint = colCbcBarShort;
            else
                paint = colCbcBarNeutral;

            BarBrushes[0] = paint;
            CandleOutlineBrushes[0] = paint;
        }

        private void UpdateHtfSeries()
        {
            if (htfSeriesIndex < 0 || CurrentBars[htfSeriesIndex] < 2)
                return;

            int idx = htfSeriesIndex;
            bool htfState = previousHtfCbc;
            bool bearFlip = Closes[idx][0] < Lows[idx][1];
            bool bullFlip = Closes[idx][0] > Highs[idx][1];
            if (previousHtfCbc && bearFlip)
            {
                htfState = false;
                pendingHtfBearLabel = true;
            }
            else if (!previousHtfCbc && bullFlip)
            {
                htfState = true;
                pendingHtfBullLabel = true;
            }

            lastHtfHigh1 = Highs[idx][1];
            lastHtfLow1 = Lows[idx][1];
            lastClosedHtfHigh = Highs[idx][1];
            lastClosedHtfLow = Lows[idx][1];
            lastClosedHtfOpen = Times[idx][1];
            if (!double.IsNaN(lastHtfHigh1) || !double.IsNaN(lastHtfLow1))
                lastClosedHtfClose = lastClosedHtfOpen.Add(BarsPeriodToTimeSpan(htfBarsPeriod));
            previousHtfCbc = htfState;

            double actualHtfEma9 = EMA(Closes[idx], HtfEmaFastPeriod)[0];
            double actualHtfEma20 = EMA(Closes[idx], HtfEmaSlowPeriod)[0];
            actualHtfEmaBullish = actualHtfEma9 >= actualHtfEma20;

            // Kept, not just compared. The option zones need the levels themselves: zone 2 is
            // the trade at the HTF 20, and the cloud-side gate needs both to know whether
            // price is still on the near side of them.
            actualHtfEmaFastVal = actualHtfEma9;
            actualHtfEmaSlowVal = actualHtfEma20;
        }

        private void UpdateHtfPivots()
        {
            if (!ShowHtfPivots || !HtfEnabled || htfSeriesIndex < 0) return;
            int n = HtfPivN;
            if (CurrentBars[htfSeriesIndex] < 2 * n) return;

            int idx = htfSeriesIndex;
            double candidateHigh = Highs[idx][n];
            double candidateLow = Lows[idx][n];
            bool isPivotHigh = true;
            bool isPivotLow = true;
            for (int off = 0; off <= 2 * n; off++)
            {
                if (off == n) continue;
                if (Highs[idx][off] >= candidateHigh) isPivotHigh = false;
                if (Lows[idx][off] <= candidateLow) isPivotLow = false;
                if (!isPivotHigh && !isPivotLow) break;
            }

            if (isPivotHigh)
            {
                lastHtfPivotHigh = candidateHigh;
                lastHtfPivotHighTime = Times[idx][n];
            }
            if (isPivotLow)
            {
                lastHtfPivotLow = candidateLow;
                lastHtfPivotLowTime = Times[idx][n];
            }
        }

        private void UpdateHtfLabelsOnLtf()
        {
            if (!HtfEnabled || !ShowHtfCbcLabels || CurrentBar < 1)
            {
                pendingHtfBullLabel = false;
                pendingHtfBearLabel = false;
                return;
            }

            SimpleFont labelFont = new SimpleFont("Arial", 10);

            if (pendingHtfBullLabel)
            {
                int barOffset = FindHtfAnchorBar(true, lastClosedHtfLow, lastClosedHtfOpen, lastClosedHtfClose, 32);
                Draw.Text(this, "ADVHTFBullLabel" + CurrentBar, false, "LONG",
                          barOffset, Low[barOffset], -25,
                          Brushes.Black, labelFont, TextAlignment.Center,
                          Brushes.Transparent, HtfLongLabelBg, HtfLabelOpacity);
                pendingHtfBullLabel = false;
            }
            if (pendingHtfBearLabel)
            {
                int barOffset = FindHtfAnchorBar(false, lastClosedHtfHigh, lastClosedHtfOpen, lastClosedHtfClose, 32);
                Draw.Text(this, "ADVHTFBearLabel" + CurrentBar, false, "SHORT",
                          barOffset, High[barOffset], 25,
                          Brushes.Black, labelFont, TextAlignment.Center,
                          Brushes.Transparent, HtfShortLabelBg, HtfLabelOpacity);
                pendingHtfBearLabel = false;
            }
        }

        private int FindHtfAnchorBar(bool isLong, double sigPrice, DateTime open, DateTime close, int maxOff)
        {
            int bestOff = 0;
            double bestErr = double.MaxValue;
            for (int off = 0; off <= maxOff && off <= CurrentBar; off++)
            {
                DateTime t = Times[0][off];
                if (t >= open && t <= close)
                {
                    double err = isLong ? Math.Abs(Lows[0][off] - sigPrice) : Math.Abs(Highs[0][off] - sigPrice);
                    if (err < bestErr || (err == bestErr && off > bestOff))
                    {
                        bestErr = err;
                        bestOff = off;
                    }
                }
            }
            return bestOff;
        }

        private bool TryParseTimeframe(string value, out BarsPeriodType periodType, out int valueOut)
        {
            periodType = BarsPeriodType.Minute;
            valueOut = 10;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            string trimmed = value.Trim().ToUpperInvariant();
            if (trimmed.EndsWith("D"))
            {
                if (int.TryParse(trimmed.Substring(0, trimmed.Length - 1), out int v))
                {
                    periodType = BarsPeriodType.Day;
                    valueOut = v > 0 ? v : 1;
                    return true;
                }
            }
            else if (trimmed.EndsWith("W"))
            {
                if (int.TryParse(trimmed.Substring(0, trimmed.Length - 1), out int v))
                {
                    periodType = BarsPeriodType.Week;
                    valueOut = v > 0 ? v : 1;
                    return true;
                }
            }
            else if (trimmed.EndsWith("M"))
            {
                if (int.TryParse(trimmed.Substring(0, trimmed.Length - 1), out int v))
                {
                    periodType = BarsPeriodType.Month;
                    valueOut = v > 0 ? v : 1;
                    return true;
                }
            }
            else if (trimmed.EndsWith("H"))
            {
                if (int.TryParse(trimmed.Substring(0, trimmed.Length - 1), out int v))
                {
                    periodType = BarsPeriodType.Minute;
                    valueOut = v > 0 ? v * 60 : 60;
                    return true;
                }
            }
            else if (int.TryParse(trimmed, out int minutes))
            {
                periodType = BarsPeriodType.Minute;
                valueOut = minutes > 0 ? minutes : 1;
                return true;
            }

            return false;
        }

        private static void FreezeIfNeeded(Brush brush)
        {
            if (brush != null && brush.CanFreeze && !brush.IsFrozen)
                brush.Freeze();
        }

        private TimeZoneInfo GetEasternTimeZone()
        {
            return GetTzById("Eastern Standard Time", TimeZoneInfo.Local);
        }

        private static TimeZoneInfo GetTzById(string id, TimeZoneInfo fallback)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { return fallback; }
        }

        private TimeZoneInfo GetSourceTimeZone()
        {
            switch (ChartTz)
            {
                case MapleStaxChartTimeZone.Eastern:  return GetTzById("Eastern Standard Time",  TimeZoneInfo.Local);
                case MapleStaxChartTimeZone.Central:  return GetTzById("Central Standard Time",  TimeZoneInfo.Local);
                case MapleStaxChartTimeZone.Mountain: return GetTzById("Mountain Standard Time", TimeZoneInfo.Local);
                case MapleStaxChartTimeZone.Pacific:  return GetTzById("Pacific Standard Time",  TimeZoneInfo.Local);
                case MapleStaxChartTimeZone.Utc:      return TimeZoneInfo.Utc;
                case MapleStaxChartTimeZone.Local:    return TimeZoneInfo.Local;
                case MapleStaxChartTimeZone.AutoDetect:
                default:
                    return TimeZoneInfo.Local;
            }
        }

        private DateTime ConvertToEastern(DateTime sourceTime)
        {
            if (etTimeZone == null) etTimeZone = GetEasternTimeZone();
            if (sourceTimeZone == null) sourceTimeZone = GetSourceTimeZone();
            return TimeZoneInfo.ConvertTime(sourceTime, sourceTimeZone, etTimeZone);
        }

        private bool IsSessionActive(DateTime et, int hour, int minute, int lengthMinutes)
        {
            DateTime sessionStart = new DateTime(et.Year, et.Month, et.Day, hour, minute, 0);
            DateTime sessionEnd = sessionStart.AddMinutes(lengthMinutes);
            return et >= sessionStart && et < sessionEnd;
        }

        private bool IsAsianSessionActive(DateTime et, int lengthMinutes)
        {
            DateTime sessionStart = new DateTime(et.Year, et.Month, et.Day, 20, 0, 0);
            if (et.Hour < 3)
                sessionStart = sessionStart.AddDays(-1);
            DateTime sessionEnd = sessionStart.AddMinutes(lengthMinutes);
            return et >= sessionStart && et < sessionEnd;
        }

        private bool IsPremarket(DateTime et)
        {
            int minutes = et.Hour * 60 + et.Minute;
            return minutes >= 4 * 60 && minutes < 9 * 60 + 30;
        }

        private bool IsRegular(DateTime et)
        {
            int minutes = et.Hour * 60 + et.Minute;
            return minutes >= 9 * 60 + 30 && minutes < 16 * 60;
        }

        // Globex futures session: 17:00 ET (prev day) -> 16:00 ET (curr day), with a 16:00-17:00 ET maintenance break.
        private bool IsInGlobex(DateTime et)
        {
            int minutes = et.Hour * 60 + et.Minute;
            return minutes < 16 * 60 || minutes >= 17 * 60;
        }

        private string BarsPeriodDescription(BarsPeriod period)
        {
            if (period.BarsPeriodType == BarsPeriodType.Minute)
            {
                if (period.Value % 60 == 0 && period.Value >= 60)
                    return (period.Value / 60) + "H";
                return period.Value + "m";
            }
            if (period.BarsPeriodType == BarsPeriodType.Second)
                return period.Value + "s";
            if (period.BarsPeriodType == BarsPeriodType.Day)
                return period.Value == 1 ? "1D" : period.Value + "D";
            if (period.BarsPeriodType == BarsPeriodType.Week)
                return period.Value == 1 ? "1W" : period.Value + "W";
            if (period.BarsPeriodType == BarsPeriodType.Month)
                return period.Value + "M";
            return period.ToString();
        }

        private string MarketSessionLabel(DateTime et)
        {
            int minutes = et.Hour * 60 + et.Minute;
            if (minutes >= 20 * 60 || minutes < 3 * 60)
                return "Asian";
            if (minutes < 9 * 60 + 30)
                return "London";
            return "New York";
        }

        private string OpeningRangeStatus(DateTime et)
        {
            if (!OrNy && !OrLondon && !OrAsian)
                return "Off";

            if (OrAsian && IsAsianSessionActive(et, OrMinutes))
                return "Before open";
            if (OrLondon && IsSessionActive(et, 3, 0, OrMinutes))
                return "Before open";
            if (OrNy && IsSessionActive(et, 9, 30, OrMinutes))
                return "Before open";

            bool orActive = (OrNy && orHighNy.HasValue) || (OrLondon && orHighLondon.HasValue) || (OrAsian && orHighAsian.HasValue);
            if (!orActive)
                return "—";

            double? hi = null;
            double? lo = null;
            var session = MarketSessionLabel(et);
            if (session == "New York") { hi = orHighNy; lo = orLowNy; }
            if (session == "London") { hi = orHighLondon; lo = orLowLondon; }
            if (session == "Asian") { hi = orHighAsian; lo = orLowAsian; }

            if (!hi.HasValue || !lo.HasValue)
                return "Pending";
            if (Close[0] > hi.Value)
                return "Above range";
            if (Close[0] < lo.Value)
                return "Below range";
            return "Inside range";
        }

        private int GetFontSize(MapleStaxStatusTextSize size)
        {
            switch (size)
            {
                case MapleStaxStatusTextSize.Tiny: return 10;
                case MapleStaxStatusTextSize.Small: return 12;
                case MapleStaxStatusTextSize.Normal: return 14;
                case MapleStaxStatusTextSize.Large: return 16;
                case MapleStaxStatusTextSize.Huge: return 18;
            }
            return 12;
        }

        private DashStyleHelper GetDashStyle(MapleStaxRefLineStyle style)
        {
            switch (style)
            {
                case MapleStaxRefLineStyle.Dashed: return DashStyleHelper.Dash;
                case MapleStaxRefLineStyle.Dotted: return DashStyleHelper.Dot;
                default: return DashStyleHelper.Solid;
            }
        }

        private TimeSpan BarsPeriodToTimeSpan(BarsPeriod period)
        {
            switch (period.BarsPeriodType)
            {
                case BarsPeriodType.Minute:
                    return TimeSpan.FromMinutes(period.Value);
                case BarsPeriodType.Second:
                    return TimeSpan.FromSeconds(period.Value);
                case BarsPeriodType.Day:
                    return TimeSpan.FromDays(period.Value);
                case BarsPeriodType.Week:
                    return TimeSpan.FromDays(period.Value * 7);
                case BarsPeriodType.Month:
                    return TimeSpan.FromDays(period.Value * 30);
                default:
                    return TimeSpan.FromMinutes(period.Value);
            }
        }

        protected override void OnRender(NinjaTrader.Gui.Chart.ChartControl chartControl, NinjaTrader.Gui.Chart.ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            if (RenderTarget == null) return;

            // Zones first so the status panel paints over them, not under.
            RenderOptionZones(chartControl, chartScale);

            if (!ShowStatusTable || statusRowData == null || statusRowData.Length == 0) return;

            float fontSize = GetFontSize(StatusTableTextSize);
            var typeface = new SharpDX.DirectWrite.TextFormat(NinjaTrader.Core.Globals.DirectWriteFactory, "Arial", fontSize) { TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading };
            float pad = 4f;
            float headerH = fontSize * (1.4f + StatusTableHeaderNewlines);
            float rowH = fontSize * 1.6f;
            float colLabelW = fontSize * 9f;
            float colValueW = fontSize * 7f;
            float tableW = colLabelW + colValueW;
            float tableH = headerH + rowH * statusRowData.Length;

            float originX, originY;
            ResolveStatusOrigin(chartControl, chartScale, tableW, tableH, out originX, out originY);

            DrawStatusCell(originX, originY, tableW, headerH, StatusHeaderBg, StatusHeaderText, statusHeaderText, typeface, pad, true);
            for (int i = 0; i < statusRowData.Length; i++)
            {
                StatusRow r = statusRowData[i];
                float y = originY + headerH + rowH * i;
                DrawStatusCell(originX,           y, colLabelW, rowH, r.LeftBg,  r.LeftFg,  r.Label, typeface, pad, false);
                DrawStatusCell(originX+colLabelW, y, colValueW, rowH, r.RightBg, r.RightFg, r.Value, typeface, pad, false);
            }

            DrawStatusFrame(originX, originY, tableW, tableH);

            typeface.Dispose();
        }

        private void ResolveStatusOrigin(NinjaTrader.Gui.Chart.ChartControl chartControl, NinjaTrader.Gui.Chart.ChartScale chartScale, float tableW, float tableH, out float x, out float y)
        {
            float left = (float)chartScale.ChartPanel.X + 8f;
            float right = (float)(chartScale.ChartPanel.X + chartScale.ChartPanel.W) - tableW - 48f;
            float center = (float)(chartScale.ChartPanel.X + chartScale.ChartPanel.W / 2.0) - tableW / 2f;
            float top = (float)chartScale.ChartPanel.Y + 8f;
            float bottom = (float)(chartScale.ChartPanel.Y + chartScale.ChartPanel.H) - tableH - 8f;
            float middle = (float)(chartScale.ChartPanel.Y + chartScale.ChartPanel.H / 2.0) - tableH / 2f;

            switch (StatusTablePosition)
            {
                case MapleStaxStatusTablePosition.TopLeft:      x = left;   y = top;    return;
                case MapleStaxStatusTablePosition.TopCenter:    x = center; y = top;    return;
                case MapleStaxStatusTablePosition.MiddleLeft:   x = left;   y = middle; return;
                case MapleStaxStatusTablePosition.MiddleCenter: x = center; y = middle; return;
                case MapleStaxStatusTablePosition.MiddleRight:  x = right;  y = middle; return;
                case MapleStaxStatusTablePosition.BottomLeft:   x = left;   y = bottom; return;
                case MapleStaxStatusTablePosition.BottomCenter: x = center; y = bottom; return;
                case MapleStaxStatusTablePosition.BottomRight:  x = right;  y = bottom; return;
                default:                                        x = right;  y = top;    return; // TopRight
            }
        }

        private void DrawStatusCell(float x, float y, float w, float h, Brush bg, Brush fg, string text, SharpDX.DirectWrite.TextFormat typeface, float pad, bool centerH)
        {
            var rect = new SharpDX.RectangleF(x, y, w, h);
            using (var bgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToD2DColor(bg)))
                RenderTarget.FillRectangle(rect, bgBrush);
            using (var borderBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0.58f, 0.64f, 0.72f, 0.7f)))
                RenderTarget.DrawRectangle(rect, borderBrush, 1f);
            using (var fgBrush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, ToD2DColor(fg)))
            {
                var textRect = new SharpDX.RectangleF(x + pad, y, w - pad * 2, h);
                if (centerH) typeface.TextAlignment = SharpDX.DirectWrite.TextAlignment.Center;
                else typeface.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                RenderTarget.DrawText(text ?? string.Empty, typeface, textRect, fgBrush);
            }
        }

        private void DrawStatusFrame(float x, float y, float w, float h)
        {
            var frameColor = new SharpDX.Color4(0.58f, 0.64f, 0.72f, 1f);
            using (var brush = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, frameColor))
            {
                var inner = new SharpDX.RectangleF(x, y, w, h);
                var outer = new SharpDX.RectangleF(x - 3f, y - 3f, w + 6f, h + 6f);
                RenderTarget.DrawRectangle(outer, brush, 1.5f);
                RenderTarget.DrawRectangle(inner, brush, 1.5f);
            }
        }

        private static Brush PickRightFg(Brush bg)
        {
            return IsYellowBackground(bg) ? Brushes.Black : Brushes.White;
        }

        private static bool IsYellowBackground(Brush b)
        {
            var scb = b as SolidColorBrush;
            if (scb == null) return false;
            var c = scb.Color;
            return c.R >= 200 && c.G >= 180 && c.B < c.R - 80 && c.B < c.G - 60;
        }

        private static SharpDX.Color4 ToD2DColor(Brush b)
        {
            var scb = b as SolidColorBrush;
            if (scb == null) return new SharpDX.Color4(0.5f, 0.5f, 0.5f, 1f);
            var c = scb.Color;
            return new SharpDX.Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        }
    }
}

