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
        private bool _hasSymbolChar;
        private bool _identityFrozen;
        public SymbolBox()
        {
            SymbolID = Guid.NewGuid();
            SymbolChar = '\0';
            SenseID = null;
            SType = UnicodeCategory.OtherNotAssigned;
            _hasSymbolChar = false;
        }

        public SymbolBox(Guid id, char c)
        {
            ValidateIdentityId(id, nameof(id));
            SymbolID = id;
            SymbolChar = c;
            SType = char.GetUnicodeCategory(c);
            _hasSymbolChar = true;

        }

        public Guid GetSymbolID()
        {
            return GraphSynchronization.Execute(() => SymbolID);
        }

        public char GetSymbolChar()
        {
            return GraphSynchronization.Execute(() => SymbolChar);
        }
        public Guid? GetSenseID()
        {
            return GraphSynchronization.Execute(() => SenseID);
        }
        public void SetSenseID(Guid senseId)
        {
            ValidateIdentityId(senseId, nameof(senseId));
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (SenseID == null)
                {
                    SenseID = senseId;
                }
                else
                {
                    throw new InvalidOperationException("SenseID is already set and cannot be changed.");
                }
            });
        }

        public void SetSymbolChar(char symbolChar)
        {
            GraphSynchronization.Execute(() =>
            {
                EnsureIdentityMutable();
                if (!_hasSymbolChar)
                {
                    SymbolChar = symbolChar;
                    SType = char.GetUnicodeCategory(symbolChar);
                    _hasSymbolChar = true;
                }
                else
                {
                    throw new InvalidOperationException("Symbol character is already set and cannot be changed.");
                }
            });
        }

        public UnicodeCategory GetSymbolType()
        {
            return GraphSynchronization.Execute(() => SType);
        }

        internal bool HasCompleteIdentity => GraphSynchronization.Execute(() =>
            SymbolID != Guid.Empty &&
            _hasSymbolChar &&
            SenseID.HasValue &&
            SenseID.Value != Guid.Empty);

        internal SymbolIdentityKey GetIdentityKey()
        {
            return GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Symbol identity is incomplete.");
                }

                return new SymbolIdentityKey(SymbolChar, SenseID!.Value);
            });
        }

        internal bool IsIdentityFrozen => GraphSynchronization.Execute(() => _identityFrozen);

        internal void FreezeIdentity()
        {
            GraphSynchronization.Execute(() =>
            {
                if (!HasCompleteIdentity)
                {
                    throw new InvalidOperationException("Cannot trust a symbol with incomplete identity.");
                }

                _identityFrozen = true;
            });
        }

        private void EnsureIdentityMutable()
        {
            if (_identityFrozen)
            {
                throw new InvalidOperationException("Symbol identity is frozen after trust.");
            }
        }

        private static void ValidateIdentityId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", parameterName);
            }
        }
    }

    internal sealed class SymbolIdentityKey : IEquatable<SymbolIdentityKey>
    {
        internal SymbolIdentityKey(char symbolChar, Guid senseId)
        {
            if (senseId == Guid.Empty)
            {
                throw new ArgumentException("Identity identifiers cannot be Guid.Empty.", nameof(senseId));
            }
            SymbolChar = symbolChar;
            SenseId = senseId;
        }

        internal char SymbolChar { get; }
        internal Guid SenseId { get; }

        public bool Equals(SymbolIdentityKey? other)
        {
            return other != null && SymbolChar == other.SymbolChar && SenseId == other.SenseId;
        }

        public override bool Equals(object? obj)
        {
            return obj is SymbolIdentityKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SymbolChar, SenseId);
        }
    }
}
