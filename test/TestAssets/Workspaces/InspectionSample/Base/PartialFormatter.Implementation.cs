namespace Sample;

public partial class PartialFormatter
{
    public partial string Build(string value)
    {
        var unusedPartial = value.Length;
        return value.Trim();
    }
}
