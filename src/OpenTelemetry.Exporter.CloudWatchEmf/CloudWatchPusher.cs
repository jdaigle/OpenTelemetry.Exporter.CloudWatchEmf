using System.Text;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using OpenTelemetry.Exporter.CloudWatchEmf.Model;

namespace OpenTelemetry.Exporter.CloudWatchEmf;

/// <summary>
/// Pushes metrics to CloudWatch Logs using the Embedded Metrics Format.
/// </summary>
public class CloudWatchPusher
{
    private static readonly Encoding _unicodeEncoding = Encoding.Unicode;
    private static readonly int _maxMessageSizeInBytes = 1024 * 1024; // 1 MB

    private readonly CloudWatchPusherOptions _options;
    private readonly IAmazonCloudWatchLogs _client = new AmazonCloudWatchLogsClient();

    private int _totalMessageSize = 0;
    private readonly PutLogEventsRequest _putLogEventsRequest = new PutLogEventsRequest();

    /// <summary>
    /// Create a new instance of <see cref="CloudWatchPusher"/>.
    /// </summary>
    /// <param name="options">The <see cref="CloudWatchPusherOptions"/> for this instance.</param>
    public CloudWatchPusher(CloudWatchPusherOptions options)
    {
        _options = options;
        _putLogEventsRequest.LogGroupName = _options.LogGroup;
        ((AmazonCloudWatchLogsClient)_client).BeforeRequestEvent += ServiceClientBeforeRequestEvent;
    }

    /// <summary>
    /// Sends a batch of EMF log messages to CloudWatch
    /// </summary>
    /// <param name="batch">The batch of EMF log messages to send to CloudWatch.</param>
    internal void PushMetricsToCloudWatch(in IList<CloudWatchRootNode> batch)
    {
        _totalMessageSize = 0;
        _putLogEventsRequest.LogEvents.Clear();

        foreach (var item in batch)
        {
            AddMessage(item.ToJson());
        }

        Flush();
    }

    private void AddMessage(string rawMessage)
    {
        var prospectiveLengthInBytes = _totalMessageSize + _unicodeEncoding.GetMaxByteCount(rawMessage.Length);
        if (prospectiveLengthInBytes > _maxMessageSizeInBytes)
        {
            // Flush current batch
            Flush();
        }

        _totalMessageSize += _unicodeEncoding.GetMaxByteCount(rawMessage.Length);
        _putLogEventsRequest.LogEvents.Add(new InputLogEvent
        {
            Timestamp = DateTime.UtcNow,
            Message = rawMessage,
        });
    }

    private void Flush()
    {
        if (_putLogEventsRequest.LogEvents.Count > 0)
        {
            // Make sure the log events are in the right order.
            _putLogEventsRequest.LogEvents.Sort((ev1, ev2) => ev1.Timestamp.CompareTo(ev2.Timestamp));

            using var cancellationTokenSource = new CancellationTokenSource(_options.SendTimeout);

            // Blocking is required since the exporter cannot be async. But we can timeout/cancel.
            SendMessagesAsync(cancellationTokenSource.Token).GetAwaiter().GetResult();
        }

        _totalMessageSize = 0;
        _putLogEventsRequest.LogEvents.Clear();
    }

    private void ServiceClientBeforeRequestEvent(object sender, RequestEventArgs e)
    {
        if (e is WebServiceRequestEventArgs args)
        {
            args.Headers["x-amzn-logs-format"] = "json/emf";
        }
    }

    private async Task SetUpLogStreamAsync(CancellationToken cancellationToken)
    {
        var logGroupName = _options.LogGroup;

        var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
        {
            LogGroupNamePrefix = logGroupName,
        }, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessStatusCode(logGroupResponse))
        {
            LogError($"DescribeLogGroups {logGroupName} returned status: {logGroupResponse.HttpStatusCode}");
        }

        if (!logGroupResponse.LogGroups.Any(x => string.Equals(x.LogGroupName, logGroupName, StringComparison.Ordinal)))
        {
            var createGroupResponse = await _client.CreateLogGroupAsync(new CreateLogGroupRequest
            {
                LogGroupName = logGroupName
            }, cancellationToken).ConfigureAwait(false);
            if (!IsSuccessStatusCode(logGroupResponse))
            {
                LogError($"CreateLogGroup {logGroupName} returned status: {logGroupResponse.HttpStatusCode}");
            }
        }

        var currentStreamName = "opentelementry-metrics-emf-" + Guid.NewGuid().ToString();

        var streamResponse = await _client.CreateLogStreamAsync(new CreateLogStreamRequest
        {
            LogGroupName = logGroupName,
            LogStreamName = currentStreamName
        }, cancellationToken).ConfigureAwait(false);
        if (!IsSuccessStatusCode(logGroupResponse))
        {
            LogError($"CreateLogStream {logGroupName} returned status: {logGroupResponse.HttpStatusCode}");
        }

        _putLogEventsRequest.LogStreamName = currentStreamName;
        _putLogEventsRequest.SequenceToken = null;
    }

    private async Task SendMessagesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_putLogEventsRequest.LogStreamName))
        {
            await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        // Loop to handle exceptions
        while (!cancellationToken.IsCancellationRequested && _putLogEventsRequest.LogEvents.Count > 0)
        {
            try
            {
                var response = await _client.PutLogEventsAsync(_putLogEventsRequest, cancellationToken).ConfigureAwait(false);
                _putLogEventsRequest.SequenceToken = response.NextSequenceToken;
                _putLogEventsRequest.LogEvents.Clear();
                _totalMessageSize = 0;
            }
            catch (InvalidSequenceTokenException ex)
            {
                // In case the NextSequenceToken is invalid for the last sent message, refresh or create new stream.
                LogError(ex);
                await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ResourceNotFoundException ex)
            {
                // The specified log stream does not exist. Refresh or create new stream.
                LogError(ex);
                await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(ex);
                await Task.Delay(Math.Max(100, DateTime.UtcNow.Second * 10), cancellationToken); // backoff a bit
            }
        }
    }

    private void LogError(string error)
    {
        Console.WriteLine(error);
    }

    private void LogError(Exception ex)
    {
        Console.WriteLine(ex.ToString());
    }

    private static bool IsSuccessStatusCode(AmazonWebServiceResponse serviceResponse) => (int)serviceResponse.HttpStatusCode >= 200 && (int)serviceResponse.HttpStatusCode <= 299;
}
