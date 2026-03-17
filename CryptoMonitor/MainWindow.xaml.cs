using CryptoMonitor.Models;
using CryptoMonitor.ViewModels;
using System.Windows;

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
    }
}