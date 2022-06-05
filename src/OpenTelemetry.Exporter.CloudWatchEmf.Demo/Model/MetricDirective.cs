using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

/// <summary>
/// The directives in the Metadata.
/// This specifies for CloudWatch how to parse and create metrics from the log message.
/// </summary>
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class MetricDirective
{
    private static readonly IEnumerable<IEnumerable<string>> _defaultEmptyDimensionSetArray = new string[][] { Array.Empty<string>() };

    public MetricDirective()
    {
        //Namespace = Constants.DEFAULT_NAMESPACE;
        Metrics = new List<MetricDefinition>();
        DimensionSets = new List<DimensionSet>();
    }

    /// <summary>
    ///  A string representing the CloudWatch namespace for the metric.
    /// </summary>
    [JsonProperty("Namespace")]
    public string Namespace { get; set; } = "DefaultCustomNamespace";

    public List<DimensionSet> DimensionSets { get; }

    /// <summary>
    /// A DimensionSet array.
    /// A DimensionSet is an array of strings containing the dimension keys that will be applied to all metrics in the document.
    /// Every DimensionSet used creates a new metric in CloudWatch.
    /// </summary>
    [JsonProperty("Dimensions")]
    public IEnumerable<IEnumerable<string>> DimensionSetKeys
    {
        get
        {
            if (DimensionSets.Count == 0)
            {
                return _defaultEmptyDimensionSetArray;
            }
            return DimensionSets.Select(x => x.DimensionKeys);
        }
    }

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

    [JsonProperty("Metrics")]
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

    public MetricDirective DeepCloneWithNewMetrics(List<MetricDefinition> metrics)
    {
        var clone = new MetricDirective
        {
            Namespace = Namespace
        };
        clone.DimensionSets.AddRange(DimensionSets);
        foreach (var metric in metrics)
        {
            clone.Metrics.Add(metric);
        }
        return clone;
    }
}
