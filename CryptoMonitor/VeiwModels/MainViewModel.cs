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
        private readonly Dictionary<string, (List<PricePoint> History, List<OhlcPoint> Ohlc)> _dataCache = new();
        private string _currentCoinId = "";
        private CancellationTokenSource _cts;
        private bool _isPreloading = false;

        public ObservableCollection<CryptoCoin> Coins { get; set; } = new();

        private CryptoCoin _selectedCoin;
        public CryptoCoin SelectedCoin
        {
            get => _selectedCoin;
            set
            {
                if (_selectedCoin?.Id == value?.Id)
                    return;

                _selectedCoin = value;
                OnPropertyChanged();

                if (value != null)
                {
                    // Мгновенно показываем данные из кэша если есть
                    if (_dataCache.TryGetValue(value.Id, out var cached))
                    {
                        History = cached.History;
                        Ohlc = cached.Ohlc;
                        System.Diagnostics.Debug.WriteLine($"Данные из кэша для {value.Id}: History={History?.Count}, Ohlc={Ohlc?.Count}");
                    }
                    else
                    {
                        // Если данных нет, загружаем
                        System.Diagnostics.Debug.WriteLine($"Нет данных в кэше для {value.Id}, загружаем");
                        _ = LoadHistoryForCoin(value.Id);
                    }
                }
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
                System.Diagnostics.Debug.WriteLine($"History обновлен: {(value?.Count ?? 0)} точек");
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
                System.Diagnostics.Debug.WriteLine($"Ohlc обновлен: {(value?.Count ?? 0)} точек");
            }
        }

        private bool _isLoadingCoins;
        public bool IsLoadingCoins
        {
            get => _isLoadingCoins;
            set
            {
                _isLoadingCoins = value;
                OnPropertyChanged();
            }
        }

        private string _loadingMessage = "";
        public string LoadingMessage
        {
            get => _loadingMessage;
            set
            {
                _loadingMessage = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadCoinsCommand { get; }
        public ICommand OpenAnalysisCommand { get; }

        public MainViewModel()
        {
            LoadCoinsCommand = new RelayCommand(async _ => await LoadCoins());
            OpenAnalysisCommand = new RelayCommand(async _ => await OpenAnalysis());
            _ = LoadCoins();
        }

        public async Task LoadCoins()
        {
            if (IsLoadingCoins) return;

            IsLoadingCoins = true;
            LoadingMessage = "Загрузка списка монет...";

            try
            {
                var coins = await _service.GetTopCoinsAsync(CancellationToken.None);

                Coins.Clear();
                foreach (var coin in coins)
                    Coins.Add(coin);

                if (Coins.Any() && SelectedCoin == null)
                {
                    SelectedCoin = Coins.First();

                    // Загружаем данные для первой монеты
                    await LoadHistoryForCoin(SelectedCoin.Id);

                    // Запускаем фоновую предзагрузку для остальных монет
                    var remainingCoins = Coins.Skip(1).Select(c => c.Id).ToList();
                    _ = PreloadRemainingCoins(remainingCoins);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки монет: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoadingCoins = false;
                LoadingMessage = "";
            }
        }

        private async Task PreloadRemainingCoins(List<string> coinIds)
        {
            if (_isPreloading) return;
            _isPreloading = true;

            try
            {
                int loadedCount = 0;
                foreach (var coinId in coinIds)
                {
                    if (_dataCache.ContainsKey(coinId))
                        continue;

                    // Ждем 3 секунды между загрузками, чтобы не превысить лимит
                    await Task.Delay(3000);

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Начинаем предзагрузку: {coinId}");
                        var history = await _service.GetCoinHistoryAsync(coinId, CancellationToken.None, 7);
                        var ohlc = await _service.GetOhlcAsync(coinId, CancellationToken.None, 7);
                        _dataCache[coinId] = (history, ohlc);
                        loadedCount++;
                        System.Diagnostics.Debug.WriteLine($"Предзагружена монета: {coinId} ({loadedCount}/{coinIds.Count}) - History:{history.Count}, Ohlc:{ohlc.Count}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка предзагрузки {coinId}: {ex.Message}");
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Предзагрузка завершена. Загружено {loadedCount} из {coinIds.Count} монет");
            }
            finally
            {
                _isPreloading = false;
            }
        }

        private async Task LoadHistoryForCoin(string coinId)
        {
            if (string.IsNullOrEmpty(coinId))
                return;

            if (_dataCache.TryGetValue(coinId, out var cached))
            {
                History = cached.History;
                Ohlc = cached.Ohlc;
                _currentCoinId = coinId;
                return;
            }

            _currentCoinId = coinId;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                System.Diagnostics.Debug.WriteLine($"Загружаем данные для {coinId}");
                var historyTask = _service.GetCoinHistoryAsync(coinId, _cts.Token, 7);
                var ohlcTask = _service.GetOhlcAsync(coinId, _cts.Token, 7);
                await Task.WhenAll(historyTask, ohlcTask);

                History = await historyTask;
                Ohlc = await ohlcTask;
                _dataCache[coinId] = (History, Ohlc);
                System.Diagnostics.Debug.WriteLine($"Данные загружены для {coinId}: History:{History.Count}, Ohlc:{Ohlc.Count}");
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки {coinId}: {ex.Message}");
            }
        }

        private async Task OpenAnalysis()
        {
            if (History == null || History.Count == 0)
            {
                MessageBox.Show("Данные еще не загружены", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new AnalysisWindow(History);
            window.ShowDialog();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}