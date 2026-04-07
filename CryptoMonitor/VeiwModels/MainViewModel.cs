using CryptoMonitor.Models;
using CryptoMonitor.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows;
using CryptoMonitor.Helpers;
using System.IO;
using System.Text.Json;

namespace CryptoMonitor.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CryptoApiService _service = new();
        private readonly Dictionary<string, (List<PricePoint> History, List<OhlcPoint> Ohlc, TimeSpan Span)> _dataCache = new();
        private string _currentCoinId = "";
        private CancellationTokenSource _cts;

        public ObservableCollection<CryptoCoin> Coins { get; set; } = new();
        public ICollectionView FilteredCoins { get; private set; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilteredCoins?.Refresh();
            }
        }

        private bool _showOnlyPortfolio;
        public bool ShowOnlyPortfolio
        {
            get => _showOnlyPortfolio;
            set
            {
                _showOnlyPortfolio = value;
                OnPropertyChanged();
                FilteredCoins?.Refresh();
            }
        }

        private bool FilterCoinsOverride(object item)
        {
            if (item is CryptoCoin coin)
            {
                bool matchSearch = string.IsNullOrWhiteSpace(_searchText) || 
                                   (coin.Symbol?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true) || 
                                   (coin.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) == true);
                                   
                bool matchPortfolio = !_showOnlyPortfolio || coin.IsFavorite;
                
                return matchSearch && matchPortfolio;
            }
            return false;
        }

        private const string PortfolioFile = "portfolio.json";
        
        private void SavePortfolio()
        {
            try
            {
                var favorites = Coins.Where(c => c.IsFavorite).Select(c => c.Id).ToList();
                File.WriteAllText(PortfolioFile, JsonSerializer.Serialize(favorites));
            }
            catch { }
        }

        private void LoadPortfolio()
        {
            try
            {
                if (File.Exists(PortfolioFile))
                {
                    var json = File.ReadAllText(PortfolioFile);
                    var favorites = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                    foreach (var coin in Coins)
                    {
                        if (favorites.Contains(coin.Id))
                        {
                            coin.IsFavorite = true;
                        }
                    }
                }
            }
            catch { }
        }

        private void Coin_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CryptoCoin.IsFavorite))
            {
                SavePortfolio();
                if (ShowOnlyPortfolio)
                {
                    FilteredCoins?.Refresh();
                }
            }
        }

        private int _selectedDays = 7;
        public int SelectedDays
        {
            get => _selectedDays;
            set
            {
                if (_selectedDays != value)
                {
                    _selectedDays = value;
                    OnPropertyChanged();
                    DebouncedLoadHistory();
                }
            }
        }

        // Вызов при выборе другой монеты
        private async Task SwitchCoinAsync(string coinId)
        {
            if (string.IsNullOrEmpty(coinId)) return;

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoadingCoins = true;
            LoadingMessage = "Загрузка графика...";

            await ProcessHistoryLoadAsync(coinId, false, token);

            if (_cts.Token == token)
                IsLoadingCoins = false;
        }

        // Вызов при переключении таймфрейма (с дебаунсом)
        private async void DebouncedLoadHistory()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsLoadingCoins = true;
            LoadingMessage = "Загрузка графика...";

            try
            {
                await Task.Delay(500, token); 
                await ProcessHistoryLoadAsync(_currentCoinId, true, token);
            }
            catch (TaskCanceledException) { }
            finally
            {
                if (_cts.Token == token)
                    IsLoadingCoins = false;
            }
        }

        public TimeSpan CurrentOhlcSpan { get; private set; } = TimeSpan.FromHours(4);

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
                    _ = SwitchCoinAsync(value.Id);
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
            FilteredCoins = CollectionViewSource.GetDefaultView(Coins);
            FilteredCoins.Filter = FilterCoinsOverride;
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
                {
                    coin.PropertyChanged += Coin_PropertyChanged;
                    Coins.Add(coin);
                }

                LoadPortfolio();

                if (Coins.Any() && SelectedCoin == null)
                {
                    SelectedCoin = Coins.First();
                    
                    _ = SwitchCoinAsync(SelectedCoin.Id);
                   
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

        private async Task ProcessHistoryLoadAsync(string coinId, bool forceRefresh, CancellationToken token)
        {
            if (string.IsNullOrEmpty(coinId))
                return;

            _currentCoinId = coinId;
            string cacheKey = $"{coinId}_{SelectedDays}";

            if (!forceRefresh && _dataCache.TryGetValue(cacheKey, out var cached))
            {
                CurrentOhlcSpan = cached.Span;
                History = cached.History;
                Ohlc = cached.Ohlc;
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"Загружаем данные для {cacheKey}");
                
                var history = await _service.GetCoinHistoryAsync(coinId, token, SelectedDays);

                if (token.IsCancellationRequested || _currentCoinId != coinId) 
                    return;

                if (history == null || history.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Пустой результат от API для {cacheKey} (Возможно 429 Limit)");
                    return;
                }

                TimeSpan span = TimeSpan.FromHours(4);
                if (SelectedDays == 1) span = TimeSpan.FromMinutes(30);
                else if (SelectedDays == 7) span = TimeSpan.FromHours(4);
                else if (SelectedDays == 30) span = TimeSpan.FromDays(1);
                else if (SelectedDays >= 365) span = TimeSpan.FromDays(7);

                CurrentOhlcSpan = span;
                History = history;
                Ohlc = GenerateOhlcFromHistory(History, span);

                _dataCache[cacheKey] = (History, Ohlc, span);

                System.Diagnostics.Debug.WriteLine($"Данные загружены для {cacheKey}: History:{History.Count}, Ohlc:{Ohlc.Count}");
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

        private List<OhlcPoint> GenerateOhlcFromHistory(List<PricePoint> history, TimeSpan timeSpan)
        {
            if (history == null || !history.Any())
                return new List<OhlcPoint>();

            var ohlcList = new List<OhlcPoint>();

            // Группируем точки по временным интервалам
            var grouped = history.GroupBy(p => p.Time.Ticks / timeSpan.Ticks);

            foreach (var group in grouped)
            {
                // Сортируем точки внутри группы по времени, чтобы точно знать, где начало, а где конец
                var points = group.OrderBy(p => p.Time).ToList();

                var ohlc = new OhlcPoint
                {
                    // Берем время начала свечи
                    Time = new DateTime(group.Key * timeSpan.Ticks),
                    // Открытие - первая цена в отрезке
                    Open = points.First().Price,
                    // Закрытие - последняя цена в отрезке
                    Close = points.Last().Price,
                    // Максимум и минимум
                    High = points.Max(p => p.Price),
                    Low = points.Min(p => p.Price)
                };

                ohlcList.Add(ohlc);
            }

            return ohlcList;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}