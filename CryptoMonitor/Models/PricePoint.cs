using Newtonsoft.Json;
using CryptoMonitor.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace CryptoMonitor.Models
{
    public class PricePoint
    {
        [JsonConverter(typeof(SafeDoubleConverter))]
        public double Price { get; set; }
        
        [JsonConverter(typeof(SafeDoubleConverter))]
        public double Volume { get; set; }
        public DateTime Time { get; set; }
    }

    public class MarketChartResponse 
    {
        [JsonProperty("prices")]
        public List<List<double>> ? Prices { get; set; }
        
        [JsonProperty("total_volumes")]
        public List<List<double>> ? TotalVolumes { get; set; }
    }
}
