using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OPEN_LRD.Tools;

namespace OPEN_LRD.Foundations
{
    /// <summary>
    /// Converts between raw text conversations and symbolically compressed conversations.
    /// Each text row maps to one sentence. Sentences are built from words, words from symbols.
    /// Whitespace and punctuation are tokens, so rebuilding a row reproduces its original spacing exactly.
    /// </summary>
    internal static class ConversationCodec
    {
        /// <summary>Compresses every text row into a sentence Guid, keyed by the same row identifier.</summary>
        /// <param name="text">Source text conversation.</param>
        /// <param name="senseId">Sense for symbols, words and sentences. Defaults to <see cref="LRD_Core.DefaultSenseId"/>.</param>
        /// <returns>A symbolic conversation with the same conversation identifier and row keys.</returns>
        public static ConversationSymbolic TextToSymbolic(ConversationText text, Guid? senseId = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            Guid sense = ResolveSense(senseId);
            Dictionary<UInt128, List<Guid>> rows = new Dictionary<UInt128, List<Guid>>();
            foreach (KeyValuePair<UInt128, string> row in text.GetTextArray().OrderBy(entry => entry.Key))
            {
                rows.Add(row.Key, new List<Guid> { ResolveSentence(row.Value, sense) });
            }
            return new ConversationSymbolic(text.GetConversationID(), rows);
        }

        /// <summary>Rebuilds each symbolic row's text from its sentence, word and symbol registries, trusted or staged.</summary>
        /// <param name="symbolic">Source symbolic conversation.</param>
        /// <returns>A text conversation with the same conversation identifier and row keys.</returns>
        /// <exception cref="InvalidOperationException">A referenced sentence, word or symbol is not registered.</exception>
        public static ConversationText SymbolicToText(ConversationSymbolic symbolic)
        {
            ArgumentNullException.ThrowIfNull(symbolic);
            Dictionary<UInt128, string> rows = new Dictionary<UInt128, string>();
            foreach (KeyValuePair<UInt128, List<Guid>> row in symbolic.GetSymbolicArray().OrderBy(entry => entry.Key))
            {
                StringBuilder builder = new StringBuilder();
                foreach (Guid sentenceId in row.Value)
                {
                    builder.Append(ReadSentenceText(sentenceId));
                }
                rows.Add(row.Key, builder.ToString());
            }
            return new ConversationText(symbolic.GetConversationID(), rows);
        }

        /// <summary>Reads a file with the existing grid reader and creates one text row per grid row, trimmed to its logical length.</summary>
        /// <param name="filePath">File to read.</param>
        /// <param name="conversationId">Conversation identifier. A new one is generated when omitted.</param>
        /// <param name="maxColumns">Optional column limit passed to the reader.</param>
        /// <returns>A text conversation with keys 0..n-1, one per row.</returns>
        public static ConversationText ReadFileToTextConversation(string filePath, Guid? conversationId = null, int? maxColumns = null)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            FileGridDocument document = Tool_1.ReadFileToCharGridDocument(filePath, maxColumns);
            Dictionary<UInt128, string> rows = new Dictionary<UInt128, string>();
            for (int row = 0; row < document.RowLengths.Count; row++)
            {
                int length = document.RowLengths[row];
                char[] characters = new char[length];
                for (int column = 0; column < length; column++)
                {
                    characters[column] = document.Characters[row, column];
                }
                rows.Add((UInt128)(ulong)row, new string(characters));
            }
            return new ConversationText(conversationId ?? Guid.NewGuid(), rows);
        }

        /// <summary>
        /// Splits a row into tokens: runs of letters or digits, runs of whitespace, and single other characters.
        /// Concatenating the tokens reproduces the row exactly.
        /// </summary>
        internal static List<string> Tokenize(string row)
        {
            ArgumentNullException.ThrowIfNull(row);
            List<string> tokens = new List<string>();
            int index = 0;
            while (index < row.Length)
            {
                int start = index;
                char current = row[index];
                if (char.IsLetterOrDigit(current))
                {
                    while (index < row.Length && char.IsLetterOrDigit(row[index])) index++;
                }
                else if (char.IsWhiteSpace(current))
                {
                    while (index < row.Length && char.IsWhiteSpace(row[index])) index++;
                }
                else
                {
                    index++;
                }
                tokens.Add(row.Substring(start, index - start));
            }
            return tokens;
        }

        private static Guid ResolveSense(Guid? senseId)
        {
            Guid sense = senseId ?? LRD_Core.DefaultSenseId;
            if (sense == Guid.Empty)
            {
                throw new ArgumentException("Sense identifiers cannot be Guid.Empty.", nameof(senseId));
            }
            return sense;
        }

        /// <summary>Resolves a row to a sentence Guid: trusted match, then staged match, then a newly staged sentence.</summary>
        private static Guid ResolveSentence(string row, Guid sense)
        {
            List<Guid> wordIds = Tokenize(row).Select(token => ResolveWord(token, sense)).ToList();
            LRD_Core core = LRD_Core.Instance;
            SentenceBox candidate = core.GetNewSentenceBox(wordIds, sense, checkUntrusted: true);
            Guid sentenceId = candidate.GetSentenceID();
            if (core.GetTrustedSentenceBox(sentenceId) != null || core.GetUntrustedSentenceBox(sentenceId) != null)
            {
                return sentenceId;
            }
            if (!core.SetUntrustedSentence(candidate))
            {
                throw new InvalidOperationException("Failed to stage a new untrusted sentence.");
            }
            return sentenceId;
        }

        /// <summary>Resolves a token to a word Guid: trusted match, then staged match, then a newly staged word.</summary>
        private static Guid ResolveWord(string token, Guid sense)
        {
            List<Guid> symbolIds = token.Select(symbolChar => ResolveSymbol(symbolChar, sense)).ToList();
            LRD_Core core = LRD_Core.Instance;
            // Placeholder classification: words created here stay untrusted until a real part of speech is assigned.
            const PartOfSpeech partOfSpeech = PartOfSpeech.Unknown;
            WordBox candidate = core.GetNewWordBox(symbolIds, sense, LRD_Core.DefaultUsageId, partOfSpeech);
            Guid wordId = candidate.GetWordID();
            if (core.GetTrustedWordBox(wordId) != null)
            {
                return wordId;
            }
            WordBox? staged = core.FindUntrustedWordByIdentity(symbolIds, sense, LRD_Core.DefaultUsageId, partOfSpeech);
            if (staged != null)
            {
                return staged.GetWordID();
            }
            if (!core.SetUntrustedWord(candidate))
            {
                throw new InvalidOperationException("Failed to stage a new untrusted word.");
            }
            return wordId;
        }

        /// <summary>Resolves a character to a symbol Guid: trusted match, then staged match, then a newly staged symbol.</summary>
        private static Guid ResolveSymbol(char symbolChar, Guid sense)
        {
            LRD_Core core = LRD_Core.Instance;
            SymbolBox candidate = core.GetNewSymbolBox(symbolChar, sense);
            Guid symbolId = candidate.GetSymbolID();
            if (core.GetTrustedSymbolBox(symbolId) != null)
            {
                return symbolId;
            }
            SymbolBox? staged = core.FindUntrustedSymbolByIdentity(symbolChar, sense);
            if (staged != null)
            {
                return staged.GetSymbolID();
            }
            if (!core.SetUntrustedSymbol(candidate))
            {
                throw new InvalidOperationException("Failed to stage a new untrusted symbol.");
            }
            return symbolId;
        }

        private static string ReadSentenceText(Guid sentenceId)
        {
            LRD_Core core = LRD_Core.Instance;
            SentenceBox? sentence = core.GetTrustedSentenceBox(sentenceId) ?? core.GetUntrustedSentenceBox(sentenceId);
            if (sentence == null)
            {
                throw new InvalidOperationException($"Sentence '{sentenceId}' is not registered.");
            }
            StringBuilder builder = new StringBuilder();
            foreach (Guid wordId in sentence.GetWords() ?? new List<Guid>())
            {
                builder.Append(ReadWordText(wordId));
            }
            return builder.ToString();
        }

        private static string ReadWordText(Guid wordId)
        {
            LRD_Core core = LRD_Core.Instance;
            WordBox? word = core.GetTrustedWordBox(wordId) ?? core.GetUntrustedWordBox(wordId);
            if (word == null)
            {
                throw new InvalidOperationException($"Word '{wordId}' is not registered.");
            }
            StringBuilder builder = new StringBuilder();
            foreach (Guid symbolId in word.GetSymbols() ?? new List<Guid>())
            {
                SymbolBox? symbol = core.GetTrustedSymbolBox(symbolId) ?? core.GetUntrustedSymbolBox(symbolId);
                if (symbol == null)
                {
                    throw new InvalidOperationException($"Symbol '{symbolId}' is not registered.");
                }
                builder.Append(symbol.GetSymbolChar());
            }
            return builder.ToString();
        }
    }
}
