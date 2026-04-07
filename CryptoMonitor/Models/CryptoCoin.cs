using Newtonsoft.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CryptoMonitor.Models
{
    public class CryptoCoin : INotifyPropertyChanged
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

        private bool _isFavorite;
        
        [JsonIgnore]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
