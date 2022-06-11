using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTelemetry.Exporter.CloudWatchEmf;

public class CloudWatchPusherOptions
{
    public string LogGroup = "/metrics/default";

    /// <summary>
    /// Internal MonitorSleepTime property. This specifies the timespan after which the Monitor wakes up.
    /// MonitorSleepTime  dictates the timespan after which the Monitor checks the size and time constarint on the batch log event and the existing in-memory buffer for new messages.
    /// <para>
    /// The value is 500 Milliseconds.
    /// </para>
    /// </summary>
    public TimeSpan MonitorSleepTime = TimeSpan.FromMilliseconds(500);
}

public class CloudWatchPusher /*: IHostedService*/
{
    private static readonly Encoding _unicodeEncoding = Encoding.Unicode;

    private readonly IOptions<CloudWatchPusherOptions> _options;
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
    private readonly IAmazonCloudWatchLogs _client = new AmazonCloudWatchLogsClient();


    private CancellationTokenSource? _cancelMonitorSource;

    private readonly PutLogEventsRequest _putLogEventsRequest = new PutLogEventsRequest();
    private int _totalMessageSize = 0;

    public CloudWatchPusher(IOptions<CloudWatchPusherOptions> options)
    {
        _options = options;
        _putLogEventsRequest.LogGroupName = _options.Value.LogGroup;
        ((AmazonCloudWatchLogsClient)this._client).BeforeRequestEvent += ServiceClientBeforeRequestEvent;
    }

    public void AddMessage(string rawMessage)
    {
        const int MaxMessageSize = 1024 * 1024;
        var prospectiveLength = _totalMessageSize + _unicodeEncoding.GetMaxByteCount(rawMessage.Length);
        if (prospectiveLength > MaxMessageSize)
        {
            // flush now

        }

        _totalMessageSize += _unicodeEncoding.GetMaxByteCount(rawMessage.Length);
        _putLogEventsRequest.LogEvents.Add(new InputLogEvent
        {
            Timestamp = DateTime.UtcNow,
            Message = rawMessage,
        });
    }

    public void Flush()
    {

    }

    //public Task StartAsync(CancellationToken cancellationToken)
    //{
    //    _cancelMonitorSource = new CancellationTokenSource();
    //    Task.Run(async () =>
    //    {
    //        await Monitor(_cancelMonitorSource.Token);
    //    });
    //    return Task.CompletedTask;
    //}

    //private async Task Monitor(CancellationToken token)
    //{
    //    while (!token.IsCancellationRequested)
    //    {
    //        await Task.Delay(_options.Value.MonitorSleepTime);
    //    }
    //}

    //public Task StopAsync(CancellationToken cancellationToken)
    //{
    //    try
    //    {
    //        Flush();
    //        _cancelMonitorSource?.Cancel();
    //    }
    //    catch (Exception)
    //    {
    //        // LogLibraryServiceError(ex);
    //    }
    //    return Task.CompletedTask;
    //}

    private void ServiceClientBeforeRequestEvent(object sender, Amazon.Runtime.RequestEventArgs e)
    {
        if (e is WebServiceRequestEventArgs args)
        {
            args.Headers["x-amzn-logs-format"] = "json/emf";
        }
    }

    private async Task SetUpLogStreamAsync(CancellationToken cancellationToken = default)
    {
        var logGroupName = _options.Value.LogGroup;

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

    private async Task SendMessagesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_putLogEventsRequest.LogStreamName))
        {
            await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        // Loop to handle exceptions
        while (_putLogEventsRequest.LogEvents.Count > 0)
        {
            // Ensures only a single thread is sending at a time.
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                // Make sure the log events are in the right order.
                _putLogEventsRequest.LogEvents.Sort((ev1, ev2) => ev1.Timestamp.CompareTo(ev2.Timestamp));
                var response = await _client.PutLogEventsAsync(_putLogEventsRequest, cancellationToken).ConfigureAwait(false);
                _putLogEventsRequest.LogEvents.Clear();
                _putLogEventsRequest.SequenceToken = response.NextSequenceToken;
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
            finally
            {
                _sendLock.Release();
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

    //private class LogEventBatch
    //{
    //    private static readonly Encoding _unicodeEncoding = Encoding.Unicode;


    //    public LogEventBatch(string logGroupName, string streamName, TimeSpan timeIntervalBetweenPushes, int maxBatchSize)
    //    {
    //        Request.LogGroupName = logGroupName;
    //        Request.LogStreamName = streamName;
    //        TimeIntervalBetweenPushes = timeIntervalBetweenPushes;
    //        MaxBatchSize = maxBatchSize;
    //        Reset(nextSequenceToken: null);
    //    }

    //    public LogEventBatch()
    //    {
    //    }

    //    public TimeSpan TimeIntervalBetweenPushes { get; }

    //    public int MaxBatchSize { get; }

    //    public PutLogEventsRequest Request { get; } = new PutLogEventsRequest();

    //    public int TotalMessageSize { get; private set; }

    //    public DateTime NextPushTime { get; private set; }

    //    public int CurrentBatchMessageCount => Request.LogEvents.Count;

    //    public bool IsEmpty => Request.LogEvents.Count == 0;

    //    public bool ShouldSendRequest(int maxQueuedEvents)
    //    {
    //        if (Request.LogEvents.Count == 0)
    //        {
    //            return false;
    //        }

    //        if (NextPushTime <= DateTime.UtcNow)
    //        {
    //            return true;
    //        }

    //        if (maxQueuedEvents <= Request.LogEvents.Count)
    //            return true;

    //        return false;
    //    }

    //    public bool IsSizeConstraintViolated(string message)
    //    {
    //        var prospectiveLength = TotalMessageSize + _unicodeEncoding.GetMaxByteCount(message.Length);
    //        return prospectiveLength > MaxBatchSize;
    //    }

    //    public void AddMessage(InputLogEvent ev)
    //    {
    //        TotalMessageSize += _unicodeEncoding.GetMaxByteCount(ev.Message.Length);
    //        Request.LogEvents.Add(ev);
    //    }

    //    public void Reset(string? nextSequenceToken)
    //    {
    //        Request.LogEvents.Clear();
    //        TotalMessageSize = 0;
    //        Request.SequenceToken = nextSequenceToken;
    //        NextPushTime = DateTime.UtcNow.Add(TimeIntervalBetweenPushes);
    //    }
    //}
}
