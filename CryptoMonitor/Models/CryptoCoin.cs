using Newtonsoft.Json;

namespace CryptoMonitor.Models
{
    public class CryptoCoin
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("current_price")]
        public decimal CurrentPrice { get; set; }

        [JsonProperty("market_cap")]
        public decimal MarketCap { get; set; }

        [JsonProperty("price_change_percentage_24h")]
        public decimal PriceChangePercentage24h { get; set; }
    }
}
