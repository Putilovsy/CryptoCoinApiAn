using CryptoMonitor.Models;
using CryptoMonitor.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CryptoMonitor.Helpers;
using System.Windows;

namespace CryptoMonitor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CryptoApiService _service = new();

        public ObservableCollection<CryptoCoin> Coins { get; set; } = new();

        private CryptoCoin _selectedCoin;
        public CryptoCoin SelectedCoin
        {
            get => _selectedCoin;
            set
            {
                _selectedCoin = value;
                OnPropertyChanged();
                LoadHistoryCommand.Execute(null);
            }
        }

        private List<PricePoint> _history;
        public List<PricePoint> History
        {
            get => _history;
            set
            {
                _history = value;
                OnPropertyChanged();
            }
        }

        private List<OhlcPoint> _ohlc;
        public List<OhlcPoint> Ohlc
        {
            get => _ohlc;
            set
            {
                _ohlc = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadCoinsCommand { get; }
        public ICommand LoadHistoryCommand { get; }
        public ICommand OpenAnalysisCommand { get; }

        private CancellationTokenSource _cts;

        public MainViewModel()
        {
            LoadCoinsCommand = new RelayCommand(async _ => await LoadCoins());
            LoadHistoryCommand = new RelayCommand(async _ => await LoadHistory());
            OpenAnalysisCommand = new RelayCommand(async _ => await OpenAnalysis());
        }

        public async Task LoadCoins()
        {
            var coins = await _service.GetTopCoinsAsync(CancellationToken.None);

            Coins.Clear();
            foreach (var coin in coins)
                Coins.Add(coin);
        }

        private async Task LoadHistory()
        {
            if (SelectedCoin == null)
                return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Delay(400, _cts.Token);

                History = await _service.GetCoinHistoryAsync(
                    SelectedCoin.Id,
                    _cts.Token,
                    7);

                Ohlc = await _service.GetOhlcAsync(
                    SelectedCoin.Id,
                    _cts.Token,
                    7);
            }
            catch (TaskCanceledException) { }
        }

        private async Task OpenAnalysis()
        {
            if (History == null || History.Count == 0)
                return;

            var window = new AnalysisWindow(History);
            window.ShowDialog();
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}