
namespace CryptoMonitor.Models
{
    public class CryptoCoin
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public string Name { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketCup { get; set; }
        public double PriceChangePercentage24h { get; set; }
    }
}
