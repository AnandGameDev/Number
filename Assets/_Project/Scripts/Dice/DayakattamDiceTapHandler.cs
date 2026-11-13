using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using DayakattamDungeon.Board;
using DayakattamDungeon.Core;
using DayakattamDungeon.Tutorial;

namespace DayakattamDungeon.Dice
{
    /// <summary>
    /// Enables direct 3D mouse click and touch tap interaction on the Dayakattam dice holder,
    /// brass sticks, and wooden rolling tray to roll the dice just like the UI roll button.
    /// Supports Unity EventSystem (IPointerClickHandler) and direct physics (OnMouseDown).
    /// </summary>
    public class DayakattamDiceTapHandler : MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static float lastRollTimestamp = -1f;
        private static int lastRollFrame = -1;

        private Vector3 defaultLocalScale;
        private Coroutine activeJuiceRoutine;
        private bool isHovered = false;

        private void Awake()
        {
            defaultLocalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (defaultLocalScale == Vector3.zero)
            {
                defaultLocalScale = transform.localScale;
            }
        }

        /// <summary>
        /// Checks if Player 1 is currently in a valid rolling phase.
        /// Matches the exact conditions used by the UI RollDiceButton.
        /// </summary>
        public static bool CanPlayerRoll()
        {
            if (TurnAndStreakManager.Instance == null) return false;
            if (TurnAndStreakManager.Instance.ActivePlayer != PlayerType.Player1) return false;

            var state = TurnAndStreakManager.Instance.CurrentState;
            bool isP1Rolling = (state == GameState.P1_RollingPhase ||
                               (state == GameState.TutorialIntro && TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive &&
                               (TutorialManager.Instance.CurrentStep == TutorialStep.Step1_RollThayam ||
                                TutorialManager.Instance.CurrentStep == TutorialStep.Step2_RollStreak)));

            if (!isP1Rolling) return false;

            // Prevent rolling while the sticks are actively tumbling mid-air
            if (DayakattamRoller.Instance != null && DayakattamRoller.Instance.IsTumbling)
            {
                return false;
            }

            return true;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (CanPlayerRoll())
            {
                // Tactile visual compression on press
                transform.localScale = defaultLocalScale * 0.94f;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Vector3 worldPos = transform.position;
            if (eventData.pointerCurrentRaycast.isValid)
            {
                worldPos = eventData.pointerCurrentRaycast.worldPosition;
            }
            TryExecuteRoll(worldPos);
        }

        private void OnMouseDown()
        {
            // Direct physics click fallback (for clicks outside EventSystem raycasts)
            TryExecuteRoll(transform.position);
        }

        private void OnMouseUp()
        {
            if (!isHovered)
            {
                transform.localScale = defaultLocalScale;
            }
            else
            {
                transform.localScale = defaultLocalScale * 1.06f;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            if (CanPlayerRoll())
            {
                transform.localScale = defaultLocalScale * 1.06f;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            transform.localScale = defaultLocalScale;
        }

        /// <summary>
        /// Attempts to trigger a roll on the TurnAndStreakManager.
        /// Debounces duplicate clicks within the same frame or within 250ms.
        /// </summary>
        public void TryExecuteRoll(Vector3 tapWorldPos)
        {
            if (Time.frameCount == lastRollFrame) return;
            if (Time.time - lastRollTimestamp < 0.25f) return;

            if (!CanPlayerRoll())
            {
                return;
            }

            lastRollFrame = Time.frameCount;
            lastRollTimestamp = Time.time;

            // Trigger juice bounce on tapped component
            TriggerJuiceBounce(tapWorldPos);

            // Also trigger subtle tactile bounce on the sticks if tray was tapped
            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.TriggerTapReaction();
            }

            // Execute roll identically to UI roll button
            TurnAndStreakManager.Instance.RequestRoll();
        }

        public void TriggerJuiceBounce(Vector3 tapWorldPos)
        {
            if (activeJuiceRoutine != null) StopCoroutine(activeJuiceRoutine);
            activeJuiceRoutine = StartCoroutine(JuiceBounceRoutine());

            if (GameJuiceManager.Instance != null)
            {
                GameJuiceManager.Instance.SpawnImpactSparks(tapWorldPos + Vector3.up * 0.1f, new Color(1f, 0.88f, 0.25f), 10);
            }
        }

        private IEnumerator JuiceBounceRoutine()
        {
            Vector3 targetPunch = defaultLocalScale * 1.12f;
            float elapsed = 0f;
            float duration = 0.16f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float scaleFactor = Mathf.Sin(t * Mathf.PI);
                transform.localScale = Vector3.Lerp(defaultLocalScale, targetPunch, scaleFactor);
                yield return null;
            }

            transform.localScale = isHovered ? defaultLocalScale * 1.06f : defaultLocalScale;
            activeJuiceRoutine = null;
        }
    }
}
