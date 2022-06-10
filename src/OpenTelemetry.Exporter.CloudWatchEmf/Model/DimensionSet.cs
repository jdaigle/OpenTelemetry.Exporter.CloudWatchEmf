namespace OpenTelemetry.Exporter.CloudWatchEmf.Model;

public class DimensionSet
{
    /// <summary>
    /// Creates a DimensionSet with one Dimension with the specified key-value pair.
    /// </summary>
    /// <param name="key">the key for the dimension</param>
    /// <param name="value">the value for the dimension</param>
    public DimensionSet(string key, string value)
    {
        Dimensions[key] = value;
    }

    public Dictionary<string, string> Dimensions { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Adds a dimension to this DimensionSet
    /// </summary>
    /// <param name="key">the dimension name</param>
    /// <param name="value">the dimension value</param>
    public void AddDimension(string key, string value)
    {
        Dimensions[key] = value;
    }

    /// <summary>
    /// Get all the dimension names in the dimension set.
    /// </summary>
    public IEnumerable<string> DimensionKeys => Dimensions.Keys;
}
