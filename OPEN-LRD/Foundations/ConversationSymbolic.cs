using System;
using System.Collections.Generic;
using System.Linq;

namespace OPEN_LRD.Foundations
{
    internal sealed class ConversationSymbolic
    {
        private readonly Guid conversationId;
        private Dictionary<UInt128, List<Guid>> symbolicArray = new Dictionary<UInt128, List<Guid>>();
        private UInt128 currentSymbolicCount;
        private UInt128 currentSymbolicIndex;
        private bool idSpaceExhausted;

        public ConversationSymbolic()
            : this(LRD_Core.Instance.GenerateConversationId())
        {
        }

        public ConversationSymbolic(Guid conversationId)
        {
            ValidateConversationId(conversationId);
            this.conversationId = conversationId;
        }

        public ConversationSymbolic(Dictionary<UInt128, List<Guid>> symbolicArray)
            : this(LRD_Core.Instance.GenerateConversationId(), symbolicArray)
        {
        }

        public ConversationSymbolic(Guid conversationId, Dictionary<UInt128, List<Guid>> symbolicArray)
        {
            ValidateConversationId(conversationId);
            ArgumentNullException.ThrowIfNull(symbolicArray);
            KeyValuePair<UInt128, List<Guid>>[] snapshot = SnapshotAndValidate(symbolicArray, nameof(symbolicArray));
            this.conversationId = conversationId;
            this.symbolicArray = snapshot.ToDictionary(entry => entry.Key, entry => new List<Guid>(entry.Value));
            SetNextIdFromExistingKeys(snapshot.Select(entry => entry.Key).DefaultIfEmpty().Max());
            if (snapshot.Length == 0)
            {
                currentSymbolicCount = 0;
            }
        }

        public void AddSymbolic(List<Guid> symbols)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            Guid[] snapshot = SnapshotAndValidateSymbols(symbols, nameof(symbols));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdAvailable(idSpaceExhausted, nameof(AddSymbolic));
                UInt128 assignedId = currentSymbolicCount;
                symbolicArray.Add(assignedId, new List<Guid>(snapshot));
                AdvanceAfterAssignedId(assignedId, out currentSymbolicCount, out idSpaceExhausted);
            });
        }

        public void AddSymbolicRange(List<List<Guid>> symbolsList)
        {
            ArgumentNullException.ThrowIfNull(symbolsList);
            List<Guid[]> snapshot = new List<Guid[]>(symbolsList.Count);
            foreach (List<Guid>? symbols in symbolsList)
            {
                if (symbols == null)
                {
                    throw new ArgumentException("Symbolic ranges cannot contain null entries.", nameof(symbolsList));
                }
                snapshot.Add(SnapshotAndValidateSymbols(symbols, nameof(symbolsList)));
            }

            GraphSynchronization.Execute(() =>
            {
                (UInt128 nextId, bool exhausted) = PreflightSequentialIds(currentSymbolicCount, idSpaceExhausted, snapshot.Count);
                UInt128 assignedId = currentSymbolicCount;
                for (int index = 0; index < snapshot.Count; index++)
                {
                    symbolicArray.Add(assignedId, new List<Guid>(snapshot[index]));
                    if (index + 1 < snapshot.Count && assignedId != UInt128.MaxValue)
                    {
                        assignedId++;
                    }
                }
                currentSymbolicCount = nextId;
                idSpaceExhausted = exhausted;
            });
        }

        public void AddSymbolicRange(Dictionary<UInt128, List<Guid>> symbolsDict)
        {
            ArgumentNullException.ThrowIfNull(symbolsDict);
            KeyValuePair<UInt128, List<Guid>>[] snapshot = SnapshotAndValidate(symbolsDict, nameof(symbolsDict));
            GraphSynchronization.Execute(() =>
            {
                (UInt128 nextId, bool exhausted) = AdvanceForExplicitIds(currentSymbolicCount,
                    idSpaceExhausted, snapshot.Select(entry => entry.Key));
                foreach (KeyValuePair<UInt128, List<Guid>> entry in snapshot)
                {
                    symbolicArray[entry.Key] = new List<Guid>(entry.Value);
                }
                currentSymbolicCount = nextId;
                idSpaceExhausted = exhausted;
            });
        }

        public Dictionary<UInt128, List<Guid>> GetSymbolicArray()
        {
            return GraphSynchronization.Execute(() => symbolicArray.ToDictionary(
                entry => entry.Key, entry => new List<Guid>(entry.Value)));
        }

        public List<Guid>? GetSymbolicById(UInt128 symbolicId)
        {
            return GraphSynchronization.Execute(() => symbolicArray.TryGetValue(symbolicId, out List<Guid>? symbols)
                ? new List<Guid>(symbols)
                : null);
        }

        public Guid GetConversationID() => GraphSynchronization.Execute(() => conversationId);
        public UInt128 GetCurrentSymbolicIndex() => GraphSynchronization.Execute(() => currentSymbolicIndex);
        public void SetCurrentSymbolicIndex(UInt128 index) => GraphSynchronization.Execute(() => currentSymbolicIndex = index);
        public UInt128 GetCurrentSymbolicCount() => GraphSynchronization.Execute(() => currentSymbolicCount);
        public int GetEntryCount() => GraphSynchronization.Execute(() => symbolicArray.Count);
        public bool IsIdSpaceExhausted => GraphSynchronization.Execute(() => idSpaceExhausted);
        public bool ContainsId(UInt128 id) => GraphSynchronization.Execute(() => symbolicArray.ContainsKey(id));
        public bool Remove(UInt128 id) => GraphSynchronization.Execute(() => symbolicArray.Remove(id));

        public void Clear()
        {
            GraphSynchronization.Execute(() =>
            {
                symbolicArray.Clear();
                currentSymbolicCount = 0;
                currentSymbolicIndex = 0;
                idSpaceExhausted = false;
            });
        }

        private void SetNextIdFromExistingKeys(UInt128 maximumKey)
        {
            if (symbolicArray.Count == 0)
            {
                currentSymbolicCount = 0;
                return;
            }
            if (maximumKey == UInt128.MaxValue)
            {
                currentSymbolicCount = UInt128.MaxValue;
                idSpaceExhausted = true;
            }
            else
            {
                currentSymbolicCount = maximumKey + 1;
            }
        }

        private static (UInt128 NextId, bool Exhausted) PreflightSequentialIds(UInt128 nextId, bool exhausted, int count)
        {
            for (int index = 0; index < count; index++)
            {
                EnsureIdAvailable(exhausted, nameof(count));
                AdvanceAfterAssignedId(nextId, out nextId, out exhausted);
            }
            return (nextId, exhausted);
        }

        private static (UInt128 NextId, bool Exhausted) AdvanceForExplicitIds(
            UInt128 currentId,
            bool exhausted,
            IEnumerable<UInt128> ids)
        {
            foreach (UInt128 id in ids)
            {
                if (exhausted || id < currentId)
                {
                    continue;
                }
                if (id == UInt128.MaxValue)
                {
                    currentId = UInt128.MaxValue;
                    exhausted = true;
                }
                else
                {
                    currentId = id + 1;
                }
            }
            return (currentId, exhausted);
        }

        private static KeyValuePair<UInt128, List<Guid>>[] SnapshotAndValidate(
            Dictionary<UInt128, List<Guid>> values,
            string parameterName)
        {
            KeyValuePair<UInt128, List<Guid>>[] snapshot = values.ToArray();
            for (int index = 0; index < snapshot.Length; index++)
            {
                List<Guid>? symbols = snapshot[index].Value;
                if (symbols == null)
                {
                    throw new ArgumentException("Symbolic entries cannot contain null lists.", parameterName);
                }
                snapshot[index] = new KeyValuePair<UInt128, List<Guid>>(
                    snapshot[index].Key, new List<Guid>(SnapshotAndValidateSymbols(symbols, parameterName)));
            }
            return snapshot;
        }

        private static Guid[] SnapshotAndValidateSymbols(IEnumerable<Guid> symbols, string parameterName)
        {
            Guid[] snapshot = symbols.ToArray();
            if (snapshot.Any(symbolId => symbolId == Guid.Empty))
            {
                throw new ArgumentException("Symbol identifiers cannot be Guid.Empty.", parameterName);
            }
            return snapshot;
        }

        private static void EnsureIdAvailable(bool exhausted, string parameterName)
        {
            if (exhausted)
            {
                throw new OverflowException($"No further UInt128 IDs are available for {parameterName}.");
            }
        }

        private static void AdvanceAfterAssignedId(UInt128 assignedId, out UInt128 nextId, out bool exhausted)
        {
            if (assignedId == UInt128.MaxValue)
            {
                nextId = UInt128.MaxValue;
                exhausted = true;
            }
            else
            {
                nextId = assignedId + 1;
                exhausted = false;
            }
        }

        private static void ValidateConversationId(Guid id)
        {
            LRD_Core.Instance.ValidateNewConversationId(id, nameof(id));
        }
    }
}
