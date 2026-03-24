using CryptoMonitor.Models;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace CryptoMonitor.Services
{
    public class CryptoApiService
    {
        private static readonly HttpClient _httpClient = new();
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        // Уменьшаем задержку между запросами до 1.5 секунд
        private const int MinDelayMs = 1500;

        private static DateTime _lastRequestTime = DateTime.MinValue;
        private static int _requestCount = 0;
        private static DateTime _minuteStart = DateTime.UtcNow;
        private static DateTime _blockedUntil = DateTime.MinValue;

        static CryptoApiService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent
                .Add(new ProductInfoHeaderValue("CryptoMonitorApp", "1.0"));
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        private async Task<bool> WaitIfNeededAsync(CancellationToken token)
        {
            var now = DateTime.UtcNow;

            // Если заблокированы API, ждем
            if (now < _blockedUntil)
            {
                var waitTime = _blockedUntil - now;
                System.Diagnostics.Debug.WriteLine($"API заблокирован, ждем {waitTime.TotalSeconds} сек");
                await Task.Delay(waitTime, token);
                return false;
            }

            // Сброс счетчика каждую минуту
            if ((now - _minuteStart).TotalSeconds >= 60)
            {
                _requestCount = 0;
                _minuteStart = now;
                System.Diagnostics.Debug.WriteLine("Сброс счетчика запросов");
            }

            // Если сделано 28 запросов за минуту, ждем до следующей минуты
            if (_requestCount >= 28)
            {
                var waitSeconds = 60 - (now - _minuteStart).TotalSeconds;
                if (waitSeconds > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Достигнут лимит ({_requestCount}), ждем {waitSeconds:F1} сек");
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), token);
                    _requestCount = 0;
                    _minuteStart = DateTime.UtcNow;
                    return false;
                }
            }

            return true;
        }

        public async Task<List<CryptoCoin>> GetTopCoinsAsync(CancellationToken token, int count = 10)
        {
            if (!await WaitIfNeededAsync(token))
                return new List<CryptoCoin>();

            string url = $"https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page={count}&page=1&sparkline=false";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<CryptoCoin>();

            string json = await response.Content.ReadAsStringAsync();
            _requestCount++;
            return JsonConvert.DeserializeObject<List<CryptoCoin>>(json) ?? new List<CryptoCoin>();
        }

        public async Task<List<PricePoint>> GetCoinHistoryAsync(
            string coinId,
            CancellationToken token,
            int days = 7)
        {
            if (!await WaitIfNeededAsync(token))
                return new List<PricePoint>();

            string url = $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart?vs_currency=usd&days={days}";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<PricePoint>();

            string json = await response.Content.ReadAsStringAsync();
            _requestCount++;

            var data = JsonConvert.DeserializeObject<MarketChartResponse>(json);
            var list = new List<PricePoint>();

            if (data?.Prices != null)
            {
                foreach (var point in data.Prices)
                {
                    long unix = (long)point[0];
                    double price = point[1];

                    list.Add(new PricePoint
                    {
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(unix).LocalDateTime,
                        Price = price
                    });
                }
            }

            return list;
        }

        public async Task<List<OhlcPoint>> GetOhlcAsync(string coinId, CancellationToken token, int days = 7)
        {
            if (!await WaitIfNeededAsync(token))
                return new List<OhlcPoint>();

            string url = $"https://api.coingecko.com/api/v3/coins/{coinId}/ohlc?vs_currency=usd&days={days}";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<OhlcPoint>();

            string json = await response.Content.ReadAsStringAsync();
            _requestCount++;

            var raw = JsonConvert.DeserializeObject<List<List<double>>>(json);
            var result = new List<OhlcPoint>();

            if (raw != null)
            {
                foreach (var item in raw)
                {
                    result.Add(new OhlcPoint
                    {
                        Time = DateTimeOffset.FromUnixTimeMilliseconds((long)item[0]).LocalDateTime,
                        Open = item[1],
                        High = item[2],
                        Low = item[3],
                        Close = item[4]
                    });
                }
            }

            return result;
        }

        private async Task<HttpResponseMessage?> SafeGetAsync(string url, CancellationToken token)
        {
            await _semaphore.WaitAsync(token);

            try
            {
                // Задержка между запросами
                var diff = DateTime.UtcNow - _lastRequestTime;
                if (diff.TotalMilliseconds < MinDelayMs)
                {
                    int delay = MinDelayMs - (int)diff.TotalMilliseconds;
                    await Task.Delay(delay, token);
                }

                var response = await _httpClient.GetAsync(url, token);
                _lastRequestTime = DateTime.UtcNow;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    
                    _blockedUntil = DateTime.UtcNow.AddSeconds(10);
                    System.Diagnostics.Debug.WriteLine($"TooManyRequests! Блокировка на 10 секунд");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка запроса: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}