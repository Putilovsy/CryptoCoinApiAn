using Newtonsoft.Json;
using System;

namespace CryptoMonitor.Helpers
{
    /// <summary>
    /// Конвертер для decimal, который безопасно обрабатывает null значения от API, превращая их в 0.
    /// </summary>
    public class SafeDecimalConverter : JsonConverter<decimal>
    {
        public override void WriteJson(JsonWriter writer, decimal value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }

        public override decimal ReadJson(JsonReader reader, Type objectType, decimal existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return 0m;
            
            try
            {
                return Convert.ToDecimal(reader.Value);
            }
            catch
            {
                return 0m;
            }
        }
    }

    /// <summary>
    /// Конвертер для double, который безопасно обрабатывает null значения от API, превращая их в 0.
    /// </summary>
    public class SafeDoubleConverter : JsonConverter<double>
    {
        public override void WriteJson(JsonWriter writer, double value, JsonSerializer serializer)
        {
            writer.WriteValue(value);
        }

        public override double ReadJson(JsonReader reader, Type objectType, double existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return 0.0;

            try
            {
                return Convert.ToDouble(reader.Value);
            }
            catch
            {
                return 0.0;
            }
        }
    }
}
