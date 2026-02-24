using CryptoMonitor.Models;
using CryptoMonitor.Services;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ScottPlot;


namespace CryptoMonitor
{
    public partial class MainWindow : Window
    {
        private readonly CryptoApiService _service = new CryptoApiService();
        private List<PricePoint> _currentHistory;

        public async Task LoadCoins()
        {
            try
            {
                var coins = await _service.GetTopCoinsAsync();
                CoinsTable.ItemsSource = coins;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += async (_, _) => await LoadCoins();

        }

        private void DrawChart(List<PricePoint> history)
        {
            double[] prices = history.Select(h => h.Price).ToArray();
            double[] times = history
                .Select(h => h.Time.ToOADate())
                .ToArray();
            //double[] prices = new double[] { 15, 4, 10, 8};
            //double[] times = new double[] { 1, 2, 3, 4 };
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

        private async void CoinsTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CoinsTable.SelectedItem == null) return;

            var coin = CoinsTable.SelectedItem as CryptoCoin;

            if(coin == null) return;

            try
            {
                var history = await _service.GetCoinHistoryAsync(coin.Id, 7);
                _currentHistory = history;
                DrawChart(history);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка загрузки истории", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void OpenAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistory == null) return;

            var AnalysisWindow = new AnalysisWindow(_currentHistory);
            AnalysisWindow.Owner = this;
            AnalysisWindow.ShowDialog();
        }
    }
        
}