using System;
using System.Collections.Generic;

namespace StellarFramework
{
    /// <summary>
    /// 连续二维点的动态均匀空间哈希索引。
    /// 该类型只保存 SpatialId、坐标和内部桶链表，不持有业务对象或 Unity 生命周期。
    /// </summary>
    public sealed class SpatialIndex2D
    {
        private const int MinimumGrowth = 4;

        private readonly float _bucketSize;
        private readonly Dictionary<SpatialId, int> _idToSlot;
        private readonly Dictionary<SpatialBucketCoord, int> _bucketHeads;
        private SpatialEntrySlot[] _slots;
        private int _nextSlot;
        private int _freeHead = -1;
        private int _count;

        /// <summary>每个均匀空间桶的边长。</summary>
        public float BucketSize => _bucketSize;

        /// <summary>当前活动点数量。</summary>
        public int Count => _count;

        public SpatialIndex2D(float bucketSize, int initialCapacity = 0)
        {
            if (float.IsNaN(bucketSize) || float.IsInfinity(bucketSize) || bucketSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(bucketSize), bucketSize, "BucketSize 必须是有限正数。");
            }

            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            _bucketSize = bucketSize;
            _idToSlot = new Dictionary<SpatialId, int>(initialCapacity);
            _bucketHeads = new Dictionary<SpatialBucketCoord, int>(initialCapacity);
            _slots = initialCapacity == 0 ? Array.Empty<SpatialEntrySlot>() : new SpatialEntrySlot[initialCapacity];
        }

        /// <summary>判断有效 ID 是否已插入。</summary>
        public bool Contains(SpatialId id)
        {
            return id.IsValid && _idToSlot.ContainsKey(id);
        }

        /// <summary>读取已插入点的位置。</summary>
        public bool TryGetPosition(SpatialId id, out SpatialPoint position)
        {
            if (id.IsValid && _idToSlot.TryGetValue(id, out int slotIndex))
            {
                position = _slots[slotIndex].Position;
                return true;
            }

            position = default(SpatialPoint);
            return false;
        }

        /// <summary>插入点；失败时不改变索引状态。</summary>
        public SpatialMutationResult TryInsert(SpatialId id, SpatialPoint position)
        {
            if (!id.IsValid)
            {
                return SpatialMutationResult.Failed(SpatialMutationError.InvalidId);
            }

            if (!TryGetBucket(position, out SpatialBucketCoord bucket))
            {
                return SpatialMutationResult.Failed(SpatialMutationError.PositionOutOfRange);
            }

            if (_idToSlot.ContainsKey(id))
            {
                return SpatialMutationResult.Failed(SpatialMutationError.DuplicateId);
            }

            int slotIndex = AcquireSlot();
            SpatialEntrySlot slot = _slots[slotIndex];
            slot.Id = id;
            slot.Position = position;
            slot.Bucket = bucket;
            slot.Previous = -1;
            slot.Next = -1;
            slot.NextFree = -1;
            _slots[slotIndex] = slot;

            _idToSlot.Add(id, slotIndex);
            LinkSlot(slotIndex, bucket);
            _count++;
            return SpatialMutationResult.Succeeded();
        }

        /// <summary>移除点；有效但不存在的 ID 返回 NotFound。</summary>
        public SpatialMutationResult TryRemove(SpatialId id)
        {
            if (!id.IsValid)
            {
                return SpatialMutationResult.Failed(SpatialMutationError.InvalidId);
            }

            if (!_idToSlot.TryGetValue(id, out int slotIndex))
            {
                return SpatialMutationResult.Failed(SpatialMutationError.NotFound);
            }

            SpatialEntrySlot slot = _slots[slotIndex];
            UnlinkSlot(slotIndex, slot.Bucket);
            _idToSlot.Remove(id);

            slot.Id = default(SpatialId);
            slot.Position = default(SpatialPoint);
            slot.Previous = -1;
            slot.Next = -1;
            slot.NextFree = _freeHead;
            _slots[slotIndex] = slot;
            _freeHead = slotIndex;
            _count--;
            return SpatialMutationResult.Succeeded();
        }

        /// <summary>更新点的位置；跨桶移动使用原子摘链/挂链。</summary>
        public SpatialMutationResult TryUpdatePosition(SpatialId id, SpatialPoint newPosition)
        {
            if (!id.IsValid)
            {
                return SpatialMutationResult.Failed(SpatialMutationError.InvalidId);
            }

            if (!_idToSlot.TryGetValue(id, out int slotIndex))
            {
                return SpatialMutationResult.Failed(SpatialMutationError.NotFound);
            }

            if (!TryGetBucket(newPosition, out SpatialBucketCoord newBucket))
            {
                return SpatialMutationResult.Failed(SpatialMutationError.PositionOutOfRange);
            }

            SpatialEntrySlot slot = _slots[slotIndex];
            if (slot.Bucket != newBucket)
            {
                UnlinkSlot(slotIndex, slot.Bucket);
                slot.Position = newPosition;
                slot.Bucket = newBucket;
                _slots[slotIndex] = slot;
                LinkSlot(slotIndex, newBucket);
            }
            else
            {
                slot.Position = newPosition;
                _slots[slotIndex] = slot;
            }

            return SpatialMutationResult.Succeeded();
        }

        /// <summary>查询半开矩形内的点。</summary>
        public SpatialQueryResult QueryRect(SpatialRect rect, Span<SpatialId> results)
        {
            if (rect.IsEmpty)
            {
                return new SpatialQueryResult(0, 0);
            }

            if (!TryMapBucket(rect.MinX, out int minX) || !TryMapBucket(rect.MinY, out int minY) ||
                !TryMapBucket(rect.MaxExclusiveX, out int maxX) || !TryMapBucket(rect.MaxExclusiveY, out int maxY))
            {
                throw new ArgumentOutOfRangeException(nameof(rect), "SpatialRect 的桶范围超出 Int32 BucketCoord。");
            }

            if (_count == 0)
            {
                return new SpatialQueryResult(0, 0);
            }

            int written = 0;
            int matches = 0;
            for (int bucketX = minX; ; bucketX++)
            {
                for (int bucketY = minY; ; bucketY++)
                {
                    if (_bucketHeads.TryGetValue(new SpatialBucketCoord(bucketX, bucketY), out int slotIndex))
                    {
                        while (slotIndex != -1)
                        {
                            SpatialEntrySlot slot = _slots[slotIndex];
                            if (rect.Contains(slot.Position))
                            {
                                if (written < results.Length)
                                {
                                    results[written] = slot.Id;
                                    written++;
                                }

                                matches++;
                            }

                            slotIndex = slot.Next;
                        }
                    }

                    if (bucketY == maxY)
                    {
                        break;
                    }
                }

                if (bucketX == maxX)
                {
                    break;
                }
            }

            return new SpatialQueryResult(written, matches);
        }

        /// <summary>查询闭圆内的点，边界距离使用 <=。</summary>
        public SpatialQueryResult QueryCircle(SpatialPoint center, float radius, Span<SpatialId> results)
        {
            ValidateRadius(radius, nameof(radius));
            double radiusValue = radius;
            double radiusSquared = radiusValue * radiusValue;
            double minWorldX = (double)center.X - radiusValue;
            double maxWorldX = (double)center.X + radiusValue;
            double minWorldY = (double)center.Y - radiusValue;
            double maxWorldY = (double)center.Y + radiusValue;
            if (!TryMapBucket(minWorldX, out int minX) || !TryMapBucket(minWorldY, out int minY) ||
                !TryMapBucket(maxWorldX, out int maxX) || !TryMapBucket(maxWorldY, out int maxY))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "QueryCircle 的桶范围超出 Int32 BucketCoord。");
            }

            if (_count == 0)
            {
                return new SpatialQueryResult(0, 0);
            }

            int written = 0;
            int matches = 0;
            for (int bucketX = minX; ; bucketX++)
            {
                for (int bucketY = minY; ; bucketY++)
                {
                    if (_bucketHeads.TryGetValue(new SpatialBucketCoord(bucketX, bucketY), out int slotIndex))
                    {
                        while (slotIndex != -1)
                        {
                            SpatialEntrySlot slot = _slots[slotIndex];
                            double dx = (double)slot.Position.X - center.X;
                            double dy = (double)slot.Position.Y - center.Y;
                            if (dx * dx + dy * dy <= radiusSquared)
                            {
                                if (written < results.Length)
                                {
                                    results[written] = slot.Id;
                                    written++;
                                }

                                matches++;
                            }

                            slotIndex = slot.Next;
                        }
                    }

                    if (bucketY == maxY)
                    {
                        break;
                    }
                }

                if (bucketX == maxX)
                {
                    break;
                }
            }

            return new SpatialQueryResult(written, matches);
        }

        /// <summary>查找半径内距离最近的点；相同距离时选择数值更小的 ID。</summary>
        public bool TryFindNearest(SpatialPoint center, float maxRadius, out SpatialId id)
        {
            return TryFindNearest(center, maxRadius, default(SpatialId), out id);
        }

        /// <summary>查找半径内距离最近的点，并忽略指定的有效 ID。</summary>
        public bool TryFindNearest(SpatialPoint center, float maxRadius, SpatialId excludedId, out SpatialId id)
        {
            ValidateRadius(maxRadius, nameof(maxRadius));
            double radiusValue = maxRadius;
            double radiusSquared = radiusValue * radiusValue;
            double minWorldX = (double)center.X - radiusValue;
            double maxWorldX = (double)center.X + radiusValue;
            double minWorldY = (double)center.Y - radiusValue;
            double maxWorldY = (double)center.Y + radiusValue;

            bool found = false;
            double bestDistanceSquared = 0d;
            SpatialId bestId = default(SpatialId);
            if (!TryMapBucket(minWorldX, out int minX) || !TryMapBucket(minWorldY, out int minY) ||
                !TryMapBucket(maxWorldX, out int maxX) || !TryMapBucket(maxWorldY, out int maxY))
            {
                throw new ArgumentOutOfRangeException(nameof(maxRadius), "TryFindNearest 的桶范围超出 Int32 BucketCoord。");
            }
            else
            {
                for (int bucketX = minX; ; bucketX++)
                {
                    for (int bucketY = minY; ; bucketY++)
                    {
                        if (_bucketHeads.TryGetValue(new SpatialBucketCoord(bucketX, bucketY), out int slotIndex))
                        {
                            ScanNearestChain(center, radiusSquared, excludedId, slotIndex,
                                ref found, ref bestDistanceSquared, ref bestId);
                        }

                        if (bucketY == maxY)
                        {
                            break;
                        }
                    }

                    if (bucketX == maxX)
                    {
                        break;
                    }
                }
            }

            id = bestId;
            return found;
        }

        /// <summary>清空所有点并保留已分配的数组和字典容量。</summary>
        public void Clear()
        {
            _idToSlot.Clear();
            _bucketHeads.Clear();
            _nextSlot = 0;
            _freeHead = -1;
            _count = 0;
        }

        private int AcquireSlot()
        {
            if (_freeHead != -1)
            {
                int freeSlot = _freeHead;
                _freeHead = _slots[freeSlot].NextFree;
                return freeSlot;
            }

            if (_nextSlot >= _slots.Length)
            {
                GrowSlots();
            }

            return _nextSlot++;
        }

        private void GrowSlots()
        {
            int current = _slots.Length;
            int next;
            if (current == 0)
            {
                next = MinimumGrowth;
            }
            else
            {
                if (current > int.MaxValue / 2)
                {
                    throw new InvalidOperationException("SpatialIndex2D 已达到可用槽位上限。");
                }

                next = current * 2;
            }

            Array.Resize(ref _slots, next);
        }

        private void LinkSlot(int slotIndex, SpatialBucketCoord bucket)
        {
            int head = -1;
            if (_bucketHeads.TryGetValue(bucket, out int existingHead))
            {
                head = existingHead;
            }

            SpatialEntrySlot slot = _slots[slotIndex];
            slot.Bucket = bucket;
            slot.Previous = -1;
            slot.Next = head;
            _slots[slotIndex] = slot;
            if (head != -1)
            {
                SpatialEntrySlot oldHead = _slots[head];
                oldHead.Previous = slotIndex;
                _slots[head] = oldHead;
            }

            _bucketHeads[bucket] = slotIndex;
        }

        private void UnlinkSlot(int slotIndex, SpatialBucketCoord bucket)
        {
            SpatialEntrySlot slot = _slots[slotIndex];
            if (slot.Previous == -1)
            {
                if (slot.Next == -1)
                {
                    _bucketHeads.Remove(bucket);
                }
                else
                {
                    _bucketHeads[bucket] = slot.Next;
                }
            }
            else
            {
                SpatialEntrySlot previous = _slots[slot.Previous];
                previous.Next = slot.Next;
                _slots[slot.Previous] = previous;
            }

            if (slot.Next != -1)
            {
                SpatialEntrySlot next = _slots[slot.Next];
                next.Previous = slot.Previous;
                _slots[slot.Next] = next;
            }
        }

        private bool TryGetBucket(SpatialPoint position, out SpatialBucketCoord bucket)
        {
            if (!TryMapBucket(position.X, out int bucketX) || !TryMapBucket(position.Y, out int bucketY))
            {
                bucket = default(SpatialBucketCoord);
                return false;
            }

            bucket = new SpatialBucketCoord(bucketX, bucketY);
            return true;
        }

        private bool TryMapBucket(double coordinate, out int bucket)
        {
            double mapped = Math.Floor(coordinate / (double)_bucketSize);
            if (mapped < int.MinValue || mapped > int.MaxValue)
            {
                bucket = 0;
                return false;
            }

            bucket = (int)mapped;
            return true;
        }

        private void ScanNearestChain(SpatialPoint center, double radiusSquared, SpatialId excludedId, int slotIndex,
            ref bool found, ref double bestDistanceSquared, ref SpatialId bestId)
        {
            while (slotIndex != -1)
            {
                SpatialEntrySlot slot = _slots[slotIndex];
                ConsiderNearest(center, radiusSquared, excludedId, slot,
                    ref found, ref bestDistanceSquared, ref bestId);
                slotIndex = slot.Next;
            }
        }

        private static void ConsiderNearest(SpatialPoint center, double radiusSquared, SpatialId excludedId,
            SpatialEntrySlot slot, ref bool found, ref double bestDistanceSquared, ref SpatialId bestId)
        {
            if (excludedId.IsValid && slot.Id == excludedId)
            {
                return;
            }

            double dx = (double)slot.Position.X - center.X;
            double dy = (double)slot.Position.Y - center.Y;
            double distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > radiusSquared)
            {
                return;
            }

            if (!found || distanceSquared < bestDistanceSquared ||
                (distanceSquared == bestDistanceSquared && slot.Id.Value < bestId.Value))
            {
                found = true;
                bestDistanceSquared = distanceSquared;
                bestId = slot.Id;
            }
        }

        private static void ValidateRadius(float radius, string parameterName)
        {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, radius, "半径必须是有限非负数。");
            }
        }
    }
}
