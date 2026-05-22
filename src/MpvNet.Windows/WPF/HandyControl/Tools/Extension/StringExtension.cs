

using System.ComponentModel;

namespace HandyControl.Tools.Extension
{
    public static class StringExtension
    {
        public static T? Value<T>(this string input)
        {
            try
            {
                var value = TypeDescriptor.GetConverter(typeof(T)).ConvertFromString(input);
                return value is T typedValue ? typedValue : default;
            }
            catch
            {
                return default;
            }
        }

        public static object? Value(this string input, Type type)
        {
            try
            {
                return TypeDescriptor.GetConverter(type).ConvertFromString(input);
            }
            catch
            {
                return null;
            }
        }
    }
}
