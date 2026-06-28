using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Shared._NF.Shipyard.Events;

/// <summary>
/// Event fired when a ship has been successfully loaded from YAML data.
/// Used to trigger post-load operations like updating player ID cards with deeds and updating consoles.
/// </summary>
public sealed class ShipLoadedEvent : EntityEventArgs
{
    /// <summary>
    /// The EntityUid of the console used to load the ship
    /// </summary>
    public EntityUid ConsoleUid { get; set; }

    /// <summary>
    /// The EntityUid of the ID card that should receive the deed
    /// </summary>
    public EntityUid IdCardUid { get; set; }

    /// <summary>
    /// The EntityUid of the loaded ship grid
    /// </summary>
    public EntityUid ShipGridUid { get; set; }

    /// <summary>
    /// The name of the ship that was loaded
    /// </summary>
    public string ShipName { get; set; } = string.Empty;

    /// <summary>
    /// The user ID of the player who loaded the ship
    /// </summary>
    public string PlayerUserId { get; set; } = string.Empty;

    /// <summary>
    /// The player session that initiated the load
    /// </summary>
    public ICommonSession? PlayerSession { get; set; }

    /// <summary>
    /// The YAML data used to load the ship
    /// </summary>
    public string YamlData { get; set; } = string.Empty;

    /// <summary>
    /// The file path of the ship (if loaded from file, can be null)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// The shipyard channel used for announcements
    /// </summary>
    public string ShipyardChannel { get; set; } = string.Empty;
}
