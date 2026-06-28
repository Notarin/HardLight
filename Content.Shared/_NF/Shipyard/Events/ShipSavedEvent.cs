using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Shared._NF.Shipyard.Events;

/// <summary>
/// Event fired when a ship has been successfully saved.
/// Used to trigger cleanup operations like removing shuttle deeds and updating consoles.
/// </summary>
public sealed class ShipSavedEvent : EntityEventArgs
{
    /// <summary>
    /// The EntityUid of the grid that was saved (before deletion)
    /// </summary>
    public EntityUid GridUid { get; set; }

    /// <summary>
    /// The name the ship was saved under
    /// </summary>
    public string ShipName { get; set; } = string.Empty;

    /// <summary>
    /// The user ID of the player who saved the ship
    /// </summary>
    public string PlayerUserId { get; set; } = string.Empty;

    /// <summary>
    /// The player session that initiated the save
    /// </summary>
    public ICommonSession? PlayerSession { get; set; }
}
