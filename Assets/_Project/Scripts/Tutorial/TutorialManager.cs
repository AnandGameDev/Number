using System;
using System.Collections;
using UnityEngine;
using DayakattamDungeon.Board;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;

namespace DayakattamDungeon.Tutorial
{
    public enum TutorialStep
    {
        NotStarted,
        Step1_RollThayam,      // Force 1
        Step2_RollStreak,      // Force 12, then 3
        Step3_MoveToMalai,     // Spend 1, spend 3
        Step4_CaptureShadow,   // Spawn dummy bot, spend 12 to strike
        Completed
    }

    /// <summary>
    /// Judge-friendly 60-second interactive onboarding tutorial.
    /// Guides the player through Dayakattam stick rules, Lucky streaks, Malai safe havens, and combat strikes.
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        private static TutorialManager instance;
        public static TutorialManager Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<TutorialManager>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Tutorial State")]
        [SerializeField] private TutorialStep currentStep = TutorialStep.NotStarted;
        [SerializeField] private bool isTutorialActive = false;

        [Header("References")]
        [SerializeField] private PawnController dummyBotPawn;

        // Events
        public event Action<TutorialStep, string> OnTutorialDialogueUpdated;
        public event Action OnTutorialCompleted;

        public TutorialStep CurrentStep => currentStep;
        public bool IsTutorialActive => isTutorialActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void StartTutorial()
        {
            isTutorialActive = true;
            currentStep = TutorialStep.Step1_RollThayam;

            // Prepare Step 1: Force roll to 1 (Thayam: 1 + 0)
            DayakattamRoller.Instance.ForceNextRoll(1, 0);

            string dialogue = "Grandma: 'Roll the sacred brass sticks, kanna! In Dayakattam, rolling 1 (Thayam) is required to awaken your spirit pawn onto the path. Auspicious rolls grant an extra roll!'";
            OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);

            DayakattamRoller.Instance.OnRollCompleted -= HandleTutorialDiceRoll;
            DayakattamRoller.Instance.OnRollCompleted += HandleTutorialDiceRoll;
        }

        private void HandleTutorialDiceRoll(DayakattamRollResult roll)
        {
            if (!isTutorialActive) return;

            if (currentStep == TutorialStep.Step1_RollThayam)
            {
                // Step 1 complete: Player rolled 1 (Thayam)
                currentStep = TutorialStep.Step2_RollStreak;

                // Rig next roll to 12 (0 + 0 = Pannirendu!)
                DayakattamRoller.Instance.ForceNextRoll(0, 0);

                string dialogue = "Grandma: 'Thayam awakened! Notice you received an Extra Roll! Roll again to trigger an Auspicious Streak!'";
                OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);
            }
            else if (currentStep == TutorialStep.Step2_RollStreak)
            {
                if (roll.total == 12)
                {
                    // Rig next roll to standard 3 (2 + 1) to transition to Action phase
                    DayakattamRoller.Instance.ForceNextRoll(2, 1);

                    string dialogue = "Grandma: 'PANNIRENDU! Both sticks showed 0, equaling 12! Your Combo Multiplier has surged to 2.0x! Roll once more!'";
                    OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);
                }
                else if (roll.total == 3)
                {
                    // Standard roll ended streak!
                    currentStep = TutorialStep.Step3_MoveToMalai;

                    string dialogue = "Grandma: 'A standard roll (3) ended the streak! In the Action Phase, click roll [1] to awaken your Spirit Pawn, then spend [3] to reach the Malai (Cross) Safe Haven!'";
                    OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);

                    StartCoroutine(MonitorStep3());
                }
            }
        }

        private IEnumerator MonitorStep3()
        {
            // Wait until player pawn has awakened and reached Step 3 (The Malai safe tile)
            var p1Pawns = TurnAndStreakManager.Instance.Player1Pawns;
            PawnController activePawn = (p1Pawns.Count > 0) ? p1Pawns[0] : null;

            while (isTutorialActive && activePawn != null && activePawn.CurrentPathStep < 3)
            {
                yield return new WaitForSeconds(0.2f);
            }

            if (!isTutorialActive) yield break;

            // Step 3 reached: Pawn is on Malai!
            currentStep = TutorialStep.Step4_CaptureShadow;

            // Spawn dummy bot pawn at step 15 (which is 12 steps ahead of step 3!)
            SpawnDummyBotAtStep(15);

            string dialogue = "Grandma: 'Safe tiles (Malai) grant absolute immunity! Now, I have placed one of my test tokens 12 steps ahead on an open tile. Click roll [12] to cut it with your 2.0x Combo!'";
            OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);

            StartCoroutine(MonitorStep4(activePawn));
        }

        private void SpawnDummyBotAtStep(int pathStep)
        {
            if (dummyBotPawn == null)
            {
                // Create a temporary greybox dummy pawn
                GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dummy.name = "Tutorial_Dummy_Grandma";
                dummy.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                dummyBotPawn = dummy.AddComponent<PawnController>();
                dummyBotPawn.Initialize(PlayerType.Bot, 999, Vector3.up * 10f);
            }

            BoardNode targetNode = ThayamBoardGrid.Instance.GetNodeAtStep(PlayerType.Player1, pathStep);
            if (targetNode != null)
            {
                dummyBotPawn.transform.position = targetNode.WorldPosition + Vector3.up * 0.2f;
                targetNode.RegisterOccupant(dummyBotPawn);
            }
        }

        private IEnumerator MonitorStep4(PawnController p1Pawn)
        {
            while (isTutorialActive && p1Pawn != null && p1Pawn.CurrentPathStep < 15)
            {
                yield return new WaitForSeconds(0.2f);
            }

            if (!isTutorialActive) yield break;

            // Opponent pawn struck!
            currentStep = TutorialStep.Completed;
            string dialogue = "Grandma: 'Splendid strike, kanna! When you cut a token, it is sent all the way back to reserve! The first to guide all pawns into the Central Palace wins. Let us duel!'";
            OnTutorialDialogueUpdated?.Invoke(currentStep, dialogue);

            yield return new WaitForSeconds(3.0f);
            FinishTutorial();
        }

        public void SkipTutorial()
        {
            Debug.Log("[TutorialManager] Tutorial Skipped by user.");
            FinishTutorial();
        }

        private void FinishTutorial()
        {
            isTutorialActive = false;
            currentStep = TutorialStep.Completed;

            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.OnRollCompleted -= HandleTutorialDiceRoll;
            }

            if (dummyBotPawn != null)
            {
                if (dummyBotPawn.CurrentNode != null)
                {
                    dummyBotPawn.CurrentNode.UnregisterOccupant(dummyBotPawn);
                }
                Destroy(dummyBotPawn.gameObject);
                dummyBotPawn = null;
            }

            OnTutorialCompleted?.Invoke();
            // Start fresh match
            TurnAndStreakManager.Instance.InitializeMatch(startWithTutorial: false);
        }
    }
}
