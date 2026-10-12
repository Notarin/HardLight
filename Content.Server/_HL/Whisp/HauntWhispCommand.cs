using Content.Server.Administration;
using Content.Shared._HL.Whisp.Components;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._HL.Whisp;

/// <summary>
/// Starts a whisp haunt countdown on the input entity.
/// Usage: `self hauntwhisp [distance] [speed]`
/// </summary>
[ToolshedCommand(Name = "hauntwhisp"), AdminCommand(AdminFlags.Fun)]
public sealed class HauntWhispCommand : ToolshedCommand
{
    /// <inheritdoc cref="WhispHauntSystem" />
    private WhispHauntSystem? _haunt;

    /// <inheritdoc cref="HauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Single entity input variant.
    /// </remarks>
    /// <param name="input">The entity to start a whisp haunt on.</param>
    /// <param name="distance">See <see cref="WhispComponent.CurrentDistance" /></param>
    /// <param name="speed">See <see cref="WhispComponent.ClosingSpeed" /></param>
    /// <returns>The entity if haunted, null otherwise.</returns>
    [CommandImplementation]
    public EntityUid? HauntWhisp(
        [PipedArgument] EntityUid input,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        _haunt ??= GetSys<WhispHauntSystem>();
        return _haunt.StartHaunt(input, distance, speed) ? input : null;
    }

    /// <inheritdoc cref="HauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Many entity input variant.
    /// </remarks>
    /// <param name="input">The entities to start a whisp haunt on.</param>
    /// <param name="distance">See <see cref="WhispComponent.CurrentDistance" /></param>
    /// <param name="speed">See <see cref="WhispComponent.ClosingSpeed" /></param>
    /// <returns>The entities that were successfully haunted.</returns>
    [CommandImplementation]
    public IEnumerable<EntityUid> HauntWhisp(
        [PipedArgument] IEnumerable<EntityUid> input,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        _haunt ??= GetSys<WhispHauntSystem>();
        foreach (var ent in input)
        {
            if (_haunt.StartHaunt(ent, distance, speed))
                yield return ent;
        }
    }

    /// <inheritdoc cref="HauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// No input variant. Targets the executing entity (you).
    /// </remarks>
    /// <param name="ctx">
    /// See <see cref="IInvocationContext"/>. Not an actual param, auto-grabs the invocation context.
    /// Used to resolve the executing entity.
    /// </param>
    /// <param name="distance">See <see cref="WhispComponent.CurrentDistance" /></param>
    /// <param name="speed">See <see cref="WhispComponent.ClosingSpeed" /></param>
    /// <returns>The entity if haunted, null otherwise.</returns>
    [CommandImplementation]
    public EntityUid? HauntWhisp(
        IInvocationContext ctx,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        // Technically possible to execute from console, C#, SCSI, and probably more.
        // So gotta guard that.
        if (ExecutingEntity(ctx) is not { } ent)
            return null;

        _haunt ??= GetSys<WhispHauntSystem>();
        return _haunt.StartHaunt(ent, distance, speed) ? ent : null;
    }
}

/// <summary>
/// Cancels an ongoing whisp haunt on the input entity.
/// Usage: `self unhauntwhisp`
/// </summary>
[ToolshedCommand(Name = "unhauntwhisp"), AdminCommand(AdminFlags.Fun)]
public sealed class UnhauntWhispCommand : ToolshedCommand
{
    /// <inheritdoc cref="WhispHauntSystem" />
    private WhispHauntSystem? _haunt;

    /// <inheritdoc cref="UnhauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Single entity input variant.
    /// </remarks>
    /// <param name="input">The entity to cancel the haunt on.</param>
    /// <returns>The entity if unhaunted, null otherwise.</returns>
    [CommandImplementation]
    public EntityUid? UnhauntWhisp([PipedArgument] EntityUid input)
    {
        _haunt ??= GetSys<WhispHauntSystem>();
        return _haunt.StopHaunt(input) ? input : null;
    }

    /// <inheritdoc cref="UnhauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Many entity input variant.
    /// </remarks>
    /// <param name="input">The entities to cancel the haunt on.</param>
    /// <returns>The entities that were successfully unhaunted.</returns>
    [CommandImplementation]
    public IEnumerable<EntityUid> UnhauntWhisp([PipedArgument] IEnumerable<EntityUid> input)
    {
        _haunt ??= GetSys<WhispHauntSystem>();
        foreach (var ent in input)
        {
            if (_haunt.StopHaunt(ent))
                yield return ent;
        }
    }

    /// <inheritdoc cref="UnhauntWhispCommand" />
    /// <br/>
    /// <remarks>
    /// No input variant. Targets the executing entity (you).
    /// </remarks>
    /// <param name="ctx">
    /// See <see cref="IInvocationContext"/>. Not an actual param, auto-grabs the invocation context.
    /// Used to resolve the executing entity.
    /// </param>
    /// <returns>The entity if unhaunted, null otherwise.</returns>
    [CommandImplementation]
    public EntityUid? UnhauntWhisp(IInvocationContext ctx)
    {
        // Technically possible to execute from console, C#, SCSI, and probably more.
        // So gotta guard that.
        if (ExecutingEntity(ctx) is not { } ent)
            return null;

        _haunt ??= GetSys<WhispHauntSystem>();
        return _haunt.StopHaunt(ent) ? ent : null;
    }
}
