using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD
{
    internal class ConversationText
    {
        private Guid ConversationID;
        private Dictionary<UInt128, string>? textArray;
        private UInt128 currentTextId = 0;
        private UInt128 currentTextIndex = 0;

        public ConversationText()
        {
            ConversationID = Guid.NewGuid();
            textArray = new Dictionary<UInt128, string>();
        }

        public ConversationText(Guid conversationId)
        {
            ValidateConversationId(conversationId);
            ConversationID = conversationId;
            textArray = new Dictionary<UInt128, string>();
        }

        public ConversationText(Dictionary<UInt128, string> textArray)
            : this(Guid.NewGuid(), textArray)
        {
        }

        public ConversationText(Guid conversationId, Dictionary<UInt128, string> textArray)
        {
            ValidateConversationId(conversationId);
            ArgumentNullException.ThrowIfNull(textArray);
            ConversationID = conversationId;
            this.textArray = GraphSynchronization.Execute(() => new Dictionary<UInt128, string>(textArray));
            if (textArray.Count > 0)
            {
                currentTextId = textArray.Keys.Max() + 1;
            }
        }

        public void AddText( string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            GraphSynchronization.Execute(() =>
            {
                if (textArray == null)
                {
                    textArray = new Dictionary<UInt128, string>();
                }
                textArray[currentTextId] = text;
                currentTextId++;
            });
        }

        public void AddTextRange(List<string> texts)
        {
            ArgumentNullException.ThrowIfNull(texts);
            GraphSynchronization.Execute(() =>
            {
                if (textArray == null)
                {
                    textArray = new Dictionary<UInt128, string>();
                }
                foreach (var text in texts)
                {
                    textArray[currentTextId] = text;
                    currentTextId++;
                }
            });
        }

        public void AddTextRange(Dictionary<UInt128, string> textsDict)
        {
            ArgumentNullException.ThrowIfNull(textsDict);
            GraphSynchronization.Execute(() =>
            {
                if (textArray == null)
                {
                    textArray = new Dictionary<UInt128, string>();
                }
                foreach (var kvp in textsDict)
                {
                    textArray[kvp.Key] = kvp.Value;
                    if (kvp.Key >= currentTextId)
                    {
                        currentTextId = kvp.Key + 1;
                    }
                }
            });
        }

        public Dictionary<UInt128, string>? GetTextArray()
        {
            return GraphSynchronization.Execute(() =>
                textArray == null ? null : new Dictionary<UInt128, string>(textArray));
        }

        public string? GetTextById(UInt128 textId)
        {
            return GraphSynchronization.Execute(() =>
                textArray != null && textArray.TryGetValue(textId, out string? text) ? text : null);
        }

        public Guid GetConversationID()
        {
            return GraphSynchronization.Execute(() => ConversationID);
        }

        public void SetCurrentTextIndex(UInt128 index)
        {
            GraphSynchronization.Execute(() => currentTextIndex = index);
        }

        public UInt128 GetCurrentTextIndex()
        {
            return GraphSynchronization.Execute(() => currentTextIndex);
        }

        private static void ValidateConversationId(Guid conversationId)
        {
            if (conversationId == Guid.Empty)
            {
                throw new ArgumentException("Conversation identifiers cannot be Guid.Empty.", nameof(conversationId));
            }
        }
    }
}

