using System;
using UnityEngine;
using DayakattamDungeon.Core;

namespace DayakattamDungeon.Dice
{
    [System.Serializable]
    public struct DayakattamRollResult
    {
        public int stickA;        // 0, 1, 2, 3
        public int stickB;        // 0, 1, 2, 3
        public int total;         // 1..6 or 12
        public bool isLucky;      // 1, 5, 6, 12
        public string rollName;   // e.g. "Thayam", "Pannirendu"

        public DayakattamRollResult(int a, int b, int sum, bool lucky, string name)
        {
            stickA = a;
            stickB = b;
            total = sum;
            isLucky = lucky;
            rollName = name;
        }
    }

    /// <summary>
    /// Simulates two traditional Tamil 4-sided oblong stick dice (Dayakattam).
    /// Faces: 0, 1, 2, 3.
    /// Special Rule: 0 + 0 = 12 (Pannirendu).
    /// Lucky Numbers: 1 (Thayam), 5, 6, 12.
    /// </summary>
    public class DayakattamRoller : MonoBehaviour
    {
        private static DayakattamRoller instance;
        public static DayakattamRoller Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<DayakattamRoller>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Physical Stick References (Optional Visuals)")]
        [SerializeField] private Transform stickAVisual;
        [SerializeField] private Transform stickBVisual;
        [SerializeField] private DayakattamStickVisual stickAController;
        [SerializeField] private DayakattamStickVisual stickBController;

        [Header("Audio & Feedback")]
        [SerializeField] private AudioSource audioSource;

        public event Action<DayakattamRollResult> OnRollCompleted;

        private bool hasForcedRoll = false;
        private int forcedStickA = 0;
        private int forcedStickB = 0;

        public bool IsTumbling
        {
            get
            {
                if (stickAController != null && stickAController.IsTumbling) return true;
                if (stickBController != null && stickBController.IsTumbling) return true;
                return false;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureCollidersAndTapHandlers();
        }

        private void Start()
        {
            EnsureCollidersAndTapHandlers();
        }

        /// <summary>
        /// Ensures all 3D dice objects (Stick_A, Stick_B, DiceTray, Floor, rims) have
        /// appropriate BoxColliders and DayakattamDiceTapHandler components for touch/click rolling.
        /// </summary>
        public void EnsureCollidersAndTapHandlers()
        {
            // 1. Sticks
            if (stickAVisual != null)
            {
                if (stickAVisual.localScale.sqrMagnitude < 0.01f)
                {
                    stickAVisual.localScale = DayakattamStickVisual.AuthenticStickScale;
                }
                EnsureColliderAndHandler(stickAVisual.gameObject);
            }
            if (stickBVisual != null)
            {
                if (stickBVisual.localScale.sqrMagnitude < 0.01f)
                {
                    stickBVisual.localScale = DayakattamStickVisual.AuthenticStickScale;
                }
                EnsureColliderAndHandler(stickBVisual.gameObject);
            }

            // 2. Dice Tray & Floor
            Transform tray = transform.Find("DiceTray");
            if (tray != null)
            {
                var trayCol = tray.GetComponent<BoxCollider>();
                if (trayCol == null)
                {
                    trayCol = tray.gameObject.AddComponent<BoxCollider>();
                }
                trayCol.center = new Vector3(0f, 0.3f, 0f);
                trayCol.size = new Vector3(2.8f, 0.8f, 3.8f);

                if (tray.GetComponent<DayakattamDiceTapHandler>() == null)
                {
                    tray.gameObject.AddComponent<DayakattamDiceTapHandler>();
                }

                Transform floor = tray.Find("Floor");
                if (floor != null)
                {
                    EnsureColliderAndHandler(floor.gameObject);
                }

                foreach (Transform child in tray)
                {
                    if (child.name.StartsWith("DiceTray_Rim"))
                    {
                        EnsureColliderAndHandler(child.gameObject);
                    }
                }
            }

            // 3. Holder root
            if (GetComponent<DayakattamDiceTapHandler>() == null)
            {
                gameObject.AddComponent<DayakattamDiceTapHandler>();
            }
        }

        private void EnsureColliderAndHandler(GameObject go)
        {
            if (go == null) return;
            if (go.GetComponent<Collider>() == null)
            {
                go.AddComponent<BoxCollider>();
            }
            if (go.GetComponent<DayakattamDiceTapHandler>() == null)
            {
                go.AddComponent<DayakattamDiceTapHandler>();
            }
        }

        /// <summary>
        /// Triggers visual feedback on sticks when tapping on the tray or dice holder.
        /// </summary>
        public void TriggerTapReaction()
        {
            if (GameJuiceManager.Instance != null && stickAVisual != null)
            {
                GameJuiceManager.Instance.SpawnImpactSparks(stickAVisual.position + Vector3.up * 0.1f, new Color(1f, 0.88f, 0.25f), 6);
            }
        }

        /// <summary>
        /// Rig the next roll for deterministic tutorial steps or testing.
        /// </summary>
        public void ForceNextRoll(int stickA, int stickB)
        {
            hasForcedRoll = true;
            forcedStickA = Mathf.Clamp(stickA, 0, 3);
            forcedStickB = Mathf.Clamp(stickB, 0, 3);
        }

        /// <summary>
        /// Executes a roll of the two Dayakattam stick dice.
        /// </summary>
        public DayakattamRollResult RollDice()
        {
            int stickA;
            int stickB;

            if (hasForcedRoll)
            {
                stickA = forcedStickA;
                stickB = forcedStickB;
                hasForcedRoll = false;
            }
            else
            {
                stickA = UnityEngine.Random.Range(0, 4); // 0, 1, 2, 3
                stickB = UnityEngine.Random.Range(0, 4); // 0, 1, 2, 3
            }

            int total;
            string rollName;

            // Dayakattam Core Rule:
            // 0 + 0 = 12 (Pannirendu)
            // Any other combination = sum of both sticks (1, 2, 3, 4, 5, 6)
            if (stickA == 0 && stickB == 0)
            {
                total = 12;
                rollName = "Pannirendu (12)";
            }
            else
            {
                total = stickA + stickB;
                switch (total)
                {
                    case 1: rollName = "Thayam (1)"; break;
                    case 2: rollName = "Rendu (2)"; break;
                    case 3: rollName = "Moondru (3)"; break;
                    case 4: rollName = "Naangu (4)"; break;
                    case 5: rollName = "Ainthu (5)"; break;
                    case 6: rollName = "Aaru (6)"; break;
                    default: rollName = total.ToString(); break;
                }
            }

            bool isLucky = IsLuckyNumber(total);
            DayakattamRollResult result = new DayakattamRollResult(stickA, stickB, total, isLucky, rollName);

            // Animate stick dice in 3D if assigned
            AnimateDiceVisuals(stickA, stickB);

            if (isLucky && GameJuiceManager.Instance != null)
            {
                GameJuiceManager.Instance.TriggerShake(total == 12 ? 0.28f : 0.16f);
                GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.5f, rollName + "!", Color.yellow, 1.25f);
                GameJuiceManager.Instance.SpawnImpactSparks(transform.position, Color.yellow, 16);
            }

            OnRollCompleted?.Invoke(result);
            return result;
        }

        /// <summary>
        /// In Dayakattam, 1 (Thayam), 5, 6, and 12 trigger an Auspicious Streak.
        /// </summary>
        public static bool IsLuckyNumber(int number)
        {
            return number == 1 || number == 5 || number == 6 || number == 12;
        }

        private void AnimateDiceVisuals(int stickA, int stickB)
        {
            if (stickAController == null && stickAVisual != null)
            {
                stickAController = stickAVisual.GetComponent<DayakattamStickVisual>();
            }
            if (stickBController == null && stickBVisual != null)
            {
                stickBController = stickBVisual.GetComponent<DayakattamStickVisual>();
            }

            if (stickAController != null)
            {
                stickAController.TumbleToFace(stickA);
            }
            else if (stickAVisual != null)
            {
                stickAVisual.localRotation = Quaternion.Euler(stickA * 90f, UnityEngine.Random.Range(-15f, 15f), 0f);
            }

            if (stickBController != null)
            {
                stickBController.TumbleToFace(stickB);
            }
            else if (stickBVisual != null)
            {
                stickBVisual.localRotation = Quaternion.Euler(stickB * 90f, UnityEngine.Random.Range(-15f, 15f), 0f);
            }
        }
    }
}
