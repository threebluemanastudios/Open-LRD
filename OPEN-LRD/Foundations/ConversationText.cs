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
        public ConversationText(Dictionary<UInt128, string> textArray)
        {
            ConversationID = Guid.NewGuid();
            this.textArray = textArray;
        }

        public void AddText( string text)
        {
            if (textArray == null)
            {
                textArray = new Dictionary<UInt128, string>();
            }
            textArray[currentTextId] = text;
            currentTextId++;
        }

        public void AddTextRange(List<string> texts)
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
        }

        public void AddTextRange(Dictionary<UInt128, string> textsDict)
        {
            if (textArray == null)
            {
                textArray = new Dictionary<UInt128, string>();
            }
            foreach (var kvp in textsDict)
            {
                textArray[kvp.Key] = kvp.Value;
                currentTextId++;
            }
        }

        public Dictionary<UInt128, string>? GetTextArray()
        {
            return textArray;
        }

        public string? GetTextById(UInt128 textId)
        {
            if (textArray != null && textArray.ContainsKey(textId))
            {
                return textArray[textId];
            }
            return null;
        }

        public Guid GetConversationID()
        {
            return ConversationID;
        }

        public void SetCurrentTextIndex(UInt128 index)
        {
            currentTextIndex = index;
        }

        public UInt128 GetCurrentTextIndex()
        {
            return currentTextIndex;
        }
    }
}

