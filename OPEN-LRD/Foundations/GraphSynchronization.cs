using System;

namespace OPEN_LRD
{
    internal static class GraphSynchronization
    {
        private static readonly object _syncRoot = new object();

        internal static T Execute<T>(Func<T> action)
        {
            lock (_syncRoot)
            {
                return action();
            }
        }

        internal static void ExecuteAction(Action action)
        {
            lock (_syncRoot)
            {
                action();
            }
        }

        internal static void Execute(Action action)
        {
            ExecuteAction(action);
        }
    }
}
