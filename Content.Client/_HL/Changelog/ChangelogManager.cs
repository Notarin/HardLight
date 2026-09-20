using System.Linq;
using System.Threading.Tasks;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;

namespace Content.Client._HL.Changelog;

/// <summary>
/// Is changelog manager, manages changelog!
/// Finds yml fields in the changelog dir and basically reflected them into in-game changelogs.
/// Does more, like tracking what's read vs. unread, when there are new entries, etc.
/// </summary>
public sealed partial class ChangelogManager : IPostInjectInit
{
    [Dependency] private readonly ILogManager _logManager = null!;
    [Dependency] private readonly IResourceManager _resource = null!;
    [Dependency] private readonly ISerializationManager _serialization = null!;
    [Dependency] private readonly IConfigurationManager _configManager = null!;

    private const string SawmillName = "changelog";
    private const string MainChangelogName = "HardlightChangelog";

    /// <inheritdoc cref="ISawmill" />
    private ISawmill _sawmill = null!;
    private List<Changelog> _cachedChangelogs = [];

    public bool NewChangelogEntries { get; private set; }
    public event Action? NewChangelogEntriesChanged;

    public async void Initialize()
    {
        try
        {
            var changelogs = await LoadGameChangelogs();
            UpdateChangelogs(changelogs);
        }
        catch (Exception e)
        {
            _sawmill.Error("Failed to load changelog: {e}", e);
        }
    }

    public void SaveNewReadId()
    {
        NewChangelogEntries = false;
        NewChangelogEntriesChanged?.Invoke();

        var directory = new ResPath("/Changelog");
        foreach (var file in _resource.ContentFindFiles(directory))
        {
            if (file.Extension != "yml" || file.Directory != directory) continue;
            
            var path = new ResPath($"/changelog_last_seen_{_configManager.GetCVar(CCVars.ServerId)}_{file.Filename}");
            using var readStream = _resource.ContentFileRead(file);
            using var writeStream = _resource.UserData.OpenWrite(path);
            readStream.CopyTo(writeStream);
        }
    }

    private void UpdateChangelogs(List<Changelog> changelogs)
    {
        _cachedChangelogs = changelogs;

        if (changelogs.Count == 0) return;

        var mainChangelogs = changelogs.Where(c => c.Name == MainChangelogName).ToArray();
        if (mainChangelogs.Length == 0)
        {
            _sawmill.Error($"No changelog file found in Resources/Changelog with name {MainChangelogName}");
            return;
        }

        var oldPath = new ResPath($"/changelog_last_seen_{_configManager.GetCVar(CCVars.ServerId)}.txt");
        var lastReadVersions = new Dictionary<string, Version>();
        bool isOldFormat = false;

        if (_resource.UserData.TryReadAllText(oldPath, out var text))
        {
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(':');
                if (parts.Length != 2 || !Version.TryParse(parts[1], out var version)) continue;
                lastReadVersions[parts[0]] = version;
                isOldFormat = true;
            }
        }

        bool hasUnread = false;

        foreach (var file in _resource.ContentFindFiles(new ResPath("/Changelog/")))
        {
            if (file.Extension != "yml") continue;
            
            var changelog = changelogs.FirstOrDefault(c => c.Name == file.FilenameWithoutExtension);
            if (changelog == null) continue;

            changelog.UnreadEntries.Clear();

            var oldChangelog = LoadOldSingleChangelog(file);

            if (oldChangelog != null)
            {
                foreach (var (version, entry) in changelog.Entries)
                {
                    if (oldChangelog.Entries.TryGetValue(version, out var oldEntry) &&
                        GetEntryHash(entry) == GetEntryHash(oldEntry)) continue;
                    changelog.UnreadEntries.Add(version);
                }
            }
            else if (isOldFormat)
            {
                lastReadVersions.TryGetValue(changelog.Name, out var lastRead);
                lastRead ??= new Version(0, 0);

                foreach (var (version, _) in changelog.Entries)
                {
                    if (version > lastRead)
                    {
                        changelog.UnreadEntries.Add(version);
                    }
                }
            }
            else 
            {
                foreach (var version in changelog.Entries.Keys)
                {
                    changelog.UnreadEntries.Add(version);
                }
            }

            if (changelog.HasUnreadEntries)
            {
                hasUnread = true;
            }
        }

        NewChangelogEntries = hasUnread;
        NewChangelogEntriesChanged?.Invoke();
    }

    private Changelog? LoadOldSingleChangelog(ResPath file)
    {
        var path = new ResPath($"/changelog_last_seen_{_configManager.GetCVar(CCVars.ServerId)}_{file.Filename}");
        if (!_resource.UserData.Exists(path)) return null;

        using var reader = _resource.UserData.OpenText(path);
        var yamlData = new YamlDotNet.RepresentationModel.YamlStream();
        yamlData.Load(reader);

        if (yamlData.Documents.Count == 0) return null;

        var rootNode = yamlData.Documents[0].RootNode.ToDataNodeCast<MappingDataNode>();

        var changelog = new Changelog
        {
            Name = file.FilenameWithoutExtension,
            Entries = new Dictionary<Version, ChangelogEntry>()
        };

        foreach (var pair in rootNode)
        {
            var versionStr = pair.Key;

            if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionStr = versionStr[1..];

            if (!Version.TryParse(versionStr, out var version)) continue;
            var entry = _serialization.Read<ChangelogEntry>(pair.Value, notNullableOverride: true);
            changelog.Entries[version] = entry;
        }

        return changelog;
    }

    private static int GetEntryHash(ChangelogEntry entry)
    {
        var hash = new HashCode();
        var changeLists = new[] { entry.Add, entry.Fix, entry.Tweak, entry.Remove };
        
        foreach (var changeList in changeLists)
        {
            if (changeList == null) continue;
            foreach (var c in changeList)
            {
                hash.Add(c.Message);
                hash.Add(c.Author);
            }
        }

        return hash.ToHashCode();
    }
    public HashSet<Version> GetUnreadEntries(string changelogName)
    {
        var cached = _cachedChangelogs.FirstOrDefault(c => c.Name == changelogName);
        return cached?.UnreadEntries ?? [];
    }

    public Task<List<Changelog>> LoadGameChangelogs()
    {
        return Task.Run(() =>
        {
            var directory = new ResPath("/Changelog");
            var changelogs = (from file in _resource.ContentFindFiles(new ResPath("/Changelog/"))
                where file.Directory == directory
                select LoadSingleChangelog(file)).Where(c => c != null).Select(c => c!).ToList();

            changelogs = [.. changelogs.OrderBy(x => x.Name != MainChangelogName)];
            return changelogs;
        });
    }

    private Changelog? LoadSingleChangelog(ResPath file)
    {
        if (file.Extension != "yml") return null;

        var yamlData = _resource.ContentFileReadYaml(file);
        if (yamlData.Documents.Count == 0) return null;

        var rootNode = yamlData.Documents[0].RootNode.ToDataNodeCast<MappingDataNode>();

        var changelog = new Changelog
        {
            Name = file.FilenameWithoutExtension,
            Entries = new Dictionary<Version, ChangelogEntry>()
        };

        foreach (var pair in rootNode)
        {
            var versionStr = pair.Key;

            if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                versionStr = versionStr[1..];

            if (!Version.TryParse(versionStr, out var version)) continue;
            var entry = _serialization.Read<ChangelogEntry>(pair.Value, notNullableOverride: true);
            changelog.Entries[version] = entry;
        }

        return changelog;
    }
    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill(SawmillName);
    }

    public string GetClientVersion()
    {
        var fork = _configManager.GetCVar(CVars.BuildForkId);
        var version = _configManager.GetCVar(CVars.BuildVersion);
        if (version.Length > 7) version = version[..7];
        if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(fork)) return Loc.GetString("changelog-version-unknown");
        return Loc.GetString("changelog-version-tag", ("fork", fork), ("version", version));
    }

    /// <summary>
    /// Represents an entire loaded changelog file (like HardlightChangelog.yml).
    /// Holds all the versions and keeps track of which ones the user hasn't seen yet.
    /// </summary>
    public sealed class Changelog
    {
        /// <summary>
        /// The name of the changelog file.
        /// This is generally automatically parsed out from the filename.
        /// </summary>
        public string Name = string.Empty;
        /// <summary>
        /// The changes in the changelog, divvied up by semver release.
        /// </summary>
        internal Dictionary<Version, ChangelogEntry> Entries = new();

        /// <summary>
        /// A collection of versions representing changelog entries that have not yet been read by the user.
        /// </summary>
        /// <remarks>
        /// The algorithm to find these entries is intentionally particularly aggressive.
        /// It will flag any version with the slightest change/difference as an entirely new change.
        /// TODO: Implement splitting versions by what's new and whats now so only new/modified entries bubble to the top without pulling up old changes.
        /// </remarks>
        public HashSet<Version> UnreadEntries = [];
        /// <summary>
        /// Stupid simple helper to check if there are any unread entries.
        /// Just here because I found it convenient, despite it only getting 2 uses.
        /// </summary>
        public bool HasUnreadEntries => UnreadEntries.Count > 0;
    }

    /// <summary>
    /// Abstract for classes that Represent a category all the changes included in a single version release.
    /// These are broken down into categories (like additions, fixes, tweaks) so we can group them nicely in the UI.
    /// </summary>
    [DataDefinition]
    public sealed partial class ChangelogEntry
    {
        /// <summary>
        /// <see cref="ChangelogLineType.Add" /> variant of <see cref="ChangelogEntry" />
        /// Contains a list of changelog entries, all additions.
        /// </summary>
        [DataField]
        public List<ChangelogChange>? Add { get; private set; }

        /// <summary>
        /// <see cref="ChangelogLineType.Fix" /> variant of <see cref="ChangelogEntry" />
        /// Contains a list of changelog entries, all fixes.
        /// </summary>
        [DataField]
        public List<ChangelogChange>? Fix { get; private set; }

        /// <summary>
        /// <see cref="ChangelogLineType.Tweak" /> variant of <see cref="ChangelogEntry" />
        /// Contains a list of changelog entries, all changes to existing features.
        /// </summary>
        [DataField]
        public List<ChangelogChange>? Tweak { get; private set; }

        /// <summary>
        /// <see cref="ChangelogLineType.Remove" /> variant of <see cref="ChangelogEntry" />
        /// Contains a list of changelog entries, all removals.
        /// </summary>
        [DataField]
        public List<ChangelogChange>? Remove { get; private set; }
    }

    /// <summary>
    /// A single line item in the changelog, written by someone who (hopefully) fixed a bug or added a feature.
    /// It can optionally include a commit hash so nerds can click through to see the actual code changes on GitHub/GitLab.
    /// </summary>
    [DataDefinition]
    public sealed partial class ChangelogChange
    {
        /// <summary>
        /// The author of the change. It is preferable this be addressed to the username in the code forge, as that is
        /// what this is used for, to link to said code-forge profile from the changelog.
        /// </summary>
        [DataField]
        public string Author { get; private set; } = string.Empty;

        /// <summary>
        /// A short brief description of the change.
        /// It is important that these are always player-oriented, as that is who the changelog is for.
        /// Code refactors, internal changes, private changes, etc. do NOT get a changelog entry.
        /// </summary>
        [DataField]
        public string Message { get; private set; } = string.Empty;

        /// <summary>
        /// The commit hash of either when the feature was introduced or merged.
        /// For small features, this typically points to the commit where the feature was introduced.
        /// For larger features, this points to the merge commit where the feature was merged.
        /// Technically optional. Is nullable. There are situations where it isn't practical to have a commit hash.
        /// </summary>
        [DataField]
        public string? Hash { get; private set; }
    }

    /// <summary>
    /// This enum is a strict collection of all the different types of changelog entries.
    /// Elsewhere, this is matched to select style and the icon for the changelog UI.
    /// </summary>
    public enum ChangelogLineType
    {
        /// <summary>
        /// Represents the inverse of <see cref="Remove" />.
        /// Is the introduction or an extension of a feature.
        /// Does not cover in-place changes to values or reconfigurations of existing features.
        /// </summary>
        /// <example>
        /// Added a system to detect players through walls via echolocation.
        /// </example>
        Add,
        /// <summary>
        /// Represents the inverse of <see cref="Add" />.
        /// Is the removal of a feature.
        /// </summary>
        /// <example>
        /// Removed the ability to detect players through walls via echolocation.
        /// </example>
        Remove,
        /// <summary>
        /// Represents a change that addresses and resolves an existing bug or regression.
        /// </summary>
        /// <example>
        /// Fixed a bug where activating echolocation goggles caused the previous player to use them to go blind.
        /// </example>
        Fix,
        /// <summary>
        /// Represents a change that changes an existing feature or reconfigures it.
        /// Most often editing a prototype.
        /// </summary>
        /// <example>
        /// Extended/Buffed the range of echolocation goggles.
        /// </example>
        Tweak,
    }
}
