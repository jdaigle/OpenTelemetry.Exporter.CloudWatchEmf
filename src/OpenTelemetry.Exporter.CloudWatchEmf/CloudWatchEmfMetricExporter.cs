using OpenTelemetry.Exporter.CloudWatchEmf.Model;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.CloudWatchEmf;

/// <summary>
/// An implementation of an OpenTelementry <see cref="Metric"/> Exporter that can send batches of metrics
/// to CloudWatch using the CloudWatch Logs Embedded Metrics Format.
/// </summary>
public class CloudWatchEmfMetricExporter : BaseExporter<Metric>
{
    private readonly CloudWatchEmfMetricExporterOptions _exporterOptions;
    private readonly CloudWatchPusher _pusher;

    /// <summary>
    /// Create a new instance of <see cref="CloudWatchEmfMetricExporter"/>.
    /// </summary>
    /// <param name="exporterOptions">The <see cref="CloudWatchEmfMetricExporterOptions"/> for this instance.</param>
    public CloudWatchEmfMetricExporter(CloudWatchEmfMetricExporterOptions exporterOptions)
    {
        _exporterOptions = exporterOptions;
        _pusher = new CloudWatchPusher(exporterOptions.CloudWatchPusherOptions);
    }

    /// <inheritdoc/>
    public override ExportResult Export(in Batch<Metric> batch)
    {
        if (batch.Count == 0)
        {
            return ExportResult.Success;
        }

        var items = new List<CloudWatchRootNode>();

        foreach (var metric in batch)
        {
            foreach (var point in metric.GetMetricPoints())
            {
                items.Add(BuildCloudWatchEmfNode(metric, point));
            }
        }

        if (items.Count > 0)
        {
            try
            {
                _pusher.PushMetricsToCloudWatch(items);
            }
            catch (Exception ex)
            {
                // TODO: logging
                Console.WriteLine(ex);
                return ExportResult.Failure;
            }
        }

        return ExportResult.Success;
    }

    private CloudWatchRootNode BuildCloudWatchEmfNode(Metric metric, MetricPoint point)
    {
        var rootNode = new CloudWatchRootNode();
        rootNode.AwsMetadata.Timestamp = point.EndTime;

        var resource = ParentProvider.GetResource();
        foreach (var attribute in resource.Attributes)
        {
            if (attribute.Value?.ToString() is string value)
            {
                // TODO: figure out which resource attributes should be included, and mapped
                // rootNode.AddDimension(attribute.Key, value);
            }
        }

        // TODO: figure out which Tags should become dimensions?
        foreach (var tag in point.Tags)
        {
            if (tag.Value?.ToString() is string value)
            {
                rootNode.AddDimension(tag.Key, value);
            }
        }

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
                rootNode.PutMetric(metric.Name, point.GetSumLong());
                break;
            case MetricType.DoubleSum:
                rootNode.PutMetric(metric.Name, point.GetSumDouble());
                break;
            case MetricType.LongGauge:
                rootNode.PutMetric(metric.Name, point.GetGaugeLastValueLong());
                break;
            case MetricType.DoubleGauge:
                rootNode.PutMetric(metric.Name, point.GetGaugeLastValueDouble());
                break;
            case MetricType.Histogram:
                var buckets = point.GetHistogramBuckets();
                var sum = point.GetHistogramSum();
                var count = point.GetHistogramCount();
                // TODO: current otel version does not support Min/Max. But we could probably derive that from the min/max bucket for now.
                // Start with avg for min/max, because the PositivitInfinity bucket can be problematic.
                var avg = sum / count;
                var min = avg;
                var max = avg;
                foreach (var bucket in buckets)
                {
                    if (bucket.BucketCount > 0 && bucket.ExplicitBound != double.PositiveInfinity)
                    {
                        min = min == 0 ? bucket.ExplicitBound : Math.Min(bucket.ExplicitBound, min);
                        max = Math.Max(bucket.ExplicitBound, max);
                    }
                }
                rootNode.AwsMetadata.MetricDirective.Metrics.Add(new MetricDefinition(metric.Name)
                {
                    StatisticSet = new StatisticSet
                    {
                        Sum = point.GetHistogramSum(),
                        Count = point.GetHistogramCount(),
                        Min = min,
                        Max = max,
                    },
                });
                break;
            default:
                throw new NotImplementedException();
        }

        return rootNode;
    }
}
