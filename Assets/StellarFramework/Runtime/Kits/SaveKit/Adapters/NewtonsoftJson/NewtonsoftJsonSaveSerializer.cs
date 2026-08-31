using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

namespace StellarFramework.SaveKitAdapters.NewtonsoftJson
{
    /// <summary>可选 JSON Adapter。TypeNameHandling 固定关闭，外部存档不会触发动态类型实例化。</summary>
    public sealed class NewtonsoftJsonSaveSerializer : ISaveSerializer
    {
        private readonly JsonSerializerSettings _settings;

        public NewtonsoftJsonSaveSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MaxDepth = 64,
                Culture = System.Globalization.CultureInfo.InvariantCulture
            };
        }

        public string Id => "newtonsoft-json";

        public UniTask SerializeAsync(Type dataType, object value, Stream destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType == null || destination == null) throw new ArgumentNullException(dataType == null ? nameof(dataType) : nameof(destination));
            using (var writer = new StreamWriter(destination, new UTF8Encoding(false), 4096, true))
            using (var jsonWriter = new JsonTextWriter(writer) { CloseOutput = false })
            {
                JsonSerializer serializer = JsonSerializer.Create(_settings);
                serializer.Serialize(jsonWriter, value, dataType);
                jsonWriter.Flush();
                writer.Flush();
            }

            return UniTask.CompletedTask;
        }

        public UniTask<object> DeserializeAsync(Type dataType, Stream source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (dataType == null || source == null) throw new ArgumentNullException(dataType == null ? nameof(dataType) : nameof(source));
            using (var reader = new StreamReader(source, new UTF8Encoding(false), true, 4096, true))
            using (var jsonReader = new JsonTextReader(reader))
            {
                JsonSerializer serializer = JsonSerializer.Create(_settings);
                object value = serializer.Deserialize(jsonReader, dataType);
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.FromResult(value);
            }
        }
    }
}
