using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using CryptoMonitor.Models;
using System.Net.Http.Headers;

namespace CryptoMonitor.Services
{
    public class CryptoApiService
    {
        private static readonly HttpClient _httpClient;

        static CryptoApiService()
        {
            _httpClient = new HttpClient(); 
            _httpClient.DefaultRequestHeaders.UserAgent
                .Add(new ProductInfoHeaderValue("CryptoMonitorApp", "1.0"));
        }
        public async Task<List<CryptoCoin>> GetTopCoinsAsync(int count = 10)
        {
            try
            {
                string url = $"https://api.coingecko.com/api/v3/coins/markets" +
                         $"?vs_currency=usd&order=market_cap_desc&per_page={count}&page=1&sparkline=false";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                return JsonConvert.DeserializeObject<List<CryptoCoin>>(json) ?? new List<CryptoCoin>();
            } 
            catch (Exception ex)
            {
                throw new Exception("Ошибка загрузки списка монет: " + ex.Message);
            }
            
        }

        public async Task<List<PricePoint>> GetCoinHistoryAsync(string coinId, int days = 7)
        {
            try
            {
                string url = $"https://api.coingecko.com/api/v3/coins/{coinId}/market_chart" +
                         $"?vs_currency=usd&days={days}";

                var response = await _httpClient.GetAsync(url);

                

                response.EnsureSuccessStatusCode();

                string json = await response.Content.ReadAsStringAsync();

                var data = JsonConvert.DeserializeObject<MarketChartResponse>(json);

                var list = new List<PricePoint>();

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

                return list;
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка загрузки истории: " + ex.Message);
            }
        }
    }


}

