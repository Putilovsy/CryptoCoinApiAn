using CryptoMonitor.Models;
using CryptoMonitor.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CryptoMonitor
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            Loaded += async (_, _) => await _vm.LoadCoins();

           

            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "History")
                {
                    DrawChart(_vm.History);
                }

                if (e.PropertyName == "Ohlc")
                {
                    DrawCandles(_vm.Ohlc);
                }
            };
        }

        private void DrawChart(List<PricePoint> history)
        {
            double[] prices = history.Select(h => h.Price).ToArray();
            double[] times = history
                .Select(h => h.Time.ToOADate())
                .ToArray();
            var plt = CryptoPlot.Plot;

            plt.Clear();

            plt.Add.Scatter(times, prices);

            plt.Axes.DateTimeTicksBottom();
            plt.Axes.AutoScale();

            plt.Title("Price");
            plt.YLabel("USD");
            plt.XLabel("Time");

            CryptoPlot.Refresh();

        }

        private void DrawCandles(List<OhlcPoint> ohlc)
        {
            if (ohlc == null || ohlc.Count == 0)
                return;

            var plt = CryptoPlot.Plot;
            plt.Clear();

            var candles = ohlc.Select(x => new ScottPlot.OHLC(
                open: x.Open,
                high: x.High,
                low: x.Low,
                close: x.Close,
                start: x.Time,
                span: TimeSpan.FromMinutes(60)
            )).ToArray();

            plt.Add.Candlestick(candles);

            plt.Axes.DateTimeTicksBottom();
            plt.Axes.AutoScale();

            plt.Title("Candlestick Chart");
            plt.YLabel("USD");
            plt.XLabel("Time");

            CryptoPlot.Refresh();
        }
        private void ChartTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm.History == null || _vm.History.Count == 0)
                return;

            var selected = (ChartTypeCombo.SelectedItem as ComboBoxItem)?.Content.ToString();

            switch (selected)
            {
                case "Line":
                    DrawChart(_vm.History);
                    break;

                case "Candlestick":
                    if (_vm.Ohlc != null && _vm.Ohlc.Count > 0)
                        DrawCandles(_vm.Ohlc);
                    break;
            }
        }
    }
}