namespace OPEN_LRD.Nodes
{
    /// <summary>Stores the identity, metadata, lifetime, trust, and usage weights of a graph node.</summary>
    public class Node : IDisposable
    {
        /// <summary>Gets the upward-weight threshold at which temporary nodes become permanent.</summary>
        public const int PromotionWeightThreshold = 100;
        /// <summary>Gets the downward-weight threshold at which temporary nodes are disposed.</summary>
        public const int DemotionWeightThreshold = 100;
        /// <summary>Gets the default lifetime counter assigned to temporary nodes.</summary>
        public const int DefaultTimeToLive = 6000;

        /// <summary>Gets the immutable unique identifier of this node.</summary>
        public Guid ID { get; }
        /// <summary>Gets the semantic type used by derived node implementations.</summary>
        protected NodeTypes nodeType;
        /// <summary>Gets or sets the accumulated verified-usage weight for derived node implementations.</summary>
        protected float weightUp;
        /// <summary>Gets or sets the accumulated invalid-evidence weight for derived node implementations.</summary>
        protected float weightDown;
        /// <summary>Gets or sets the remaining temporary lifetime counter.</summary>
        protected int timeToLive;
        /// <summary>Gets or sets the current lifetime classification.</summary>
        protected NodeLifetime lifetime;
        private readonly List<string> aliases = new List<string>();
        private DataPointer? dataPointer;
        private double? trustRating;
        private bool disposed;

        /// <summary>Gets the normalized node label, when one is assigned.</summary>
        public string? Label { get; private set; }

        /// <summary>Gets a read-only view of the node's normalized aliases.</summary>
        public IReadOnlyCollection<string> Aliases => aliases.AsReadOnly();

        /// <summary>Gets the node's optional trust rating in the inclusive range from zero to one.</summary>
        public double? TrustRating => trustRating;

        /// <summary>Creates a temporary basic node with a generated identifier and the default TTL.</summary>
        public Node()
            : this(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        /// <summary>Creates a temporary basic node with the supplied identifier and default TTL.</summary>
        /// <param name="nodeID">The unique identifier for the node.</param>
        public Node(Guid nodeID)
            : this(nodeID, NodeTypes.Basics, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        /// <summary>Creates a temporary node of the specified type with the supplied identifier.</summary>
        /// <param name="nodeType">The semantic type of the node.</param>
        /// <param name="nodeID">The unique identifier for the node.</param>
        public Node(NodeTypes nodeType, Guid nodeID)
            : this(nodeID, nodeType, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        /// <summary>Creates a temporary node with a specified identifier, type, and TTL.</summary>
        /// <param name="id">The unique identifier for the node.</param>
        /// <param name="type">The semantic type of the node.</param>
        /// <param name="ttl">The initial non-negative time-to-live counter.</param>
        public Node(Guid id, NodeTypes type, int ttl)
            : this(id, type, NodeLifetime.Temporary, ttl)
        {
        }

        /// <summary>Creates a node with an explicit lifetime and TTL.</summary>
        /// <param name="id">The unique identifier for the node.</param>
        /// <param name="type">The semantic type of the node.</param>
        /// <param name="lifetime">The node's initial lifetime classification.</param>
        /// <param name="ttl">The initial non-negative time-to-live counter.</param>
        public Node(Guid id, NodeTypes type, NodeLifetime lifetime, int ttl)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Node ID cannot be empty.", nameof(id));

            ArgumentNullException.ThrowIfNull(type);

            if (ttl < 0)
                throw new ArgumentOutOfRangeException(nameof(ttl), "Time-to-live cannot be negative.");

            ID = id;
            nodeType = type;
            this.lifetime = lifetime;
            timeToLive = ttl;
            weightUp = 0.0f;
            weightDown = 0.0f;
        }

        /// <summary>Decrements the temporary lifetime counter when it is positive.</summary>
        protected void DecrementTTL()
        {
            if (timeToLive > 0)
                timeToLive--;
        }

        /// <summary>Runs one node maintenance step, updating lifetime or disposing expired knowledge.</summary>
        public virtual void PerformMaintenance()
        {
            if (disposed)
                return;

            if (lifetime == NodeLifetime.Permanent)
                return;

            if (weightUp >= PromotionWeightThreshold)
            {
                lifetime = NodeLifetime.Permanent;
                return;
            }

            if (weightDown >= DemotionWeightThreshold)
            {
                Dispose();
                return;
            }

            DecrementTTL();

            if (timeToLive <= 0)
                Dispose();
        }

        /// <summary>Registers verified usage and promotes the node when its threshold is reached.</summary>
        /// <param name="increment">The positive amount to add to upward weight.</param>
        /// <returns><see langword="true"/> when this call promotes the node.</returns>
        public bool RegisterVerifiedReference(float increment = 1f)
        {
            if (increment <= 0.0f)
                throw new ArgumentOutOfRangeException(nameof(increment), "Increment must be greater than zero.");

            if (disposed)
                return false;

            weightUp += increment;

            if (lifetime == NodeLifetime.Temporary && weightUp >= PromotionWeightThreshold)
            {
                lifetime = NodeLifetime.Permanent;
                return true;
            }

            return false;
        }

        /// <summary>Registers invalid evidence and disposes the node when its threshold is reached.</summary>
        /// <param name="increment">The positive amount to add to downward weight.</param>
        /// <returns><see langword="true"/> when this call disposes the node.</returns>
        public bool RegisterInvalidReference(float increment = 1f)
        {
            if (increment <= 0.0f)
                throw new ArgumentOutOfRangeException(nameof(increment), "Increment must be greater than zero.");

            if (disposed)
                return false;

            weightDown += increment;

            if (weightDown >= DemotionWeightThreshold)
            {
                Dispose();
                return true;
            }

            return false;
        }

        /// <summary>Disposes the node and releases its external data pointer.</summary>
        public virtual void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            dataPointer = null;
        }

        /// <summary>Returns the node's identifier.</summary>
        public Guid Get_ID()
        {
            return ID;
        }

        /// <summary>Sets the node label after trimming surrounding whitespace.</summary>
        /// <param name="label">The label to assign, or <see langword="null"/> to clear it.</param>
        public void Set_Label(string? label)
        {
            Label = NormalizeLabel(label);
        }

        /// <summary>Adds a normalized alias unless it duplicates the label or an existing alias.</summary>
        /// <param name="alias">The alias to add.</param>
        /// <returns><see langword="true"/> when a new alias is added.</returns>
        public bool Add_Alias(string alias)
        {
            string? normalized = NormalizeLabel(alias);

            if (normalized is null)
                return false;

            if (Has_Label(normalized) || Has_Alias(normalized))
                return false;

            aliases.Add(normalized);
            return true;
        }

        /// <summary>Removes a matching alias without regard to case.</summary>
        /// <param name="alias">The alias to remove.</param>
        /// <returns><see langword="true"/> when an alias is removed.</returns>
        public bool Remove_Alias(string alias)
        {
            string? normalized = NormalizeLabel(alias);

            if (normalized is null)
                return false;

            int index = aliases.FindIndex(
                existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
                return false;

            aliases.RemoveAt(index);
            return true;
        }

        /// <summary>Checks the label using the specified string comparison.</summary>
        /// <param name="value">The label value to compare.</param>
        /// <param name="comparison">The string comparison rule.</param>
        /// <returns><see langword="true"/> when the label matches.</returns>
        public bool Has_Label(string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            return Label is not null && string.Equals(Label, value, comparison);
        }

        /// <summary>Checks the aliases using the specified string comparison.</summary>
        /// <param name="value">The alias value to compare.</param>
        /// <param name="comparison">The string comparison rule.</param>
        /// <returns><see langword="true"/> when an alias matches.</returns>
        public bool Has_Alias(string value, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
        {
            return aliases.Exists(existing => string.Equals(existing, value, comparison));
        }

        /// <summary>Trims a label or alias value and returns null when it is blank.</summary>
        private static string? NormalizeLabel(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>Returns the optional external data pointer.</summary>
        public DataPointer? Get_DataPointer()
        {
            return dataPointer;
        }

        /// <summary>Assigns an external data pointer to the node.</summary>
        /// <param name="pointer">The validated pointer to store.</param>
        public void Set_DataPointer(DataPointer pointer)
        {
            ArgumentNullException.ThrowIfNull(pointer);
            dataPointer = pointer;
        }

        /// <summary>Removes the external data pointer from the node.</summary>
        public void Clear_DataPointer()
        {
            dataPointer = null;
        }

        /// <summary>Sets the optional trust rating after validating its range and finiteness.</summary>
        /// <param name="value">A trust score in [0, 1], or <see langword="null"/> to clear it.</param>
        public void Set_TrustRating(double? value)
        {
            if (value.HasValue
                && (!double.IsFinite(value.Value) || value.Value < 0.0 || value.Value > 1.0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Trust rating must be finite and between 0.0 and 1.0.");
            }

            trustRating = value;
        }

        /// <summary>Returns the accumulated upward usage weight.</summary>
        public float Get_WeightUp()
        {
            return weightUp;
        }

        /// <summary>Adds verified usage weight through the promotion policy.</summary>
        /// <param name="value">The positive weight increment.</param>
        public void Add_WeightUp(float value)
        {
            RegisterVerifiedReference(value);
        }

        /// <summary>Returns the accumulated downward usage weight.</summary>
        public float Get_WeightDown()
        {
            return weightDown;
        }

        /// <summary>Adds invalid-evidence weight through the disposal policy.</summary>
        /// <param name="value">The positive weight increment.</param>
        public void Add_WeightDown(float value)
        {
            RegisterInvalidReference(value);
        }

        /// <summary>Returns the node's current time-to-live counter.</summary>
        public int Get_TTL()
        {
            return timeToLive;
        }

        /// <summary>Returns the node's semantic type.</summary>
        public NodeTypes Get_NodeType()
        {
            return nodeType;
        }

        /// <summary>Returns the node's lifetime classification.</summary>
        public NodeLifetime Get_Lifetime()
        {
            return lifetime;
        }

        /// <summary>Gets whether the node is temporary.</summary>
        public bool IsTemporary => lifetime == NodeLifetime.Temporary;

        /// <summary>Gets whether the node is permanent.</summary>
        public bool IsPermanent => lifetime == NodeLifetime.Permanent;

        /// <summary>Gets whether the node has been disposed.</summary>
        public bool IsDisposed => disposed;

        /// <summary>Promotes this node to permanent lifetime.</summary>
        public void PromoteToPermanent()
        {
            lifetime = NodeLifetime.Permanent;
        }

        /// <summary>Merges compatible labels, aliases, trust, weights, lifetime, and data pointers from another node.</summary>
        internal bool MergeMetadataFrom(Node other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (dataPointer is not null
                && other.dataPointer is not null
                && !Equals(dataPointer, other.dataPointer))
            {
                return false;
            }

            if (dataPointer is null)
                dataPointer = other.dataPointer;

            if (other.trustRating.HasValue)
            {
                trustRating = trustRating.HasValue
                    ? Math.Max(trustRating.Value, other.trustRating.Value)
                    : other.trustRating;
            }

            if (Label is null && other.Label is not null)
                Set_Label(other.Label);
            else if (other.Label is not null && !Has_Label(other.Label))
                Add_Alias(other.Label);

            foreach (string alias in other.aliases)
                Add_Alias(alias);

            weightUp = Math.Max(weightUp, other.weightUp);
            weightDown = Math.Max(weightDown, other.weightDown);
            timeToLive = Math.Max(timeToLive, other.timeToLive);

            if (lifetime == NodeLifetime.Permanent || other.lifetime == NodeLifetime.Permanent)
                lifetime = NodeLifetime.Permanent;

            return true;
        }
    }
}
