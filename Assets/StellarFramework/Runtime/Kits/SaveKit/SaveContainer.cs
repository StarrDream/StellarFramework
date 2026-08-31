using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StellarFramework
{
    public static class SaveContainerFormat
    {
        public const int ContainerVersion = SaveKitOptions.CurrentContainerVersion;
        public const int EndianMarker = 0x01020304;
        public static readonly byte[] Magic = { (byte)'S', (byte)'T', (byte)'S', (byte)'V' };
    }

    public static class SaveContainerWriter
    {
        public static void Write(Stream destination, SaveMetadata metadata, IReadOnlyList<SaveSectionEntry> sections,
            SaveKitOptions options)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (!metadata.SlotId.IsValid || metadata.Revision < 1 || metadata.CreatedUtc == default(DateTime) ||
                metadata.UpdatedUtc == default(DateTime))
            {
                throw new InvalidDataException("Metadata 不完整或非法。" );
            }
            if (sections.Count > options.MaxSectionCount) throw new InvalidDataException("Section 数量超过上限。" );

            using (var writer = new BinaryWriter(destination, Encoding.UTF8, true))
            {
                writer.Write(SaveContainerFormat.Magic);
                writer.Write(SaveContainerFormat.ContainerVersion);
                writer.Write(SaveContainerFormat.EndianMarker);
                writer.Write(metadata.Revision);
                writer.Write(metadata.CreatedUtc.ToUniversalTime().Ticks);
                writer.Write(metadata.UpdatedUtc.ToUniversalTime().Ticks);
                WriteString(writer, metadata.SlotId.Value, options.MaxStringBytes);
                WriteString(writer, metadata.ApplicationVersion ?? string.Empty, options.MaxStringBytes);

                if (metadata.CustomMetadata.Count > options.MaxCustomMetadataCount)
                {
                    throw new InvalidDataException("CustomMetadata 数量超过上限。" );
                }

                writer.Write(metadata.CustomMetadata.Count);
                foreach (KeyValuePair<string, string> pair in metadata.CustomMetadata.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    WriteString(writer, pair.Key, options.MaxStringBytes);
                    WriteString(writer, pair.Value, options.MaxStringBytes);
                }

                writer.Write(sections.Count);
                long payloadTotal = 0L;
                foreach (SaveSectionEntry entry in sections)
                {
                    if (entry == null || entry.Descriptor == null || !entry.Descriptor.Id.IsValid)
                    {
                        throw new InvalidDataException("Section Descriptor 非法。" );
                    }

                    byte[] payload = entry.Payload ?? Array.Empty<byte>();
                    if (payload.LongLength > options.MaxPayloadBytes || payloadTotal > options.MaxPayloadBytes - payload.LongLength)
                    {
                        throw new InvalidDataException("Payload 总大小超过上限。" );
                    }

                    payloadTotal += payload.LongLength;
                    if (entry.Descriptor.PayloadLength != 0 && entry.Descriptor.PayloadLength != payload.LongLength)
                    {
                        throw new InvalidDataException($"Section {entry.Descriptor.Id} PayloadLength 与实际长度不一致。" );
                    }
                    ulong actualChecksum = new XxHash64Checksum().Compute(payload, 0, payload.Length);
                    if (entry.Descriptor.Checksum != actualChecksum)
                    {
                        throw new InvalidDataException($"Section {entry.Descriptor.Id} Checksum 与实际 Payload 不一致。" );
                    }
                    WriteString(writer, entry.Descriptor.Id.Value, options.MaxStringBytes);
                    writer.Write(entry.Descriptor.SchemaVersion);
                    WriteString(writer, entry.Descriptor.SerializerId ?? string.Empty, options.MaxStringBytes);
                    writer.Write(entry.Descriptor.PayloadLength == 0 ? payload.LongLength : entry.Descriptor.PayloadLength);
                    writer.Write(entry.Descriptor.Checksum);
                    writer.Write(entry.Descriptor.Flags);
                }

                foreach (SaveSectionEntry entry in sections)
                {
                    byte[] payload = entry.Payload ?? Array.Empty<byte>();
                    writer.Write(payload);
                }

                writer.Flush();
            }
        }

        internal static void WriteString(BinaryWriter writer, string value, int maxBytes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maxBytes) throw new InvalidDataException("字符串长度超过上限。" );
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }

    public static class SaveContainerReader
    {
        public static bool TryRead(Stream source, SaveKitOptions options, out SaveSnapshot snapshot,
            out SaveErrorCode errorCode, out string errorMessage)
        {
            return TryReadInternal(source, options, true, out snapshot, out errorCode, out errorMessage);
        }

        private static bool TryReadInternal(Stream source, SaveKitOptions options, bool readPayloads,
            out SaveSnapshot snapshot, out SaveErrorCode errorCode, out string errorMessage)
        {
            snapshot = null;
            errorCode = SaveErrorCode.None;
            errorMessage = null;
            if (source == null || options == null || !source.CanRead || !source.CanSeek)
            {
                errorCode = SaveErrorCode.ContainerCorrupted;
                errorMessage = "存档流不可读取或不可定位。";
                return false;
            }

            try
            {
                if (source.Length > options.MaxPayloadBytes + 1024L * 1024L)
                {
                    return Fail(SaveErrorCode.ContainerCorrupted, "存档文件超过大小上限。", out errorCode, out errorMessage);
                }

                using (var reader = new BinaryReader(source, Encoding.UTF8, true))
                {
                    byte[] magic = reader.ReadBytes(SaveContainerFormat.Magic.Length);
                    if (magic.Length != SaveContainerFormat.Magic.Length || !magic.SequenceEqual(SaveContainerFormat.Magic))
                    {
                        return Fail(SaveErrorCode.ContainerCorrupted, "存档 Magic 不匹配。", out errorCode, out errorMessage);
                    }

                    int version = reader.ReadInt32();
                    if (version != SaveContainerFormat.ContainerVersion)
                    {
                        return Fail(SaveErrorCode.UnsupportedContainerVersion, $"不支持 ContainerVersion {version}。", out errorCode, out errorMessage);
                    }

                    if (reader.ReadInt32() != SaveContainerFormat.EndianMarker)
                    {
                        return Fail(SaveErrorCode.ContainerCorrupted, "存档字节序标记不匹配。", out errorCode, out errorMessage);
                    }

                    long revision = reader.ReadInt64();
                    long createdTicks = reader.ReadInt64();
                    long updatedTicks = reader.ReadInt64();
                    if (revision < 1 || createdTicks < 0 || createdTicks > DateTime.MaxValue.Ticks ||
                        updatedTicks < 0 || updatedTicks > DateTime.MaxValue.Ticks)
                    {
                        return Fail(SaveErrorCode.InvalidManifest, "Metadata Revision 或时间戳非法。", out errorCode, out errorMessage);
                    }

                    var metadata = new SaveMetadata
                    {
                        Revision = revision,
                        CreatedUtc = new DateTime(createdTicks, DateTimeKind.Utc),
                        UpdatedUtc = new DateTime(updatedTicks, DateTimeKind.Utc),
                        ContainerVersion = version
                    };

                    string slot = ReadString(reader, options.MaxStringBytes);
                    if (!SaveSlotId.TryCreate(slot, out SaveSlotId slotId, out string slotError))
                    {
                        return Fail(SaveErrorCode.InvalidManifest, slotError, out errorCode, out errorMessage);
                    }

                    metadata.SlotId = slotId;
                    metadata.ApplicationVersion = ReadString(reader, options.MaxStringBytes);
                    int customCount = ReadCount(reader, options.MaxCustomMetadataCount, "CustomMetadata");
                    if (customCount < 0)
                    {
                        return Fail(SaveErrorCode.InvalidManifest, "CustomMetadata 数量非法。", out errorCode, out errorMessage);
                    }

                    for (int i = 0; i < customCount; i++)
                    {
                        string key = ReadString(reader, options.MaxStringBytes);
                        string value = ReadString(reader, options.MaxStringBytes);
                        if (string.IsNullOrEmpty(key) || metadata.CustomMetadata.ContainsKey(key))
                        {
                            return Fail(SaveErrorCode.InvalidManifest, "CustomMetadata 键为空或重复。", out errorCode, out errorMessage);
                        }

                        metadata.CustomMetadata.Add(key, value);
                    }

                    int sectionCount = ReadCount(reader, options.MaxSectionCount, "Section");
                    if (sectionCount < 0)
                    {
                        return Fail(SaveErrorCode.InvalidManifest, "Section 数量非法。", out errorCode, out errorMessage);
                    }

                    var entries = new List<SaveSectionEntry>(sectionCount);
                    var descriptors = new List<SaveSectionDescriptor>(sectionCount);
                    var seenIds = new HashSet<string>(StringComparer.Ordinal);
                    long payloadTotal = 0L;
                    for (int i = 0; i < sectionCount; i++)
                    {
                        string id = ReadString(reader, options.MaxStringBytes);
                        if (!SaveSectionId.TryCreate(id, out SaveSectionId sectionId, out string sectionError) || !seenIds.Add(id))
                        {
                            return Fail(SaveErrorCode.InvalidManifest, string.IsNullOrEmpty(sectionError) ? "Section ID 重复。" : sectionError,
                                out errorCode, out errorMessage);
                        }

                        int schemaVersion = reader.ReadInt32();
                        if (schemaVersion < 1)
                        {
                            return Fail(SaveErrorCode.InvalidManifest, $"Section {sectionId} SchemaVersion 非法。", out errorCode, out errorMessage);
                        }

                        string serializerId = ReadString(reader, options.MaxStringBytes);
                        long payloadLength = reader.ReadInt64();
                        ulong checksum = reader.ReadUInt64();
                        byte flags = reader.ReadByte();
                        if (string.IsNullOrEmpty(serializerId) || payloadLength < 0 || payloadLength > options.MaxPayloadBytes ||
                            payloadTotal > options.MaxPayloadBytes - payloadLength)
                        {
                            return Fail(SaveErrorCode.InvalidManifest, $"Section {sectionId} Payload 长度非法。", out errorCode, out errorMessage);
                        }

                        payloadTotal += payloadLength;
                        descriptors.Add(new SaveSectionDescriptor
                        {
                            Id = sectionId,
                            SchemaVersion = schemaVersion,
                            SerializerId = serializerId,
                            PayloadLength = payloadLength,
                            Checksum = checksum,
                            Flags = flags
                        });
                    }

                    foreach (SaveSectionDescriptor descriptor in descriptors)
                    {
                        long offset = source.Position;
                        descriptor.PayloadOffset = offset;
                        if (descriptor.PayloadLength > int.MaxValue && readPayloads)
                        {
                            return Fail(SaveErrorCode.ContainerCorrupted, "单个 Section Payload 超过可读取范围。", out errorCode, out errorMessage);
                        }

                        if (!readPayloads)
                        {
                            if (descriptor.PayloadLength > source.Length - source.Position)
                            {
                                return Fail(SaveErrorCode.ContainerCorrupted, "存档文件被截断。", out errorCode, out errorMessage);
                            }

                            source.Seek(descriptor.PayloadLength, SeekOrigin.Current);
                            entries.Add(new SaveSectionEntry { Descriptor = descriptor, Payload = Array.Empty<byte>() });
                            continue;
                        }

                        byte[] payload = reader.ReadBytes((int)descriptor.PayloadLength);
                        if (payload.LongLength != descriptor.PayloadLength)
                        {
                            return Fail(SaveErrorCode.ContainerCorrupted, "存档文件被截断。", out errorCode, out errorMessage);
                        }

                        ulong actualChecksum = new XxHash64Checksum().Compute(payload, 0, payload.Length);
                        if (actualChecksum != descriptor.Checksum)
                        {
                            return Fail(SaveErrorCode.ChecksumMismatch, $"Section {descriptor.Id} Checksum 不匹配。", out errorCode, out errorMessage);
                        }

                        entries.Add(new SaveSectionEntry { Descriptor = descriptor, Payload = payload });
                    }

                    if (source.Position != source.Length)
                    {
                        return Fail(SaveErrorCode.ContainerCorrupted, "存档末尾存在未声明的数据。", out errorCode, out errorMessage);
                    }

                    snapshot = new SaveSnapshot { Metadata = metadata, Sections = entries };
                    return true;
                }
            }
            catch (EndOfStreamException)
            {
                return Fail(SaveErrorCode.ContainerCorrupted, "存档文件被截断。", out errorCode, out errorMessage);
            }
            catch (InvalidDataException exception)
            {
                return Fail(SaveErrorCode.InvalidManifest, exception.Message, out errorCode, out errorMessage);
            }
            catch (Exception exception)
            {
                return Fail(SaveErrorCode.ContainerCorrupted, exception.Message, out errorCode, out errorMessage);
            }
        }

        public static bool TryReadMetadata(Stream source, SaveKitOptions options, out SaveMetadata metadata,
            out SaveErrorCode errorCode, out string errorMessage)
        {
            metadata = null;
            SaveSnapshot snapshot;
            if (!TryReadInternal(source, options, false, out snapshot, out errorCode, out errorMessage))
            {
                return false;
            }

            metadata = snapshot.Metadata;
            return true;
        }

        private static string ReadString(BinaryReader reader, int maxBytes)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > maxBytes)
            {
                throw new InvalidDataException("字符串长度非法。" );
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new EndOfStreamException();
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static int ReadCount(BinaryReader reader, int max, string label)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw new InvalidDataException($"{label} 数量超过上限。" );
            }

            return count;
        }

        private static bool Fail(SaveErrorCode code, string message, out SaveErrorCode errorCode, out string errorMessage)
        {
            errorCode = code;
            errorMessage = message;
            return false;
        }
    }
}
