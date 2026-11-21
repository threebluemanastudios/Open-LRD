using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class ConversationSymbolic
    {
        private Guid ConversationID;
        private Dictionary<UInt128, List<Guid>>? symbolicArray;
        private UInt128 currentSymbolicCount = 0;
        private UInt128 currentSymbolicIndex = 0;

        public ConversationSymbolic()
        {
            ConversationID = Guid.NewGuid();
            symbolicArray = new Dictionary<UInt128, List<Guid>>();
        }

        public ConversationSymbolic(Guid conversationId)
        {
            ValidateConversationId(conversationId);
            ConversationID = conversationId;
            symbolicArray = new Dictionary<UInt128, List<Guid>>();
        }

        public ConversationSymbolic(Dictionary<UInt128, List<Guid>> symbolicArray)
            : this(Guid.NewGuid(), symbolicArray)
        {
        }

        public ConversationSymbolic(Guid conversationId, Dictionary<UInt128, List<Guid>> symbolicArray)
        {
            ValidateConversationId(conversationId);
            ArgumentNullException.ThrowIfNull(symbolicArray);
            ValidateSymbolicDictionary(symbolicArray, nameof(symbolicArray));
            ConversationID = conversationId;
            this.symbolicArray = GraphSynchronization.Execute(() =>
                symbolicArray.ToDictionary(entry => entry.Key, entry => new List<Guid>(entry.Value)));
            if (symbolicArray.Count > 0)
            {
                currentSymbolicCount = symbolicArray.Keys.Max() + 1;
            }
        }

        public void AddSymbolic(List<Guid> symbols)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            ValidateSymbolIds(symbols, nameof(symbols));
            GraphSynchronization.Execute(() =>
            {
                if (symbolicArray == null)
                {
                    symbolicArray = new Dictionary<UInt128, List<Guid>>();
                }
                symbolicArray[currentSymbolicCount] = new List<Guid>(symbols);
                currentSymbolicCount++;
            });
        }

        public void AddSymbolicRange(List<List<Guid>> symbolsList)
        {
            ArgumentNullException.ThrowIfNull(symbolsList);
            foreach (List<Guid> symbols in symbolsList)
            {
                ArgumentNullException.ThrowIfNull(symbols);
                ValidateSymbolIds(symbols, nameof(symbolsList));
            }
            GraphSynchronization.Execute(() =>
            {
                if (symbolicArray == null)
                {
                    symbolicArray = new Dictionary<UInt128, List<Guid>>();
                }
                foreach (var symbols in symbolsList)
                {
                    ArgumentNullException.ThrowIfNull(symbols);
                    symbolicArray[currentSymbolicCount] = new List<Guid>(symbols);
                    currentSymbolicCount++;
                }
            });
        }

        public void AddSymbolicRange(Dictionary<UInt128, List<Guid>> symbolsDict)
        {
            ArgumentNullException.ThrowIfNull(symbolsDict);
            ValidateSymbolicDictionary(symbolsDict, nameof(symbolsDict));
            GraphSynchronization.Execute(() =>
            {
                if (symbolicArray == null)
                {
                    symbolicArray = new Dictionary<UInt128, List<Guid>>();
                }
                foreach (var kvp in symbolsDict)
                {
                    ArgumentNullException.ThrowIfNull(kvp.Value);
                    symbolicArray[kvp.Key] = new List<Guid>(kvp.Value);
                    if (kvp.Key >= currentSymbolicCount)
                    {
                        currentSymbolicCount = kvp.Key + 1;
                    }
                }
            });
        }

        public Dictionary<UInt128, List<Guid>>? GetSymbolicArray()
        {
            return GraphSynchronization.Execute(() =>
                symbolicArray == null
                    ? null
                    : symbolicArray.ToDictionary(entry => entry.Key, entry => new List<Guid>(entry.Value)));
        }

        public List<Guid>? GetSymbolicById(UInt128 symbolicId)
        {
            return GraphSynchronization.Execute(() =>
                symbolicArray != null && symbolicArray.TryGetValue(symbolicId, out List<Guid>? symbols)
                    ? new List<Guid>(symbols)
                    : null);
        }

        public Guid GetConversationID()
        {
            return GraphSynchronization.Execute(() => ConversationID);
        }

        public void SetCurrentSymbolicIndex(UInt128 index)
        {
            GraphSynchronization.Execute(() => currentSymbolicIndex = index);
        }

        public UInt128 GetCurrentSymbolicIndex()
        {
            return GraphSynchronization.Execute(() => currentSymbolicIndex);
        }

        public UInt128 GetCurrentSymbolicCount()
        {
            return GraphSynchronization.Execute(() => currentSymbolicCount);
        }

        private static void ValidateConversationId(Guid conversationId)
        {
            if (conversationId == Guid.Empty)
            {
                throw new ArgumentException("Conversation identifiers cannot be Guid.Empty.", nameof(conversationId));
            }
        }

        private static void ValidateSymbolicDictionary(Dictionary<UInt128, List<Guid>> values, string parameterName)
        {
            foreach (KeyValuePair<UInt128, List<Guid>> entry in values)
            {
                ArgumentNullException.ThrowIfNull(entry.Value, parameterName);
                ValidateSymbolIds(entry.Value, parameterName);
            }
        }

        private static void ValidateSymbolIds(IEnumerable<Guid> symbols, string parameterName)
        {
            foreach (Guid symbolId in symbols)
            {
                if (symbolId == Guid.Empty)
                {
                    throw new ArgumentException("Symbol identifiers cannot be Guid.Empty.", parameterName);
                }
            }
        }
    }
}
