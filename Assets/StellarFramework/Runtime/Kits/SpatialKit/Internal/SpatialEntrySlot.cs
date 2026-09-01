namespace StellarFramework
{
    /// <summary>空间桶内侵入式链表节点。一个活动节点恰好属于一个桶。</summary>
    internal struct SpatialEntrySlot
    {
        public SpatialId Id;
        public SpatialPoint Position;
        public SpatialBucketCoord Bucket;
        public int Previous;
        public int Next;
        public int NextFree;
    }
}
