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
        private CancellationTokenSource _historyCts;

        public async Task LoadCoins()
        {
            try
            {
                var coins = await _service.GetTopCoinsAsync(CancellationToken.None);
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
            if (CoinsTable.SelectedItem is not CryptoCoin coin)
                return;

            _historyCts?.Cancel();
            _historyCts = new CancellationTokenSource();

            try
            {
                await Task.Delay(300, _historyCts.Token);

                var history = await _service.GetCoinHistoryAsync( coin.Id, _historyCts.Token, 7);

                _currentHistory = history;
                DrawChart(history);
            }
            catch (TaskCanceledException)
            {
                // запрос отменён
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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