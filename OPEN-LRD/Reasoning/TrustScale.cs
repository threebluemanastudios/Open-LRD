namespace OPEN_LRD.Reasoning
{
    /// <summary>Stores weighted verified and invalid evidence and computes source/topic trust assessments.</summary>
    internal sealed class TrustScale
    {
        private readonly ReasoningOptions options;
        private readonly Dictionary<TrustKey, TrustEvidence> evidence = new Dictionary<TrustKey, TrustEvidence>();

        /// <summary>Initializes a trust scale configured with the specified reasoning options.</summary>
        public TrustScale(ReasoningOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>Records a single verified or invalid trust evidence observation for a source and optional topic.</summary>
        public void RecordEvidence(
            Guid sourceNodeID,
            Guid? topicNodeID,
            bool verified,
            DateTimeOffset? timestamp = null)
        {
            if (sourceNodeID == Guid.Empty)
                throw new ArgumentException("A trust source node ID is required.", nameof(sourceNodeID));

            DateTimeOffset now = timestamp ?? DateTimeOffset.UtcNow;
            TrustKey key = new TrustKey(sourceNodeID, topicNodeID);

            if (!evidence.TryGetValue(key, out TrustEvidence? entry))
            {
                entry = new TrustEvidence
                {
                    FirstSeen = now
                };
                evidence[key] = entry;
            }

            if (verified)
                entry.Verified += 1.0;
            else
                entry.Invalid += 1.0;

            entry.LastUpdated = now;
        }

        /// <summary>Records a batch of verified and invalid evidence counts for a source and optional topic.</summary>
        public void RecordEvidenceBatch(
            Guid sourceNodeID,
            Guid? topicNodeID,
            double verifiedCount,
            double invalidCount,
            DateTimeOffset? timestamp = null)
        {
            if (sourceNodeID == Guid.Empty)
                throw new ArgumentException("A trust source node ID is required.", nameof(sourceNodeID));

            if (verifiedCount < 0 || invalidCount < 0)
                throw new ArgumentOutOfRangeException(nameof(verifiedCount), "Evidence counts cannot be negative.");

            if (verifiedCount == 0 && invalidCount == 0)
                return;

            DateTimeOffset now = timestamp ?? DateTimeOffset.UtcNow;
            TrustKey key = new TrustKey(sourceNodeID, topicNodeID);

            if (!evidence.TryGetValue(key, out TrustEvidence? entry))
            {
                entry = new TrustEvidence
                {
                    FirstSeen = now
                };
                evidence[key] = entry;
            }

            entry.Verified += verifiedCount;
            entry.Invalid += invalidCount;
            entry.LastUpdated = now;
        }

        /// <summary>Computes the smoothed trust assessment and decision for a source and optional topic.</summary>
        public TrustAssessment Assess(Guid sourceNodeID, Guid? topicNodeID)
        {
            if (sourceNodeID == Guid.Empty)
                throw new ArgumentException("A trust source node ID is required.", nameof(sourceNodeID));

            bool usesTopic = false;
            bool usesGlobalFallback = false;
            TrustEvidence? selected = null;
            TrustEvidence? topicEvidence = null;
            TrustEvidence? globalEvidence = null;

            if (topicNodeID.HasValue)
                evidence.TryGetValue(new TrustKey(sourceNodeID, topicNodeID), out topicEvidence);

            evidence.TryGetValue(new TrustKey(sourceNodeID, null), out globalEvidence);

            if (HasEvidence(topicEvidence)
                && topicEvidence!.Verified + topicEvidence.Invalid >= options.MinTrustEvidence)
            {
                selected = topicEvidence;
                usesTopic = true;
            }
            else if (HasEvidence(globalEvidence))
            {
                selected = globalEvidence;
                usesGlobalFallback = topicNodeID.HasValue;
            }
            else if (HasEvidence(topicEvidence))
            {
                selected = topicEvidence;
                usesTopic = true;
            }

            double verified = selected?.Verified ?? 0.0;
            double invalid = selected?.Invalid ?? 0.0;
            double total = verified + invalid;

            double ratioDenominator = total + options.TrustPriorVerified + options.TrustPriorInvalid;
            double ratio = ratioDenominator <= 0.0
                ? 0.5
                : (verified + options.TrustPriorVerified) / ratioDenominator;

            double confidence = options.TrustVolumeSmoothing <= 0.0
                ? (total > 0.0 ? 1.0 : 0.0)
                : total / (total + options.TrustVolumeSmoothing);

            double score = ratio * confidence;

            TrustDecision decision = TrustDecision.Neutral;

            if (total >= options.MinTrustEvidence && total > 0.0)
            {
                if (score >= options.TrustedThreshold)
                    decision = TrustDecision.Trusted;
                else if (score <= options.DiscreditedThreshold)
                    decision = TrustDecision.Discredited;
            }

            return new TrustAssessment(
                sourceNodeID,
                topicNodeID,
                verified,
                invalid,
                total,
                ratio,
                confidence,
                score,
                decision,
                usesTopic,
                usesGlobalFallback);
        }

        /// <summary>Returns the computed trust score for a source and optional topic.</summary>
        public double GetScore(Guid sourceNodeID, Guid? topicNodeID)
        {
            return Assess(sourceNodeID, topicNodeID).Score;
        }

        /// <summary>Returns the computed trust decision for a source and optional topic.</summary>
        public TrustDecision GetDecision(Guid sourceNodeID, Guid? topicNodeID)
        {
            return Assess(sourceNodeID, topicNodeID).Decision;
        }

        /// <summary>Attempts to retrieve the raw trust evidence entry for a source and optional topic.</summary>
        public bool TryGetEvidence(Guid sourceNodeID, Guid? topicNodeID, out TrustEvidence foundEvidence)
        {
            if (evidence.TryGetValue(new TrustKey(sourceNodeID, topicNodeID), out TrustEvidence? entry))
            {
                foundEvidence = entry;
                return true;
            }

            foundEvidence = null!;
            return false;
        }

        /// <summary>Determines whether a trust evidence entry contains any recorded observations.</summary>
        private static bool HasEvidence(TrustEvidence? entry)
        {
            return entry is not null && entry.Verified + entry.Invalid > 0.0;
        }
    }
}
