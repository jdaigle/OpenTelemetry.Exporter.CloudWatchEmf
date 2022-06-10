using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Exporter.CloudWatchEmf.Model;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo;

public class Program
{
    public static async Task Main()
    {
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDefaultAWSOptions(context.Configuration.GetAWSOptions());

                services.AddOpenTelemetryMetrics(builder =>
                {
                    builder
                        .SetResourceBuilder(
                            ResourceBuilder.CreateDefault().AddTelemetrySdk().AddService("My Service")
                        )
                        .AddMeter("OpenTelemetry.Exporter.CloudWatchEmf.Demo")
                        .AddReader(new PeriodicExportingMetricReader(new TestMetricExporter(), exportIntervalMilliseconds: 5000) { TemporalityPreference = MetricReaderTemporalityPreference.Delta })
                        //.AddConsoleExporter((expoterOptions, readerOptions) =>
                        // {
                        //     readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
                        //     readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
                        // })
                        ;
                });
            })
            .ConfigureServices((c, services) =>
            {
                services.AddSingleton<IHostedService, SampleMetricGenerator>();
            })
            .UseConsoleLifetime()
            .Build();

        await host.RunAsync();
    }

    public class SampleMetricGenerator : IHostedService
    {
        private static readonly AssemblyName AssemblyName = typeof(SampleMetricGenerator).Assembly.GetName();
        private static readonly string InstrumentationName = AssemblyName.Name!;
        private static readonly string InstrumentationVersion = AssemblyName.Version!.ToString();

        private static readonly Meter SameMetricMeter = new(InstrumentationName, InstrumentationVersion);

        private static readonly Counter<long> SuccessTotalCounter =
            SameMetricMeter.CreateCounter<long>("messaging.successes", "Total messages processed successfully by the transport.");
        private static readonly Histogram<double> CriticalTimeSecondsHistogram =
            SameMetricMeter.CreateHistogram<double>("messaging.client_server.duration", "ms", "The duration from sending to processing the message. Also known as lead time.");

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                var rand = new Random();
                int counter = 0;
                while (true)
                {
                    try
                    {
                        var latency = rand.Next(100, 2000);
                        await Task.Delay(TimeSpan.FromMilliseconds(latency));
                        counter++;
                        if (counter % 3 == 0)
                        {
                            SuccessTotalCounter.Add(1, new KeyValuePair<string, object?>("TagName1", "Value%3"), new KeyValuePair<string, object?>("TagName2", "Value%3"));
                            CriticalTimeSecondsHistogram.Record(latency, new KeyValuePair<string, object?>("TagName1", "Value%3"), new KeyValuePair<string, object?>("TagName2", "Value%3"));
                        }
                        else if (counter % 2 == 0)
                        {
                            SuccessTotalCounter.Add(1, new KeyValuePair<string, object?>("TagName1", "Value%2"), new KeyValuePair<string, object?>("TagName2", "Value%2"));
                            CriticalTimeSecondsHistogram.Record(latency, new KeyValuePair<string, object?>("TagName1", "Value%2"), new KeyValuePair<string, object?>("TagName2", "Value%2"));
                        }
                        else
                        {
                            SuccessTotalCounter.Add(1, new KeyValuePair<string, object?>("TagName1", "Default"));
                            CriticalTimeSecondsHistogram.Record(latency, new KeyValuePair<string, object?>("TagName1", "Default"));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public class TestMetricExporter : BaseExporter<Metric>
    {
        private IAmazonCloudWatchLogs _client = new AmazonCloudWatchLogsClient();
        private LogEventBatch? _logEventBatch;

        public TestMetricExporter()
        {
            ((AmazonCloudWatchLogsClient)this._client).BeforeRequestEvent += ServiceClientBeforeRequestEvent;
        }

        private void ServiceClientBeforeRequestEvent(object sender, Amazon.Runtime.RequestEventArgs e)
        {
            if (e is Amazon.Runtime.WebServiceRequestEventArgs args)
            {
                args.Headers["x-amzn-logs-format"] = "json/emf";
            }
        }

        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            return base.OnForceFlush(timeoutMilliseconds);
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Exporting batch of {batch.Count} metrics");
            if (batch.Count > 0)
            {
                foreach (var item in batch)
                {
                    foreach (var point in item.GetMetricPoints())
                    {
                        var rootNode = new RootNode();
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

                        switch (item.MetricType)
                        {
                            case MetricType.LongSum:
                                rootNode.PutMetric(item.Name, point.GetSumLong(), Unit.NONE);
                                break;
                            case MetricType.DoubleSum:
                                rootNode.PutMetric(item.Name, point.GetSumDouble(), Unit.NONE);
                                break;
                            case MetricType.LongGauge:
                                rootNode.PutMetric(item.Name, point.GetGaugeLastValueLong(), Unit.NONE);
                                break;
                            case MetricType.DoubleGauge:
                                rootNode.PutMetric(item.Name, point.GetGaugeLastValueDouble(), Unit.NONE);
                                break;
                            case MetricType.Histogram:
                                // TODO: needs to emit a metric value like
                                // {
                                //     "Max": 0,
                                //     "Min": 0,
                                //     "Count": 3,
                                //     "Sum": 71.898217
                                // }
                                var buckets = point.GetHistogramBuckets();
                                var sum = point.GetHistogramSum();
                                var count = point.GetHistogramCount();
                                // TODO: current version does not support Min/Max. But we could probably derive that from the min/max bucket for now.
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
                                var metric = new MetricDefinition(item.Name)
                                {
                                    StatisticSet = new StatisticSet
                                    {
                                        Sum = point.GetHistogramSum(),
                                        Count = point.GetHistogramCount(),
                                        Min = min,
                                        Max = max,
                                    },
                                };
                                rootNode.AwsMetadata.MetricDirective.Metrics.Add(metric);
                                break;
                            default:
                                throw new NotImplementedException();
                        }

                        var message = rootNode.ToJson();
                        if (_logEventBatch is null)
                        {
                            //SetUpLogStreamAsync().GetAwaiter().GetResult(); // cannot await
                        }
                        if (_logEventBatch is null)
                        {
                            throw new System.InvalidOperationException();
                        }
                        _logEventBatch.AddMessage(new InputLogEvent { Message = message, Timestamp = DateTime.UtcNow });
                        var messageSize = Encoding.Unicode.GetMaxByteCount(message.Length);
                        Console.WriteLine(message + $"({messageSize} bytes)");
                        // flush
                        //SendMessagesAsync().GetAwaiter().GetResult(); // cannot await
                    }
                }
            }
            return ExportResult.Success;
        }

        private async Task SendMessagesAsync(CancellationToken cancellationToken = default)
        {
            if (_logEventBatch is null)
            {
                return;
            }
            try
            {
                //Make sure the log events are in the right order.
                _logEventBatch._request.LogEvents.Sort((ev1, ev2) => ev1.Timestamp.CompareTo(ev2.Timestamp));    
                var response = await _client.PutLogEventsAsync(_logEventBatch._request, cancellationToken).ConfigureAwait(false);
                _logEventBatch.Reset(response.NextSequenceToken);
                //_requestCount = 5;
            }
            catch (InvalidSequenceTokenException ex)
            {
                //In case the NextSequenceToken is invalid for the last sent message, a new stream would be 
                //created for the said application.
                //LogLibraryServiceError(ex);
                Console.WriteLine(ex);

                //if (_requestCount > 0)
                //{
                //    _requestCount--;
                //    var regexResult = invalid_sequence_token_regex.Match(ex.Message);
                //    if (regexResult.Success)
                //    {
                //        _repo._request.SequenceToken = regexResult.Groups[1].Value;
                //        await SendMessages(token).ConfigureAwait(false);
                //    }
                //}
                //else
                {
                    await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
                    //_currentStreamName = await LogEventTransmissionSetup(token).ConfigureAwait(false);
                }
            }
            catch (ResourceNotFoundException ex)
            {
                // The specified log stream does not exist. Refresh or create new stream.
                //LogLibraryServiceError(ex);
                Console.WriteLine(ex);

                await SetUpLogStreamAsync(cancellationToken).ConfigureAwait(false);
                //_currentStreamName = await LogEventTransmissionSetup(token).ConfigureAwait(false);
            }
        }

        private async Task<string> SetUpLogStreamAsync(CancellationToken cancellationToken = default)
        {
            //string serviceURL = GetServiceUrl();

            //if (!_config.DisableLogGroupCreation)
            {
                var logGroupResponse = await _client.DescribeLogGroupsAsync(new DescribeLogGroupsRequest
                {
                    LogGroupNamePrefix = "/metrics/default"
                }, cancellationToken).ConfigureAwait(false);
                //if (!IsSuccessStatusCode(logGroupResponse))
                //{
                //    LogLibraryServiceError(new System.Net.WebException($"Lookup LogGroup {_config.LogGroup} returned status: {logGroupResponse.HttpStatusCode}"), serviceURL);
                //}

                if (logGroupResponse.LogGroups.FirstOrDefault(x => string.Equals(x.LogGroupName, "/metrics/default", StringComparison.Ordinal)) == null)
                {
                    var createGroupResponse = await _client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = "/metrics/default" }, cancellationToken).ConfigureAwait(false);
                    //if (!IsSuccessStatusCode(createGroupResponse))
                    //{
                    //    LogLibraryServiceError(new System.Net.WebException($"Create LogGroup {_config.LogGroup} returned status: {createGroupResponse.HttpStatusCode}"), serviceURL);
                    //}
                }
            }

            var currentStreamName = "otel-stream " + Guid.NewGuid().ToString();

            var streamResponse = await _client.CreateLogStreamAsync(new CreateLogStreamRequest
            {
                LogGroupName = "/metrics/default",
                LogStreamName = currentStreamName
            }, cancellationToken).ConfigureAwait(false);
            //if (!IsSuccessStatusCode(streamResponse))
            //{
            //    LogLibraryServiceError(new System.Net.WebException($"Create LogStream {currentStreamName} for LogGroup {_config.LogGroup} returned status: {streamResponse.HttpStatusCode}"), serviceURL);
            //}

            _logEventBatch = new LogEventBatch("/metrics/default", currentStreamName, timeIntervalBetweenPushes: 60, maxBatchSize: 1024 * 1024);

            return currentStreamName;
        }

        private class LogEventBatch
        {
            public TimeSpan TimeIntervalBetweenPushes { get; private set; }
            public int MaxBatchSize { get; private set; }

            public bool ShouldSendRequest(int maxQueuedEvents)
            {
                if (_request.LogEvents.Count == 0)
                    return false;

                if (_nextPushTime < DateTime.UtcNow)
                    return true;

                if (maxQueuedEvents <= _request.LogEvents.Count)
                    return true;

                return false;
            }

            int _totalMessageSize { get; set; }
            DateTime _nextPushTime;
            public PutLogEventsRequest _request = new PutLogEventsRequest();
            public LogEventBatch(string logGroupName, string streamName, int timeIntervalBetweenPushes, int maxBatchSize)
            {
                _request.LogGroupName = logGroupName;
                _request.LogStreamName = streamName;
                TimeIntervalBetweenPushes = TimeSpan.FromSeconds(timeIntervalBetweenPushes);
                MaxBatchSize = maxBatchSize;
                Reset(null);
            }

            public LogEventBatch()
            {
            }

            public int CurrentBatchMessageCount
            {
                get { return this._request.LogEvents.Count; }
            }

            public bool IsEmpty => _request.LogEvents.Count == 0;

            public bool IsSizeConstraintViolated(string message)
            {
                Encoding unicode = Encoding.Unicode;
                int prospectiveLength = _totalMessageSize + unicode.GetMaxByteCount(message.Length);
                if (MaxBatchSize < prospectiveLength)
                    return true;

                return false;
            }

            public void AddMessage(InputLogEvent ev)
            {
                Encoding unicode = Encoding.Unicode;
                _totalMessageSize += unicode.GetMaxByteCount(ev.Message.Length);
                _request.LogEvents.Add(ev);
            }

            public void Reset(string? SeqToken)
            {
                _request.LogEvents.Clear();
                _totalMessageSize = 0;
                _request.SequenceToken = SeqToken;
                _nextPushTime = DateTime.UtcNow.Add(TimeIntervalBetweenPushes);
            }
        }
    }
}
