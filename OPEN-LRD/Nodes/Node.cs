namespace OPEN_LRD.Nodes
{
    public class Node : IDisposable
    {
        public const int PromotionWeightThreshold = 100;
        public const int DemotionWeightThreshold = 100;
        public const int DefaultTimeToLive = 6000;

        public Guid ID { get; }
        protected NodeTypes nodeType;
        protected float weightUp, weightDown;
        protected int timeToLive;
        protected NodeLifetime lifetime;
        private DataPointer? dataPointer;
        private bool disposed;

        public Node()
            : this(Guid.NewGuid(), NodeTypes.Basics, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        public Node(Guid nodeID)
            : this(nodeID, NodeTypes.Basics, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        public Node(NodeTypes nodeType, Guid nodeID)
            : this(nodeID, nodeType, NodeLifetime.Temporary, DefaultTimeToLive)
        {
        }

        public Node(Guid id, NodeTypes type, int ttl)
            : this(id, type, NodeLifetime.Temporary, ttl)
        {
        }

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

        protected void DecrementTTL()
        {
            if (timeToLive > 0)
                timeToLive--;
        }

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

        public virtual void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            dataPointer = null;
        }

        public Guid Get_ID()
        {
            return ID;
        }

        public DataPointer? Get_DataPointer()
        {
            return dataPointer;
        }

        public void Set_DataPointer(DataPointer pointer)
        {
            ArgumentNullException.ThrowIfNull(pointer);
            dataPointer = pointer;
        }

        public void Clear_DataPointer()
        {
            dataPointer = null;
        }

        public float Get_WeightUp()
        {
            return weightUp;
        }

        public void Add_WeightUp(float value)
        {
            RegisterVerifiedReference(value);
        }

        public float Get_WeightDown()
        {
            return weightDown;
        }

        public void Add_WeightDown(float value)
        {
            RegisterInvalidReference(value);
        }

        public int Get_TTL()
        {
            return timeToLive;
        }

        public NodeTypes Get_NodeType()
        {
            return nodeType;
        }

        public NodeLifetime Get_Lifetime()
        {
            return lifetime;
        }

        public bool IsTemporary => lifetime == NodeLifetime.Temporary;

        public bool IsPermanent => lifetime == NodeLifetime.Permanent;

        public bool IsDisposed => disposed;

        public void PromoteToPermanent()
        {
            lifetime = NodeLifetime.Permanent;
        }
    }
}
