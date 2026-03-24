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

        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MinDelayMs = 3000; 

        private static DateTime _blockedUntil = DateTime.MinValue;

        
        private static readonly Dictionary<string, List<PricePoint>> _historyCache = new();

        static CryptoApiService()
        {
            _httpClient.DefaultRequestHeaders.UserAgent
                .Add(new ProductInfoHeaderValue("CryptoMonitorApp", "1.0"));
        }

        
        public async Task<List<CryptoCoin>> GetTopCoinsAsync(CancellationToken token, int count = 10)
        {
            string url =
                $"https://api.coingecko.com/api/v3/coins/markets" +
                $"?vs_currency=usd&order=market_cap_desc&per_page={count}&page=1&sparkline=false";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<CryptoCoin>(); 

            string json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<CryptoCoin>>(json)
                   ?? new List<CryptoCoin>();
        }

        
        
        public async Task<List<PricePoint>> GetCoinHistoryAsync(
            string coinId,
            CancellationToken token,
            int days = 7)
        {
            
            if (_historyCache.TryGetValue(coinId, out var cached))
                return cached;

            string url =
                $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart" +
                $"?vs_currency=usd&days={days}";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<PricePoint>();

            string json = await response.Content.ReadAsStringAsync();
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
                        Time = DateTimeOffset
                            .FromUnixTimeMilliseconds(unix)
                            .LocalDateTime,
                        Price = price
                    });
                }
            }

            
            _historyCache[coinId] = list;

            return list;
        }

        
        private async Task<HttpResponseMessage?> SafeGetAsync(
            string url,
            CancellationToken token)
        {
            
            if (DateTime.UtcNow < _blockedUntil)
                return null;

            await _semaphore.WaitAsync(token);

            try
            {
                
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
                    
                    _blockedUntil = DateTime.UtcNow.AddSeconds(60);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                return response;
            }
            catch
            {
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<OhlcPoint>> GetOhlcAsync(string coinId, CancellationToken token, int days = 7)
        {
            string url =
                $"https://api.coingecko.com/api/v3/coins/{coinId}/ohlc" +
                $"?vs_currency=usd&days={days}";

            var response = await SafeGetAsync(url, token);
            if (response == null)
                return new List<OhlcPoint>();

            string json = await response.Content.ReadAsStringAsync();

            var raw = JsonConvert.DeserializeObject<List<List<double>>>(json);

            var result = new List<OhlcPoint>();

            if (raw != null)
            {
                foreach (var item in raw)
                {
                    result.Add(new OhlcPoint
                    {
                        Time = DateTimeOffset
                            .FromUnixTimeMilliseconds((long)item[0])
                            .LocalDateTime,
                        Open = item[1],
                        High = item[2],
                        Low = item[3],
                        Close = item[4]
                    });
                }
            }

            return result;
        }
    }
}