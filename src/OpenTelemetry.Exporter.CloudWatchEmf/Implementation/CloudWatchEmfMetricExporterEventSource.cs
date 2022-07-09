using System.Diagnostics.Tracing;
using System.Net;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Implementation;

[EventSource(Name = "OpenTelemetry-Exporter-CloudWatchEmf")]
internal class CloudWatchEmfMetricExporterEventSource : EventSource
{
    public static readonly CloudWatchEmfMetricExporterEventSource Log = new();

    [Event(1, Message = "CloudWatchLogs API Error for method {0} and parameter {1}. Return status code {2}.", Level = EventLevel.Error)]
    public void CloudWatchLogsApiError(string method, string parameter, HttpStatusCode httpStatusCode)
        => WriteEvent(1, method, parameter, (int)httpStatusCode);

    [NonEvent]
    public void CloudWatchLogsApiException(string method, Exception exception)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            CloudWatchLogsApiException(method, exception.ToString());
        }
    }

    [Event(2, Message = "CloudWatchLogs API Exception for method {0} threw exception {1}.", Level = EventLevel.Error)]
    public void CloudWatchLogsApiException(string method, string exception)
        => WriteEvent(2, method, exception);

    [Event(3, Message = "Sending {0} events to CloudWatch Logs Log Group \"{1}\" Stream \"{2}\"", Level = EventLevel.Verbose)]
    public void SendingCloudWatchLogEvents(int count, string logGroupName, string logStreamName)
        => WriteEvent(3, count, logGroupName, logStreamName);

    [NonEvent]
    public void ExceptionCallingPushMetricsToCloudWatch(Exception exception)
    {
        if (Log.IsEnabled(EventLevel.Error, EventKeywords.All))
        {
            ExceptionCallingPushMetricsToCloudWatch(exception.ToString());
        }
    }

    [Event(4, Message = "Exception thrown when pushing metrics to CloudWatch: {0}", Level = EventLevel.Error)]
    public void ExceptionCallingPushMetricsToCloudWatch(string exception)
        => WriteEvent(4, exception);
}
