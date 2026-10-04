using Content.Shared._HL.Whisp.Components;
using Content.Shared._HL.Whisp.Events;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Sources;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

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
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private WhispStaticOverlay _overlay = default!;
    private WhispTextOverlay _textOverlay = default!;
    private WhispBlackoutOverlay _blackoutOverlay = default!;

    private AudioStream? _staticStream;
    private IAudioSource? _audioSource;

    private AudioStream? _screechStream;
    private IAudioSource? _screechSource;

    private bool _isAudioMuted;
    private float _muteTimer;

    private TimeSpan _nextJumpscareAllowed = TimeSpan.Zero;
    private static readonly TimeSpan JumpscareCooldown = TimeSpan.FromSeconds(11);

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<WhispReachedTargetEvent>(OnWhispReachedTarget);

        _overlay = new WhispStaticOverlay();
        _textOverlay = new WhispTextOverlay();
        _blackoutOverlay = new WhispBlackoutOverlay();

        var screechSamples = GenerateScreechSamples();
        _screechStream = _audio.LoadAudioRaw(screechSamples, 1, 22050, "whisp_screech_procedural");
    }

    /// <inheritdoc/>
    public override void Shutdown()
    {
        base.Shutdown();

        ClearEffects();
        EndAudioMute();

        _audioSource?.Dispose();
        _audioSource = null;

        _staticStream?.Dispose();
        _staticStream = null;

        _screechSource?.StopPlaying();
        _screechSource?.Dispose();
        _screechSource = null;
        _screechStream?.Dispose();
        _screechStream = null;

        _blackoutOverlay.Reset();
        _nextJumpscareAllowed = TimeSpan.Zero;
        if (_overlayMan.HasOverlay<WhispBlackoutOverlay>())
            _overlayMan.RemoveOverlay(_blackoutOverlay);
    }

    /// <summary>
    /// Handles <see cref="LocalPlayerDetachedEvent" />.
    /// <br/>
    /// Stops the static overlay and audio and hides whisps.
    /// </summary>
    /// <param name="args">See <see cref="LocalPlayerDetachedEvent"/>.</param>
    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        HideAllWhisps();
        ClearEffects();
        EndAudioMute();
        _screechSource?.StopPlaying();
        _blackoutOverlay.Reset();
        _nextJumpscareAllowed = TimeSpan.Zero;
        if (_overlayMan.HasOverlay<WhispBlackoutOverlay>())
        {
            _overlayMan.RemoveOverlay(_blackoutOverlay);
        }
    }

    /// <summary>
    /// Handles <see cref="WhispReachedTargetEvent"/> when the local player is caught by a whisp.
    /// </summary>
    private void OnWhispReachedTarget(WhispReachedTargetEvent ev)
    {
        if (_player.LocalEntity is not { Valid: true } localPlayer)
            return;

        if (GetEntity(ev.Target) != localPlayer)
            return;

        TriggerCaughtByWhisp();
    }

    /// <summary>
    /// Triggers the caught-by-whisp sequence: plays the procedural screech, mutes general audio,
    /// and activates the screen distortion/blackout overlay.
    /// <br/>
    /// After it finishes, the screen returns to normal and so does audio.
    /// </summary>
    private void TriggerCaughtByWhisp()
    {
        var curTime = _timing.CurTime;
        if (curTime < _nextJumpscareAllowed)
            return;

        _nextJumpscareAllowed = curTime + JumpscareCooldown;

        ClearEffects();
        PlayScreech();
        StartAudioMute(WhispBlackoutOverlay.Duration);

        _blackoutOverlay.Trigger();
        if (!_overlayMan.HasOverlay<WhispBlackoutOverlay>())
        {
            _overlayMan.AddOverlay(_blackoutOverlay);
        }
    }

    /// <summary>
    /// Plays the procedural screech audio stream.
    /// </summary>
    private void PlayScreech()
    {
        if (_screechStream == null)
            return;

        _screechSource?.StopPlaying();
        _screechSource?.Dispose();
        _screechSource = _audio.CreateAudioSource(_screechStream);
        if (_screechSource == null)
            return;
        _screechSource.Global = true;
        _screechSource.Looping = false;
        _screechSource.Gain = 1.0f;
        _screechSource.StartPlaying();
    }

    /// <summary>
    /// Temporarily suppresses game audio to give uncomfortable silence
    /// </summary>
    /// <param name="duration">Duration in seconds to suppress audio.</param>
    private void StartAudioMute(float duration)
    {
        _muteTimer = duration;
        if (_isAudioMuted)
            return;
        _isAudioMuted = true;
        _audioSystem.ProcessStreamOverride += OnProcessStreamMute;
    }

    /// <summary>
    /// Restores regular game audio after the mute duration has expired.
    /// </summary>
    private void EndAudioMute()
    {
        if (_isAudioMuted)
        {
            _isAudioMuted = false;
            _audioSystem.ProcessStreamOverride -= OnProcessStreamMute;
        }
        _muteTimer = 0f;
    }

    /// <summary>
    /// Audio stream override hook that zeroes out stream gain while audio is muted.
    /// </summary>
    private static void OnProcessStreamMute(EntityUid entity, AudioComponent component, TransformComponent xform, MapCoordinates listener)
    {
        component.Gain = 0f;
    }

    /// <inheritdoc/>
    /// <summary>
    /// Evaluates the distance of all Whisps targeting the local player.
    /// Applies and scales the static overlay and audio gain based on whichever Whisp is closest.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_isAudioMuted)
        {
            _muteTimer -= frameTime;
            if (_muteTimer <= 0f)
            {
                EndAudioMute();
            }
        }

        if (!_blackoutOverlay.IsActive && _overlayMan.HasOverlay<WhispBlackoutOverlay>())
        {
            _overlayMan.RemoveOverlay(_blackoutOverlay);
            EndAudioMute();
        }

        if (_blackoutOverlay.IsActive)
        {
            ClearEffects();
            return;
        }

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

        if (!_overlayMan.HasOverlay<WhispTextOverlay>())
            _overlayMan.AddOverlay(_textOverlay);
    }

    /// <summary>
    /// Safely removes the overlay and resets its strength to zero.
    /// </summary>
    /// <remarks>
    /// Prroooobably don't need to clean up, but it can't hurt.
    /// </remarks>
    private void RemoveOverlay()
    {
        if (_overlayMan.HasOverlay<WhispStaticOverlay>())
        {
            // Scrubadubdub
            _overlay.Strength = 0f;
            _overlayMan.RemoveOverlay(_overlay);
        }

        if (_overlayMan.HasOverlay<WhispTextOverlay>())
            _overlayMan.RemoveOverlay(_textOverlay);
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

    /// <summary>
    /// Procedurally synthesizes a screech audio buffer.
    /// </summary>
    /// <remarks>
    /// I'm not gonna lie, I forced my way through this. Not super proud of this one.
    /// </remarks>
    /// <returns>16-bit PCM mono audio sample buffer.</returns>
    private static short[] GenerateScreechSamples()
    {
        const int sampleRate = 22050;
        const float duration = 1.6f;
        const int totalSamples = (int)(sampleRate * duration);
        var samples = new short[totalSamples];
        var random = new Random();

        var phaseMain = 0.0;
        var phaseSub = 0.0;
        var phaseMod = 0.0;

        for (var i = 0; i < totalSamples; i++)
        {
            var t = (float)i / sampleRate;
            var progress = t / duration;

            // Pitch drops exponentially from 2600 Hz down to 1000 Hz
            var baseFreq = 2600.0 * Math.Exp(-0.95 * progress);

            // Vibrato / shrieking frequency modulation (48-60 Hz)
            // And more fancy words ig, bc I don't have a simpler way to explain it. :|
            var vibratoRate = 48.0 + 12.0 * Math.Sin(t * 10.0);
            var vibratoDepth = 380.0 * (1.0 - progress * 0.4);
            phaseMod += MathF.Tau * vibratoRate / sampleRate;
            var fmOffset = Math.Sin(phaseMod) * vibratoDepth;

            var instFreq = baseFreq + fmOffset;
            phaseMain += MathF.Tau * instFreq / sampleRate;
            phaseSub += MathF.Tau * (instFreq * 1.4142) / sampleRate;

            var sig = Math.Sin(phaseMain) + 0.5 * Math.Sin(phaseSub);

            // Noise ig
            var noise = random.NextDouble() * 2.0 - 1.0;
            sig = sig * 0.72 + noise * 0.28;

            // I am so sorry, I don't even know what this is...
            const double pushCoeff = 3.5;
            var push = Math.Tanh(sig * pushCoeff);

            // Tail it off.
            double amp;
            switch (progress)
            {
                case < 0.005f:
                    amp = progress / 0.005f;
                    break;
                case < 0.65f:
                    amp = 1.0;
                    break;
                default:
                    var fade = (progress - 0.65f) / 0.35f;
                    amp = Math.Pow(1.0 - fade, 1.8);
                    break;
            }

            // Clamp
            var val = (int)(push * amp * 31500.0);
            if (val > 32767)
                val = 32767;
            if (val < -32767)
                val = -32767;
            samples[i] = (short)val;
        }

        return samples;
    }
}
