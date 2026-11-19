using System.Collections.ObjectModel;
using OPEN_LRD.Foundations;
using OPEN_LRD.Reasoning;
using OPEN_LRD.Skills;
using OPEN_LRD.IngestionPipeline;


namespace OPEN_LRD
{
    /// <summary>Owns the graph, runtime registries, conversations, persistence hooks, and lifecycle operations.</summary>
    internal class LRD_Core
    {
        private static readonly LRD_Core _instance = new LRD_Core();
        public bool initialized = false;

        protected Dictionary<Guid, SourceBox>? SourceDictionary; /// <summary>Stores SourceBox Dictionary.</summary>

        protected Dictionary<Guid, ConversationText>? TextConversationDictionary; /// <summary>Stores GroupBox Dictionary.</summary>
        protected Dictionary<Guid, ConversationSymbolic>? SymbolicConversationDictionary; /// <summary>Stores ConversationSymbolic Dictionary.</summary>

        protected Dictionary<Guid, SymbolBox> TrustedSymbolDictionary = new Dictionary<Guid, SymbolBox>();

        protected Dictionary<Guid, WordBox> TrustedWordDictionary = new Dictionary<Guid, WordBox>();

        protected Dictionary<Guid, SentenceBox> TrustedSentenceDictionary = new Dictionary<Guid, SentenceBox>();

        protected Dictionary<Guid, SymbolBox> UntrustedSymbolDictionary = new Dictionary<Guid, SymbolBox>();

        protected Dictionary<Guid, WordBox> UntrustedWordDictionary = new Dictionary<Guid, WordBox>();

        protected Dictionary<Guid, SentenceBox> UntrustedSentenceDictionary = new Dictionary<Guid, SentenceBox>();

        /// <summary>Initializes the singleton core type.</summary>
        static LRD_Core() { }

        /// <summary>Prevents external instantiation of the singleton core.</summary>
        private LRD_Core() { }

        public static LRD_Core Instance => _instance;

        // Initializes the graph dictionary and the temporary query/response chains.
        /// <summary>Initializes or resets the singleton graph and its runtime state.</summary>
        public void InitializeCore()
        {
            initialized = true;
        }

        public LRD_Core GetInstance()
        {
            return _instance;
        }

        public void ResetCore()
        {
            initialized = false;
        }

        public char? GetSymbolChar(Guid symbolId)
        {
            if (TrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbol))
            {
                return symbol.GetSymbolChar();
            }
            else
            {
                return null;
            }
        }

        public Guid? GetSymbolID(char symbolChar)
        {
            foreach (var symbol in TrustedSymbolDictionary.Values)
            {
                if (symbol.GetSymbolChar() == symbolChar)
                {
                    return symbol.GetSymbolID();
                }
            }
            return null;
        }

        public List<Guid> GetSymbolsByWordID(Guid wordId)
        {
            List<Guid> symbols = new List<Guid>();
            if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
            {
                var wordSymbols = wordBox.GetSymbols();
                if (wordSymbols != null)
                {
                    symbols.AddRange(wordSymbols);
                }
            }
            return symbols;
        }

        public List<Guid> GetSymbolsBySenseID(Guid senseId)
        {
            List<Guid> symbols = new List<Guid>();
            foreach (var symbol in TrustedSymbolDictionary.Values)
            {
                if (symbol.GetSenseID() == senseId)
                {
                    symbols.Add(symbol.GetSymbolID());
                }
            }
            return symbols;
        }

        public List<Guid> GetWordsBySenseID(Guid senseId)
        {
            List<Guid> words = new List<Guid>();
            foreach (var word in TrustedWordDictionary.Values)
            {
                if (word.GetSenseID() == senseId)
                {
                    words.Add(word.GetWordID());
                }
            }
            return words;
        }

        public List<Guid> GetWordsFromSentenceID(Guid sentenceId)
        {
            List<Guid> words = new List<Guid>();
            foreach (var sentenceBox in TrustedSentenceDictionary.Values)
            {
                if (sentenceBox.GetSentenceID() == sentenceId)
                {
                    var sentenceWords = sentenceBox.GetWords();
                    if (sentenceWords != null)
                    {
                        words.AddRange(sentenceWords);
                    }
                }
            }
            return words;
        }

        public string GetTextFromWordID(Guid wordId)
        {
            if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
            {
                var symbols = wordBox.GetSymbols();
                if (symbols != null)
                {
                    string wordText = "";
                    foreach (var symbolId in symbols)
                    {
                        if (TrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                        {
                            var symbolChar = symbolBox.GetSymbolChar();
                            if (symbolChar.GetType() == typeof(char))
                            {
                                wordText += symbolChar;
                            }
                        }
                    }
                    return wordText;
                }
            }
            return string.Empty;
        }

        public string GetTextFromSentenceID(Guid sentenceId)
        {
            var words = GetWordsFromSentenceID(sentenceId);
            List<string> wordTexts = new List<string>();
            foreach (var wordId in words)
            {
                if (TrustedWordDictionary.TryGetValue(wordId, out WordBox? wordBox))
                {
                    var symbols = wordBox.GetSymbols();
                    if (symbols != null)
                    {
                        string wordText = "";
                        foreach (var symbolId in symbols)
                        {
                            if (TrustedSymbolDictionary.TryGetValue(symbolId, out SymbolBox? symbolBox))
                            {
                                var symbolChar = symbolBox.GetSymbolChar();
                                if (symbolChar.GetType() == typeof(char))
                                {
                                    wordText += symbolChar;
                                }
                            }
                        }
                        wordTexts.Add(wordText);
                    }
                }
            }
            return string.Join(" ", wordTexts);
        }



    }
}
