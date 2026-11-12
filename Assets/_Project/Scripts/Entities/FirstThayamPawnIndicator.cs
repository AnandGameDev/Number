using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using DayakattamDungeon.Core;
using DayakattamDungeon.Board;

namespace DayakattamDungeon.Entities
{
    /// <summary>
    /// Displays an intuitive, impossible-to-miss visual indicator pointing to the resting pawn
    /// when the player rolls their first 1 (Thayam), guiding them to touch/click the pawn
    /// to awaken it from reserve onto the board.
    /// </summary>
    [RequireComponent(typeof(PawnController))]
    public class FirstThayamPawnIndicator : MonoBehaviour
    {
        private PawnController pawn;
        private GameObject indicatorRoot;
        private RectTransform billboardRect;
        private TextMeshPro promptText;
        private GameObject groundPulseRing;
        private Material ringMaterial;

        private Vector3 initialLocalScale;
        private bool isIndicatorActive = false;
        private static bool hasAwakenedFirstPawnThisMatch = false;

        public static bool HasAwakenedFirstPawn => hasAwakenedFirstPawnThisMatch;

        public static void ResetMatchFlag()
        {
            hasAwakenedFirstPawnThisMatch = false;
        }

        private void Awake()
        {
            pawn = GetComponent<PawnController>();
            initialLocalScale = transform.localScale;
            CreateIndicatorVisuals();
        }

        private void Start()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnRollPoolUpdated += HandleRollPoolUpdated;
                TurnAndStreakManager.Instance.OnStateChanged += HandleStateChanged;
            }
            pawn.OnPawnReachedPalace += _ => HideIndicator();
        }

        private void OnDestroy()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnRollPoolUpdated -= HandleRollPoolUpdated;
                TurnAndStreakManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void CreateIndicatorVisuals()
        {
            if (indicatorRoot != null) return;

            indicatorRoot = new GameObject("FirstThayam_PromptIndicator");
            indicatorRoot.transform.SetParent(transform, false);
            indicatorRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f);

            // 1. Floating 3D Billboard Text with Bouncing Arrow
            GameObject textGO = new GameObject("PromptText");
            textGO.transform.SetParent(indicatorRoot.transform, false);
            textGO.transform.localPosition = Vector3.zero;

            promptText = textGO.AddComponent<TextMeshPro>();
            promptText.text = "<b><size=130%><color=#FFDD44>▼</color></size></b>\n<b><color=#FFD700>TOUCH PAWN</color></b>\n<size=75%><color=#FFFFFF>TO ENTER</color></size>";
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 1.35f;
            promptText.color = Color.white;
            promptText.enableWordWrapping = false;

            // Load custom font if available
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bilderberg OTF SDF")
                                ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Beyond Wonderland SDF")
                                ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font != null) promptText.font = font;

            billboardRect = textGO.GetComponent<RectTransform>();

            // 2. Pulsing golden ground beacon ring at pawn base
            groundPulseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            groundPulseRing.name = "GroundPulseRing";
            groundPulseRing.transform.SetParent(transform, false);
            groundPulseRing.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            groundPulseRing.transform.localScale = new Vector3(0.95f, 0.015f, 0.95f);

            var col = groundPulseRing.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            var rend = groundPulseRing.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            ringMaterial = new Material(shader);
            ringMaterial.color = new Color(1.0f, 0.85f, 0.2f, 0.75f);
            rend.sharedMaterial = ringMaterial;

            indicatorRoot.SetActive(false);
            groundPulseRing.SetActive(false);
        }

        private void Update()
        {
            if (!isIndicatorActive) return;

            // Camera Billboarding (perpendicular to screen)
            if (Camera.main != null && indicatorRoot != null)
            {
                indicatorRoot.transform.rotation = Camera.main.transform.rotation;
            }

            // Animated vertical bobbing
            float bobOffset = Mathf.Sin(Time.time * 4.5f) * 0.08f;
            if (indicatorRoot != null)
            {
                indicatorRoot.transform.localPosition = new Vector3(0f, 1.15f + bobOffset, 0f);
            }

            // Pawn subtle excited breathing/hop animation
            float pawnPulse = 1.0f + Mathf.PingPong(Time.time * 3.5f, 0.06f);
            transform.localScale = new Vector3(initialLocalScale.x * pawnPulse, initialLocalScale.y * (1.0f + bobOffset * 0.3f), initialLocalScale.z * pawnPulse);

            // Pulsing golden ring scale and alpha
            if (groundPulseRing != null)
            {
                float ringScale = 0.95f + Mathf.PingPong(Time.time * 2.8f, 0.2f);
                groundPulseRing.transform.localScale = new Vector3(ringScale, 0.015f, ringScale);
            }

            // Dismiss automatically if pawn is no longer in reserve
            if (pawn.State != PawnState.InReserve)
            {
                hasAwakenedFirstPawnThisMatch = true;
                HideIndicator();
            }
        }

        private void HandleRollPoolUpdated(System.Collections.Generic.IReadOnlyList<int> pool)
        {
            EvaluateIndicatorState();
        }

        private void HandleStateChanged(GameState state)
        {
            EvaluateIndicatorState();
        }

        public void EvaluateIndicatorState()
        {
            if (pawn == null || pawn.Owner != PlayerType.Player1)
            {
                HideIndicator();
                return;
            }

            if (pawn.State != PawnState.InReserve)
            {
                HideIndicator();
                return;
            }

            if (TurnAndStreakManager.Instance == null)
            {
                HideIndicator();
                return;
            }

            bool isP1Turn = TurnAndStreakManager.Instance.ActivePlayer == PlayerType.Player1;
            bool hasThayamRoll = TurnAndStreakManager.Instance.CurrentRollPool.Contains(1);

            // Only highlight the FIRST reserve pawn of Player 1 to avoid clutter
            bool isFirstReservePawn = false;
            var p1Pawns = TurnAndStreakManager.Instance.Player1Pawns;
            if (p1Pawns != null)
            {
                foreach (var p in p1Pawns)
                {
                    if (p != null && p.State == PawnState.InReserve)
                    {
                        isFirstReservePawn = (p == pawn);
                        break;
                    }
                }
            }

            // Show indicator if it's Player 1's turn, a 1 is available, and this is the first reserve pawn
            if (isP1Turn && hasThayamRoll && isFirstReservePawn && !hasAwakenedFirstPawnThisMatch)
            {
                ShowIndicator();
            }
            else
            {
                HideIndicator();
            }
        }

        public void ShowIndicator()
        {
            if (isIndicatorActive) return;
            isIndicatorActive = true;

            if (indicatorRoot != null) indicatorRoot.SetActive(true);
            if (groundPulseRing != null) groundPulseRing.SetActive(true);

            // Also trigger pawn selection glow
            pawn.SetSelected(true);
        }

        public void HideIndicator()
        {
            if (!isIndicatorActive) return;
            isIndicatorActive = false;

            if (indicatorRoot != null) indicatorRoot.SetActive(false);
            if (groundPulseRing != null) groundPulseRing.SetActive(false);

            transform.localScale = initialLocalScale;
            pawn.SetSelected(false);
        }

        public void OnPawnAwakened()
        {
            hasAwakenedFirstPawnThisMatch = true;
            HideIndicator();
        }
    }
}
