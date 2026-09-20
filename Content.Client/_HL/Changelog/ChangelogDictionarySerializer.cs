using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Client._HL.Changelog;

/// <summary>
/// Custom serializer specifically made for changelogs addressed by semver.
/// See <see cref="Dictionary{Version, ChangelogEntry}"/>, and <see cref="Version"/>
/// </summary>
[TypeSerializer]
public sealed class ChangelogDictionarySerializer : ITypeSerializer<Dictionary<Version, ChangelogEntry>, MappingDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        var mapping = new Dictionary<ValidationNode, ValidationNode>();

        foreach ((string keyStr, DataNode valueNode) in node)
        {
            var versionStr = keyStr;
            if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionStr = versionStr[1..];

            var valueKey = new ValueDataNode(keyStr);

            ValidationNode validatedKey = Version.TryParse(versionStr, out _)
                ? new ValidatedValueNode(valueKey)
                : new ErrorNode(valueKey, "Invalid Version format. Must be like '0.1.0'");

            var validatedVal = serializationManager.ValidateNode<ChangelogEntry>(valueNode, context);

            mapping.Add(validatedKey, validatedVal);
        }

        return new ValidatedMappingNode(mapping);
    }

    public Dictionary<Version, ChangelogEntry> Read(
        ISerializationManager serializationManager,
        MappingDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null, ISerializationManager.InstantiationDelegate<Dictionary<Version, ChangelogEntry>>? instanceProvider = null)
    {
        var dict = new Dictionary<Version, ChangelogEntry>();

        foreach ((string keyStr, DataNode valueNode) in node)
        {
            var versionStr = keyStr;
            if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionStr = versionStr[1..];

            if (!Version.TryParse(versionStr, out var version)) continue;
            var entry = serializationManager.Read<ChangelogEntry>(valueNode, hookCtx, context, notNullableOverride: true);
            dict[version] = entry;
        }

        return dict;
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        Dictionary<Version, ChangelogEntry> value, 
        IDependencyCollection dependencies,
        bool alwaysWrite = false, 
        ISerializationContext? context = null)
    {
        var mapping = new MappingDataNode();

        foreach (var (version, entry) in value)
        {
            var valNode = serializationManager.WriteValue(entry, alwaysWrite, context, notNullableOverride: true);
            mapping.Add(version.ToString(), valNode);
        }

        return mapping;
    }
}