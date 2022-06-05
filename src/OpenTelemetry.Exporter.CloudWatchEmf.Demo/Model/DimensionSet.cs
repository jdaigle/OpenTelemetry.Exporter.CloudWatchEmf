namespace OpenTelemetry.Exporter.CloudWatchEmf.Demo.Model;

public class DimensionSet
{
    /// <summary>
    /// Creates an empty DimensionSet
    /// </summary>
    public DimensionSet()
    {
    }

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
    /// Append all the dimensions from the specified dimension set to this one and return this dimension set.
    /// </summary>
    /// <param name="other">The dimension set to append to this one</param>
    /// <returns>this dimension set with the other appended</returns>
    public DimensionSet AddRange(DimensionSet other)
    {
        foreach (var dimension in other.Dimensions)
        {
            Dimensions[dimension.Key] = dimension.Value;
        }
        return this;
    }

    /// <summary>
    /// Get all the dimension names in the dimension set.
    /// </summary>
    public IEnumerable<string> DimensionKeys => Dimensions.Keys;

    public DimensionSet DeepClone()
    {
        var clone = new DimensionSet();
        foreach (var dimension in Dimensions)
        {
            clone.Dimensions.Add(dimension.Key, dimension.Value);
        }
        return clone;
    }
}
