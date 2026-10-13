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
using PositionSizerTool; // pure position-sizing math (PositionSizerMath, Direction)
#endregion

// Position Sizer RR — manual-trading helper for NinjaTrader 8.
// Drag two short lines (Entry + Stop) on the right of the chart; it computes how
// many contracts fit a fixed dollar risk and draws the 1R/2R/3R targets. The lines
// are drawn by the indicator and dragged via mouse events (no full-width lines, no
// drawing objects). It does NOT place any orders.
namespace NinjaTrader.NinjaScript.Indicators
{
    public class PositionSizerRR : Indicator
    {
        private const int   TargetCount = 3;    // fixed 1R / 2R / 3R
        private const float GrabPx      = 7f;    // vertical grab tolerance (device px)

        // Line prices (serialized -> survive reloads such as changing the risk $).
        private double entryPrice = 0;
        private double stopPrice  = 0;

        // Cached from OnRender for mouse hit-testing.
        private ChartScale lastScale;
        private float      lastLineLeft;
        private float      lastAxisX;

        private bool draggingEntry;
        private bool draggingStop;

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

                // Parámetros
                RiskDollars                 = 500;
                DefaultStopTicks            = 40;
                LabelFontSize               = 13;

                // Estética
                ShowPriceTags               = true;
                ShowSummaryBox              = false;
                LineLengthPct               = 35;

                // Colores
                EntryColor                  = Brushes.Goldenrod;
                StopColor                   = Brushes.Red;
                TargetColor                 = Brushes.LimeGreen;
                TextColor                   = Brushes.White;
            }
            else if (State == State.DataLoaded)
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        ChartControl.PreviewMouseLeftButtonDown += OnChartMouseDown;
                        ChartControl.MouseMove                  += OnChartMouseMove;
                        ChartControl.PreviewMouseLeftButtonUp   += OnChartMouseUp;
                    });
            }
            else if (State == State.Terminated)
            {
                if (ChartControl != null)
                    ChartControl.Dispatcher.InvokeAsync(() =>
                    {
                        ChartControl.PreviewMouseLeftButtonDown -= OnChartMouseDown;
                        ChartControl.MouseMove                  -= OnChartMouseMove;
                        ChartControl.PreviewMouseLeftButtonUp   -= OnChartMouseUp;
                    });
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 0)
                return;

            if (State == State.Historical && CurrentBar != Count - 1)
                return;

            // Seed default positions once; dragged/persisted values are kept.
            double tickSize = Instrument.MasterInstrument.TickSize;
            if (entryPrice <= 0)
                entryPrice = Instrument.MasterInstrument.RoundToTickSize(Close[0]);
            if (stopPrice <= 0)
                stopPrice = Instrument.MasterInstrument.RoundToTickSize(entryPrice - DefaultStopTicks * tickSize);
        }

        #region Mouse dragging

        private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ChartControl == null || ChartPanel == null || lastScale == null)
                return;

            Point p = e.GetPosition(ChartControl);
            float xPix = (float)ChartingExtensions.ConvertToHorizontalPixels(p.X, ChartControl.PresentationSource);
            float yPix = (float)ChartingExtensions.ConvertToVerticalPixels(p.Y, ChartControl.PresentationSource);

            // Only grab within the right region where the short lines live.
            if (xPix < lastLineLeft - 6f || xPix > lastAxisX + 6f)
                return;

            float yEntry = lastScale.GetYByValue(entryPrice);
            float yStop  = lastScale.GetYByValue(stopPrice);

            if (Math.Abs(yPix - yEntry) <= GrabPx)
            {
                draggingEntry = true;
                e.Handled = true;
            }
            else if (Math.Abs(yPix - yStop) <= GrabPx)
            {
                draggingStop = true;
                e.Handled = true;
            }
        }

        private void OnChartMouseMove(object sender, MouseEventArgs e)
        {
            if (!draggingEntry && !draggingStop)
                return;
            if (ChartControl == null || lastScale == null || Instrument == null)
                return;

            Point p = e.GetPosition(ChartControl);
            float yPix = (float)ChartingExtensions.ConvertToVerticalPixels(p.Y, ChartControl.PresentationSource);
            double price = Instrument.MasterInstrument.RoundToTickSize(lastScale.GetValueByY(yPix));

            bool changed;
            if (draggingEntry)
            {
                changed = price != entryPrice;
                entryPrice = price;
            }
            else
            {
                changed = price != stopPrice;
                stopPrice = price;
            }

            e.Handled = true;

            // Repaint only when the (tick-rounded) price actually changed. InvalidateVisual
            // just repaints (no recalc, no throttle) so the drag follows the cursor smoothly.
            if (changed)
                ChartControl.InvalidateVisual();
        }

        private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (draggingEntry || draggingStop)
            {
                draggingEntry = false;
                draggingStop  = false;
                e.Handled = true;
            }
        }

        #endregion

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (chartControl == null || chartScale == null || ChartPanel == null || Instrument == null)
                return;
            if (entryPrice <= 0 || stopPrice <= 0)
                return;

            double entry = entryPrice;
            double stop  = stopPrice;

            double pointValue = Instrument.MasterInstrument.PointValue;
            double tickSize   = Instrument.MasterInstrument.TickSize;

            double riskPerContract = PositionSizerMath.RiskPerContract(entry, stop, pointValue);
            int    contracts       = PositionSizerMath.Contracts(RiskDollars, riskPerContract);
            double actualRisk      = PositionSizerMath.ActualRisk(contracts, riskPerContract);
            double distTicks       = PositionSizerMath.DistanceTicks(entry, stop, tickSize);
            double distPts         = Math.Abs(entry - stop);
            Direction dir          = PositionSizerMath.Dir(entry, stop);
            bool tradable          = dir != Direction.Flat && contracts > 0;

            float xLeft  = ChartPanel.X;
            float axisX  = ChartPanel.X + ChartPanel.W;
            float tagW   = 72f;
            float tagH   = (float)LabelFontSize + 8f;
            float lineRight  = axisX - tagW - 2f;
            float labelRight = axisX - tagW - 8f;
            float lineLen    = (float)(ChartPanel.W * (LineLengthPct / 100.0));
            float lineLeft   = Math.Max(xLeft, lineRight - lineLen);

            // Cache for mouse hit-testing.
            lastScale    = chartScale;
            lastLineLeft = lineLeft;
            lastAxisX    = axisX;

            SharpDX.Direct2D1.Brush entryDX  = EntryColor.ToDxBrush(RenderTarget);
            SharpDX.Direct2D1.Brush stopDX   = StopColor.ToDxBrush(RenderTarget);
            SharpDX.Direct2D1.Brush targetDX = TargetColor.ToDxBrush(RenderTarget);
            SharpDX.Direct2D1.Brush textDX   = TextColor.ToDxBrush(RenderTarget);
            SharpDX.Direct2D1.SolidColorBrush blackDX = new SharpDX.Direct2D1.SolidColorBrush(RenderTarget, new SharpDX.Color4(0f, 0f, 0f, 1f));
            SharpDX.DirectWrite.TextFormat tf = new SimpleFont("Arial", (int)LabelFontSize).ToDirectWriteTextFormat();

            try
            {
                // Entry line (0R)
                float yE = chartScale.GetYByValue(entry);
                RenderTarget.DrawLine(new SharpDX.Vector2(lineLeft, yE), new SharpDX.Vector2(lineRight, yE), entryDX, 1.6f);
                string entryLabel;
                if (dir == Direction.Flat)   entryLabel = "0R  ·  ajustá el stop";
                else if (contracts <= 0)     entryLabel = "0R  ·  0 contratos";
                else                         entryLabel = string.Format("0R  ·  {0} contratos", contracts);
                DrawRightAlignedTextAbove(entryLabel, labelRight, yE, tf, entryDX);
                if (ShowPriceTags)
                    DrawPriceTag(axisX, yE, tagW, tagH, entryDX, FormatPrice(entry), tf, blackDX);

                // Stop line (-1R)
                float yS = chartScale.GetYByValue(stop);
                RenderTarget.DrawLine(new SharpDX.Vector2(lineLeft, yS), new SharpDX.Vector2(lineRight, yS), stopDX, 1.6f);
                string stopLabel;
                if (!tradable && dir != Direction.Flat && contracts <= 0) stopLabel = "STOP  ·  subí riesgo o achicá SL";
                else if (tradable)                                        stopLabel = string.Format("-1R  ·  STOP  -${0:N2}", actualRisk);
                else                                                      stopLabel = "STOP";
                DrawRightAlignedTextAbove(stopLabel, labelRight, yS, tf, stopDX);
                if (ShowPriceTags)
                    DrawPriceTag(axisX, yS, tagW, tagH, stopDX, FormatPrice(stop), tf, blackDX);

                // Target lines (1R / 2R / 3R)
                if (tradable)
                {
                    for (int n = 1; n <= TargetCount; n++)
                    {
                        double tp = PositionSizerMath.RoundToTick(PositionSizerMath.TargetPrice(entry, stop, n), tickSize);
                        float y = chartScale.GetYByValue(tp);
                        RenderTarget.DrawLine(new SharpDX.Vector2(lineLeft, y), new SharpDX.Vector2(lineRight, y), targetDX, 1.6f);

                        double profit = PositionSizerMath.TargetProfit(contracts, riskPerContract, n);
                        DrawRightAlignedTextAbove(string.Format("{0}R  +${1:N2}", n, profit), labelRight, y, tf, targetDX);
                        if (ShowPriceTags)
                            DrawPriceTag(axisX, y, tagW, tagH, targetDX, FormatPrice(tp), tf, blackDX);
                    }
                }

                // Optional summary box
                if (ShowSummaryBox)
                {
                    List<string> info = new List<string>();
                    if (dir == Direction.Flat)
                        info.Add("Ajustá el stop (entrada = stop)");
                    else if (RiskDollars <= 0)
                        info.Add("Poné un riesgo > 0");
                    else if (contracts == 0)
                        info.Add(string.Format("Riesgo/contrato ${0:N2} > riesgo ${1:N2}", riskPerContract, RiskDollars));
                    else
                    {
                        info.Add(string.Format("Contratos: {0}", contracts));
                        info.Add(string.Format("Riesgo: ${0:N2}  ({1:N0} ticks / {2:0.##} pts)", actualRisk, distTicks, distPts));
                        info.Add(string.Format("Dir: {0}", dir == Direction.Long ? "LONG" : "SHORT"));
                    }
                    DrawInfoBox(info, tf, textDX);
                }
            }
            finally
            {
                entryDX.Dispose();
                stopDX.Dispose();
                targetDX.Dispose();
                textDX.Dispose();
                blackDX.Dispose();
                tf.Dispose();
            }
        }

        private string FormatPrice(double price)
        {
            return Instrument.MasterInstrument.FormatPrice(price);
        }

        private void DrawPriceTag(float axisX, float y, float tagW, float tagH, SharpDX.Direct2D1.Brush fill, string text, SharpDX.DirectWrite.TextFormat tf, SharpDX.Direct2D1.Brush textBrush)
        {
            float rx = axisX - tagW;
            float ry = y - tagH / 2f;
            RenderTarget.FillRectangle(new SharpDX.RectangleF(rx, ry, tagW, tagH), fill);

            SharpDX.DirectWrite.TextLayout tl = new SharpDX.DirectWrite.TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, text, tf, tagW, tagH);
            float tx = rx + Math.Max(0f, (tagW - tl.Metrics.Width) / 2f);
            float ty = ry + Math.Max(0f, (tagH - tl.Metrics.Height) / 2f);
            RenderTarget.DrawTextLayout(new SharpDX.Vector2(tx, ty), tl, textBrush);
            tl.Dispose();
        }

        // Text whose right edge sits at rightX, resting slightly above the line at lineY.
        private void DrawRightAlignedTextAbove(string text, float rightX, float lineY, SharpDX.DirectWrite.TextFormat tf, SharpDX.Direct2D1.Brush brush)
        {
            SharpDX.DirectWrite.TextLayout tl = new SharpDX.DirectWrite.TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, text, tf, 600f, tf.FontSize * 2f);
            float x = rightX - tl.Metrics.Width;
            float y = lineY - tl.Metrics.Height - 2f;
            RenderTarget.DrawTextLayout(new SharpDX.Vector2(x, y), tl, brush);
            tl.Dispose();
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

            float pad   = 8f;
            float lineH = (float)LabelFontSize + 6f;
            float boxW  = 250f;
            float boxH  = pad * 2f + lineH * lines.Count;
            float x0    = ChartPanel.X + 12f;
            float y0    = ChartPanel.Y + 12f;

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

        [NinjaScriptProperty]
        [Display(Name = "Mostrar tags de precio", Order = 1, GroupName = "Estética")]
        public bool ShowPriceTags { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Mostrar caja resumen", Order = 2, GroupName = "Estética")]
        public bool ShowSummaryBox { get; set; }

        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Largo líneas (% ancho)", Description = "Qué parte del ancho (desde la derecha) ocupan las líneas.", Order = 3, GroupName = "Estética")]
        public double LineLengthPct { get; set; }

        [Browsable(false)]
        public double EntryPriceState { get { return entryPrice; } set { entryPrice = value; } }

        [Browsable(false)]
        public double StopPriceState { get { return stopPrice; } set { stopPrice = value; } }

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
