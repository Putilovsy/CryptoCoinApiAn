using CryptoMonitor.Models;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CryptoMonitor.Services
{
    /// <summary>
    /// Service responsible for взаимодействие с CoinGecko API.
    /// Гарантирует:
    /// - последовательное выполнение запросов
    /// - базовый rate limiting
    /// - кеширование данных
    /// </summary>
    public class CryptoApiService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly SemaphoreSlim _requestLock = new(1, 1);

        // Минимальная задержка между запросами (мс)
        private const int MinDelayMs = 1500;

        private static DateTime _lastRequestTime = DateTime.MinValue;

        // Кеш данных
        private readonly Dictionary<string, List<PricePoint>> _historyCache = new();

        static CryptoApiService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent
                .Add(new ProductInfoHeaderValue("CryptoMonitorApp", "1.0"));
        }

        /// <summary>
        /// Получает список топ монет.
        /// </summary>
        public async Task<List<CryptoCoin>> GetTopCoinsAsync(CancellationToken token, int count = 100)
        {
            string url =
                $"https://api.coingecko.com/api/v3/coins/markets" +
                $"?vs_currency=usd&order=market_cap_desc&per_page={count}&page=1&sparkline=false";

            var response = await SendRequestAsync(url, token);
            if (response == null)
                return new List<CryptoCoin>();

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<CryptoCoin>>(json)
                   ?? new List<CryptoCoin>();
        }

        /// <summary>
        /// Возвращает исторические цены монеты.
        /// Использует кеш для предотвращения повторных запросов.
        /// </summary>
        public async Task<List<PricePoint>> GetCoinHistoryAsync(string coinId, CancellationToken token, int days = 7)
        {
            string cacheKey = $"{coinId}_{days}";
            if (_historyCache.TryGetValue(cacheKey, out var cached))
                return cached;

            string url =
                $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart" +
                $"?vs_currency=usd&days={days}";

            var response = await SendRequestAsync(url, token);
            if (response == null)
                return new List<PricePoint>();

            string json = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<MarketChartResponse>(json);

            var result = new List<PricePoint>();

            if (data?.Prices != null)
            {
                foreach (var point in data.Prices)
                {
                    result.Add(new PricePoint
                    {
                        Time = DateTimeOffset
                            .FromUnixTimeMilliseconds((long)point[0])
                            .LocalDateTime,
                        Price = point[1]
                    });
                }
            }

            _historyCache[cacheKey] = result;
            return result;
        }

        /// <summary>
        /// Выполняет HTTP-запрос с учетом ограничения частоты.
        /// Все запросы проходят через единый lock.
        /// </summary>
        private async Task<HttpResponseMessage?> SendRequestAsync(string url, CancellationToken token)
        {
            await _requestLock.WaitAsync(token);

            try
            {
                var elapsed = DateTime.UtcNow - _lastRequestTime;

                if (elapsed.TotalMilliseconds < MinDelayMs)
                {
                    var delay = MinDelayMs - (int)elapsed.TotalMilliseconds;
                    await Task.Delay(delay, token);
                }

                var response = await _httpClient.GetAsync(url, token);
                _lastRequestTime = DateTime.UtcNow;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    // При 429 даем API "остыть"
                    await Task.Delay(5000, token);
                    response = await _httpClient.GetAsync(url, token);
                    _lastRequestTime = DateTime.UtcNow;
                    
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        // Вторая попытка
                        await Task.Delay(10000, token);
                        response = await _httpClient.GetAsync(url, token);
                        _lastRequestTime = DateTime.UtcNow;

                        if (response.StatusCode == HttpStatusCode.TooManyRequests)
                            return null;
                    }
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                return response;
            }
            finally
            {
                _requestLock.Release();
            }
        }
    }
}