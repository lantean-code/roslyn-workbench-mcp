using System.Threading.Tasks;

namespace Sample
{
    internal static class CandidateCSharp4
    {
        internal static int MakeMethodAsynchronous(Task operation)
        {
            await operation;
            return 1;
        }
    }
}
