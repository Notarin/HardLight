using Content.Server.Administration;
using Content.Shared._HL.Whisp;
using Content.Shared._HL.Whisp.Components;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._HL.Whisp;

/// <summary>
/// Spawns a whisp stalking the input entity at a given distance and closing speed.
/// Usage: `self spawnwhisp 10 5`
/// </summary>
[ToolshedCommand(Name = "spawnwhisp"), AdminCommand(AdminFlags.Fun)]
public sealed class SpawnWhispCommand : ToolshedCommand
{
    /// <inheritdoc cref="SharedWhispSystem" />
    private SharedWhispSystem? _whisp;

    /// <inheritdoc cref="SpawnWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Single entity input variant.
    /// </remarks>
    /// <param name="input">The entity to spawn a whisp at.</param>
    /// <param name="distance">See <see cref="WhispComponent.CurrentDistance" /></param>
    /// <param name="speed">See <see cref="WhispComponent.ClosingSpeed" /></param>
    /// <returns>The whisp, if any. See <see cref="SharedWhispSystem.SpawnWhisp"/></returns>
    [CommandImplementation]
    public EntityUid? SpawnWhisp(
        [PipedArgument] EntityUid input,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        _whisp ??= GetSys<SharedWhispSystem>();
        return _whisp.SpawnWhisp(input, distance, speed);
    }

    /// <inheritdoc cref="SpawnWhispCommand" />
    /// <br/>
    /// <remarks>
    /// Many entity input variant.
    /// </remarks>
    /// <param name="input">The entities to spawn a whisp at.</param>
    /// <param name="distance">See <see cref="WhispComponent.CurrentDistance" /></param>
    /// <param name="speed">See <see cref="WhispComponent.ClosingSpeed" /></param>
    /// <returns>The whisp, if any. See <see cref="SharedWhispSystem.SpawnWhisp"/></returns>
    [CommandImplementation]
    public IEnumerable<EntityUid> SpawnWhisp(
        [PipedArgument] IEnumerable<EntityUid> input,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        _whisp ??= GetSys<SharedWhispSystem>();
        foreach (var ent in input)
        {
            if (_whisp.SpawnWhisp(ent, distance, speed) is { } whisp)
                yield return whisp;
        }
    }

    /// <inheritdoc cref="SpawnWhispCommand" />
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
    /// <returns>The whisp, if any. See <see cref="SharedWhispSystem.SpawnWhisp"/></returns>
    [CommandImplementation]
    public EntityUid? SpawnWhisp(
        IInvocationContext ctx,
        float distance = 20f,
        float speed = 0.5f
    )
    {
        // Technically possible to execute from console, C#, SCSI, and probably more.
        // So gotta guard that.
        if (ExecutingEntity(ctx) is not { } ent)
            return null;

        _whisp ??= GetSys<SharedWhispSystem>();
        return _whisp.SpawnWhisp(ent, distance, speed);
    }
}
