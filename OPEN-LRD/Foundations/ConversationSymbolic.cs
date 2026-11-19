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
        public ConversationSymbolic(Dictionary<UInt128, List<Guid>> symbolicArray)
        {
            ConversationID = Guid.NewGuid();
            this.symbolicArray = symbolicArray;
            currentSymbolicCount = (UInt128)symbolicArray.Count;
        }

        public void AddSymbolic(List<Guid> symbols)
        {
            if (symbolicArray == null)
            {
                symbolicArray = new Dictionary<UInt128, List<Guid>>();
            }
            symbolicArray[currentSymbolicCount] = symbols;
            currentSymbolicCount++;
        }

        public void AddSymbolicRange(List<List<Guid>> symbolsList)
        {
            if (symbolicArray == null)
            {
                symbolicArray = new Dictionary<UInt128, List<Guid>>();
            }
            foreach (var symbols in symbolsList)
            {
                symbolicArray[currentSymbolicCount] = symbols;
                currentSymbolicCount++;
            }
        }

        public void AddSymbolicRange(Dictionary<UInt128, List<Guid>> symbolsDict)
        {
            if (symbolicArray == null)
            {
                symbolicArray = new Dictionary<UInt128, List<Guid>>();
            }
            foreach (var kvp in symbolsDict)
            {
                symbolicArray[kvp.Key] = kvp.Value;
                currentSymbolicCount++;
            }
        }

        public Dictionary<UInt128, List<Guid>>? GetSymbolicArray()
        {
            return symbolicArray;
        }

        public List<Guid>? GetSymbolicById(UInt128 symbolicId)
        {
            if (symbolicArray != null && symbolicArray.ContainsKey(symbolicId))
            {
                return symbolicArray[symbolicId];
            }
            return null;
        }

        public Guid GetConversationID()
        {
            return ConversationID;
        }

        public void SetCurrentSymbolicIndex(UInt128 index)
        {
            currentSymbolicIndex = index;
        }

        public UInt128 GetCurrentSymbolicIndex()
        {
            return currentSymbolicIndex;
        }

        public UInt128 GetCurrentSymbolicCount()
        {
            return currentSymbolicCount;
        }
    }
}
