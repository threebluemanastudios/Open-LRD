using System;
using System.Collections.Generic;
using System.Linq;

namespace OPEN_LRD.Foundations
{
    internal sealed class ConversationText
    {
        private readonly Guid conversationId;
        private Dictionary<UInt128, string> textArray = new Dictionary<UInt128, string>();
        private UInt128 currentTextId;
        private UInt128 currentTextIndex;
        private bool idSpaceExhausted;

        public ConversationText()
            : this(Guid.NewGuid())
        {
        }

        public ConversationText(Guid conversationId)
        {
            ValidateConversationId(conversationId);
            this.conversationId = conversationId;
        }

        public ConversationText(Dictionary<UInt128, string> textArray)
            : this(Guid.NewGuid(), textArray)
        {
        }

        public ConversationText(Guid conversationId, Dictionary<UInt128, string> textArray)
        {
            ValidateConversationId(conversationId);
            ArgumentNullException.ThrowIfNull(textArray);
            KeyValuePair<UInt128, string>[] snapshot = textArray.ToArray();
            ValidateTextEntries(snapshot, nameof(textArray));
            this.conversationId = conversationId;
            this.textArray = snapshot.ToDictionary(entry => entry.Key, entry => entry.Value);
            SetNextIdFromExistingKeys(snapshot.Select(entry => entry.Key).DefaultIfEmpty().Max());
            if (snapshot.Length == 0)
            {
                currentTextId = 0;
            }
        }

        public void AddText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            GraphSynchronization.Execute(() =>
            {
                EnsureIdAvailable(idSpaceExhausted, nameof(AddText));
                UInt128 assignedId = currentTextId;
                textArray.Add(assignedId, text);
                AdvanceAfterAssignedId(assignedId, out currentTextId, out idSpaceExhausted);
            });
        }

        public void AddTextRange(List<string> texts)
        {
            ArgumentNullException.ThrowIfNull(texts);
            string[] snapshot = texts.ToArray();
            for (int index = 0; index < snapshot.Length; index++)
            {
                if (snapshot[index] == null)
                {
                    throw new ArgumentException("Text ranges cannot contain null entries.", nameof(texts));
                }
            }

            GraphSynchronization.Execute(() =>
            {
                (UInt128 nextId, bool exhausted) = PreflightSequentialIds(currentTextId, idSpaceExhausted, snapshot.Length);
                UInt128 assignedId = currentTextId;
                for (int index = 0; index < snapshot.Length; index++)
                {
                    textArray.Add(assignedId, snapshot[index]);
                    if (index + 1 < snapshot.Length && assignedId != UInt128.MaxValue)
                    {
                        assignedId++;
                    }
                }
                currentTextId = nextId;
                idSpaceExhausted = exhausted;
            });
        }

        public void AddTextRange(Dictionary<UInt128, string> textsDict)
        {
            ArgumentNullException.ThrowIfNull(textsDict);
            KeyValuePair<UInt128, string>[] snapshot = textsDict.ToArray();
            ValidateTextEntries(snapshot, nameof(textsDict));

            GraphSynchronization.Execute(() =>
            {
                (UInt128 nextId, bool exhausted) = AdvanceForExplicitIds(currentTextId, idSpaceExhausted,
                    snapshot.Select(entry => entry.Key));
                foreach (KeyValuePair<UInt128, string> entry in snapshot)
                {
                    textArray[entry.Key] = entry.Value;
                }
                currentTextId = nextId;
                idSpaceExhausted = exhausted;
            });
        }

        public Dictionary<UInt128, string> GetTextArray()
        {
            return GraphSynchronization.Execute(() => new Dictionary<UInt128, string>(textArray));
        }

        public string? GetTextById(UInt128 textId)
        {
            return GraphSynchronization.Execute(() => textArray.TryGetValue(textId, out string? text) ? text : null);
        }

        public Guid GetConversationID() => GraphSynchronization.Execute(() => conversationId);
        public UInt128 GetCurrentTextIndex() => GraphSynchronization.Execute(() => currentTextIndex);
        public void SetCurrentTextIndex(UInt128 index) => GraphSynchronization.Execute(() => currentTextIndex = index);
        public UInt128 GetCurrentTextCount() => GraphSynchronization.Execute(() => currentTextId);
        public int GetEntryCount() => GraphSynchronization.Execute(() => textArray.Count);
        public bool IsIdSpaceExhausted => GraphSynchronization.Execute(() => idSpaceExhausted);
        public bool ContainsId(UInt128 id) => GraphSynchronization.Execute(() => textArray.ContainsKey(id));
        public bool Remove(UInt128 id) => GraphSynchronization.Execute(() => textArray.Remove(id));

        public void Clear()
        {
            GraphSynchronization.Execute(() =>
            {
                textArray.Clear();
                currentTextId = 0;
                currentTextIndex = 0;
                idSpaceExhausted = false;
            });
        }

        private static void ValidateTextEntries(IEnumerable<KeyValuePair<UInt128, string>> entries, string parameterName)
        {
            foreach (KeyValuePair<UInt128, string> entry in entries)
            {
                if (entry.Value == null)
                {
                    throw new ArgumentException("Text entries cannot contain null values.", parameterName);
                }
            }
        }

        private void SetNextIdFromExistingKeys(UInt128 maximumKey)
        {
            if (textArray.Count == 0)
            {
                currentTextId = 0;
                return;
            }
            if (maximumKey == UInt128.MaxValue)
            {
                currentTextId = UInt128.MaxValue;
                idSpaceExhausted = true;
            }
            else
            {
                currentTextId = maximumKey + 1;
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
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Conversation identifiers cannot be Guid.Empty.", nameof(id));
            }
        }
    }
}
