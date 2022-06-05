using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

/*
EXAMPLE OF DESIRED SERIALIZATION OUTPUT:
{
   "_aws": {
       "Timestamp": 1559748430481
       "CloudWatchMetrics": [
           {
               "Namespace": "lambda-function-metrics",
               "Dimensions": [ [ "functionVersion" ] ],
               "Metrics": [
                   {
                       "Name": "Time",
                       "Unit": "Milliseconds"
                   }
               ]
           }
       ]
   },
   "time": 100,
}
*/
/// <remarks>
/// https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/CloudWatch_Embedded_Metric_Format_Specification.html
/// </remarks>
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class RootNode
{
    [JsonProperty("_aws")]
    public Metadata AwsMetadata { get; private set; } = new Metadata();

    /// <summary>
    /// Emits the target members that are referenced by metrics, dimensions and properties.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, object> CloudWatchLogTargetMembers
    {
        get
        {
            var targetMembers = new Dictionary<string, object>();

            foreach (var dimensionSet in AwsMetadata.MetricDirective.DimensionSets)
            {
                foreach (var dimension in dimensionSet.Dimensions)
                {
                    targetMembers.Add(dimension.Key, dimension.Value);
                }
            }

            foreach (var metricDefinition in AwsMetadata.MetricDirective.Metrics)
            {
                if (metricDefinition.StatisticSet is not null)
                {
                    targetMembers.Add(metricDefinition.Name, metricDefinition.StatisticSet);
                }
                else
                {
                    var values = metricDefinition.Values;
                    targetMembers.Add(metricDefinition.Name, values.Count == 1 ? values[0] : values);
                }
            }

            return targetMembers;
        }
    }

    public void AddDimension(string key, string value) => AwsMetadata.MetricDirective.AddDimension(key, value);

    public void PutMetric(string key, double value) => PutMetric(key, value, Unit.NONE);

    public void PutMetric(string key, double value, Unit unit) => AwsMetadata.MetricDirective.PutMetric(key, value, unit);

    public string Serialize()
    {
        return JsonConvert.SerializeObject(this);
    }

    internal RootNode DeepCloneWithNewMetrics(List<MetricDefinition> metrics)
    {
        var clone = new RootNode
        {
            AwsMetadata = AwsMetadata.DeepCloneWithNewMetrics(metrics)
        };
        return clone;
    }
}
