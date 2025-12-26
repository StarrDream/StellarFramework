using System.Collections.Generic;

namespace StellarFramework.Bindable
{
    public static class BindableExtensions
    {
        public static BindableProperty<T> ToBindable<T>(this T value)
        {
            return new BindableProperty<T>(value);
        }

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