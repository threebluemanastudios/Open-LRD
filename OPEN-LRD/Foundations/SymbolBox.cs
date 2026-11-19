using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OPEN_LRD
{ 
    /// <summary>
    /// Represents the type of a symbol.
    /// </summary>
    

    internal class SymbolBox
    {
        
        private Guid SymbolID;
        private char SymbolChar;
        private Guid? SenseID;
        private UnicodeCategory SType;

        public SymbolBox(Guid id, char c)
        {
            
            SymbolID = id;
            SymbolChar = c;
            SType = char.GetUnicodeCategory(c);

        }

        public Guid GetSymbolID()
        {
            return SymbolID;
        }

        public char GetSymbolChar()
        {
            return SymbolChar;
        }
        public Guid? GetSenseID()
        {
            return SenseID;
        }
        public void SetSenseID(Guid senseId)
        {
            SenseID = senseId;
        }

        public UnicodeCategory GetSymbolType()
        {
            return SType;
        }
    }
}
