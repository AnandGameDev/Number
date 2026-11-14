using System.Collections;
using UnityEngine;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;

namespace DayakattamDungeon.Audio
{
    /// <summary>
    /// Background Music Engine for Dayakattam.
    /// Plays the authentic 'Mridangam Loop - (64 Kbps)' audio clip continuously on loop.
    /// Fully controllable from Settings / Pause Menu and respects audio toggles.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MridangamRhythmEngine : MonoBehaviour
    {
        public static MridangamRhythmEngine Instance { get; private set; }

        [Header("Audio Output")]
        [SerializeField] private AudioSource musicAudioSource;
        [SerializeField] private AudioSource percussionAudioSource;
        [SerializeField] private AudioClip backgroundMusicClip;

        [Header("Settings")]
        [SerializeField] private float musicVolume = 0.65f;
        [SerializeField] private bool isMusicEnabled = true;

        private const string PrefMusicEnabled = "MusicEnabled";

        public bool IsMusicEnabled => isMusicEnabled;
        public float Volume => musicVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureInitialized();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureInitialized();
        }

        private void Start()
        {
            EnsureInitialized();
            if (isMusicEnabled)
            {
                PlayMusic();
            }
        }

        private void Update()
        {
            if (isMusicEnabled && musicAudioSource != null && backgroundMusicClip != null && !musicAudioSource.isPlaying)
            {
                PlayMusic();
            }
        }

        public void EnsureInitialized()
        {
            if (musicAudioSource == null)
            {
                musicAudioSource = percussionAudioSource != null ? percussionAudioSource : GetComponent<AudioSource>();
            }
            if (musicAudioSource == null)
            {
                musicAudioSource = gameObject.AddComponent<AudioSource>();
            }

            // Configure AudioSource for high-quality 2D looping background music
            musicAudioSource.loop = true;
            musicAudioSource.playOnAwake = false;
            musicAudioSource.spatialBlend = 0f; // 2D Stereo
            musicAudioSource.volume = musicVolume;

            // Load audio clip if not assigned
            if (backgroundMusicClip == null)
            {
                backgroundMusicClip = Resources.Load<AudioClip>("Audio/Mridangam Loop - (64 Kbps)");
                if (backgroundMusicClip == null)
                {
                    backgroundMusicClip = Resources.Load<AudioClip>("Mridangam Loop - (64 Kbps)");
                }
#if UNITY_EDITOR
                if (backgroundMusicClip == null)
                {
                    backgroundMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Mridangam Loop - (64 Kbps).mp3");
                }
#endif
            }

            if (backgroundMusicClip != null)
            {
                musicAudioSource.clip = backgroundMusicClip;
            }

            // Load saved preference
            isMusicEnabled = PlayerPrefs.GetInt(PrefMusicEnabled, 1) == 1;
            musicAudioSource.mute = !isMusicEnabled;
        }

        private void PlayMusic()
        {
            if (musicAudioSource == null) return;
            if (backgroundMusicClip == null)
            {
                EnsureInitialized();
            }
            if (backgroundMusicClip == null) return;

            musicAudioSource.clip = backgroundMusicClip;
            musicAudioSource.loop = true;
            musicAudioSource.volume = musicVolume;
            musicAudioSource.mute = !isMusicEnabled;
            musicAudioSource.Stop();
            musicAudioSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void SetMusicEnabled(bool enabled)
        {
            isMusicEnabled = enabled;
            PlayerPrefs.SetInt(PrefMusicEnabled, enabled ? 1 : 0);
            PlayerPrefs.Save();

            if (musicAudioSource != null)
            {
                musicAudioSource.mute = !enabled;
                if (enabled)
                {
                    if (!musicAudioSource.isPlaying)
                    {
                        PlayMusic();
                    }
                    else
                    {
                        musicAudioSource.UnPause();
                    }
                }
                else
                {
                    musicAudioSource.Pause();
                }
            }
        }

        public void SetVolume(float vol)
        {
            musicVolume = Mathf.Clamp01(vol);
            if (musicAudioSource != null)
            {
                musicAudioSource.volume = musicVolume;
            }
        }

        public void StartRhythm()
        {
            SetMusicEnabled(true);
        }

        public void StopRhythm()
        {
            SetMusicEnabled(false);
        }

        public void PlayLuckyAccent()
        {
            // Accent placeholder maintained for compatibility with roll callbacks
        }

        public void PlayTheermanamCadence()
        {
            // Cadence placeholder maintained for compatibility with victory callbacks
        }
    }
}
