using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace StellarFramework
{
    public interface ISaveSerializer
    {
        string Id { get; }
        UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken cancellationToken);
        UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken cancellationToken);
    }

    /// <summary>Optional capability contract for serializers supplied by a Kit or a game.</summary>
    public interface ISaveSerializerCapabilities
    {
        SaveSerializerCapabilities Capabilities { get; }
    }

    public static class SaveSerializerCapabilityExtensions
    {
        public static SaveSerializerCapabilities GetCapabilities(this ISaveSerializer serializer)
        {
            return serializer is ISaveSerializerCapabilities capable
                ? capable.Capabilities
                : SaveSerializerCapabilities.None;
        }

        public static bool SupportsBackgroundExecution(this ISaveSerializer serializer)
        {
            SaveSerializerCapabilities capabilities = serializer.GetCapabilities();
            return (capabilities & (SaveSerializerCapabilities.BackgroundExecution | SaveSerializerCapabilities.ThreadSafe)) ==
                (SaveSerializerCapabilities.BackgroundExecution | SaveSerializerCapabilities.ThreadSafe);
        }
    }

    /// <summary>
    /// Core 自带的轻量 Serializer。它只使用 Unity JsonUtility，不引入 Newtonsoft，适合 DTO 和小型配置。
    /// 大型模拟数据可在业务或 Adapter 中提供流式二进制 Serializer。
    /// </summary>
    public sealed class UnityJsonSaveSerializer : ISaveSerializer, ISaveSerializerCapabilities
    {
        private sealed class StringBox
        {
            public string Value;
        }

        private sealed class ValueBox<T>
        {
            public T Value;
        }

        public string Id => "unity-json";
        public SaveSerializerCapabilities Capabilities => SaveSerializerCapabilities.None;

        public UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType == null || destination == null)
            {
                throw new ArgumentNullException(dataType == null ? nameof(dataType) : nameof(destination));
            }

            string json;
            if (dataType == typeof(string))
            {
                json = JsonUtility.ToJson(new StringBox { Value = (string)value });
            }
            else if (IsBoxedScalar(dataType))
            {
                json = SerializeScalar(dataType, value);
            }
            else
            {
                json = value == null ? "null" : JsonUtility.ToJson(value);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json ?? "null");
            destination.Write(bytes, 0, bytes.Length);
            return UniTask.CompletedTask;
        }

        public UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType == null || source == null)
            {
                throw new ArgumentNullException(dataType == null ? nameof(dataType) : nameof(source));
            }

            using (var reader = new StreamReader(source, Encoding.UTF8, true, 4096, true))
            {
                string json = reader.ReadToEnd();
                cancellationToken.ThrowIfCancellationRequested();
                if (dataType == typeof(string))
                {
                    StringBox box = JsonUtility.FromJson<StringBox>(json);
                    return UniTask.FromResult<object>(box == null ? null : box.Value);
                }

                if (IsBoxedScalar(dataType))
                {
                    return UniTask.FromResult(DeserializeScalar(dataType, json));
                }

                return UniTask.FromResult(JsonUtility.FromJson(json, dataType));
            }
        }

        private static bool IsBoxedScalar(Type dataType)
        {
            return dataType.IsPrimitive || dataType.IsEnum || dataType == typeof(decimal);
        }

        private static string SerializeScalar(Type dataType, object value)
        {
            if (dataType == typeof(bool)) return JsonUtility.ToJson(new ValueBox<bool> { Value = value != null && (bool)value });
            if (dataType == typeof(byte)) return JsonUtility.ToJson(new ValueBox<byte> { Value = value == null ? default : (byte)value });
            if (dataType == typeof(sbyte)) return JsonUtility.ToJson(new ValueBox<sbyte> { Value = value == null ? default : (sbyte)value });
            if (dataType == typeof(short)) return JsonUtility.ToJson(new ValueBox<short> { Value = value == null ? default : (short)value });
            if (dataType == typeof(ushort)) return JsonUtility.ToJson(new ValueBox<ushort> { Value = value == null ? default : (ushort)value });
            if (dataType == typeof(int)) return JsonUtility.ToJson(new ValueBox<int> { Value = value == null ? default : (int)value });
            if (dataType == typeof(uint)) return JsonUtility.ToJson(new ValueBox<uint> { Value = value == null ? default : (uint)value });
            if (dataType == typeof(long)) return JsonUtility.ToJson(new ValueBox<long> { Value = value == null ? default : (long)value });
            if (dataType == typeof(ulong)) return JsonUtility.ToJson(new ValueBox<ulong> { Value = value == null ? default : (ulong)value });
            if (dataType == typeof(float)) return JsonUtility.ToJson(new ValueBox<float> { Value = value == null ? default : (float)value });
            if (dataType == typeof(double)) return JsonUtility.ToJson(new ValueBox<double> { Value = value == null ? default : (double)value });
            if (dataType == typeof(char)) return JsonUtility.ToJson(new ValueBox<char> { Value = value == null ? default : (char)value });
            if (dataType == typeof(decimal)) return JsonUtility.ToJson(new ValueBox<decimal> { Value = value == null ? default : (decimal)value });
            if (dataType.IsEnum)
            {
                Type underlyingType = Enum.GetUnderlyingType(dataType);
                object underlyingValue = value == null ? GetEnumUnderlyingDefault(underlyingType) : Convert.ChangeType(value, underlyingType);
                return SerializeScalar(underlyingType, underlyingValue);
            }

            throw new NotSupportedException($"UnityJsonSaveSerializer 不支持标量类型: {dataType.FullName}");
        }

        private static object DeserializeScalar(Type dataType, string json)
        {
            if (dataType == typeof(bool)) return JsonUtility.FromJson<ValueBox<bool>>(json)?.Value ?? default(bool);
            if (dataType == typeof(byte)) return JsonUtility.FromJson<ValueBox<byte>>(json)?.Value ?? default(byte);
            if (dataType == typeof(sbyte)) return JsonUtility.FromJson<ValueBox<sbyte>>(json)?.Value ?? default(sbyte);
            if (dataType == typeof(short)) return JsonUtility.FromJson<ValueBox<short>>(json)?.Value ?? default(short);
            if (dataType == typeof(ushort)) return JsonUtility.FromJson<ValueBox<ushort>>(json)?.Value ?? default(ushort);
            if (dataType == typeof(int)) return JsonUtility.FromJson<ValueBox<int>>(json)?.Value ?? default(int);
            if (dataType == typeof(uint)) return JsonUtility.FromJson<ValueBox<uint>>(json)?.Value ?? default(uint);
            if (dataType == typeof(long)) return JsonUtility.FromJson<ValueBox<long>>(json)?.Value ?? default(long);
            if (dataType == typeof(ulong)) return JsonUtility.FromJson<ValueBox<ulong>>(json)?.Value ?? default(ulong);
            if (dataType == typeof(float)) return JsonUtility.FromJson<ValueBox<float>>(json)?.Value ?? default(float);
            if (dataType == typeof(double)) return JsonUtility.FromJson<ValueBox<double>>(json)?.Value ?? default(double);
            if (dataType == typeof(char)) return JsonUtility.FromJson<ValueBox<char>>(json)?.Value ?? default(char);
            if (dataType == typeof(decimal)) return JsonUtility.FromJson<ValueBox<decimal>>(json)?.Value ?? default(decimal);
            if (dataType.IsEnum)
            {
                Type underlyingType = Enum.GetUnderlyingType(dataType);
                object underlyingValue = DeserializeScalar(underlyingType, json);
                return Enum.ToObject(dataType, underlyingValue);
            }

            throw new NotSupportedException($"UnityJsonSaveSerializer 不支持标量类型: {dataType.FullName}");
        }

        private static object GetEnumUnderlyingDefault(Type dataType)
        {
            if (dataType == typeof(byte)) return default(byte);
            if (dataType == typeof(sbyte)) return default(sbyte);
            if (dataType == typeof(short)) return default(short);
            if (dataType == typeof(ushort)) return default(ushort);
            if (dataType == typeof(int)) return default(int);
            if (dataType == typeof(uint)) return default(uint);
            if (dataType == typeof(long)) return default(long);
            if (dataType == typeof(ulong)) return default(ulong);
            throw new NotSupportedException($"枚举底层类型不受支持: {dataType.FullName}");
        }
    }

    public sealed class RawBytesSaveSerializer : ISaveSerializer, ISaveSerializerCapabilities
    {
        public string Id => "raw-bytes";
        public SaveSerializerCapabilities Capabilities =>
            SaveSerializerCapabilities.BackgroundExecution | SaveSerializerCapabilities.ThreadSafe;

        public UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType != typeof(byte[]))
            {
                throw new InvalidOperationException("RawBytesSaveSerializer 只支持 byte[]。" );
            }

            byte[] bytes = value as byte[] ?? Array.Empty<byte>();
            destination.Write(bytes, 0, bytes.Length);
            return UniTask.CompletedTask;
        }

        public UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType != typeof(byte[]))
            {
                throw new InvalidOperationException("RawBytesSaveSerializer 只支持 byte[]。" );
            }

            using (var memory = new MemoryStream())
            {
                source.CopyTo(memory);
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.FromResult<object>(memory.ToArray());
            }
        }
    }
}
