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
        IPointerClickHandler, IPointerDownHandler
    {
        private static float lastRollTimestamp = -1f;
        private static int lastRollFrame = -1;

        /// <summary>
        /// Checks if Player 1 is currently in a valid rolling phase.
        /// Matches the exact conditions used by the UI RollDiceButton.
        /// </summary>
        public static bool CanPlayerRoll()
        {
            if (TurnAndStreakManager.Instance == null) return false;
            if (DayakattamDungeon.UI.GameHUD.Instance != null && DayakattamDungeon.UI.GameHUD.Instance.IsTitleScreenActive) return false;
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
            // Input captured
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Vector3 worldPos = transform.position;
            if (eventData != null && eventData.pointerCurrentRaycast.isValid)
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

            // Trigger juice sparks at tap location
            if (GameJuiceManager.Instance != null)
            {
                GameJuiceManager.Instance.SpawnImpactSparks(tapWorldPos + Vector3.up * 0.1f, new Color(1f, 0.88f, 0.25f), 10);
            }

            // Execute roll identically to UI roll button
            TurnAndStreakManager.Instance.RequestRoll();
        }
    }
}
