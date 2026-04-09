using Godot;
using System;
using System.Collections.Generic;
using System.IO;

public partial class AudioManager : Node
{
    public const string SfxBusName = "SFX";
    [Export] public int DefaultVoicesPerKey { get; set; } = 8;

    [Export] public bool StealOldestVoiceWhenFull { get; set; } = true;

    [Export] public bool DuplicateTemplateNode { get; set; } = true;

    private sealed class VoicePool
    {
        public AudioStreamPlayer Template = default!;
        public readonly List<AudioStreamPlayer> Voices = new();
        public int NextStealIndex = 0; // round-robin stealing
    }

    private readonly Dictionary<string, VoicePool> _pools = new(StringComparer.OrdinalIgnoreCase);
    public readonly Dictionary<string, AudioStream> WeaponHitSfx = new(StringComparer.OrdinalIgnoreCase);

    private const string WeaponSoundsPath = "res://Assets/WeaponSounds";
    private const string DefaultWeaponHitSfxKey = "rifleshot.mp3";

    public static AudioManager? Instance { get; private set; }

    public override void _EnterTree()
        { Instance = this; }

    public override void _Ready()
        { ReloadWeaponHitSfx(); }

    public override void _ExitTree()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    public void Register(string key, AudioStreamPlayer player)
    {
        if (string.IsNullOrWhiteSpace(key) || player == null)
            return;

        player.Bus = SfxBusName;

        if (_pools.TryGetValue(key, out var existing))
        {
            existing.Template = player;
            return;
        }

        var pool = new VoicePool { Template = player };
        _pools[key] = pool;
        EnsureVoices(key, DefaultVoicesPerKey);
    }

    public void PlayStaggeredJitter(string key, int count, float intervalSeconds)
    {
        _ = PlayStaggeredJitterAsync(key, count, intervalSeconds);
    }

    private async System.Threading.Tasks.Task PlayStaggeredJitterAsync(string key, int count, float intervalSeconds) //This method is so that multiple shooting sounds are staggered instead of simultaneous.
    {
        float jitterSeconds = intervalSeconds * (float)GD.RandRange(0.75, 1.25);
        for (int i = 0; i < count; i++)
        {
            Play(key);
            float wait = Mathf.Max(0.01f, intervalSeconds + jitterSeconds);
            await ToSignal(GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
        }
    }

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

        if (_pools.TryGetValue(key, out var pool) && pool.Template?.Stream != null)
        {
            PlayFromPool(pool);
            return;
        }
        PlayWeaponHit(key);
    }

    public void PlayWeaponHit(string key)
    {
        if (!IsSfxEnabled())
            return;
        var stream = ResolveWeaponHitStream(key);
        if (stream == null)
            return;
        var player = new AudioStreamPlayer
        {
            Stream = stream,
            Bus = SfxBusName,
            Autoplay = false
        };
        AddChild(player);
        player.Finished += () => player.QueueFree();
        player.Play();
    }

    public void ReloadWeaponHitSfx()
    {
        WeaponHitSfx.Clear();

        if (!DirAccess.DirExistsAbsolute(WeaponSoundsPath))
        {
            GD.PushWarning($"[AudioManager] Weapon sounds folder not found: {WeaponSoundsPath}");
            return;
        }

        using var dir = DirAccess.Open(WeaponSoundsPath);
        if (dir == null)
        {
            GD.PushWarning($"[AudioManager] Unable to open weapon sounds folder: {WeaponSoundsPath}");
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            var fileName = dir.GetNext();
            if (string.IsNullOrEmpty(fileName))
                break;

            if (dir.CurrentIsDir() || !fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                continue;
            var stream = GD.Load<AudioStream>($"{WeaponSoundsPath}/{fileName}");
            if (stream != null)
                WeaponHitSfx[fileName] = stream;
        }
        dir.ListDirEnd();
    }

    public AudioStream? ResolveWeaponHitStream(string key)
    {
        var normalized = NormalizeWeaponHitKey(key);
        if (!string.IsNullOrEmpty(normalized) && WeaponHitSfx.TryGetValue(normalized, out var stream))
            return stream;
        if (WeaponHitSfx.TryGetValue(DefaultWeaponHitSfxKey, out var fallback))
            return fallback;
        return null;
    }

    public string NormalizeWeaponHitKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var trimmed = key.Trim();
        if (WeaponHitSfx.ContainsKey(trimmed))
            return trimmed;

        var withExt = trimmed.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}.mp3";

        if (WeaponHitSfx.ContainsKey(withExt))
            return withExt;

        var noExt = Path.GetFileNameWithoutExtension(trimmed);
        foreach (var candidate in WeaponHitSfx.Keys)
        {
            if (Path.GetFileNameWithoutExtension(candidate).Equals(noExt, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return string.Empty;
    }

    private void PlayFromPool(VoicePool pool)
    {
        for (int i = 0; i < pool.Voices.Count; i++)
        {
            var voice = pool.Voices[i];
            if (!voice.Playing)
            {
                SyncVoiceFromTemplate(voice, pool.Template);
                voice.Play();
                return;
            }
        }

        if (!StealOldestVoiceWhenFull || pool.Voices.Count == 0)
            return;

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
            voice = (AudioStreamPlayer)template.Duplicate();
        else
        {
            voice = new AudioStreamPlayer();
            SyncVoiceFromTemplate(voice, template);
        }
        voice.ProcessMode = ProcessModeEnum.Disabled;
        return voice;
    }

    private void SyncVoiceFromTemplate(AudioStreamPlayer voice, AudioStreamPlayer template)
    {
        voice.Stream = template.Stream;
        voice.Bus = SfxBusName;
        voice.VolumeDb = template.VolumeDb;
        voice.PitchScale = template.PitchScale;
        voice.Autoplay = false;
        voice.StreamPaused = false;
        voice.MaxPolyphony = template.MaxPolyphony;
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
