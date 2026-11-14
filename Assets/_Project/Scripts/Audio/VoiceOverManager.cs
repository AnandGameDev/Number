using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Tutorial;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DayakattamDungeon.Audio
{
    /// <summary>
    /// Dedicated Voice-Over (Grandmother / Spirit Guide) audio channel for Dayakattam Dungeon.
    /// Operates on an independent AudioSource with clean interruption of older overlapping lines,
    /// cooldown management for guidance callouts, and integration with the Audio Toggle.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class VoiceOverManager : MonoBehaviour
    {
        private static VoiceOverManager instance;
        public static VoiceOverManager Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<VoiceOverManager>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Audio Output & Settings")]
        [SerializeField] private AudioSource audioSource;
        [Range(0f, 1f)] [SerializeField] private float voVolume = 1.0f;
        [SerializeField] private bool isMuted = false;
        [SerializeField] private float clickPawnPromptCooldown = 20.0f;

        [Header("Lucky Number Voice Clips")]
        [SerializeField] private AudioClip clipNo1;
        [SerializeField] private AudioClip clipNo5;
        [SerializeField] private AudioClip clipNo6;
        [SerializeField] private AudioClip clipNo12;
        [SerializeField] private AudioClip clipLuckyNumbers; // "15612 luckyno.ogg"

        [Header("Turn & Action Prompt Voice Clips")]
        [SerializeField] private AudioClip clipPressSpaceToRoll; // "PressSpacetorollthayamsticks.ogg"
        [SerializeField] private AudioClip clipClickPawnToMove; // "Click pawn to move.ogg"
        [SerializeField] private AudioClip clipYourTurn; // "yourturn.ogg"
        [SerializeField] private AudioClip clipNeedRollThayam; // "need to roll a thayam to enter the game.ogg"

        [Header("Board & Combat Voice Clips")]
        [SerializeField] private AudioClip clipMalaiSafeAtLast; // "malai Safe at last.ogg"
        [SerializeField] private AudioClip clipNiceCut; // "nicecut.ogg"
        [SerializeField] private AudioClip clipAhOhh; // "ah ohh.ogg"
        [SerializeField] private AudioClip clipAyyo; // "ayyo.ogg"
        [SerializeField] private AudioClip clipPalamOneStepCloser; // "palam one step closer to victory.ogg"

        [Header("Victory Voice Clips")]
        [SerializeField] private AudioClip clipYouWon; // "you won.ogg"
        [SerializeField] private AudioClip clipYouHaveConquered; // "youhave conqured the thayam game.ogg"

        // Match-scoped prompt tracking: ensures turn & action prompts only play once per match
        private bool hasPlayedTurnPromptThisMatch = false;
        private bool hasPlayedActionPromptThisMatch = false;
        private bool hasPlayedNeedThayamThisMatch = false;
        private Coroutine activePlaybackRoutine;

        public bool IsMuted => isMuted;
        public AudioSource Source => audioSource;

        /// <summary>
        /// Resets match-scoped prompt flags so turn and action prompts can trigger for the new match.
        /// </summary>
        public void ResetMatchPrompts()
        {
            hasPlayedTurnPromptThisMatch = false;
            hasPlayedActionPromptThisMatch = false;
            hasPlayedNeedThayamThisMatch = false;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.loop = false;
                audioSource.spatialBlend = 0f; // Pure 2D VO
            }
        }

        private void Start()
        {
            // Sync with AudioListener volume
            if (AudioListener.volume <= 0f)
            {
                SetMuted(true);
            }
        }

        public void SetMuted(bool muted)
        {
            isMuted = muted;
            if (audioSource != null)
            {
                audioSource.mute = muted;
                if (muted && audioSource.isPlaying)
                {
                    audioSource.Stop();
                }
            }
        }

        /// <summary>
        /// Plays a voice-over clip immediately, interrupting any currently playing voice line.
        /// </summary>
        public void PlayClip(AudioClip clip)
        {
            if (isMuted || clip == null) return;

            if (activePlaybackRoutine != null)
            {
                StopCoroutine(activePlaybackRoutine);
                activePlaybackRoutine = null;
            }

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) return;

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = voVolume;
            audioSource.pitch = 1.0f;
            audioSource.Play();
        }

        /// <summary>
        /// Hook for Lucky Number Rolls (1, 5, 6, 12).
        /// </summary>
        public void PlayLuckyRollVoice(int roll)
        {
            switch (roll)
            {
                case 1:
                    PlayClip(clipNo1);
                    break;
                case 5:
                    PlayClip(clipNo5);
                    break;
                case 6:
                    PlayClip(clipNo6);
                    break;
                case 12:
                    PlayClip(clipNo12);
                    break;
            }
        }

        /// <summary>
        /// Hook for Start of Player 1's Turn:
        /// Plays 'yourturn.ogg' once per match after pressing Play Duel.
        /// </summary>
        public void PlayPlayerTurnPrompt(bool ignoreMatchLimit = false)
        {
            if (DayakattamDungeon.UI.GameHUD.Instance != null && DayakattamDungeon.UI.GameHUD.Instance.IsTitleScreenActive)
            {
                return;
            }
            if (!ignoreMatchLimit && hasPlayedTurnPromptThisMatch)
            {
                return;
            }
            if (!ignoreMatchLimit)
            {
                hasPlayedTurnPromptThisMatch = true;
            }

            PlayClip(clipYourTurn);
        }

        /// <summary>
        /// Hook for Entering P1_ActionPhase:
        /// Plays 'Click pawn to move.ogg' only once per match.
        /// </summary>
        public void PlayClickPawnPrompt(bool ignoreMatchLimit = false)
        {
            if (!ignoreMatchLimit && hasPlayedActionPromptThisMatch)
            {
                return;
            }
            if (!ignoreMatchLimit)
            {
                hasPlayedActionPromptThisMatch = true;
            }

            PlayClip(clipClickPawnToMove);
        }

        /// <summary>
        /// Hook when player finishes rolling with NO 1 in pool and all pawns are locked in reserve.
        /// Plays only once per match.
        /// </summary>
        public void PlayNeedThayamToEnter(bool ignoreMatchLimit = false)
        {
            if (!ignoreMatchLimit && hasPlayedNeedThayamThisMatch)
            {
                return;
            }
            if (!ignoreMatchLimit)
            {
                hasPlayedNeedThayamThisMatch = true;
            }

            PlayClip(clipNeedRollThayam);
        }

        /// <summary>
        /// Hook when a player pawn lands on a crossed Safe Tile (Malai).
        /// </summary>
        public void PlayMalaiSafeAtLast()
        {
            PlayClip(clipMalaiSafeAtLast);
        }

        /// <summary>
        /// Hook when player pawn captures/cuts an Opponent Bot pawn.
        /// </summary>
        public void PlayPlayerCapture()
        {
            PlayClip(clipNiceCut);
        }

        /// <summary>
        /// Hook when Opponent Bot pawn captures/cuts a Player pawn:
        /// Randomly selects 50/50 between 'ah ohh.ogg' and 'ayyo.ogg'.
        /// </summary>
        public void PlayOpponentCapture()
        {
            AudioClip chosen = (UnityEngine.Random.value < 0.5f) ? clipAhOhh : clipAyyo;
            PlayClip(chosen);
        }

        /// <summary>
        /// Hook when a Player pawn reaches the center Palace square before match end.
        /// </summary>
        public void PlayPalamOneStepCloser()
        {
            PlayClip(clipPalamOneStepCloser);
        }

        /// <summary>
        /// Hook when Player 1 wins the match:
        /// Plays 'you won.ogg' followed immediately by 'youhave conqured the thayam game.ogg'.
        /// </summary>
        public void PlayVictorySequence()
        {
            if (activePlaybackRoutine != null)
            {
                StopCoroutine(activePlaybackRoutine);
            }
            activePlaybackRoutine = StartCoroutine(VictorySequenceRoutine());
        }

        private IEnumerator VictorySequenceRoutine()
        {
            if (clipYouWon != null)
            {
                PlayClip(clipYouWon);
                yield return new WaitForSeconds(clipYouWon.length + 0.3f);
            }

            if (clipYouHaveConquered != null)
            {
                PlayClip(clipYouHaveConquered);
            }
        }

        // Tutorial Onboarding specific hooks
        public void PlayTutorialStep1() => PlayClip(clipPressSpaceToRoll);
        public void PlayTutorialStep2() => PlayClip(clipLuckyNumbers);
        public void PlayTutorialStep3() => PlayClickPawnPrompt(ignoreMatchLimit: true);

#if UNITY_EDITOR
        [ContextMenu("Auto Assign Audio Clips")]
        public void AutoAssignClips()
        {
            Undo.RecordObject(this, "Auto Assign VO Clips");

            clipNo1 = FindAudioClip("no1");
            clipNo5 = FindAudioClip("no5");
            clipNo6 = FindAudioClip("no6");
            clipNo12 = FindAudioClip("no12");
            clipLuckyNumbers = FindAudioClip("15612 luckyno");
            clipPressSpaceToRoll = FindAudioClip("PressSpacetorollthayamsticks");
            clipClickPawnToMove = FindAudioClip("Click pawn to move");
            clipYourTurn = FindAudioClip("yourturn");
            clipNeedRollThayam = FindAudioClip("need to roll a thayam to enter the game");
            clipMalaiSafeAtLast = FindAudioClip("malai Safe at last");
            clipNiceCut = FindAudioClip("nicecut");
            clipAhOhh = FindAudioClip("ah ohh");
            clipAyyo = FindAudioClip("ayyo");
            clipPalamOneStepCloser = FindAudioClip("palam one step closer to victory");
            clipYouWon = FindAudioClip("you won");
            clipYouHaveConquered = FindAudioClip("youhave conqured the thayam game");

            EditorUtility.SetDirty(this);
            Debug.Log("[VoiceOverManager] Auto-assignment completed for all 16 audio clips!");
        }

        private static AudioClip FindAudioClip(string fileNameWithoutExt)
        {
            string path = $"Assets/_Project/Audio/VO/{fileNameWithoutExt}.ogg";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;

            path = $"Assets/_Project/Voice over/{fileNameWithoutExt}.ogg";
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;

            string[] guids = AssetDatabase.FindAssets("t:AudioClip");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string fn = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (string.Equals(fn, fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
                {
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                }
            }
            return null;
        }
#endif
    }
}
