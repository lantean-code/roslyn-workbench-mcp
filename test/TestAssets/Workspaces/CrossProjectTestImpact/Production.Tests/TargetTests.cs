namespace CrossProjectTestImpact;

public sealed class TargetTests
{
    private readonly Target _target = new();

    public void AccessingTest()
    {
        _target.Execute();
    }

    public void UnrelatedTest()
    {
        var value = 1;
    }
}
