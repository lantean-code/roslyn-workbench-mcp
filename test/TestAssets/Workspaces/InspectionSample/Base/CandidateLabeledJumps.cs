internal static class CandidateLabeledJumps
{
    internal static void First(bool shouldExit)
    {
        for (var outer = 0; outer < 10; outer++)
        {
            for (var inner = 0; inner < 10; inner++)
            {
                if (shouldExit)
                {
                    goto foundFirst;
                }
            }
        }

    foundFirst:
        System.Console.WriteLine();
    }

    internal static void Second(bool shouldExit)
    {
        for (var outer = 0; outer < 10; outer++)
        {
            for (var inner = 0; inner < 10; inner++)
            {
                if (shouldExit)
                {
                    goto foundSecond;
                }
            }
        }

    foundSecond:
        System.Console.WriteLine();
    }
}
