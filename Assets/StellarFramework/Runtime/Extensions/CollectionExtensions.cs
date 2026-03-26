using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework
{
    /// <summary>
    /// 高性能深拷贝接口
    /// 我要求业务对象自行提供深拷贝实现，避免运行时反射或序列化式复制带来的额外开销与不可控分配。
    /// </summary>
    public interface IDeepCopyable<T>
    {
        /// <summary>
        /// 返回当前对象的深拷贝副本
        /// </summary>
        T DeepCopy();
    }

    public static class CollectionExtensions
    {
        #region List Extensions

        /// <summary>
        /// 移除列表中的空引用
        /// 我使用 RemoveAll 保持 O(N) 清理效率，避免倒序 RemoveAt 带来的多次搬移成本。
        /// </summary>
        public static int RemoveMissing<T>(this List<T> list) where T : Object
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] RemoveMissing 失败: list 为空");
                return 0;
            }

            return list.RemoveAll(item => item == null);
        }

        /// <summary>
        /// 输出列表摘要信息
        /// 我在运行时只输出稳定、低风险的摘要，不再执行深层反射遍历。
        /// 这样设计是为了严格遵守运行时主链路禁反射规范，并避免调试工具污染性能边界。
        /// </summary>
        public static string LogList<T>(this List<T> list)
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] LogList 失败: list 为空");
                return "null";
            }

            var sb = new StringBuilder(128);
            sb.Append("List<");
            sb.Append(typeof(T).Name);
            sb.Append(">(Count=");
            sb.Append(list.Count);
            sb.Append(") [");

            int previewCount = Mathf.Min(list.Count, 10);
            for (int i = 0; i < previewCount; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                T item = list[i];
                if (item == null)
                {
                    sb.Append("null");
                }
                else
                {
                    sb.Append(item);
                }
            }

            if (list.Count > previewCount)
            {
                sb.Append(", ...");
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// 列表深拷贝
        /// 我要求元素实现 IDeepCopyable，避免运行时反射与序列化复制。
        /// </summary>
        public static List<T> DeepCopy<T>(this List<T> source) where T : IDeepCopyable<T>
        {
            if (source == null)
            {
                LogKit.LogError("[CollectionExtensions] DeepCopy(List) 失败: source 为空");
                return null;
            }

            var newList = new List<T>(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item == null)
                {
                    newList.Add(default);
                    continue;
                }

                newList.Add(item.DeepCopy());
            }

            return newList;
        }

        public static bool RemoveAndDestroy<T>(this List<T> list, T item) where T : Component
        {
            if (list == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveAndDestroy(List<Component>) 失败: list 为空, Item={(item == null ? "null" : item.name)}");
                return false;
            }

            if (item == null)
            {
                LogKit.LogError("[CollectionExtensions] RemoveAndDestroy(List<Component>) 失败: item 为空");
                return false;
            }

            if (!list.Contains(item))
            {
                return false;
            }

            item.gameObject.SafeDestroy();
            list.Remove(item);
            return true;
        }

        public static bool RemoveAtAndDestroy<T>(this List<T> list, int index) where T : Component
        {
            if (list == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveAtAndDestroy(List<Component>) 失败: list 为空, Index={index}");
                return false;
            }

            if (index < 0 || index >= list.Count)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveAtAndDestroy(List<Component>) 失败: 索引越界, Index={index}, Count={list.Count}");
                return false;
            }

            T item = list[index];
            if (item != null)
            {
                item.gameObject.SafeDestroy();
            }

            list.RemoveAt(index);
            return true;
        }

        public static void ClearAndDestroy<T>(this List<T> list) where T : Component
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] ClearAndDestroy(List<Component>) 失败: list 为空");
                return;
            }

            int count = list.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                T item = list[i];
                if (item != null)
                {
                    item.gameObject.SafeDestroy();
                }
            }

            list.Clear();
        }

        public static void ClearAndDestroy(this List<GameObject> list)
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] ClearAndDestroy(List<GameObject>) 失败: list 为空");
                return;
            }

            int count = list.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                GameObject item = list[i];
                if (item != null)
                {
                    item.SafeDestroy();
                }
            }

            list.Clear();
        }

        public static void RemoveRangeAndDestroy<T>(this List<T> list, int index, int count) where T : Component
        {
            if (list == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<Component>) 失败: list 为空, Index={index}, Count={count}");
                return;
            }

            if (count <= 0)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<Component>) 失败: count 非法, Index={index}, Count={count}, ListCount={list.Count}");
                return;
            }

            if (index < 0 || index >= list.Count)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<Component>) 失败: index 越界, Index={index}, Count={count}, ListCount={list.Count}");
                return;
            }

            int safeCount = Mathf.Min(count, list.Count - index);
            for (int i = index; i < index + safeCount; i++)
            {
                T item = list[i];
                if (item != null)
                {
                    item.gameObject.SafeDestroy();
                }
            }

            list.RemoveRange(index, safeCount);
        }

        public static void RemoveRangeAndDestroy(this List<GameObject> list, int index, int count)
        {
            if (list == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<GameObject>) 失败: list 为空, Index={index}, Count={count}");
                return;
            }

            if (count <= 0)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<GameObject>) 失败: count 非法, Index={index}, Count={count}, ListCount={list.Count}");
                return;
            }

            if (index < 0 || index >= list.Count)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] RemoveRangeAndDestroy(List<GameObject>) 失败: index 越界, Index={index}, Count={count}, ListCount={list.Count}");
                return;
            }

            int safeCount = Mathf.Min(count, list.Count - index);
            for (int i = index; i < index + safeCount; i++)
            {
                GameObject item = list[i];
                if (item != null)
                {
                    item.SafeDestroy();
                }
            }

            list.RemoveRange(index, safeCount);
        }

        public static int RemoveAllAndDestroy<T>(this List<T> list, Predicate<T> match) where T : Component
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] RemoveAllAndDestroy 失败: list 为空");
                return 0;
            }

            if (match == null)
            {
                LogKit.LogError($"[CollectionExtensions] RemoveAllAndDestroy 失败: match 为空, ListCount={list.Count}");
                return 0;
            }

            int removedCount = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                T item = list[i];
                if (!match(item))
                {
                    continue;
                }

                if (item != null)
                {
                    item.gameObject.SafeDestroy();
                }

                list.RemoveAt(i);
                removedCount++;
            }

            return removedCount;
        }

        #endregion

        #region Dictionary Extensions

        public static bool AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddOrReplace 失败: dict 为空, Key={key}, ValueType={typeof(TValue).Name}");
                return false;
            }

            bool isNew = !dict.ContainsKey(key);
            dict[key] = value;
            return isNew;
        }

        public static bool AddOrSkip<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddOrSkip 失败: dict 为空, Key={key}, ValueType={typeof(TValue).Name}");
                return false;
            }

            if (dict.ContainsKey(key))
            {
                return false;
            }

            dict.Add(key, value);
            return true;
        }

        public static bool AddSafe<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value,
            bool overwrite = true)
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddSafe 失败: dict 为空, Key={key}, Overwrite={overwrite}, ValueType={typeof(TValue).Name}");
                return false;
            }

            if (overwrite)
            {
                dict[key] = value;
                return true;
            }

            if (dict.ContainsKey(key))
            {
                return false;
            }

            dict.Add(key, value);
            return true;
        }

        public static int AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dict,
            IEnumerable<KeyValuePair<TKey, TValue>> items, bool overwrite = true)
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddRange(IEnumerable) 失败: dict 为空, ValueType={typeof(TValue).Name}");
                return 0;
            }

            if (items == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddRange(IEnumerable) 失败: items 为空, DictCount={dict.Count}, ValueType={typeof(TValue).Name}");
                return 0;
            }

            int count = 0;
            foreach (KeyValuePair<TKey, TValue> item in items)
            {
                if (dict.AddSafe(item.Key, item.Value, overwrite))
                {
                    count++;
                }
            }

            return count;
        }

        public static int AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dict, Dictionary<TKey, TValue> other,
            bool overwrite = true)
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddRange(Dictionary) 失败: dict 为空, ValueType={typeof(TValue).Name}");
                return 0;
            }

            if (other == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] AddRange(Dictionary) 失败: other 为空, DictCount={dict.Count}, ValueType={typeof(TValue).Name}");
                return 0;
            }

            int count = 0;
            foreach (KeyValuePair<TKey, TValue> kvp in other)
            {
                if (dict.AddSafe(kvp.Key, kvp.Value, overwrite))
                {
                    count++;
                }
            }

            return count;
        }

        public static string LogDict<TKey, TValue>(this Dictionary<TKey, TValue> dict)
        {
            if (dict == null)
            {
                LogKit.LogError("[CollectionExtensions] LogDict 失败: dict 为空");
                return "null";
            }

            var sb = new StringBuilder(128);
            sb.Append("Dictionary<");
            sb.Append(typeof(TKey).Name);
            sb.Append(", ");
            sb.Append(typeof(TValue).Name);
            sb.Append(">(Count=");
            sb.Append(dict.Count);
            sb.Append(") {");

            int previewCount = 0;
            foreach (KeyValuePair<TKey, TValue> kvp in dict)
            {
                if (previewCount > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(kvp.Key);
                sb.Append(":");
                sb.Append(kvp.Value);

                previewCount++;
                if (previewCount >= 10)
                {
                    break;
                }
            }

            if (dict.Count > previewCount)
            {
                sb.Append(", ...");
            }

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// 字典深拷贝
        /// 我默认认为 Key 通常是值语义或稳定标识，因此只深拷贝 Value。
        /// </summary>
        public static Dictionary<TKey, TValue> DeepCopy<TKey, TValue>(this Dictionary<TKey, TValue> source)
            where TValue : IDeepCopyable<TValue>
        {
            if (source == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] DeepCopy(Dictionary) 失败: source 为空, KeyType={typeof(TKey).Name}, ValueType={typeof(TValue).Name}");
                return null;
            }

            var newDict = new Dictionary<TKey, TValue>(source.Count);
            foreach (KeyValuePair<TKey, TValue> kvp in source)
            {
                TValue newValue = kvp.Value == null ? default : kvp.Value.DeepCopy();
                newDict.Add(kvp.Key, newValue);
            }

            return newDict;
        }

        public static bool RemoveAndDestroy<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
            where TValue : Component
        {
            if (dict == null)
            {
                LogKit.LogError($"[CollectionExtensions] RemoveAndDestroy(Dictionary) 失败: dict 为空, Key={key}");
                return false;
            }

            if (!dict.TryGetValue(key, out TValue value))
            {
                return false;
            }

            if (value != null)
            {
                value.gameObject.SafeDestroy();
            }

            dict.Remove(key);
            return true;
        }

        public static void ClearAndDestroy<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TValue : Component
        {
            if (dict == null)
            {
                LogKit.LogError(
                    $"[CollectionExtensions] ClearAndDestroy(Dictionary) 失败: dict 为空, KeyType={typeof(TKey).Name}, ValueType={typeof(TValue).Name}");
                return;
            }

            foreach (TValue value in dict.Values)
            {
                if (value != null)
                {
                    value.gameObject.SafeDestroy();
                }
            }

            dict.Clear();
        }

        #endregion

        #region Random & Shuffle

        /// <summary>
        /// 随机获取列表中的一个元素
        /// 我在非法输入时直接返回默认值，不抛异常，避免把工具方法变成崩溃源。
        /// </summary>
        public static T GetRandomItem<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0)
            {
                return default;
            }

            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>
        /// Fisher-Yates 洗牌
        /// 我使用原地交换，避免额外容器分配。
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list == null)
            {
                LogKit.LogError("[CollectionExtensions] Shuffle 失败: list 为空");
                return;
            }

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// 列表是否为空或 null
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IList<T> list)
        {
            return list == null || list.Count == 0;
        }

        #endregion
    }
}