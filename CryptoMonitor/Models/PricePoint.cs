using Newtonsoft.Json;
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
        public double Price { get; set; }
        public DateTime Time { get; set; }
    }

    public class MarketChartResponse 
    {
        [JsonProperty("prices")]
        public List<List<double>> ? Prices { get; set; }
    }
}
