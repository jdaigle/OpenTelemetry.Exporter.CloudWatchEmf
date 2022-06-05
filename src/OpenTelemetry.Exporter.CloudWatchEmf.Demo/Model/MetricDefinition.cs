using Newtonsoft.Json;

namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class MetricDefinition
{
    public MetricDefinition(string name) : this(name, Unit.NONE, new List<double>())
    {
    }

    public MetricDefinition(string name, double value) : this(name, Unit.NONE, new List<double> { value })
    {
    }

    public MetricDefinition(string name, Unit unit, double value) : this(name, unit, new List<double> { value })
    {
    }

    public MetricDefinition(string name, Unit unit, List<double> values)
    {
        Name = name;
        Unit = unit;
        Values = values;
    }

    public void AddValue(double value)
    {
        Values.Add(value);
    }

    [JsonProperty("Name")]
    public string Name { get; set; }

    public List<double> Values { get; }

    public StatisticSet? StatisticSet { get; set; }

    [JsonProperty("Unit")]
    public Unit Unit { get; set; }
}
