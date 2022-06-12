using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.CloudWatchEmf;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the CloudWatchEmf metric exporter.
/// </summary>
public static class CloudWatchEmfMetricExporterMetricBuilderProviderExtensions
{
    /// <summary>
    /// Adds <see cref="CloudWatchEmfMetricExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddCloudWatchEmfMetricExporter(this MeterProviderBuilder builder)
    {
        return AddCloudWatchEmfMetricExporter(builder, options => { });
    }

    /// <summary>
    /// Adds <see cref="CloudWatchEmfMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Exporter configuration options.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddCloudWatchEmfMetricExporter(this MeterProviderBuilder builder, Action<CloudWatchEmfMetricExporterOptions> configureExporter)
    {
        if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            return deferredMeterProviderBuilder.Configure((sp, builder) =>
            {
                var exporterOptions = sp.GetService<IOptions<CloudWatchEmfMetricExporterOptions>>()?.Value ?? new CloudWatchEmfMetricExporterOptions();
                exporterOptions.CloudWatchPusherOptions = sp.GetService<IOptions<CloudWatchPusherOptions>>()?.Value ?? new CloudWatchPusherOptions();
                AddCloudWatchEmfMetricExporter(builder, exporterOptions, configureExporter);
            });
        }

        return AddCloudWatchEmfMetricExporter(builder, new CloudWatchEmfMetricExporterOptions(), configureExporter);
    }

    private static MeterProviderBuilder AddCloudWatchEmfMetricExporter(this MeterProviderBuilder builder, CloudWatchEmfMetricExporterOptions exporterOptions, Action<CloudWatchEmfMetricExporterOptions> configureExporter)
    {
        configureExporter?.Invoke(exporterOptions);

        var exporter = new CloudWatchEmfMetricExporter(exporterOptions);

        var metricReader = new PeriodicExportingMetricReader(exporter, exporterOptions.ExportIntervalMilliseconds, exporterOptions.ExportTimeoutMilliseconds)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Delta,
        };

        return builder.AddReader(metricReader);
    }
}
