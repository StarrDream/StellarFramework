using System.Collections.Generic;

namespace StellarFramework.Bindable
{
    /// <summary>
    /// BindableKit 的便捷构造扩展。
    /// </summary>
    public static class BindableExtensions
    {
        /// <summary>
        /// 将普通值包装为新的 <see cref="BindableProperty{T}"/>。
        /// </summary>
        public static BindableProperty<T> ToBindable<T>(this T value)
        {
            return new BindableProperty<T>(value);
        }

        /// <summary>
        /// 将序列复制到新的 <see cref="BindableList{T}"/>。
        /// </summary>
        /// <remarks>
        /// 返回值与源集合彼此独立；后续修改源集合不会自动同步。
        /// </remarks>
        public static BindableList<T> ToBindableList<T>(this IEnumerable<T> collection)
        {
            var list = new BindableList<T>();
            if (collection != null)
            {
                foreach (var item in collection)
                {
                    list.Add(item);
                }
            }

            return list;
        }
    }
}