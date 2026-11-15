using System.Collections.Generic;
using UnityEngine;

// Plays the game's sounds. Put ONE of these on an empty GameObject in your first scene.
// WAV files live in Assets/Resources/Sounds (made by make_sounds.py); no setup beyond that.
//
//   SoundManager.Play("click");   SoundManager.Play("goal");   SoundManager.PlayMusic();
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private AudioSource sfx;
    private AudioSource music;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        sfx = gameObject.AddComponent<AudioSource>();
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        ApplyVolumes();
    }

    public void ApplyVolumes()
    {
        SettingsData s = GameSettings.Data;
        if (sfx != null) sfx.volume = s.muted ? 0f : s.sfxVolume;
        if (music != null) music.volume = s.muted ? 0f : s.musicVolume;
    }

    public static void Play(string name)
    {
        if (Instance == null || GameSettings.Data.muted) return;
        AudioClip clip = Instance.Get(name);
        if (clip != null) Instance.sfx.PlayOneShot(clip, 1f);
    }

    public static void PlayMusic()
    {
        if (Instance == null) return;
        AudioClip clip = Instance.Get("music_menu");
        if (clip == null || Instance.music.isPlaying) return;
        Instance.music.clip = clip;
        Instance.music.Play();
    }

    public static void StopMusic() { if (Instance != null) Instance.music.Stop(); }

    private AudioClip Get(string name)
    {
        AudioClip clip;
        if (clips.TryGetValue(name, out clip)) return clip;
        clip = Resources.Load<AudioClip>("Sounds/" + name);
        if (clip == null) Debug.LogWarning("Missing sound: " + name);
        clips[name] = clip;
        return clip;
    }

    // Plays the right reveal sound for a pack tier name.
    public static void PlayReveal(string tier)
    {
        Play("reveal_" + (tier ?? "bronze").ToLowerInvariant());
    }
}
