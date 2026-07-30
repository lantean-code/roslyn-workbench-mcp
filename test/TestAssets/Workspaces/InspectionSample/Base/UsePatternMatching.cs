namespace Sample;

public sealed class UsePatternMatchingSample
{
    private int i;

    public bool GetValue(object obj)
    {
        return obj is UsePatternMatchingSample && ((UsePatternMatchingSample)obj).i > 0;
    }
}