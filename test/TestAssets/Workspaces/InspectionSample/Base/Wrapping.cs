namespace Sample;

public static class WrappingSample
{
    public static void Goobar(int left, int right)
    {
    }

    public static void Run(int left, int right)
    {
        Goobar(left, right);
    }
}
