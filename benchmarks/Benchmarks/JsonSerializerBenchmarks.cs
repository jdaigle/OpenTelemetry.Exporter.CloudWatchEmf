using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.CloudWatchEmf.Model;

namespace Benchmarks;

public class JsonSerializerBenchmarks
{
    private RootNode _rootNode;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _rootNode = new RootNode();
        _rootNode.AddDimension("Dimension1", "Value1");
        _rootNode.AddDimension("Dimension2", "Value2");
        _rootNode.AddDimension("Dimension3", "Value3");
        _rootNode.PutMetric("Metric1", Math.PI, Unit.NONE);
        _rootNode.PutMetric("Metric2", Math.Tau, Unit.NONE);
        _rootNode.PutMetric("Metric3", Math.E, Unit.NONE);
    }

    [Benchmark]
    public string ToJsonWithJsonTextWriter()
    {
        return _rootNode.ToJson();
    }
}
