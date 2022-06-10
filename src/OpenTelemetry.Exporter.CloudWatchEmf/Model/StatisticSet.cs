using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Model
{
    /// <summary>
    /// Represents aggregated metric values.
    /// This appears to be supported with the Embedded Metric Format, although not documented.
    /// </summary>
    public class StatisticSet
    {
        public double Sum { get; set; }

        public double Count { get; set; }

        public double Min { get; set; }

        public double Max { get; set; }

        public void WriteJson(JsonTextWriter writer)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("Sum");
            writer.WriteValue(Sum);

            writer.WritePropertyName("Count");
            writer.WriteValue(Count);

            writer.WritePropertyName("Min");
            writer.WriteValue(Min);

            writer.WritePropertyName("Max");
            writer.WriteValue(Max);

            writer.WriteEndObject();
        }
    }
}
