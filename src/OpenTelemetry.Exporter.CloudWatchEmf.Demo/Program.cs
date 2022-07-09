using System.Diagnostics.Metrics;
using System.Diagnostics.Tracing;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo;

public class Program
{
    public static async Task Main()
    {
        var listener = new ConsoleWriterEventListener();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddDefaultAWSOptions(context.Configuration.GetAWSOptions());

                services.AddOpenTelemetryMetrics(builder =>
                {
                    builder
                        .SetResourceBuilder(
                            ResourceBuilder.CreateDefault().AddTelemetrySdk().AddService("DemoService")
                        )
                        .AddMeter("OpenTelemetry.Exporter.CloudWatchEmf.Demo")
                        // Can add a view to customize the metrics
                        .AddView(instrument =>
                        {
                            if (instrument.Meter.Name == "OpenTelemetry.Exporter.CloudWatchEmf.Demo")
                            {
                                if (instrument.Name == "sample_metrics.duration")
                                {
                                    return new ExplicitBucketHistogramConfiguration
                                    {
                                        Boundaries = Array.Empty<double>(), // An empty array would result in no histogram buckets being
                                        TagKeys = new[] { "Foo", "Baz" }, // filter the Tags for aggregation and sending
                                    };
                                }
                                return new MetricStreamConfiguration()
                                {
                                    TagKeys = new[] { "Foo", "Baz" }, // filter the Tags for aggregation and sending
                                };
                            }
                            return null;
                        })
                        .AddCloudWatchEmfMetricExporter(options =>
                        {
                            options.ExportIntervalMilliseconds = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
                            options.ExportTimeoutMilliseconds = (int)TimeSpan.FromSeconds(10).TotalMilliseconds;
                            options.ResourceAttributesToIncludeAsDimensions = new string[] { "service.name", "service.instance.id" };
                            options.CloudWatchPusherOptions.SendTimeout = TimeSpan.FromSeconds(10);
                            options.CloudWatchPusherOptions.LogGroup = "/metrics/demo";
                            // These can effectively be "static" dimensions added to each metric, like instance id or environment.
                        });
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
        private static readonly AssemblyName _assemblyName = typeof(SampleMetricGenerator).Assembly.GetName();
        private static readonly string _instrumentationName = _assemblyName.Name!;
        private static readonly string _instrumentationVersion = _assemblyName.Version!.ToString();

        private static readonly Meter SameMetricMeter = new Meter(_instrumentationName, _instrumentationVersion);

        public static readonly Counter<long> SuccessTotalCounter =
            SameMetricMeter.CreateCounter<long>("sample_metrics.successes", description: "Total Successes.", unit: "Count");

        public static readonly ObservableGauge<long> CurrentMinuteGauge =
            SameMetricMeter.CreateObservableGauge<long>("sample_metrics.current_minute", GetCurrentMinuteGaugeValue, description: "Gauge representing the current minute of the hour.");

        public static readonly Histogram<double> CriticalTimeSecondsHistogram =
            SameMetricMeter.CreateHistogram<double>("sample_metrics.duration", description: "Sample duration in Milliseconds", unit: "Milliseconds");

        private static Measurement<long> GetCurrentMinuteGaugeValue()
        {
            var now = DateTime.UtcNow;
            return new Measurement<long>(now.Minute);
        }

        private bool _running = false;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _running = true;
            Task.Run(async () =>
            {
                var rand = new Random();
                while (_running)
                {
                    try
                    {
                        var latency = rand.Next(100, 2000);
                        await Task.Delay(TimeSpan.FromMilliseconds(latency));
                        SuccessTotalCounter.Add(1, new KeyValuePair<string, object?>("Foo", latency % 3), new KeyValuePair<string, object?>("Bar", "Baz"));
                        CriticalTimeSecondsHistogram.Record(latency, new KeyValuePair<string, object?>("Foo", latency % 3), new KeyValuePair<string, object?>("Bar", "Baz"));
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
            _running = false;
            return Task.CompletedTask;
        }
    }

    public class ConsoleWriterEventListener : EventListener
    {
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("OpenTelemetry", StringComparison.OrdinalIgnoreCase))
            {
                EnableEvents(eventSource, EventLevel.Verbose);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            Console.WriteLine(eventData.TimeStamp + " " + eventData.EventName + " " + eventData.Level + " " + string.Format(eventData.Message!, eventData.Payload?.ToArray() ?? Array.Empty<string>()));
        }
    }
}
