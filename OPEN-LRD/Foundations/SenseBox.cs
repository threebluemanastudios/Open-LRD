using System;
using System.Collections.Generic;
using System.Text;

namespace OPEN_LRD.Foundations
{
    internal class SenseBox
    {
        private Guid SenseID;
        private List<Guid> _sentences;
        private List<Guid> _leadingWords;
        private List<Guid> _trailingWords;
        private float AverageWordPosition;
        private float AverageSentencePosition;
        private float AverageSpread;

        public SenseBox(Guid senseId)
        {
            SenseID = senseId;
            AverageWordPosition = 0.0f;
            AverageSentencePosition = 0.0f;
            AverageSpread = 0.0f;
            _sentences = new List<Guid>();
            _leadingWords = new List<Guid>();
            _trailingWords = new List<Guid>();
        }
        public void AddSentence(SentenceBox sentence)
        {
            if (_sentences == null)
            {
                _sentences = new List<Guid>();
            }
            _sentences.Add(sentence.GetSentenceID());
        }
        public void AddLeadingWord(WordBox word)
        {
            if (_leadingWords == null)
            {
                _leadingWords = new List<Guid>();
            }
            _leadingWords.Add(word.GetWordID());
        }
        public void AddTrailingWord(WordBox word)
        {
            if (_trailingWords == null)
            {
                _trailingWords = new List<Guid>();
            }
            _trailingWords.Add(word.GetWordID());
        }
        public Guid GetSenseID()
        {
            return SenseID;
        }
        public List<Guid>? GetSentences()
        {
            return _sentences;
        }
        public List<Guid>? GetLeadingWords()
        {
            return _leadingWords;
        }
        public List<Guid>? GetTrailingWords()
        {
            return _trailingWords;
        }
        public float GetAverageWordPosition()
        {
            return AverageWordPosition;
        }

        public float GetAverageSentencePosition()
        {
            return AverageSentencePosition;
        }

        public float GetAverageSpread()
        {
            return AverageSpread;
        }
        public void SetAverageWordPosition(float avgWordPos)
        {
            AverageWordPosition = avgWordPos;
        }

        public void SetAverageSentencePosition(float avgSentPos)
        {
            AverageSentencePosition = avgSentPos;
        }

        public void SetAverageSpread(float avgSpread)
        {
            AverageSpread = avgSpread;
        }
        public void UpdateAverageMetrics(float avgWordPos, float avgSentPos, float avgSpread)
        {
            AverageWordPosition = avgWordPos;
            AverageSentencePosition = avgSentPos;
            AverageSpread = avgSpread;
        }
        public void ResetAverageMetrics()
        {
            AverageWordPosition = 0.0f;
            AverageSentencePosition = 0.0f;
            AverageSpread = 0.0f;
        }
        public void ClearSentences()
        {
            if (_sentences != null)
            {
                _sentences.Clear();
            }
        }
        public void RemoveSentence(Guid sentenceId)
        {
            if (_sentences != null && _sentences.Contains(sentenceId))
            {
                _sentences.Remove(sentenceId);
            }
        }

        public void ClearLeadingWords()
        {
            if (_leadingWords != null)
            {
                _leadingWords.Clear();
            }
        }

        public void RemoveLeadingWord(Guid wordId)
        {
            if (_leadingWords != null && _leadingWords.Contains(wordId))
            {
                _leadingWords.Remove(wordId);
            }
        }
        public void ClearTrailingWords()
        {
            if (_trailingWords != null)
            {
                _trailingWords.Clear();
            }
        }
        public void RemoveTrailingWord(Guid wordId)
        {
            if (_trailingWords != null && _trailingWords.Contains(wordId))
            {
                _trailingWords.Remove(wordId);
            }
        } 
        
    }
}
