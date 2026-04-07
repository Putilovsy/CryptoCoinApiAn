using CryptoMonitor.Models;
using CryptoMonitor.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace CryptoMonitor
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private bool _isCandlestick = false;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            Loaded += async (_, _) => await _vm.LoadCoins();

            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "History" && !_isCandlestick)
                {
                    DrawChart(_vm.History);
                }
                else if (e.PropertyName == "Ohlc" && _isCandlestick)
                {
                    DrawCandles(_vm.Ohlc);
                }
            };
        }

        private void ApplyDarkStyle(ScottPlot.Plot plt)
        {
            var panelDark = ScottPlot.Color.FromHex("#1C2541");
            var bgDark = ScottPlot.Color.FromHex("#0B132B");
            var textMuted = ScottPlot.Color.FromHex("#8D99AE");
            var borderColor = ScottPlot.Color.FromHex("#2B3655");

            plt.FigureBackground.Color = panelDark;

            plt.DataBackground.Color = bgDark;
            
            plt.Axes.Color(textMuted);

            plt.Grid.MajorLineColor = borderColor;
        }

        private void DrawChart(List<PricePoint> history)
        {
            double[] prices = history.Select(h => h.Price).ToArray();
            double[] times = history
                .Select(h => h.Time.ToOADate())
                .ToArray();

            var plt = CryptoPlot.Plot;
            plt.Clear();

            var scatter = plt.Add.Scatter(times, prices);

            scatter.Color = ScottPlot.Color.FromHex("#5BC0BE");

            plt.Axes.DateTimeTicksBottom();
            plt.Axes.AutoScale();

            plt.Title("Price");
            plt.YLabel("USD");
            plt.XLabel("Time");

            ApplyDarkStyle(plt);

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
                span: _vm.CurrentOhlcSpan
            )).ToArray();

            plt.Add.Candlestick(candles);

            plt.Axes.DateTimeTicksBottom();
            plt.Axes.AutoScale();

            plt.Title("Candlestick Chart");
            plt.YLabel("USD");
            plt.XLabel("Time");

            ApplyDarkStyle(plt);

            CryptoPlot.Refresh();
        }

        private void ToggleChartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.History == null || _vm.History.Count == 0)
                return;

            _isCandlestick = !_isCandlestick; // Меняем состояние

            if (_isCandlestick)
            {
                ToggleChartBtn.Content = "📈 В линию"; // Меняем текст кнопки для возврата
                if (_vm.Ohlc != null && _vm.Ohlc.Count > 0)
                    DrawCandles(_vm.Ohlc);
            }
            else
            {
                ToggleChartBtn.Content = "📊 В свечи"; // Меняем текст кнопки для возврата
                DrawChart(_vm.History);
            }
        }
    }
}