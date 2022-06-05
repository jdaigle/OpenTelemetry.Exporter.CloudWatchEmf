using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model
{
    /// <summary>
    /// Represents aggregated metric values.
    /// This appears to be supported with the Embedded Metric Format, although not documented.
    /// </summary>
    [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
    public class StatisticSet
    {
        [JsonProperty("Sum")]
        public double Sum { get; set; }

        [JsonProperty("Count")]
        public double Count { get; set; }

        [JsonProperty("Min")]
        public double Min { get; set; }

        [JsonProperty("Max")]
        public double Max { get; set; }
    }
}
