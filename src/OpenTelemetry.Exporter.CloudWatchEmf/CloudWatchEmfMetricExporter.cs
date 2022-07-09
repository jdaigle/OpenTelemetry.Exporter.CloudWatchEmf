using OpenTelemetry.Exporter.CloudWatchEmf.Implementation;
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
                CloudWatchEmfMetricExporterEventSource.Log.ExceptionCallingPushMetricsToCloudWatch(ex);
                return ExportResult.Failure;
            }
        }

        return ExportResult.Success;
    }

    private static readonly char[] _namespaceSeperator = new[] { '.' };

    private CloudWatchRootNode BuildCloudWatchEmfNode(Metric metric, MetricPoint point)
    {
        var rootNode = new CloudWatchRootNode();
        rootNode.AwsMetadata.Timestamp = point.EndTime;
        var metricName = metric.Name;
        var nameParts = metricName.Split(_namespaceSeperator, 2, StringSplitOptions.RemoveEmptyEntries);
        if (nameParts.Length > 1)
        {
            rootNode.AwsMetadata.MetricDirective.Namespace = nameParts[0];
            metricName = nameParts[1];
        }

        if (_exporterOptions.ResourceAttributesToIncludeAsDimensions is object &&
            _exporterOptions.ResourceAttributesToIncludeAsDimensions.Length > 0)
        {
            var resource = ParentProvider.GetResource();
            foreach (var attribute in resource.Attributes)
            {
                if (_exporterOptions.ResourceAttributesToIncludeAsDimensions.Contains(attribute.Key))
                {
                    if (attribute.Value?.ToString() is string value)
                    {
                        rootNode.AddDimension(attribute.Key, value);
                    }
                }
            }
        }

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
                rootNode.PutMetric(metricName, point.GetSumLong());
                break;
            case MetricType.DoubleSum:
                rootNode.PutMetric(metricName, point.GetSumDouble());
                break;
            case MetricType.LongGauge:
                rootNode.PutMetric(metricName, point.GetGaugeLastValueLong());
                break;
            case MetricType.DoubleGauge:
                rootNode.PutMetric(metricName, point.GetGaugeLastValueDouble());
                break;
            case MetricType.Histogram:
                var sum = point.GetHistogramSum();
                var count = point.GetHistogramCount();
                var avg = count > 0 ? sum / count : 0;
                rootNode.AwsMetadata.MetricDirective.Metrics.Add(new MetricDefinition(metricName)
                {
                    StatisticSet = new StatisticSet
                    {
                        Sum = sum,
                        Count = count,
                        // TODO: min/max not yet supported by OpenTelemetry SDK, so just set to the avg
                        Min = avg,
                        Max = avg,
                    },
                });
                break;
            default:
                throw new NotImplementedException();
        }

        return rootNode;
    }
}
