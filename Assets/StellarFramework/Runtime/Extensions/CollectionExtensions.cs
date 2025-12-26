using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StellarFramework
{
    /// <summary>
    /// 高性能深拷贝接口
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
        /// [高性能] 移除列表中的空引用 (O(N) 复杂度)
        /// </summary>
        public static int RemoveMissing<T>(this List<T> list) where T : Object
        {
            if (list == null) return 0;
            // 使用 RemoveAll 比倒序 RemoveAt 快得多，因为 RemoveAt 会导致多次数组内存移动
            return list.RemoveAll(item => item == null);
        }

        public static string LogList<T>(this List<T> list)
        {
            if (list == null || list.Count == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append("[");
            for (var i = 0; i < list.Count; i++)
            {
                sb.Append(list[i]);
                if (i < list.Count - 1) sb.Append(", ");
            }

            sb.Append("]");
            return sb.ToString();
        }

        /// <summary>
        /// [高性能] 列表深拷贝
        /// 要求元素类型实现 IDeepCopyable 接口
        /// </summary>
        public static List<T> DeepCopy<T>(this List<T> source) where T : IDeepCopyable<T>
        {
            if (source == null) return null;

            // 预分配内存，避免扩容GC
            var newList = new List<T>(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                // 如果元素为空，直接添加默认值
                if (source[i] == null)
                {
                    newList.Add(default);
                }
                else
                {
                    // 调用接口方法，0反射，0装箱
                    newList.Add(source[i].DeepCopy());
                }
            }

            return newList;
        }


        public static bool RemoveAndDestroy<T>(this List<T> list, T item) where T : Component
        {
            if (list.Contains(item))
            {
                if (item != null) item.gameObject.SafeDestroy();
                list.Remove(item);
                return true;
            }

            return false;
        }

        public static bool RemoveAtAndDestroy<T>(this List<T> list, int index) where T : Component
        {
            if (index >= 0 && index < list.Count)
            {
                var item = list[index];
                if (item != null) item.gameObject.SafeDestroy();
                list.RemoveAt(index);
                return true;
            }

            return false;
        }

        public static void ClearAndDestroy<T>(this List<T> list) where T : Component
        {
            var items = list.ToArray();
            list.Clear();
            foreach (var item in items)
            {
                if (item != null) Object.Destroy(item.gameObject);
            }
        }

        public static void RemoveRangeAndDestroy<T>(this List<T> list, int index, int count) where T : Component
        {
            if (index < 0 || index >= list.Count) return;
            var endIndex = Mathf.Min(index + count, list.Count);
            for (var i = index; i < endIndex; i++)
            {
                var item = list[i];
                if (item != null) item.gameObject.SafeDestroy();
            }

            list.RemoveRange(index, count);
        }

        public static int RemoveAllAndDestroy<T>(this List<T> list, Predicate<T> match) where T : Component
        {
            var itemsToRemove = list.FindAll(match);
            foreach (var item in itemsToRemove)
                if (item != null)
                    item.gameObject.SafeDestroy();
            return list.RemoveAll(match);
        }

        #endregion

        #region Dictionary Extensions

        public static bool AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            var isNew = !dict.ContainsKey(key);
            dict[key] = value;
            return isNew;
        }

        public static bool AddOrSkip<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (dict.ContainsKey(key)) return false;
            dict.Add(key, value);
            return true;
        }

        public static bool AddSafe<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value, bool overwrite = true)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (overwrite)
            {
                dict[key] = value;
                return true;
            }

            if (dict.ContainsKey(key)) return false;
            dict.Add(key, value);
            return true;
        }

        public static int AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dict, IEnumerable<KeyValuePair<TKey, TValue>> items, bool overwrite = true)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (items == null) throw new ArgumentNullException(nameof(items));
            var count = 0;
            foreach (var item in items)
                if (dict.AddSafe(item.Key, item.Value, overwrite))
                    count++;
            return count;
        }

        public static int AddRange<TKey, TValue>(this Dictionary<TKey, TValue> dict, Dictionary<TKey, TValue> other, bool overwrite = true)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            if (other == null) throw new ArgumentNullException(nameof(other));
            var count = 0;
            foreach (var kvp in other)
                if (dict.AddSafe(kvp.Key, kvp.Value, overwrite))
                    count++;
            return count;
        }

        public static string LogDict<TKey, TValue>(this Dictionary<TKey, TValue> dict)
        {
            if (dict == null) return "null";
            var sb = new StringBuilder();
            sb.Append("{");
            foreach (var kvp in dict)
            {
                sb.Append(kvp.Key).Append(":").Append(kvp.Value).Append(",");
            }

            if (sb.Length > 1) sb.Remove(sb.Length - 1, 1);
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// [高性能] 字典深拷贝
        /// 要求 Value 实现 IDeepCopyable 接口 (Key通常是基础类型，直接复制即可)
        /// </summary>
        public static Dictionary<TKey, TValue> DeepCopy<TKey, TValue>(this Dictionary<TKey, TValue> source)
            where TValue : IDeepCopyable<TValue>
        {
            if (source == null) return null;

            var newDict = new Dictionary<TKey, TValue>(source.Count);

            foreach (var kvp in source)
            {
                TValue newValue = kvp.Value == null ? default : kvp.Value.DeepCopy();
                newDict.Add(kvp.Key, newValue);
            }

            return newDict;
        }


        public static bool RemoveAndDestroy<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TValue : Component
        {
            if (dict.TryGetValue(key, out var value))
            {
                if (value != null) value.gameObject.SafeDestroy();
                dict.Remove(key);
                return true;
            }

            return false;
        }

        public static void ClearAndDestroy<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TValue : Component
        {
            foreach (var value in dict.Values)
                if (value != null)
                    value.gameObject.SafeDestroy();
            dict.Clear();
        }

        #endregion

        #region Random & Shuffle

        /// <summary>
        /// 随机获取列表中的一个元素
        /// </summary>
        public static T GetRandomItem<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0) return default;
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        /// <summary>
        /// 洗牌算法 (Fisher-Yates Shuffle)
        /// </summary>
        public static void Shuffle<T>(this IList<T> list)
        {
            if (list == null) return;
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = UnityEngine.Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// 列表是否为空或Null
        /// </summary>
        public static bool IsNullOrEmpty<T>(this IList<T> list)
        {
            return list == null || list.Count == 0;
        }

        #endregion
    }
}