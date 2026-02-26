using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Polyphonic SFX AudioManager with per-key voice pools.
/// - Register(key, player): registers a template player (stream, bus, volume, pitch, etc.)
/// - Play(key): plays using an available pooled voice (won't cut off overlapping sounds)
/// </summary>
public partial class AudioManager : Node
{
    public const string SfxBusName = "SFX";

    // How many simultaneous instances per sound key.
    [Export] public int DefaultVoicesPerKey { get; set; } = 8;

    // If all voices are busy, should we stop one and reuse it?
    [Export] public bool StealOldestVoiceWhenFull { get; set; } = true;

    // If true, we duplicate the template AudioStreamPlayer node when creating voices.
    // If false, we create new AudioStreamPlayer nodes and copy settings manually.
    [Export] public bool DuplicateTemplateNode { get; set; } = true;

    private sealed class VoicePool
    {
        public AudioStreamPlayer Template = default!;
        public readonly List<AudioStreamPlayer> Voices = new();
        public int NextStealIndex = 0; // round-robin stealing
    }

    private readonly Dictionary<string, VoicePool> _pools = new(StringComparer.OrdinalIgnoreCase);

    public static AudioManager? Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    /// <summary>
    /// Register a sound key with a template AudioStreamPlayer.
    /// The template should already have its Stream set and any settings you want (volume, pitch, etc.).
    /// The template node can stay in the scene; we will NOT play it directly.
    /// </summary>
    public void Register(string key, AudioStreamPlayer player)
    {
        if (string.IsNullOrWhiteSpace(key) || player == null)
            return;

        // Ensure SFX bus routing
        player.Bus = SfxBusName;

        if (_pools.TryGetValue(key, out var existing))
        {
            existing.Template = player;
            // Optional: clear old voices if you want to rebuild; leaving them is fine.
            return;
        }

        var pool = new VoicePool { Template = player };
        _pools[key] = pool;

        // Pre-warm voices for this key
        EnsureVoices(key, DefaultVoicesPerKey);
    }

    public void PlayStaggeredJitter(string key, int count, float intervalSeconds)
    {
        _ = PlayStaggeredJitterAsync(key, count, intervalSeconds);
    }

    private async System.Threading.Tasks.Task PlayStaggeredJitterAsync(string key, int count, float intervalSeconds)
    {
        float jitterSeconds = intervalSeconds * (float)GD.RandRange(0.75, 1.25);
        for (int i = 0; i < count; i++)
        {
            Play(key);
            float wait = Mathf.Max(0.01f, intervalSeconds + jitterSeconds);
            await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
        }
    }

    /// <summary>
    /// Ensure a key has at least voiceCount pooled players.
    /// Call this after Register if you want more than DefaultVoicesPerKey for a specific sound.
    /// </summary>
    public void EnsureVoices(string key, int voiceCount)
    {
        if (!_pools.TryGetValue(key, out var pool) || pool.Template == null)
            return;

        voiceCount = Mathf.Max(1, voiceCount);

        while (pool.Voices.Count < voiceCount)
        {
            var voice = CreateVoiceFromTemplate(pool.Template);
            pool.Voices.Add(voice);
            AddChild(voice);
        }
    }

    public void Play(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !IsSfxEnabled())
            return;

        if (!_pools.TryGetValue(key, out var pool) || pool.Template?.Stream == null)
            return;

        // Find a free voice
        for (int i = 0; i < pool.Voices.Count; i++)
        {
            var v = pool.Voices[i];
            if (!v.Playing)
            {
                // Keep stream/settings synced (in case you changed template at runtime)
                SyncVoiceFromTemplate(v, pool.Template);
                v.Play();
                return;
            }
        }

        // No free voice: either steal one or drop
        if (!StealOldestVoiceWhenFull || pool.Voices.Count == 0)
            return;

        // Round-robin steal to avoid always cutting the same voice
        int idx = pool.NextStealIndex % pool.Voices.Count;
        pool.NextStealIndex = (pool.NextStealIndex + 1) % pool.Voices.Count;

        var stolen = pool.Voices[idx];
        SyncVoiceFromTemplate(stolen, pool.Template);
        stolen.Stop();
        stolen.Play();
    }

    private AudioStreamPlayer CreateVoiceFromTemplate(AudioStreamPlayer template)
    {
        AudioStreamPlayer voice;

        if (DuplicateTemplateNode)
        {
            // Duplicate copies all inspector-configured properties automatically.
            voice = (AudioStreamPlayer)template.Duplicate();
        }
        else
        {
            // Manual copy (kept for completeness).
            voice = new AudioStreamPlayer();
            SyncVoiceFromTemplate(voice, template);
        }

        // We don't want these pooled voices to run scripts or process unnecessarily.
        voice.ProcessMode = ProcessModeEnum.Disabled;

        return voice;
    }

    private void SyncVoiceFromTemplate(AudioStreamPlayer voice, AudioStreamPlayer template)
    {
        // Copy the important playback properties.
        voice.Stream = template.Stream;
        voice.Bus = SfxBusName;

        voice.VolumeDb = template.VolumeDb;
        voice.PitchScale = template.PitchScale;

        voice.Autoplay = false;
        voice.StreamPaused = false;

        // If you use these in your template, keep them consistent:
        voice.MaxPolyphony = template.MaxPolyphony; // usually irrelevant for AudioStreamPlayer, but safe
        voice.MixTarget = template.MixTarget;
    }

    public bool IsSfxEnabled()
    {
        var busIndex = AudioServer.GetBusIndex(SfxBusName);
        if (busIndex < 0)
            return true;

        return !AudioServer.IsBusMute(busIndex);
    }

    public bool ToggleSfx()
    {
        var busIndex = AudioServer.GetBusIndex(SfxBusName);
        if (busIndex < 0)
            return true;

        var enabled = AudioServer.IsBusMute(busIndex);
        AudioServer.SetBusMute(busIndex, !enabled);
        return enabled;
    }
}
