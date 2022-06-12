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
                        .AddCloudWatchEmfMetricExporter(options =>
                        {
                        })
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
}
