using UnityEngine;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;

namespace DayakattamDungeon.Audio
{
    /// <summary>
    /// Procedural audio generator for Dayakattam Dungeon.
    /// Synthesizes metallic brass stick clatter, auspicious temple bell stings, and impact strikes.
    /// Requires zero external audio files.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class DayakattamAudioPlayer : MonoBehaviour
    {
        private static DayakattamAudioPlayer instance;
        public static DayakattamAudioPlayer Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<DayakattamAudioPlayer>();
                return instance;
            }
            private set => instance = value;
        }

        private AudioSource audioSource;
        private AudioClip diceClackClip;
        private AudioClip templeBellClip;
        private AudioClip captureStrikeClip;
        private AudioClip stepHopClip;
        private AudioClip victoryFanfareClip;
        private AudioClip defeatStingClip;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            audioSource = GetComponent<AudioSource>();
            audioSource.playOnAwake = false;

            GenerateProceduralClips();
        }

        private void Start()
        {
            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.OnRollCompleted += HandleDiceRolled;
            }
        }

        private void OnDestroy()
        {
            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.OnRollCompleted -= HandleDiceRolled;
            }
        }

        private void HandleDiceRolled(DayakattamRollResult roll)
        {
            PlayDiceClack();

            if (roll.isLucky)
            {
                // Auspicious Temple Bell Sting
                PlayTempleBell();
            }
        }

        public void PlayDiceClack()
        {
            if (diceClackClip != null)
            {
                audioSource.pitch = Random.Range(0.9f, 1.15f);
                audioSource.PlayOneShot(diceClackClip, 0.7f);
            }
        }

        public void PlayTempleBell()
        {
            if (templeBellClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(templeBellClip, 0.9f);
            }
        }

        public void PlayCaptureStrike()
        {
            if (captureStrikeClip != null)
            {
                audioSource.pitch = Random.Range(0.85f, 1.0f);
                audioSource.PlayOneShot(captureStrikeClip, 1.0f);
            }
        }

        public void PlayHop()
        {
            if (stepHopClip != null)
            {
                audioSource.pitch = Random.Range(1.1f, 1.3f);
                audioSource.PlayOneShot(stepHopClip, 0.4f);
            }
        }

        public void PlayVictoryFanfare()
        {
            if (victoryFanfareClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(victoryFanfareClip, 1.0f);
            }
        }

        public void PlayDefeatSting()
        {
            if (defeatStingClip != null)
            {
                audioSource.pitch = 1.0f;
                audioSource.PlayOneShot(defeatStingClip, 0.95f);
            }
        }

        private void GenerateProceduralClips()
        {
            diceClackClip = CreateDiceClackClip();
            templeBellClip = CreateTempleBellClip();
            captureStrikeClip = CreateCaptureStrikeClip();
            stepHopClip = CreateHopClip();
            victoryFanfareClip = CreateVictoryFanfareClip();
            defeatStingClip = CreateDefeatStingClip();
        }

        private AudioClip CreateDiceClackClip()
        {
            int sampleRate = 44100;
            float duration = 0.22f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // High metallic resonance + noise transient
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 35f);
                float ring1 = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 25f);
                float ring2 = Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 40f);
                samples[i] = (noise * 0.4f + ring1 * 0.4f + ring2 * 0.2f);
            }

            AudioClip clip = AudioClip.Create("DiceClack", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateTempleBellClip()
        {
            int sampleRate = 44100;
            float duration = 1.6f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // Auspicious bell harmonics: 520Hz, 1045Hz, 1580Hz, 2600Hz
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 2.8f);
                float fundamental = Mathf.Sin(2f * Mathf.PI * 520f * t);
                float overtone1 = Mathf.Sin(2f * Mathf.PI * 1045f * t) * 0.6f;
                float overtone2 = Mathf.Sin(2f * Mathf.PI * 1580f * t) * 0.35f;
                float overtone3 = Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.15f;

                samples[i] = (fundamental + overtone1 + overtone2 + overtone3) * decay * 0.5f;
            }

            AudioClip clip = AudioClip.Create("TempleBell", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateCaptureStrikeClip()
        {
            int sampleRate = 44100;
            float duration = 0.45f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float punch = Mathf.Sin(2f * Mathf.PI * 120f * (1f - t * 2f)) * Mathf.Exp(-t * 15f);
                float metalSlice = Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 20f);
                float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 25f);

                samples[i] = Mathf.Clamp(punch * 0.7f + metalSlice * 0.3f + noise * 0.2f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("CaptureStrike", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateHopClip()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float thud = Mathf.Sin(2f * Mathf.PI * 240f * t) * Mathf.Exp(-t * 50f);
                samples[i] = thud * 0.3f;
            }

            AudioClip clip = AudioClip.Create("PawnHop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateVictoryFanfareClip()
        {
            int sampleRate = 44100;
            float duration = 2.4f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            // Rising temple bells arpeggio: 520Hz -> 650Hz -> 780Hz -> 1040Hz
            float[] chordFreqs = new float[] { 520f, 650f, 780f, 1040f };
            float[] noteTimes = new float[] { 0.0f, 0.28f, 0.56f, 0.84f };

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float val = 0f;

                for (int n = 0; n < chordFreqs.Length; n++)
                {
                    if (t >= noteTimes[n])
                    {
                        float dt = t - noteTimes[n];
                        float decay = Mathf.Exp(-dt * 2.0f);
                        float tone = Mathf.Sin(2f * Mathf.PI * chordFreqs[n] * dt);
                        float harm = Mathf.Sin(2f * Mathf.PI * chordFreqs[n] * 2f * dt) * 0.4f;
                        val += (tone + harm) * decay * 0.35f;
                    }
                }

                samples[i] = Mathf.Clamp(val, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("VictoryFanfare", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private AudioClip CreateDefeatStingClip()
        {
            int sampleRate = 44100;
            float duration = 2.0f;
            int sampleCount = (int)(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 1.8f);
                float gong = Mathf.Sin(2f * Mathf.PI * (110f - t * 25f) * t) * 0.6f;
                float dissonance = Mathf.Sin(2f * Mathf.PI * 155f * t) * 0.3f;
                float sub = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.4f;

                samples[i] = Mathf.Clamp((gong + dissonance + sub) * decay * 0.7f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("DefeatSting", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
