using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Blueprinter
{
    public static class JsonUtilities
    {
        public static T Deserialize<T>(string json)
        {
            var data = MiniJSON.Json.Deserialize(json);
            return (T)ConvertToType(data, typeof(T));
        }

        private static object ConvertToType(object value, Type targetType)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            if (targetType.IsEnum)
            {
                if (value is string s && Enum.IsDefined(targetType, s))
                    return Enum.Parse(targetType, s, true);

                return Enum.ToObject(targetType, System.Convert.ChangeType(value, Enum.GetUnderlyingType(targetType), CultureInfo.InvariantCulture));
            }

            if (targetType == typeof(string))
                return System.Convert.ToString(value, CultureInfo.InvariantCulture);

            if (IsNumericType(targetType) || targetType == typeof(bool))
                return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);

            if (targetType.IsArray && value is IList listValue)
                return ConvertToArray(listValue, targetType.GetElementType());

            if (targetType.IsGenericType && typeof(IList).IsAssignableFrom(targetType) && value is IList sourceList)
                return ConvertToList(sourceList, targetType);

            if (targetType.IsGenericType && typeof(IDictionary).IsAssignableFrom(targetType) && value is IDictionary sourceDict)
                return ConvertToDictionary(sourceDict, targetType);

            if (value is IDictionary<string, object> dict)
                return ConvertToObject(dict, targetType);

            return System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static object ConvertToObject(IDictionary<string, object> dict, Type targetType)
        {
            var instance = Activator.CreateInstance(targetType);
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (var field in fields)
            {
                if (!dict.TryGetValue(field.Name, out var rawValue))
                    continue;

                var converted = ConvertToType(rawValue, field.FieldType);
                field.SetValue(instance, converted);
            }

            return instance;
        }

        private static Array ConvertToArray(IList listValue, Type elementType)
        {
            var array = Array.CreateInstance(elementType, listValue.Count);
            for (int i = 0; i < listValue.Count; i++)
            {
                array.SetValue(ConvertToType(listValue[i], elementType), i);
            }

            return array;
        }

        private static object ConvertToList(IList sourceList, Type targetType)
        {
            var elementType = targetType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(targetType);

            foreach (var item in sourceList)
            {
                list.Add(ConvertToType(item, elementType));
            }

            return list;
        }

        private static object ConvertToDictionary(IDictionary sourceDict, Type targetType)
        {
            var args = targetType.GetGenericArguments();
            var keyType = args.Length > 0 ? args[0] : typeof(object);
            var valueType = args.Length > 1 ? args[1] : typeof(object);

            var dict = (IDictionary)Activator.CreateInstance(targetType);

            foreach (DictionaryEntry entry in sourceDict)
            {
                var key = ConvertToType(entry.Key, keyType);
                var value = ConvertToType(entry.Value, valueType);
                dict[key] = value;
            }

            return dict;
        }

        private static bool IsNumericType(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }
    }
}
