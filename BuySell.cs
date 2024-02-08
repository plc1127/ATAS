﻿namespace ATAS.Indicators.Technical
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using System.Drawing;
    using System.Net;
    using ATAS.Indicators;
    using ATAS.Indicators.Drawing;
    using ATAS.Indicators.Technical.Properties;
    using OFT.Attributes.Editors;
    using Newtonsoft.Json.Linq;
    using System.Collections.Immutable;
    using OFT.Rendering.Context;
    using OFT.Rendering.Tools;
    using static System.Runtime.InteropServices.JavaScript.JSType;
    using static ATAS.Indicators.Technical.BarTimer;
    using static ATAS.Indicators.Technical.SampleProperties;

    using Color = System.Drawing.Color;
    using MColor = System.Windows.Media.Color;
    using MColors = System.Windows.Media.Colors;
    using Pen = System.Drawing.Pen;
    using String = String;
    using ATAS.DataFeedsCore;
    using System.Reflection.Emit;

    [DisplayName("TraderOracle Buy/Sell")]
    public class BuySell : Indicator
    {
        private const String sVersion = "1.32";
        private List<string> lsH = new List<string>();
        private List<string> lsM = new List<string>();

        public decimal Decimal { get; set; }
        private readonly PaintbarsDataSeries _paintBars = new("Paint bars");

        private int _lastBar = -1;
        private bool _lastBarCounted;

        // Default TRUE
        private bool bShowTramp = true;          // SHOW
        private bool bShowNews = true;
        private bool bShowUp = true;
        private bool bShowDown = true;
        private bool bbShowBBBounce = true;
        private bool bUseFisher = true;          // USE
        private bool bUseWaddah = true;
        private bool bUseT3 = true;
        private bool bUseSuperTrend = true;
        private bool bUseAO = true;
        private bool bUsePSAR = true;
        private bool bVolumeImbalances = true;

        // Default FALSE
        private bool bShowIntense = false;       // SHOW
        private bool bShowAMDKama = false;
        private bool bShowPNL = false;
        private bool bNewsProcessed = false;     // USE
        private bool bUseSqueeze = false;
        private bool bUseMACD = false;
        private bool bUseKAMA = false;
        private bool bUseMyEMA = false;
        private bool bWaitTilClose = false;

        private bool bShow921 = false;
        private bool bShowSqueeze = false;
        private bool bShowRevPattern = false;
        private bool bShowTripleSupertrend = false;
        private bool bShowCloud = false;
        private bool bAdvanced = false;

        private int iMinDelta = 0;
        private int iMinDeltaPercent = 0;
        private int iMinADX = 0;
        private int iMyEMAPeriod = 21;
        private int iKAMAPeriod = 9;
        private int iOffset = 9;
        private int iFontSize = 10;
        private int iBigTrades = 25000;
        private int iNewsFont = 10;
        private int iWaddaSensitivity = 120;
        private int CandleColoring = 0;

        public BuySell() :
            base(true)
        {
            EnableCustomDrawing = true;
            //DenyToChangePanel = true;

            DataSeries[0] = _posSeries;
            DataSeries.Add(_negSeries);
            DataSeries.Add(_negWhite);
            DataSeries.Add(_posWhite);
            DataSeries.Add(_nine21);
            DataSeries.Add(_squeezie);
            DataSeries.Add(_paintBars);
            DataSeries.Add(_dnTrend);
            DataSeries.Add(_upTrend);
            DataSeries.Add(_upCloud);
            DataSeries.Add(_dnCloud);
            DataSeries.Add(_kamanine);

            Add(_ao);
            Add(_ft);
            Add(_sq);
            Add(_psar);
            Add(_st1);
            Add(_st2);
            Add(_st3);
            Add(_adx);
            Add(_kama9);
            Add(_kama21);
            Add(_atr);
        }

        private void ParseStockEvents(String result, int bar)
        {
            int iJSONStart = 0;
            int iJSONEnd = -1;
            String sFinalText = String.Empty; String sNews = String.Empty; String name = String.Empty; String impact = String.Empty; String time = String.Empty; String actual = String.Empty; String previous = String.Empty; String forecast = String.Empty; 

            try
            { 
                iJSONStart = result.IndexOf("window.calendarComponentStates[1] = ");
                iJSONEnd = result.IndexOf("\"}]}],", iJSONStart); 
                sFinalText = result.Substring(iJSONStart, iJSONEnd - iJSONStart);
                sFinalText = sFinalText.Replace("window.calendarComponentStates[1] = ", "");
                sFinalText += "\"}]}]}";
                 
                var jsFile = JObject.Parse(sFinalText);
                foreach (JToken j3 in (JArray)jsFile["days"])
                {
                    JToken j2 = j3.SelectToken("events");
                    foreach (JToken j in j2)
                    {
                        name = j["name"].ToString();
                        impact = j["impactTitle"].ToString();
                        time = j["timeLabel"].ToString();
                        actual = j["actual"].ToString();
                        previous = j["previous"].ToString();
                        forecast = j["forecast"].ToString();
                        sNews = time + "     " + name;
                        if (previous.ToString().Trim().Length > 0)
                            sNews += " (Prev: " + previous + ", Forecast: " + forecast + ")";
                        if (impact.Contains("High"))
                            lsH.Add(sNews);
                        if (impact.Contains("Medium"))
                            lsM.Add(sNews);
                    }
                }
            }
            catch { }
        }

        private void LoadStock(int bar)
        {
            try
            {
                HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create("https://www.forexfactory.com/calendar?day=today");
                myRequest.Method = "GET";
                myRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.150 Safari/537.36";
                WebResponse myResponse = myRequest.GetResponse();
                StreamReader sr = new StreamReader(myResponse.GetResponseStream(), System.Text.Encoding.UTF8);
                string result = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
                ParseStockEvents(result, bar);
                bNewsProcessed = true;
            }
            catch { }
        }

        protected override void OnRender(RenderContext context, DrawingLayouts layout)
        {
            var font2 = new RenderFont("Arial", iNewsFont);
            var fontB = new RenderFont("Arial", iNewsFont, FontStyle.Bold);
            int upY = 50;
            int upX = ChartArea.Width - 350;
            int iTrades = 0;

            if (TradingManager.Portfolio != null && bShowPNL)
            {
                var txt1 = $"Account: {TradingManager.Portfolio.AccountID}";
                context.DrawString(txt1, font2, Color.Gray, upX, upY);
                var tsize = context.MeasureString(txt1, font2);

                upY += tsize.Height + 6;
                if (TradingManager.Position != null)
                {
                    tsize = context.MeasureString(txt1, fontB);
                    txt1 = $"Total PNL: {TradingManager.Position.RealizedPnL}";
                    if (TradingManager.Position.RealizedPnL > 0)
                        context.DrawString(txt1, fontB, Color.Lime, upX, upY);
                    else
                        context.DrawString(txt1, fontB, Color.Red, upX, upY);
                }
                upY += tsize.Height + 6;
                var myTrades = TradingManager.MyTrades;
                if (myTrades.Any())
                {
                    foreach (var myTrade in myTrades)
                        iTrades++;
                    tsize = context.MeasureString(txt1, font2);
                    txt1 = $"Total Trades: " + iTrades;
                    context.DrawString(txt1, font2, Color.Gray, upX, upY);
                }
            }

            if (! bShowNews)
                return;

            RenderFont font;
            Size textSize;
            int currY = 40;

            font = new RenderFont("Arial", iNewsFont + 2);
            textSize = context.MeasureString("Today's News:", font);
            context.DrawString("Today's News:", font, Color.YellowGreen, 50, currY);
            currY += textSize.Height + 10;
            font = new RenderFont("Arial", iNewsFont);

            foreach (string s in lsH)
            {
                textSize = context.MeasureString(s, font);
                context.DrawString("High - " + s, font, Color.DarkOrange, 50, currY);
                currY += textSize.Height;
            }
            currY += 9;
            foreach (string s in lsM)
            {
                textSize = context.MeasureString(s, font);
                context.DrawString("Med  - " + s, font, Color.Gray, 50, currY);
                currY += textSize.Height;
            }
        }

        private Color AMD(int bar)
        {
            var candle = GetCandle(bar);
            var diff = InstrumentInfo.TimeZone;
            var time = candle.Time.AddHours(diff);

            // Manipulation
            if (
                (time.Hour == 8 && time.Minute >= 47 && time.Minute <= 59) ||
                (time.Hour == 9 && time.Minute >= 00 && time.Minute <= 11) ||
                (time.Hour == 10 && time.Minute >= 10 && time.Minute <= 26) ||
                (time.Hour == 11 && time.Minute >= 07 && time.Minute <= 19) ||
                (time.Hour == 11 && time.Minute >= 55 && time.Minute <= 59) ||
                (time.Hour == 12 && time.Minute >= 00 && time.Minute <= 07)
                )
                return Color.FromArgb(220, 255, 0, 0);

            // Distribution
            if (
                (time.Hour == 9 && time.Minute >= 11 && time.Minute <= 47) ||
                (time.Hour == 10 && time.Minute >= 26 && time.Minute <= 50) ||
                (time.Hour == 11 && time.Minute >= 19 && time.Minute <= 37) ||
                (time.Hour == 12 && time.Minute >= 07 && time.Minute <= 25)
                )
                return Color.FromArgb(230, 0, 255, 0); 

            return Color.FromArgb(100, 244, 252, 0);
        }

        protected void DrawText(int bBar, String strX, Color cI, Color cB, bool bOverride = false, bool bSwap = false)
        {
            decimal _tick = ChartInfo.PriceChartContainer.Step;
            decimal loc = 0;

            var candle = GetCandle(bBar);
            if (candle.Close > candle.Open || bOverride)
                loc = candle.High + (_tick * iOffset);
            else
                loc = candle.Low - (_tick * iOffset);

            if (candle.Close > candle.Open && bSwap)
                loc = candle.Low - (_tick * (iOffset * 2));
            else if (candle.Close < candle.Open && bSwap)
                loc = candle.High + (_tick * iOffset);

            AddText("Aver" + bBar, strX, true, bBar, loc, cI, cB, iFontSize, DrawingText.TextAlign.Center);
        }

        // ========================================================================
        // ==========================    INDICATORS    ============================
        // ========================================================================

        private readonly RSI _rsi = new() { Period = 14 };
        private readonly ATR _atr = new() { Period = 14 };
        private readonly AwesomeOscillator _ao = new AwesomeOscillator();
        private readonly ParabolicSAR _psar = new ParabolicSAR();
        private readonly ADX _adx = new ADX() { Period = 10 };
        private readonly EMA _myEMA = new EMA() { Period = 21 };
        private readonly EMA _9 = new EMA() { Period = 9 };
        private readonly EMA _21 = new EMA() { Period = 21 };
        private readonly EMA fastEma = new EMA() { Period = 20 };
        private readonly EMA slowEma = new EMA() { Period = 40 };
        private readonly FisherTransform _ft = new FisherTransform() { Period = 10 };
        private readonly SuperTrend _st1 = new SuperTrend() { Period = 10, Multiplier = 1m };
        private readonly SuperTrend _st2 = new SuperTrend() { Period = 11, Multiplier = 2m };
        private readonly SuperTrend _st3 = new SuperTrend() { Period = 12, Multiplier = 3m };
        private readonly BollingerBands _bb = new BollingerBands() { Period = 20, Shift = 0, Width = 2 };
        private readonly KAMA _kama9 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 9 };
        private readonly KAMA _kama21 = new KAMA() { ShortPeriod = 2, LongPeriod = 109, EfficiencyRatioPeriod = 21 };
        private readonly MACD _macd = new MACD() { ShortPeriod = 12, LongPeriod = 26, SignalPeriod = 9 };
        private readonly T3 _t3 = new T3() { Period = 10, Multiplier = 1 };
        private readonly SqueezeMomentum _sq = new SqueezeMomentum() { BBPeriod = 20, BBMultFactor = 2, KCPeriod = 20, KCMultFactor = 1.5m, UseTrueRange = false };

        [Display(Name = "Font Size", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(1, 90)]
        public int TextFont { get => iFontSize; set { iFontSize = value; RecalculateValues(); } }

        [Display(Name = "Text Offset", GroupName = "Drawing", Order = int.MaxValue)]
        [Range(0, 900)]
        public int Offset { get => iOffset; set { iOffset = value; RecalculateValues(); } }

        // ========================================================================
        // =========================    DATA SERIES    ============================
        // ========================================================================

        private ValueDataSeries _kamanine = new("KAMA NINE") { VisualType = VisualMode.Line, Color = DefaultColors.Yellow.Convert(), Width = 5 };
        private RangeDataSeries _upCloud = new("Up Cloud") { RangeColor = MColor.FromArgb(73, 0, 255, 0), DrawAbovePrice = false };
        private RangeDataSeries _dnCloud = new("Down Cloud") { RangeColor = MColor.FromArgb(73, 255, 0, 0), DrawAbovePrice = false };
        private ValueDataSeries _dnTrend = new("Down SuperTrend") { VisualType = VisualMode.Square, Color = DefaultColors.Red.Convert(), Width = 2 };
        private ValueDataSeries _upTrend = new("Up SuperTrend") { Color = DefaultColors.Blue.Convert(), Width = 2, VisualType = VisualMode.Square, ShowZeroValue = false };
        private readonly ValueDataSeries _squeezie = new("Squeeze Relaxer") { Color = MColors.Yellow, VisualType = VisualMode.Dots, Width = 3 };
        private readonly ValueDataSeries _nine21 = new("9 21 cross") { Color = MColor.FromArgb(255, 0, 255, 0), VisualType = VisualMode.Block, Width = 4 };
        private readonly ValueDataSeries _posSeries = new("Regular Buy Signal") { Color = MColor.FromArgb(255, 0, 255, 0), VisualType = VisualMode.Dots, Width = 3 };
        private readonly ValueDataSeries _negSeries = new("Regular Sell Signal") { Color = MColor.FromArgb(255, 255, 0, 0), VisualType = VisualMode.Dots, Width = 3 };
        private readonly ValueDataSeries _posWhite = new("Vol Imbalance Sell") { Color = MColors.White, VisualType = VisualMode.UpArrow, Width = 3 };
        private readonly ValueDataSeries _negWhite = new("Vol Imbalance Buy") { Color = MColors.White, VisualType = VisualMode.DownArrow, Width = 3 };

        // ========================================================================
        // ========================    COLORED CANDLES    =========================
        // ========================================================================
        private class candleColor : Collection<Entity>
        {
            public candleColor()
                : base(new[]
                {
                    new Entity { Value = 1, Name = "None" },
                    new Entity { Value = 2, Name = "Waddah Explosion" },
                    new Entity { Value = 3, Name = "Squeeze" },
                    new Entity { Value = 4, Name = "Delta" }
                })
            { }
        }
        [Display(Name = "Candle Color", GroupName = "Colored Candles")]
        [ComboBoxEditor(typeof(candleColor), DisplayMember = nameof(Entity.Name), ValueMember = nameof(Entity.Value))]
        public int canColor
        {
            get => CandleColoring; set { if (value < 0) return; CandleColoring = value; RecalculateValues(); }
        }

        [Display(GroupName = "Colored Candles", Name = "Show Reversal Patterns")]
        public bool ShowRevPattern { get => bShowRevPattern; set { bShowRevPattern = value; RecalculateValues(); } }
        [Display(GroupName = "Colored Candles", Name = "Waddah Sensitivity")]
        [Range(0, 9000)]
        public int WaddaSensitivity
        {
            get => iWaddaSensitivity; set { if (value < 0) return; iWaddaSensitivity = value; RecalculateValues(); }
        }

        // ========================================================================
        // ====================    EXTRA INDICATORS / ALERTS   ====================
        // ========================================================================

        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "UseAlerts")]
        public bool UseAlerts { get; set; }
        [Display(ResourceType = typeof(Resources), GroupName = "Alerts", Name = "AlertFile")]
        public string AlertFile { get; set; } = "alert1";

        [Display(GroupName = "Extras", Name = "Show Advanced Ideas")]
        public bool ShowBrooks { get => bAdvanced; set { bAdvanced = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Triple Supertrend")]
        public bool ShowTripleSupertrend { get => bShowTripleSupertrend; set { bShowTripleSupertrend = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show 9/21 EMA Cross")]
        public bool Show_9_21_EMA_Cross { get => bShow921; set { bShow921 = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Squeeze Relaxer")]
        public bool Show_Squeeze_Relax { get => bShowSqueeze; set { bShowSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Volume Imbalances", Description = "Show gaps between two candles, indicating market strength")] 
        public bool Use_VolumeImbalances { get => bVolumeImbalances; set { bVolumeImbalances = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Nebula Cloud", Description = "Show cloud containing KAMA 9 and 21")]
        public bool Use_Cloud { get => bShowCloud; set { bShowCloud = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Trampoline", Description = "Trampoline is the ultimate reversal indicator")]
        public bool Use_Tramp { get => bShowTramp; set { bShowTramp = value; RecalculateValues(); } }

        [Display(GroupName = "Extras", Name = "Wait Until Bar Close", Description = "Ensures that the candle is closed before displaying any data if you're on 2,000 ticks")]
        public bool Use_2kTick { get => bWaitTilClose; set { bWaitTilClose = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show Bollinger Wick", Description = "Show when price wicks the bollinger bands")]
        public bool Show_BB_Wick { get => bbShowBBBounce; set { bbShowBBBounce = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Show intensity alerts (IT)")]
        public bool Use_Intense { get => bShowIntense; set { bShowIntense = value; RecalculateValues(); } }
        [Display(GroupName = "Extras", Name = "Intensity alert threshold")]
        [Range(0, 90000)]
        public int BigTrades
        { get => iBigTrades; set { iBigTrades = value; RecalculateValues(); } }

        [Display(GroupName = "High Impact News", Name = "Show today's news")]
        public bool Show_News { get => bShowNews; set { bShowNews = value; RecalculateValues(); } }
        [Display(GroupName = "High Impact News", Name = "News font")]
        [Range(1, 900)]
        public int NewsFont
        { get => iNewsFont; set { iNewsFont = value; RecalculateValues(); } }
        [Display(GroupName = "High Impact News", Name = "Show PNL on screen")]
        public bool Show_PNL { get => bShowPNL; set { bShowPNL = value; RecalculateValues(); } }


        // ========================================================================
        // =======================    FILTER INDICATORS    ========================
        // ========================================================================

        [Display(GroupName = "Buy/Sell Filters", Name = "Waddah Explosion", Description = "The Waddah Explosion must be the correct color, and have a value")]
        public bool Use_Waddah_Explosion { get => bUseWaddah; set { bUseWaddah = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Awesome Oscillator", Description = "AO is positive or negative")]
        public bool Use_Awesome { get => bUseAO; set { bUseAO = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Parabolic SAR", Description = "The PSAR must be signaling a buy/sell signal same as the arrow")]
        public bool Use_PSAR { get => bUsePSAR; set { bUsePSAR = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Squeeze Momentum", Description = "The squeeze must be the correct color")]
        public bool Use_Squeeze_Momentum { get => bUseSqueeze; set { bUseSqueeze = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "MACD", Description = "Standard 12/26/9 MACD crossing in the correct direction")]
        public bool Use_MACD { get => bUseMACD; set { bUseMACD = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "SuperTrend", Description = "Price must align to the current SuperTrend trend")]
        public bool Use_SuperTrend { get => bUseSuperTrend; set { bUseSuperTrend = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "T3", Description = "Price must cross the T3")]
        public bool Use_T3 { get => bUseT3; set { bUseT3 = value; RecalculateValues(); } }
        [Display(GroupName = "Buy/Sell Filters", Name = "Fisher Transform", Description = "Fisher Transform must cross to the correct direction")]
        public bool Use_Fisher_Transform { get => bUseFisher; set { bUseFisher = value; RecalculateValues(); } }

        [Display(GroupName = "Custom EMA", Name = "Use Custom EMA", Description = "Price crosses your own EMA period")]
        public bool Use_Custom_EMA { get => bUseMyEMA; set { bUseMyEMA = value; RecalculateValues(); } }
        [Display(GroupName = "Custom EMA", Name = "Custom EMA Period", Description = "Price crosses your own EMA period")]
        [Range(1, 1000)]
        public int Custom_EMA_Period
        {
            get => iMyEMAPeriod;
            set
            {
                if (value < 1)
                    return;
                iMyEMAPeriod = _myEMA.Period = value;
                RecalculateValues();
            }
        }

        [Display(GroupName = "Kaufman Moving Avg", Name = "Use KAMA", Description = "Price crosses KAMA")]
        public bool Use_KAMA { get => bUseKAMA; set { bUseKAMA = value; RecalculateValues(); } }
        [Display(GroupName = "Kaufman Moving Avg", Name = "KAMA Period", Description = "Price crosses KAMA")]
        [Range(1, 1000)]
        public int Custom_KAMA_Period { get => iKAMAPeriod; set { if (value < 1) return; iKAMAPeriod = _kama9.EfficiencyRatioPeriod = value; RecalculateValues(); } }

        // ========================================================================
        // ========================    VALUE FILTERS    ===========================
        // ========================================================================

        [Display(GroupName = "Value Filters", Name = "Minimum Delta", Description = "The minimum candle delta value to show buy/sell")]
        [Range(0, 9000)]
        public int Min_Delta { get => iMinDelta; set { if (value < 0) return; iMinDelta = value; RecalculateValues(); } }

        [Display(GroupName = "Value Filters", Name = "Minimum Delta Percent", Description = "Minimum diff between max delta and delta to show buy/sell")]
        [Range(0, 100)]
        public int Min_Delta_Percent { get => iMinDeltaPercent; set { if (value < 0) return; iMinDeltaPercent = value; RecalculateValues(); } }

        [Display(GroupName = "Value Filters", Name = "Minimum ADX", Description = "Minimum ADX value before showing buy/sell")]
        [Range(0, 100)]
        public int Min_ADX { get => iMinADX; set { if (value < 0) return; iMinADX = value; RecalculateValues(); } }

        private decimal VolSec(IndicatorCandle c) { return c.Volume / Convert.ToDecimal((c.LastTime - c.Time).TotalSeconds); }

        // ========================================================================
        // =========================    MAIN LOGIC      ===========================
        // ========================================================================
        protected override void OnCalculate(int bar, decimal value)
        {
            var candle = GetCandle(bar);
            value = candle.Close;
            var chT = ChartInfo.ChartType;

            if (bar == 0)
            {
                DataSeries.ForEach(x => x.Clear());
                HorizontalLinesTillTouch.Clear();
                _lastBarCounted = false;
                return;
            }
            if (bar < 5)
                return;

            if (chT == "Tick" && bWaitTilClose && candle.Ticks < 1800 && int.Parse(ChartInfo.TimeFrame) == 2000)
                return;

            decimal _tick = ChartInfo.PriceChartContainer.Step;
            var red = candle.Close < candle.Open;
            var green = candle.Close > candle.Open;
            var p1C = GetCandle(bar - 1);
            var c1G = p1C.Open < p1C.Close;
            var c1R = p1C.Open > p1C.Close;

            if (bVolumeImbalances)
            {
                var highPen = new Pen(new SolidBrush(Color.RebeccaPurple)) { Width = 2 };
                if (green && c1G && candle.Open > p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(bar, candle.Open, highPen));
                    _negWhite[bar] = candle.Low - (_tick * 2);
                }
                if (red && c1R && candle.Open < p1C.Close)
                {
                    HorizontalLinesTillTouch.Add(new LineTillTouch(bar, candle.Open, highPen));
                    _posWhite[bar] = candle.High + (_tick * 2);
                }
            }

            bShowDown = true;
            bShowUp = true;

            var p2C = GetCandle(bar - 2);
            var p3C = GetCandle(bar - 3);
            var p4C = GetCandle(bar - 4);

            var c0G = candle.Open < candle.Close;
            var c0R = candle.Open > candle.Close;
            var c2G = p2C.Open < p2C.Close;
            var c2R = p2C.Open > p2C.Close;
            var c3G = p3C.Open < p3C.Close;
            var c3R = p3C.Open > p3C.Close;
            var c4G = p4C.Open < p4C.Close;
            var c4R = p4C.Open > p4C.Close;

            var c0Body = Math.Abs(candle.Close - candle.Open);
            var c1Body = Math.Abs(p1C.Close - p1C.Open);
            var c2Body = Math.Abs(p2C.Close - p2C.Open);
            var c3Body = Math.Abs(p3C.Close - p3C.Open);
            var c4Body = Math.Abs(p4C.Close - p4C.Open);

            var upWick50PerLarger = c0R && Math.Abs(candle.High - candle.Open) > Math.Abs(candle.Low - candle.Close);
            var downWick50PerLarger = c0G && Math.Abs(candle.Low - candle.Open) > Math.Abs(candle.Close - candle.High);

            var ThreeOutUp = c2R && c1G && c0G && p1C.Open < p2C.Close && p2C.Open < p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close > p1C.Low;

            var ThreeOutDown = c2G && c1R && c0R && p1C.Open > p2C.Close && p2C.Open > p1C.Close && Math.Abs(p1C.Open - p1C.Close) > Math.Abs(p2C.Open - p2C.Close) && candle.Close < p1C.Low;

            _myEMA.Calculate(bar, value);
            _t3.Calculate(bar, value);
            fastEma.Calculate(bar, value);
            slowEma.Calculate(bar, value);
            _9.Calculate(bar, value);
            _21.Calculate(bar, value);
            _macd.Calculate(bar, value);
            _bb.Calculate(bar, value);
            _rsi.Calculate(bar, value);

            var deltaPer = candle.Delta > 0 ? (candle.Delta / candle.MaxDelta) * 100 : (candle.Delta / candle.MinDelta) * 100;

            // ========================================================================
            // ========================    SERIES FETCH    ============================
            // ========================================================================

            var ao = ((ValueDataSeries)_ao.DataSeries[0])[bar];
            var kama9 = ((ValueDataSeries)_kama9.DataSeries[0])[bar];
            var kama21 = ((ValueDataSeries)_kama9.DataSeries[0])[bar];
            var m1 = ((ValueDataSeries)_macd.DataSeries[0])[bar];
            var m2 = ((ValueDataSeries)_macd.DataSeries[1])[bar];
            var m3 = ((ValueDataSeries)_macd.DataSeries[2])[bar];
            var t3 = ((ValueDataSeries)_t3.DataSeries[0])[bar];
            var fast = ((ValueDataSeries)fastEma.DataSeries[0])[bar];
            var fastM = ((ValueDataSeries)fastEma.DataSeries[0])[bar - 1];
            var slow = ((ValueDataSeries)slowEma.DataSeries[0])[bar];
            var slowM = ((ValueDataSeries)slowEma.DataSeries[0])[bar - 1];
            var sq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar];
            var sq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar];
            var psq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar - 1];
            var psq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar - 1];
            var ppsq1 = ((ValueDataSeries)_sq.DataSeries[0])[bar - 2];
            var ppsq2 = ((ValueDataSeries)_sq.DataSeries[1])[bar - 2];
            var f1 = ((ValueDataSeries)_ft.DataSeries[0])[bar];
            var f2 = ((ValueDataSeries)_ft.DataSeries[1])[bar];
            var stu1 = ((ValueDataSeries)_st1.DataSeries[0])[bar];
            var stu2 = ((ValueDataSeries)_st2.DataSeries[0])[bar];
            var stu3 = ((ValueDataSeries)_st3.DataSeries[0])[bar];
            var std1 = ((ValueDataSeries)_st1.DataSeries[1])[bar];
            var std2 = ((ValueDataSeries)_st2.DataSeries[1])[bar];
            var std3 = ((ValueDataSeries)_st3.DataSeries[1])[bar];
            var x = ((ValueDataSeries)_adx.DataSeries[0])[bar];
            var nn = ((ValueDataSeries)_9.DataSeries[0])[bar];
            var prev_nn = ((ValueDataSeries)_9.DataSeries[0])[bar - 1];
            var twone = ((ValueDataSeries)_21.DataSeries[0])[bar];
            var prev_twone = ((ValueDataSeries)_21.DataSeries[0])[bar - 1];
            var myema = ((ValueDataSeries)_myEMA.DataSeries[0])[bar];
            var psar = ((ValueDataSeries)_psar.DataSeries[0])[bar];
            var bb_mid = ((ValueDataSeries)_bb.DataSeries[0])[bar]; // mid
            var bb_top = ((ValueDataSeries)_bb.DataSeries[1])[bar]; // top
            var bb_bottom = ((ValueDataSeries)_bb.DataSeries[2])[bar]; // bottom
            var rsi = ((ValueDataSeries)_rsi.DataSeries[0])[bar];
            var rsi1 = ((ValueDataSeries)_rsi.DataSeries[0])[bar - 1];
            var rsi2 = ((ValueDataSeries)_rsi.DataSeries[0])[bar - 2];

            var eqHigh = c0R && c1R && c2G && c3G &&
                candle.Close < p1C.Close &&
                (p1C.Close == p2C.Open || p1C.Close == p2C.Open + _tick || p1C.Close + _tick == p2C.Open);

            var eqLow = c0G && c1G && c2R && c3R &&
                candle.Close > p1C.Close &&
                (p1C.Close == p2C.Open || p1C.Close == p2C.Open + _tick || p1C.Close + _tick == p2C.Open);

            var t1 = ((fast - slow) - (fastM - slowM)) * iWaddaSensitivity;

            var fisherUp = (f1 < f2);
            var fisherDown = (f2 < f1);
            var macdUp = (m1 > m2);
            var macdDown = (m1 < m2);
            var psarBuy = (psar < candle.Close);
            var psarSell = (psar > candle.Close);

            // ========================================================================
            // ====================    SHOW/OTHER CONDITIONS    =======================
            // ========================================================================

            if (c4Body > c3Body && c3Body > c2Body && c2Body > c1Body && c1Body > c0Body && bAdvanced)
                if ((candle.Close > p1C.Close && p1C.Close > p2C.Close && p2C.Close > p3C.Close) ||
                (candle.Close < p1C.Close && p1C.Close < p2C.Close && p2C.Close < p3C.Close))
                    DrawText(bar, "Stairs", Color.Yellow, Color.Transparent);

            if (deltaPer < iMinDeltaPercent)
            {
                bShowUp = false;
                bShowDown = false;
            }

            var atr = _atr[bar];
            var median = (candle.Low + candle.High) / 2;
            var dUpperLevel = median + atr * 1.7m;
            var dLowerLevel = median - atr * 1.7m;

            if (bShowTripleSupertrend)
            {
                if ((std1 != 0 && std2 != 0) || (std3 != 0 && std2 != 0) || (std3 != 0 && std1 != 0))
                    _dnTrend[bar] = dUpperLevel;
                else if ((stu1 != 0 && stu2 != 0) || (stu3 != 0 && stu2 != 0) || (stu1 != 0 && stu3 != 0))
                    _upTrend[bar] = dLowerLevel;
            }

            // Squeeze momentum relaxer show
            if (sq1 > 0 && sq1 < psq1 && psq1 > ppsq1 && bShowSqueeze)
                _squeezie[bar] = candle.High + _tick * 4;
            if (sq1 < 0 && sq1 > psq1 && psq1 < ppsq1 && bShowSqueeze)
                _squeezie[bar] = candle.Low - _tick * 4;

            // 9/21 cross show
            if (nn > twone && prev_nn <= prev_twone && bShow921)
                DrawText(bar, "X", Color.Yellow, Color.Transparent);
            if (nn < twone && prev_nn >= prev_twone && bShow921)
                DrawText(bar, "X", Color.Yellow, Color.Transparent);

            if (eqHigh && bAdvanced && candle.Close > 0)
                DrawText(bar - 1, "Equal\nHigh", Color.Lime, Color.Transparent, false, true);
            if (eqLow && bAdvanced && candle.Close > 0)
                DrawText(bar - 1, "Equal\nLow", Color.Yellow, Color.Transparent, false, true);

            if (c0G && c1R && c2R && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta < 0 && bAdvanced && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Yellow, Color.Transparent, false, true);
            if (c0R && c1G && c2G && VolSec(p1C) > VolSec(p2C) && VolSec(p2C) > VolSec(p3C) && candle.Delta > 0 && bAdvanced && candle.Close > 0)
                DrawText(bar, "Vol\nRev", Color.Lime, Color.Transparent, false, true);

            // ========================================================================
            // ========================    UP CONDITIONS    ===========================
            // ========================================================================

            if ((candle.Delta < iMinDelta) || (!macdUp && bUseMACD) || (psarSell && bUsePSAR) || (!fisherUp && bUseFisher) || (value < t3 && bUseT3) || (value < kama9 && bUseKAMA) || (value < myema && bUseMyEMA) || (t1 < 0 && bUseWaddah) || (ao < 0 && bUseAO) || (stu2 == 0 && bUseSuperTrend) || (sq1 < 0 && bUseSqueeze) || (x < iMinADX))
                bShowUp = false;

            if (green && bShowUp)
                _posSeries[bar] = candle.Low - (_tick * 2);

            // ========================================================================
            // ========================    DOWN CONDITIONS    =========================
            // ========================================================================

            if ((candle.Delta > (iMinDelta * -1)) || (psarBuy && bUsePSAR) || (!macdDown && bUseMACD) || (!fisherDown && bUseFisher) || (value > kama9 && bUseKAMA) || (value > t3 && bUseT3) || (value > myema && bUseMyEMA) || (t1 >= 0 && bUseWaddah) || (ao > 0 && bUseAO) || (std2 == 0 && bUseSuperTrend) || (sq1 > 0 && bUseSqueeze) || (x < iMinADX))
                bShowDown = false;

            if (red && bShowDown)
                _negSeries[bar] = candle.High + _tick * 2;

            if (_lastBar != bar)
            {
                if (_lastBarCounted && UseAlerts)
                {
                    if (bVolumeImbalances)
                        if ((green && c1G && candle.Open > p1C.Close) || (red && c1R && candle.Open < p1C.Close))
                            AddAlert(AlertFile, "Volume Imbalance");

                    if (bShowUp)
                        AddAlert(AlertFile, "BUY Signal");
                    else if (bShowDown)
                        AddAlert(AlertFile, "BUY Signal");
                }
                _lastBar = bar;
            }
            else
            {
                if (!_lastBarCounted)
                    _lastBarCounted = true;
            }

            var waddah = Math.Min(Math.Abs(t1) + 70, 255);
            if (canColor == 2) // (bWaddahCandles)
                _paintBars[bar] = t1 > 0 ? MColor.FromArgb(255, 0, (byte)waddah, 0) : MColor.FromArgb(255, (byte)waddah, 0, 0);

            var filteredSQ = Math.Min(Math.Abs(sq1 * 25), 255);
            if (canColor == 3) // (bSqueezeCandles)
                _paintBars[bar] = sq1 > 0 ? MColor.FromArgb(255, 0, (byte)filteredSQ, 0) : MColor.FromArgb(255, (byte)filteredSQ, 0, 0);

            var filteredDelta = Math.Min(Math.Abs(candle.Delta), 255);
            if (canColor == 4) // (bDeltaCandles)
                _paintBars[bar] = candle.Delta > 0 ? MColor.FromArgb(255, 0, (byte)filteredDelta, 0) : MColor.FromArgb(255, (byte)filteredDelta, 0, 0);

            // ========================================================================
            // =======================    REVERSAL PATTERNS    ========================
            // ========================================================================

            // Bollinger band bounce
            if (bbShowBBBounce)
            {
                if (candle.High > bb_top && candle.Open < bb_top && c0R && candle.Close < p1C.Close && upWick50PerLarger)
                    DrawText(bar, "Wick", Color.Lime, Color.Transparent, false, true);
                if (candle.Low < bb_bottom && candle.Open > bb_bottom && c0G && candle.Close > p1C.Close && downWick50PerLarger)
                    DrawText(bar, "Wick", Color.Orange, Color.Transparent, false, true);
            }

            // _paintBars[bar] = Colors.Yellow;
            if (ThreeOutUp && bShowRevPattern)
                DrawText(bar, "3oU", Color.Yellow, Color.Transparent);
            if (ThreeOutDown && bShowRevPattern)
                DrawText(bar, "3oD", Color.Yellow, Color.Transparent);

            // Nebula cloud
            if (bShowCloud)
                if (_kama9[bar] > _kama21[bar])
                {
                    _upCloud[bar].Upper = _kama9[bar];
                    _upCloud[bar].Lower = _kama21[bar];
                }
                else
                {
                    _dnCloud[bar].Upper = _kama21[bar];
                    _dnCloud[bar].Lower = _kama9[bar];
                }

            // Trampoline
            if (bShowTramp)
            {
                if (c0R && c1R && candle.Close < p1C.Close && (rsi >= 70 || rsi1 >= 70 || rsi2 >= 70) &&
                    c2G && p2C.High >= (bb_top - (_tick * 30)))
                    DrawText(bar, "TR", Color.Yellow, Color.BlueViolet);
                if (c0G && c1G && candle.Close > p1C.Close && (rsi < 25 || rsi1 < 25 || rsi2 < 25) &&
                    c2R && p2C.Low <= (bb_bottom + (_tick * 30)))
                    DrawText(bar - 2, "TR", Color.Yellow, Color.BlueViolet);
            }

            // Intensity signal
            var candleSeconds = Convert.ToDecimal((candle.LastTime - candle.Time).TotalSeconds);
            if (candleSeconds is 0) candleSeconds = 1;
            var volPerSecond = candle.Volume / candleSeconds;
            var deltaPer1 = candle.Delta > 0 ? (candle.Delta / candle.MaxDelta) : (candle.Delta / candle.MinDelta);
            var deltaIntense = Math.Abs((candle.Delta * deltaPer1) * (candle.Volume / candleSeconds));
            if (deltaIntense > iBigTrades && candle.Delta > 350 && bShowIntense) 
                DrawText(bar, "IT", Color.Yellow, Color.Green);
            else if (deltaIntense > iBigTrades && candle.Delta < 350 && bShowIntense)
                DrawText(bar, "IT", Color.Yellow, Color.Red);

            if (bShowAMDKama && false)
            {
                _kamanine.Colors[bar] = AMD(bar);
                _kamanine.Width = 2;
                _kamanine[bar] = kama9;
            }

            if (! bNewsProcessed && bShowNews)
                LoadStock(bar);



        }

    }
}
