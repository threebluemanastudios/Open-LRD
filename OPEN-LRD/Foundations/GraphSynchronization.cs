using System;
using System.Threading;
using System.Threading.Tasks;

namespace OPEN_LRD.Foundations
{
    /// <summary>
    /// Provides assembly-wide mutual exclusion for graph state reads and mutations.
    /// </summary>
    /// <remarks>
    /// <para><b>THREAD-SAFETY CONTRACT:</b></para>
    /// <list type="number">
    ///   <item>
    ///     <term>Global Serialization:</term>
    ///     <description>
    ///     All delegates executed through this class are serialized against a single, process-wide,
    ///     non-fair mutual exclusion lock (<c>_syncRoot</c>). Concurrent calls from different threads block
    ///     until the active delegate completes or throws.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Reentrancy:</term>
    ///     <description>
    ///     Same-thread reentrancy is permitted by the underlying <see cref="Monitor"/>. Nested calls on
    ///     the same thread will not deadlock, but callers must ensure outer invariants hold before
    ///     invoking code that may re-enter <see cref="GraphSynchronization"/>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Lock Hierarchy (Deadlock Prevention):</term>
    ///     <description>
    ///     <c>_syncRoot</c> MUST be treated as a leaf lock. Delegates passed to <c>Execute</c> MUST NOT
    ///     acquire external locks, raise synchronous events to untrusted subscribers, or block on
    ///     asynchronous work (<c>.Wait()</c> / <c>.Result</c>). External locks must always be acquired
    ///     and released outside of <c>Execute</c>.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Synchronous Execution Only:</term>
    ///     <description>
    ///     Delegates MUST be strictly synchronous. Passing an asynchronous delegate (e.g., <c>Func&lt;Task&gt;</c>)
    ///     is prohibited because the lock is released as soon as the <see cref="Task"/> object is returned,
    ///     leaving subsequent asynchronous continuations unsynchronized.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Reference Escaping:</term>
    ///     <description>
    ///     Synchronization applies strictly for the duration of the delegate. Callers MUST NOT return
    ///     live, mutable internal graph references from <see cref="Execute{T}"/>; return immutable
    ///     values, snapshots, or defensive copies instead.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>Exception Safety:</term>
    ///     <description>
    ///     The lock is guaranteed to be released if the delegate throws an exception; however, callers
    ///     remain responsible for maintaining atomic state rollback on failure.
    ///     </description>
    ///   </item>
    /// </list>
    /// </remarks>
    internal static class GraphSynchronization
    {
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// Gets a value indicating whether the current thread holds the graph synchronization lock.
        /// </summary>
        /// <remarks>
        /// Intended for internal precondition assertions (for example,
        /// <c>Debug.Assert(GraphSynchronization.IsHeldByCurrentThread)</c>) so that internal graph
        /// methods do not have to redundantly re-acquire the lock to satisfy themselves.
        /// </remarks>
        internal static bool IsHeldByCurrentThread => Monitor.IsEntered(_syncRoot);

        /// <summary>
        /// Executes a synchronous function under the global graph lock and returns its result.
        /// </summary>
        /// <typeparam name="T">The return type. Must not be a <see cref="Task"/> or mutable internal graph reference.</typeparam>
        /// <param name="action">The synchronous delegate to execute.</param>
        /// <returns>The value returned by <paramref name="action"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown if <typeparamref name="T"/> is a <see cref="Task"/>.</exception>
        internal static T Execute<T>(Func<T> action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            // Guard against accidental async lambda usage: Execute(() => DoWorkAsync())
            if (typeof(Task).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException(
                    "Asynchronous delegates cannot be synchronized with GraphSynchronization.Execute.");
            }

            lock (_syncRoot)
            {
                return action();
            }
        }

        /// <summary>
        /// Executes a synchronous action under the global graph lock.
        /// </summary>
        /// <param name="action">The synchronous delegate to execute.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is <see langword="null"/>.</exception>
        internal static void ExecuteAction(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            lock (_syncRoot)
            {
                action();
            }
        }

        /// <inheritdoc cref="ExecuteAction(Action)"/>
        internal static void Execute(Action action)
        {
            ExecuteAction(action);
        }
    }
}
