using System.Collections;
using UnityEngine;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;

namespace DayakattamDungeon.Audio
{
    /// <summary>
    /// Dynamic Carnatic Percussion Engine (Mridangam).
    /// Procedurally synthesizes classic strokes (Tha, Dhi, Thom, Nam) and accelerates
    /// tempo and intensity with Auspicious Streaks and the ComboMultiplier.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MridangamRhythmEngine : MonoBehaviour
    {
        public static MridangamRhythmEngine Instance { get; private set; }

        [Header("Audio Output")]
        [SerializeField] private AudioSource percussionAudioSource;
        [SerializeField] private AudioSource droneAudioSource;

        [Header("Tempo Settings (BPM)")]
        [SerializeField] private float baseBPM = 72f;
        [SerializeField] private float streak1BPM = 98f;
        [SerializeField] private float streak2BPM = 132f;
        [SerializeField] private float streak3BPM = 164f;

        [Header("State")]
        [SerializeField] private bool isPlayingRhythm = true;
        [SerializeField] private float currentBPM = 72f;
        [SerializeField] private float volume = 0.65f;

        // Procedural AudioClips
        private AudioClip clipTha;   // Deep bass thud (left head)
        private AudioClip clipDhi;   // Sharp high slap
        private AudioClip clipThom;  // Resonant bass boom
        private AudioClip clipNam;   // Metallic bell ring (rim Chappu)
        private AudioClip clipTamburaDrone; // Meditative background drone

        private Coroutine rhythmLoopRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (percussionAudioSource == null)
            {
                percussionAudioSource = GetComponent<AudioSource>();
            }

            if (droneAudioSource == null)
            {
                droneAudioSource = gameObject.AddComponent<AudioSource>();
            }

            GenerateMridangamClips();
        }

        private void Start()
        {
            currentBPM = baseBPM;

            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStreakUpdated += HandleStreakUpdated;
                TurnAndStreakManager.Instance.OnStateChanged += HandleStateChanged;
            }

            StartDrone();
            StartRhythm();
        }

        private void OnDestroy()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStreakUpdated -= HandleStreakUpdated;
                TurnAndStreakManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStreakUpdated(int streakCount, float comboMultiplier)
        {
            // Dynamically scale Mridangam tempo with Auspicious Streak!
            switch (streakCount)
            {
                case 0:
                    currentBPM = baseBPM;
                    break;
                case 1:
                    currentBPM = streak1BPM;
                    break;
                case 2:
                    currentBPM = streak2BPM;
                    break;
                default:
                    currentBPM = streak3BPM;
                    break;
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Victory || state == GameState.Defeat)
            {
                PlayTheermanamCadence();
            }
        }

        public void StartRhythm()
        {
            if (rhythmLoopRoutine != null) StopCoroutine(rhythmLoopRoutine);
            rhythmLoopRoutine = StartCoroutine(RhythmLoop());
        }

        public void StopRhythm()
        {
            if (rhythmLoopRoutine != null)
            {
                StopCoroutine(rhythmLoopRoutine);
                rhythmLoopRoutine = null;
            }
        }

        private void StartDrone()
        {
            if (droneAudioSource != null && clipTamburaDrone != null)
            {
                droneAudioSource.clip = clipTamburaDrone;
                droneAudioSource.loop = true;
                droneAudioSource.volume = volume * 0.35f;
                droneAudioSource.Play();
            }
        }

        /// <summary>
        /// Procedural 8-beat Adi Thala percussion cycle:
        /// Beat 1: Tha (Deep Thoppi)
        /// Beat 2: Dhi (Slap)
        /// Beat 3: Thom (Open Resonance)
        /// Beat 4: Nam (Bell Rim)
        /// Beat 5: Tha
        /// Beat 6: Dhi
        /// Beat 7: Thom-Thom (Syncopated)
        /// Beat 8: Nam
        /// </summary>
        private IEnumerator RhythmLoop()
        {
            int beatIndex = 0;

            while (isPlayingRhythm)
            {
                float beatDuration = 60f / currentBPM;

                // Play stroke based on pattern
                switch (beatIndex)
                {
                    case 0:
                        PlayStroke(clipTha, 0.9f, 1.0f);
                        break;
                    case 1:
                        PlayStroke(clipDhi, 0.7f, 1.05f);
                        break;
                    case 2:
                        PlayStroke(clipThom, 0.85f, 0.98f);
                        break;
                    case 3:
                        PlayStroke(clipNam, 0.75f, 1.02f);
                        break;
                    case 4:
                        PlayStroke(clipTha, 0.95f, 1.0f);
                        break;
                    case 5:
                        PlayStroke(clipDhi, 0.75f, 1.1f);
                        break;
                    case 6:
                        PlayStroke(clipThom, 0.8f, 1.0f);
                        if (currentBPM > 110f) // Add double stroke on high streak
                        {
                            yield return new WaitForSeconds(beatDuration * 0.5f);
                            PlayStroke(clipDhi, 0.65f, 1.15f);
                            beatDuration *= 0.5f;
                        }
                        break;
                    case 7:
                        PlayStroke(clipNam, 0.9f, 1.0f);
                        break;
                }

                beatIndex = (beatIndex + 1) % 8;
                yield return new WaitForSeconds(beatDuration);
            }
        }

        private void PlayStroke(AudioClip clip, float strokeVol, float pitch)
        {
            if (clip == null || percussionAudioSource == null) return;

            percussionAudioSource.pitch = pitch * Random.Range(0.97f, 1.03f);
            percussionAudioSource.PlayOneShot(clip, strokeVol * volume);
        }

        /// <summary>
        /// Classical 3-fold rhythmic cadence played upon Auspicious Streak finish or Victory.
        /// </summary>
        public void PlayTheermanamCadence()
        {
            StartCoroutine(TheermanamRoutine());
        }

        private IEnumerator TheermanamRoutine()
        {
            float gap = 0.12f;
            for (int repetition = 0; repetition < 3; repetition++)
            {
                PlayStroke(clipTha, 1.0f, 1.0f);
                yield return new WaitForSeconds(gap);
                PlayStroke(clipDhi, 0.9f, 1.1f);
                yield return new WaitForSeconds(gap);
                PlayStroke(clipThom, 1.0f, 0.95f);
                yield return new WaitForSeconds(gap * 1.8f);
            }
            PlayStroke(clipNam, 1.0f, 1.0f);
        }

        #region Procedural Audio Synthesis
        private void GenerateMridangamClips()
        {
            clipTha = CreateThaClip();
            clipDhi = CreateDhiClip();
            clipThom = CreateThomClip();
            clipNam = CreateNamClip();
            clipTamburaDrone = CreateTamburaDroneClip();
        }

        // Tha: Bass thud with steep pitch drop
        private AudioClip CreateThaClip()
        {
            int rate = 44100;
            float dur = 0.28f;
            int count = (int)(rate * dur);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float freq = Mathf.Lerp(160f, 75f, t / dur);
                float env = Mathf.Exp(-t * 16f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            }

            AudioClip clip = AudioClip.Create("Mridangam_Tha", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Dhi: Sharp, dry slap transient
        private AudioClip CreateDhiClip()
        {
            int rate = 44100;
            float dur = 0.12f;
            int count = (int)(rate * dur);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 50f);
                float crack = Mathf.Sin(2f * Mathf.PI * 950f * t) * Mathf.Exp(-t * 40f);
                data[i] = (noise * 0.6f + crack * 0.4f);
            }

            AudioClip clip = AudioClip.Create("Mridangam_Dhi", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Thom: Open bass bloom (warm resonance)
        private AudioClip CreateThomClip()
        {
            int rate = 44100;
            float dur = 0.38f;
            int count = (int)(rate * dur);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 9f);
                float f1 = Mathf.Sin(2f * Mathf.PI * 115f * t);
                float f2 = Mathf.Sin(2f * Mathf.PI * 230f * t) * 0.4f;
                data[i] = (f1 + f2) * env;
            }

            AudioClip clip = AudioClip.Create("Mridangam_Thom", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Nam: Metallic rim resonance (Chappu)
        private AudioClip CreateNamClip()
        {
            int rate = 44100;
            float dur = 0.32f;
            int count = (int)(rate * dur);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 12f);
                float ring1 = Mathf.Sin(2f * Mathf.PI * 720f * t);
                float ring2 = Mathf.Sin(2f * Mathf.PI * 1440f * t) * 0.5f;
                float ring3 = Mathf.Sin(2f * Mathf.PI * 2880f * t) * 0.25f;
                data[i] = (ring1 + ring2 + ring3) * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Mridangam_Nam", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Tambura Drone: Meditative Indian tanpura drone in C# (Sa - Pa - Sa)
        private AudioClip CreateTamburaDroneClip()
        {
            int rate = 44100;
            float dur = 4.0f; // 4 second loop
            int count = (int)(rate * dur);
            float[] data = new float[count];

            float fSa = 138.59f; // C#3
            float fPa = 207.65f; // G#3
            float fSaHigh = 277.18f; // C#4

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                // Subtle slow LFO swell
                float swell = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);

                float s1 = Mathf.Sin(2f * Mathf.PI * fSa * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * fPa * t) * 0.6f;
                float s3 = Mathf.Sin(2f * Mathf.PI * fSaHigh * t) * 0.4f;
                float shimmer = Mathf.Sin(2f * Mathf.PI * (fSaHigh * 2f) * t) * 0.15f;

                data[i] = (s1 + s2 + s3 + shimmer) * 0.25f * swell;
            }

            AudioClip clip = AudioClip.Create("TamburaDrone", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }
}
