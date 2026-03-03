using CryptoMonitor.Models;
using ScottPlot;
using System.Windows;
using System.Xml.Serialization;

namespace CryptoMonitor
{
    public partial class AnalysisWindow : Window
    {
        public AnalysisWindow(List<PricePoint> history)
        {
            InitializeComponent();
            DrawAnalysis(history);
        }

        private void DrawAnalysis(List<PricePoint> history)
        {
            double[] prices = history.Select(x => x.Price).ToArray();
            double[] times = history.Select(x => x.Time.ToOADate()).ToArray();

            // ====== PRICE GRAPH ======
            var pricePlot = PricePlot.Plot;
            pricePlot.Clear();

            pricePlot.Add.Scatter(times, prices);

            var ema = ExponentialMovingAverage(prices, 14);
            pricePlot.Add.Scatter(times, ema);

            pricePlot.Axes.DateTimeTicksBottom();
            pricePlot.Axes.AutoScale();
            pricePlot.Title("Price + EMA");

            PricePlot.Refresh();

            // ====== RSI GRAPH ======
            var rsiPlot = RsiPlot.Plot;
            rsiPlot.Clear();

            var rsi = CalculateRSI(prices, 14);
            rsiPlot.Add.Scatter(times, rsi);

            rsiPlot.Axes.SetLimits(null, null, 0, 100);
            rsiPlot.Title("RSI (14)");

            RsiPlot.Refresh();
        }

        private double[] MovingAverage(double[] prices, int period)
        {
            var result = new List<double>();

            for (int i = 0; i < prices.Length - period; i++)
            {
                result.Add(prices.Skip(i).Take(period).Average());
            }
            return result.ToArray();
        }

        private double[] ExponentialMovingAverage(double [] prices, int period)
        {
            double[] ema = new double[prices.Length];

            double multiplier = 2.0 / (period + 1);

            ema[0] = prices[0];

            for (int i = 1; i < prices.Length; i++)
            {
                ema[i] = ((prices[i] - ema[i - 1]) * multiplier) + ema[i - 1];
            }

            return ema;
        }

        private double[] CalculateRSI(double[] prices, int period)
        {
            double[] rsi = new double[prices.Length];
            double gain = 0, loss = 0;

            for (int i = 1; i <= period; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }

            gain /= period;
            loss /= period;

            for (int i = period + 1; i < prices.Length; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0)
                {
                    gain = (gain * (period - 1) + change) / period;
                    loss = (loss * (period - 1)) / period;
                }
                else
                {
                    gain = (gain * (period - 1)) / period;
                    loss = (loss * (period - 1) - change) / period;
                }
                double rs = loss == 0 ? 0 : gain / loss;
                rsi[i] = 100 - (100 / (1 + rs));
            }
            return rsi;
        }
    }
}
