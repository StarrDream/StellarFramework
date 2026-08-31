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
            else if (dataType.IsPrimitive || dataType.IsEnum || dataType == typeof(decimal))
            {
                json = JsonUtility.ToJson(CreateValueBox(dataType, value));
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

                if (dataType.IsPrimitive || dataType.IsEnum || dataType == typeof(decimal))
                {
                    object box = CreateValueBox(dataType, null);
                    Type boxType = box.GetType();
                    object parsed = JsonUtility.FromJson(json, boxType);
                    return UniTask.FromResult(GetValueField(parsed, boxType));
                }

                return UniTask.FromResult(JsonUtility.FromJson(json, dataType));
            }
        }

        private static object CreateValueBox(Type dataType, object value)
        {
            Type boxType = typeof(ValueBox<>).MakeGenericType(dataType);
            object box = Activator.CreateInstance(boxType);
            boxType.GetField("Value").SetValue(box, value);
            return box;
        }

        private static object GetValueField(object box, Type boxType)
        {
            return box == null ? null : boxType.GetField("Value").GetValue(box);
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
