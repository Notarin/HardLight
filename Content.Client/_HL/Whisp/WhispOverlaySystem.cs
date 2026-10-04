using Content.Shared._HL.Whisp.Components;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Player;

namespace Content.Client._HL.Whisp;

/// <summary>
/// Manages the visual <see cref="WhispStaticOverlay" /> and procedural static audio.
/// Ramps up both effects as a Whisp approaches the local player.
/// </summary>
public sealed class WhispOverlaySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IAudioManager _audio = default!;

    private WhispStaticOverlay _overlay = default!;
    private AudioStream? _staticStream;
    private IAudioSource? _audioSource;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        _overlay = new WhispStaticOverlay();

        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();

        ClearEffects();

        _audioSource?.Dispose();
        _audioSource = null;

        _staticStream?.Dispose();
        _staticStream = null;
    }

    /// <summary>
    /// Handles <see cref="LocalPlayerDetachedEvent" />.
    /// <br/>
    /// Stops the static overlay and audio, and hides whisps.
    /// </summary>
    /// <param name="args">See <see cref="LocalPlayerDetachedEvent"/>.</param>
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        HideAllWhisps();
        ClearEffects();
    }

    /// <inheritdoc/>
    /// <summary>
    /// Evaluates the distance of all Whisps targeting the local player.
    /// Applies and scales the static overlay and audio gain based on whichever Whisp is closest.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // If the client has no entity/body (e.g., in the lobby), we shouldn't have any effects.
        if (_player.LocalEntity is not { Valid: true } localPlayer)
        {
            HideAllWhisps();
            ClearEffects();
            return;
        }

        var maxStrength = 0f;
        var isTargetedByWhisp = false;

        var query = EntityQueryEnumerator<WhispComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var whispComp, out var sprite))
        {
            var isTarget = whispComp.Target == localPlayer;
            if (sprite.Visible != isTarget)
                _sprite.SetVisible((uid, sprite), isTarget);

            if (!isTarget)
                continue;

            isTargetedByWhisp = true;

            // Clamped to 0.01f to prevent division by zero in the next step. (sounds familiar)
            var totalTravelDistance = MathF.Max(0.01f, whispComp.InitialDistance - whispComp.ReachDistance);
            var distanceCovered = whispComp.InitialDistance - whispComp.CurrentDistance;

            var progress = Math.Clamp(distanceCovered / totalTravelDistance, 0f, 1f);

            // If multiple whisps are targeting the player, we want the overlay to match the closest one.
            // P.S. How the fuck did you get multiple of these things after you???
            if (progress > maxStrength)
                maxStrength = progress;
        }

        if (!isTargetedByWhisp)
        {
            ClearEffects();
            return;
        }

        // We know there are whisp(s) after us here.
        EnsureOverlay();
        _overlay.Strength = maxStrength;

        EnsureAudio();
        if (_audioSource != null)
        {
            // Scale audio volume based on proximity
            _audioSource.Gain = maxStrength * 0.45f;
        }
    }

    /// <summary>
    /// Helper method to stop all visual and audio effects at once.
    /// </summary>
    private void ClearEffects()
    {
        RemoveOverlay();
        StopAudio();
    }

    /// <summary>
    /// Safely adds the overlay to the overlay manager if it isn't already active.
    /// </summary>
    private void EnsureOverlay()
    {
        if (!_overlayMan.HasOverlay<WhispStaticOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    /// <summary>
    /// Safely removes the overlay and resets its strength to zero.
    /// </summary>
    /// <remarks>
    /// Prroooobably don't need to clean up, but it can't hurt.
    /// </remarks>
    private void RemoveOverlay()
    {
        if (!_overlayMan.HasOverlay<WhispStaticOverlay>())
            return;

        // Scrubadubdub
        _overlay.Strength = 0f;
        _overlayMan.RemoveOverlay(_overlay);
    }

    /// <summary>
    /// Clears the visibility of all whisps.
    /// Kinda for outliers. Like when the player isn't a character, and this somehow slipped through all the way to here,
    /// or if the player logged out, etc.
    /// </summary>
    private void HideAllWhisps()
    {
        var whispsQuery = EntityQueryEnumerator<WhispComponent, SpriteComponent>();
        while (whispsQuery.MoveNext(out var whisp, out _, out var whispSprite))
        {
            if (whispSprite.Visible)
                _sprite.SetVisible((whisp, whispSprite), false);
        }
    }

    /// <summary>
    /// Ensures the procedural audio stream is generated, loaded into a source, and currently playing.
    /// </summary>
    /// <remarks>
    /// When on, called once every frame.
    /// </remarks>
    private void EnsureAudio()
    {
        if (_staticStream == null) // If we haven't made the audio yet, do so.
        {
            var samples = GenerateStaticSamples();
            _staticStream = _audio.LoadAudioRaw(samples, 1, 22050, "whisp_static_procedural");
        }

        if (_audioSource == null && _staticStream != null) // If we haven't loaded the audio into the game yet, do so
        {
            _audioSource = _audio.CreateAudioSource(_staticStream);
            if (_audioSource != null)
            {
                _audioSource.Global = true;
                _audioSource.Looping = true;
                _audioSource.Gain = 0f; // 0 bc start silent, Update loop scales it up
            }
        }

        if (_audioSource is { Playing: false })
            _audioSource.StartPlaying();
    }

    /// <summary>
    /// Halts the procedural audio playback and zeroes out the volume.
    /// </summary>
    private void StopAudio()
    {
        if (_audioSource == null)
            return;
        if (_audioSource.Playing)
            _audioSource.StopPlaying();

        _audioSource.Gain = 0f;
    }

    /// <summary>
    /// Procedurally generates a short, looping buffer of static.
    /// </summary>
    private static short[] GenerateStaticSamples()
    {
        const int sampleRate = 22050;

        // 2.5 seconds at a 60Hz, had to do math for this, yeuck.
        // Took care to make the start and stop of the sound wave line up as perfectly as possible so artifacts don't generate.
        const float duration = 2.5f;

        const int totalSamples = (int)(sampleRate * duration);
        var samples = new short[totalSamples];

        var random = Random.Shared;
        var previousSample = 0f;

        for (var i = 0; i < totalSamples; i++)
        {
            var whiteNoise = (float)(random.NextDouble() * 2.0 - 1.0);

            var warmNoise = (previousSample + whiteNoise) * 0.5f;
            previousSample = warmNoise;

            var crackle = 0f;
            if (random.NextDouble() < 0.001) // 0.1% chance per sample
                crackle = (float)(random.NextDouble() * 2.0 - 1.0) * 0.8f;

            var timeInSeconds = i / (float)sampleRate;
            var hum = MathF.Sin(2f * MathF.PI * 60f * timeInSeconds) * 0.08f;

            var finalMix = warmNoise * 0.6f + hum + crackle;

            // Convert float (-1.0 to 1.0) to 16-bit PCM short (-32767 to 32767)
            // What a headache...
            samples[i] = (short)Math.Clamp(finalMix * 32767f, -32767f, 32767f);
        }

        return samples;
    }
}
