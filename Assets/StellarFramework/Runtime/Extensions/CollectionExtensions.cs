using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
            // 容错检查：空列表直接返回
            if (list == null)
            {
                Debug.LogError("LogListDetail: 传入的列表为 null");
                return "null";
            }

            // 性能警告：仅在Debug模式或非高频逻辑中使用
            // 反射操作非常耗时，不要在 Update 中调用

            var sb = new StringBuilder();
            var visited = new HashSet<object>(); // 防止循环引用导致死循环
            const int MAX_DEPTH = 5; // 最大递归深度，防止堆栈溢出

            sb.Append("[\n");

            // === 本地函数：递归获取单个对象的详细字符串 ===
            string GetValueString(object obj, int depth)
            {
                if (obj == null) return "null";

                // 深度限制保护
                if (depth > MAX_DEPTH) return "...(深度限制)...";

                var type = obj.GetType();

                // 1. 基础类型、字符串、枚举直接返回，不进行反射
                if (type.IsPrimitive || obj is string || type.IsEnum || type == typeof(decimal))
                    return obj.ToString();

                // 2. Unity原生类型 (Vector3, Transform等) 直接使用其自带的ToString
                // 否则会反射出大量无用的内部数据
                if (type.Namespace != null && (type.Namespace.StartsWith("UnityEngine") || type.Namespace.StartsWith("System")))
                    return obj.ToString();

                // 3. 循环引用检测 (比如 A 引用 B, B 又引用 A)
                if (visited.Contains(obj)) return "(循环引用)";
                visited.Add(obj);

                // 4. 处理嵌套集合 (List 里面套 List)
                if (obj is IEnumerable enumerable)
                {
                    var sbSub = new StringBuilder("[");
                    foreach (var item in enumerable)
                    {
                        sbSub.Append(GetValueString(item, depth + 1)).Append(", ");
                    }

                    if (sbSub.Length > 1) sbSub.Length -= 2; // 移除末尾逗号
                    sbSub.Append("]");
                    visited.Remove(obj); // 回溯
                    return sbSub.ToString();
                }

                // 5. 自定义类：反射获取字段
                var sbObj = new StringBuilder();
                sbObj.Append(type.Name).Append(" { ");

                // 获取所有实例字段 (Public 和 Private)
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                bool hasContent = false;

                foreach (var field in fields)
                {
                    // 过滤掉编译器生成的 backing field (比如属性自动生成的字段)
                    if (field.Name.Contains("<")) continue;

                    if (hasContent) sbObj.Append(", ");

                    // 递归获取字段值
                    var val = field.GetValue(obj);
                    sbObj.Append(field.Name).Append(": ").Append(GetValueString(val, depth + 1));
                    hasContent = true;
                }

                sbObj.Append(" }");
                visited.Remove(obj); // 回溯：退出当前对象后，允许其他分支再次访问该对象
                return sbObj.ToString();
            }
            // === 本地函数结束 ===

            // 主循环遍历列表
            int count = 0;
            foreach (var item in list)
            {
                sb.Append("\t"); // 缩进
                sb.Append(GetValueString(item, 1));
                sb.Append(",\n");
                count++;
            }

            // 移除最后一个逗号和换行
            if (count > 0)
            {
                sb.Length -= 2;
                sb.AppendLine();
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

        public static void ClearAndDestroy(this List<GameObject> list)
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

        public static void RemoveRangeAndDestroy<T>(this List<GameObject> list, int index, int count)
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