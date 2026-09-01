namespace StellarFramework
{
    internal struct PathRecord
    {
        public PathNodeId Node;
        public long G;
        public long H;
        public long F;
        public int ParentIndex;
        public PathRecordState State;
        public int OpenHeapIndex;
    }
}
