#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
using PositionSizerTool; // pure position-sizing math (PositionSizerMath, Direction)
#endregion

// Position Sizer RR — manual-trading helper for NinjaTrader 8.
// You drag two lines (Entry + Stop); it computes how many contracts fit a fixed
// dollar risk and draws the 1R / 2R / 3R targets. It does NOT place any orders.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class PositionSizerRR : Indicator
    {
        private const string EntryTag = "PSRR_Entry";
        private const string StopTag  = "PSRR_Stop";
        private const int    TargetCount = 3; // fixed 1R / 2R / 3R

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                 = "Position sizer con R-múltiplos (1R/2R/3R) para operar manual. Calcula contratos por riesgo fijo en $. No coloca órdenes.";
                Name                        = "PositionSizerRR";
                Calculate                   = Calculate.OnEachTick;
                IsOverlay                   = true;
                DisplayInDataBox            = false;
                DrawOnPricePanel            = true;
                IsChartOnly                 = true;
                PaintPriceMarkers           = false;
                IsSuspendedWhileInactive    = true;

                // Defaults (acordados en la spec)
                RiskDollars                 = 500;
                DefaultStopTicks            = 40;
                LabelFontSize               = 13;
                EntryColor                  = Brushes.Goldenrod;
                StopColor                   = Brushes.Red;
                TargetColor                 = Brushes.LimeGreen;
                TextColor                   = Brushes.White;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 0)
                return;

            // Create the lines once we have the most recent price (last historical bar
            // and every realtime tick). On non-last historical bars, do nothing.
            if (State == State.Historical && CurrentBar != Count - 1)
                return;

            EnsureLines();
        }

        // Create the Entry/Stop lines if they are missing (first load, or user deleted one).
        private void EnsureLines()
        {
            double tickSize = Instrument.MasterInstrument.TickSize;

            if (DrawObjects[EntryTag] == null)
            {
                double entryDefault = Instrument.MasterInstrument.RoundToTickSize(Close[0]);
                Draw.HorizontalLine(this, EntryTag, entryDefault, EntryColor);
            }

            if (DrawObjects[StopTag] == null)
            {
                double entryNow   = GetLinePrice(EntryTag, Close[0]);
                double stopDefault = Instrument.MasterInstrument.RoundToTickSize(entryNow - DefaultStopTicks * tickSize);
                Draw.HorizontalLine(this, StopTag, stopDefault, StopColor);
            }
        }

        private double GetLinePrice(string tag, double fallback)
        {
            HorizontalLine hl = DrawObjects[tag] as HorizontalLine;
            if (hl != null && hl.StartAnchor != null)
                return hl.StartAnchor.Price;
            return fallback;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (chartControl == null || chartScale == null || ChartPanel == null || Instrument == null)
                return;

            HorizontalLine entryLine = DrawObjects[EntryTag] as HorizontalLine;
            HorizontalLine stopLine  = DrawObjects[StopTag]  as HorizontalLine;
            if (entryLine == null || stopLine == null || entryLine.StartAnchor == null || stopLine.StartAnchor == null)
                return;

            double entry = entryLine.StartAnchor.Price;
            double stop  = stopLine.StartAnchor.Price;

            double pointValue = Instrument.MasterInstrument.PointValue;
            double tickSize   = Instrument.MasterInstrument.TickSize;

            double riskPerContract = PositionSizerMath.RiskPerContract(entry, stop, pointValue);
            int    contracts       = PositionSizerMath.Contracts(RiskDollars, riskPerContract);
            double actualRisk      = PositionSizerMath.ActualRisk(contracts, riskPerContract);
            double distTicks       = PositionSizerMath.DistanceTicks(entry, stop, tickSize);
            double distPts         = Math.Abs(entry - stop);
            Direction dir          = PositionSizerMath.Dir(entry, stop);

            float xLeft  = ChartPanel.X;
            float xRight = ChartPanel.X + ChartPanel.W;
            float labelX = Math.Max(xLeft + 4f, xRight - 170f);

            // Device-dependent resources — created per render, disposed at the end.
            SharpDX.Direct2D1.Brush targetBrush = TargetColor.ToDxBrush(RenderTarget);
            SharpDX.Direct2D1.Brush textBrush   = TextColor.ToDxBrush(RenderTarget);
            SharpDX.DirectWrite.TextFormat textFormat = new SimpleFont("Arial", (int)LabelFontSize).ToDirectWriteTextFormat();

            try
            {
                bool tradable = dir != Direction.Flat && contracts > 0;

                // R-multiple target lines + profit labels
                if (tradable)
                {
                    for (int n = 1; n <= TargetCount; n++)
                    {
                        double tp = PositionSizerMath.RoundToTick(PositionSizerMath.TargetPrice(entry, stop, n), tickSize);
                        float y = chartScale.GetYByValue(tp);
                        RenderTarget.DrawLine(new SharpDX.Vector2(xLeft, y), new SharpDX.Vector2(xRight, y), targetBrush, 1.5f);

                        double profit = PositionSizerMath.TargetProfit(contracts, riskPerContract, n);
                        DrawLabel(string.Format("{0}R  +${1:N2}", n, profit), labelX, y - LabelFontSize - 2f, textFormat, textBrush);
                    }
                }

                // Entry / Stop tags (their lines are drawn by NinjaTrader itself)
                DrawLabel("ENTRADA", labelX, chartScale.GetYByValue(entry) - LabelFontSize - 2f, textFormat, textBrush);
                DrawLabel("STOP",    labelX, chartScale.GetYByValue(stop) + 2f,                 textFormat, textBrush);

                // Summary box
                List<string> info = new List<string>();
                if (dir == Direction.Flat)
                {
                    info.Add("Ajustá el stop (entrada = stop)");
                }
                else if (RiskDollars <= 0)
                {
                    info.Add("Poné un riesgo > 0");
                }
                else if (contracts == 0)
                {
                    info.Add(string.Format("Riesgo/contrato ${0:N2} > riesgo ${1:N2}", riskPerContract, RiskDollars));
                    info.Add("Achicá el stop o subí el riesgo");
                }
                else
                {
                    info.Add(string.Format("Contratos: {0}", contracts));
                    info.Add(string.Format("Riesgo: ${0:N2}  ({1:N0} ticks / {2:0.##} pts)", actualRisk, distTicks, distPts));
                    info.Add(string.Format("Dir: {0}", dir == Direction.Long ? "LONG" : "SHORT"));
                }
                DrawInfoBox(info, textFormat, textBrush);
            }
            finally
            {
                targetBrush.Dispose();
                textBrush.Dispose();
                textFormat.Dispose();
            }
        }

        private void DrawLabel(string text, float x, float y, SharpDX.DirectWrite.TextFormat tf, SharpDX.Direct2D1.Brush brush)
        {
            SharpDX.DirectWrite.TextLayout tl = new SharpDX.DirectWrite.TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, text, tf, 300f, tf.FontSize * 2f);
            RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), tl, brush);
            tl.Dispose();
        }

        private void DrawInfoBox(List<string> lines, SharpDX.DirectWrite.TextFormat tf, SharpDX.Direct2D1.Brush textBrush)
        {
            if (lines.Count == 0)
                return;

            float pad    = 8f;
            float lineH  = (float)LabelFontSize + 6f;
            float boxW   = 250f;
            float boxH   = pad * 2f + lineH * lines.Count;
            float x0     = ChartPanel.X + 12f;
            float y0     = ChartPanel.Y + 12f;

            SharpDX.Direct2D1.SolidColorBrush bg =
                new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0f, 0f, 0f, 0.6f));
            RenderTarget.FillRectangle(new SharpDX.RectangleF(x0, y0, boxW, boxH), bg);
            bg.Dispose();

            for (int i = 0; i < lines.Count; i++)
                DrawLabel(lines[i], x0 + pad, y0 + pad + i * lineH, tf, textBrush);
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "Riesgo ($)", Description = "Monto fijo a arriesgar en dólares.", Order = 1, GroupName = "Parámetros")]
        public double RiskDollars { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Distancia inicial del stop (ticks)", Description = "Dónde arranca la línea de stop respecto de la entrada al cargar.", Order = 2, GroupName = "Parámetros")]
        public int DefaultStopTicks { get; set; }

        [NinjaScriptProperty]
        [Range(6, 40)]
        [Display(Name = "Tamaño texto", Order = 3, GroupName = "Parámetros")]
        public float LabelFontSize { get; set; }

        [XmlIgnore]
        [Display(Name = "Color entrada", Order = 1, GroupName = "Colores")]
        public Brush EntryColor { get; set; }

        [Browsable(false)]
        public string EntryColorSerializable
        {
            get { return NinjaTrader.Gui.Serialize.BrushToString(EntryColor); }
            set { EntryColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color stop", Order = 2, GroupName = "Colores")]
        public Brush StopColor { get; set; }

        [Browsable(false)]
        public string StopColorSerializable
        {
            get { return NinjaTrader.Gui.Serialize.BrushToString(StopColor); }
            set { StopColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color objetivos", Order = 3, GroupName = "Colores")]
        public Brush TargetColor { get; set; }

        [Browsable(false)]
        public string TargetColorSerializable
        {
            get { return NinjaTrader.Gui.Serialize.BrushToString(TargetColor); }
            set { TargetColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Color texto", Order = 4, GroupName = "Colores")]
        public Brush TextColor { get; set; }

        [Browsable(false)]
        public string TextColorSerializable
        {
            get { return NinjaTrader.Gui.Serialize.BrushToString(TextColor); }
            set { TextColor = NinjaTrader.Gui.Serialize.StringToBrush(value); }
        }

        #endregion
    }
}
