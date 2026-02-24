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
            var plt = AnalysisPlot.Plot;
            plt.Clear();

            double[] prices = history.Select(x => x.Price).ToArray();
            double[] times = history.Select(x => x.Time.ToOADate()).ToArray();

            plt.Add.Scatter(times, prices);

            double[] movingAverage = MovingAverage(prices, 10);
            plt.Add.Scatter(times.Skip(9).ToArray(), movingAverage);

            plt.Axes.DateTimeTicksBottom();
            plt.Axes.AutoScale();

            AnalysisPlot.Refresh();
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
    }
}
