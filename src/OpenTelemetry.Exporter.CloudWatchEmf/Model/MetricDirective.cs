using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Model;

/// <summary>
/// The directives in the Metadata.
/// This specifies for CloudWatch how to parse and create metrics from the log message.
/// </summary>
internal class MetricDirective
{
    public MetricDirective()
    {
        Metrics = new List<MetricDefinition>();
        DimensionSets = new List<DimensionSet>();
    }

    /// <summary>
    ///  A string representing the CloudWatch namespace for the metric.
    /// </summary>
    public string Namespace { get; set; } = "DefaultCustomNamespace";

    /// <summary>
    /// A DimensionSet array.
    /// A DimensionSet is an array of strings containing the dimension keys that will be applied to all metrics in the document.
    /// Every DimensionSet used creates a new metric in CloudWatch.
    /// </summary>
    public List<DimensionSet> DimensionSets { get; }

    public void AddDimension(string key, string value)
    {
        if (DimensionSets.Count == 0)
        {
            DimensionSets.Add(new DimensionSet(key, value));
        }
        else
        {
            DimensionSets.First().AddDimension(key, value);
        }
    }

    public List<MetricDefinition> Metrics { get; }

    public void PutMetric(string key, double value) => PutMetric(key, value, Unit.NONE);

    public void PutMetric(string key, double value, Unit unit)
    {
        var metric = Metrics.FirstOrDefault(m => m.Name == key);
        if (metric != null)
        {
            metric.AddValue(value);
        }
        else
        {
            Metrics.Add(new MetricDefinition(key, unit, value));
        }
    }

    public void WriteJson(JsonTextWriter writer)
    {
        writer.WriteStartObject();

        writer.WritePropertyName("Namespace");
        writer.WriteValue(Namespace);

        writer.WritePropertyName("Dimensions");
        writer.WriteStartArray();
        foreach (var dimensionSet in DimensionSets)
        {
            writer.WriteStartArray();
            foreach (var dimension in dimensionSet.DimensionKeys)
            {
                writer.WriteValue(dimension);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();

        writer.WritePropertyName("Metrics");
        writer.WriteStartArray();
        foreach (var metric in Metrics)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("Name");
            writer.WriteValue(metric.Name);

            // TODO
            //writer.WritePropertyName("Unit");
            //writer.WriteValue(Unit);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
