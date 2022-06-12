using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Model;

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
internal class CloudWatchRootNode
{
    public Metadata AwsMetadata { get; private set; } = new Metadata();

    public void AddDimension(string key, string value) => AwsMetadata.MetricDirective.AddDimension(key, value);

    public void PutMetric(string key, double value) => PutMetric(key, value, Unit.NONE);

    public void PutMetric(string key, double value, Unit unit) => AwsMetadata.MetricDirective.PutMetric(key, value, unit);

    public string ToJson()
    {
        using var stringWriter = new StringWriter();
        using var writer = new JsonTextWriter(stringWriter);

        writer.WriteStartObject();

        writer.WritePropertyName("_aws");
        AwsMetadata.WriteJson(writer);

        foreach (var dimensionSet in AwsMetadata.MetricDirective.DimensionSets)
        {
            foreach (var dimension in dimensionSet.Dimensions)
            {
                writer.WritePropertyName(dimension.Key);
                writer.WriteValue(dimension.Value);
            }
        }

        foreach (var metricDefinition in AwsMetadata.MetricDirective.Metrics)
        {
            writer.WritePropertyName(metricDefinition.Name);
            if (metricDefinition.StatisticSet is not null)
            {
                metricDefinition.StatisticSet.WriteJson(writer);
            }
            else
            {
                var values = metricDefinition.Values;
                if (values.Count == 1)
                {
                    writer.WriteValue(values[0]);
                    continue;
                }
                else
                {
                    writer.WriteStartArray();
                    foreach (var value in values)
                    {
                        writer.WriteValue(value);
                    }
                    writer.WriteEndArray();
                }
            }
        }

        writer.WriteEndObject();

        return stringWriter.ToString();
    }
}
