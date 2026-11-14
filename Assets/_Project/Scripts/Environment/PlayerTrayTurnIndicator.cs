using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using DayakattamDungeon.Core;
using DayakattamDungeon.Board;

namespace DayakattamDungeon.Environment
{
    /// <summary>
    /// Displays a clear, impossible-to-miss visual indication on the player sanctuary trays
    /// showing which player is currently taking their turn.
    /// Features:
    /// 1. Pulsing glowing border frame/aura around the active player's tray.
    /// 2. Floating 3D "ACTIVE TURN" badge hovering above the active player's tray.
    /// 3. Synchronization and dynamic scale pulse on world-space tray labels (P1Text, P2Text, P3Text, P4Text).
    /// </summary>
    public class PlayerTrayTurnIndicator : MonoBehaviour
    {
        private static PlayerTrayTurnIndicator instance;
        public static PlayerTrayTurnIndicator Instance
        {
            get
            {
                if (instance == null) instance = Object.FindFirstObjectByType<PlayerTrayTurnIndicator>();
                return instance;
            }
        }

        [Header("Pulsing Settings")]
        [Tooltip("Max extra scale multiplier added when active (e.g. 0.06 = pulses between 1.0x and 1.06x).")]
        [Range(0f, 0.3f)]
        [SerializeField] private float labelPulseAmount = 0.05f;

        [Tooltip("Speed of the pulse animation.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float labelPulseSpeed = 2.5f;

        private class TrayVisualData
        {
            public PlayerType player;
            public GameObject trayGO;
            public GameObject haloGO;
            public Material haloMaterial;
            public GameObject badgeGO;
            public TextMeshPro badgeText;
            public Color playerColor;
            public string badgeTitle;
            public TextMeshProUGUI hudLabel;
            public Vector3 initialLabelScale = Vector3.one;
        }

        private readonly List<TrayVisualData> trayVisuals = new List<TrayVisualData>();
        private TMP_FontAsset badgeFont;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            badgeFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF")
                        ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Bilderberg OTF SDF")
                        ?? Resources.Load<TMP_FontAsset>("Fonts & Materials/Beyond Wonderland SDF");
        }

        private void Start()
        {
            InitializeTrays();

            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged += HandleStateChanged;
            }

            UpdateActiveTrayVisuals();
        }

        private void OnDestroy()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        public void InitializeTrays()
        {
            trayVisuals.Clear();

            // Locate the 4 Sanctuary Trays
            GameObject p1Tray = GameObject.Find("P1_Sanctuary_Tray");
            GameObject p2Tray = GameObject.Find("P2_Sanctuary_Tray");
            GameObject botTray = GameObject.Find("Bot_Sanctuary_Tray");
            GameObject p4Tray = GameObject.Find("P4_Sanctuary_Tray");

            // Locate World-space HUD Labels
            TextMeshProUGUI p1Label = GameObject.Find("P1Text")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI p2Label = GameObject.Find("P2Text")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI p3Label = GameObject.Find("P3Text")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI p4Label = GameObject.Find("P4Text")?.GetComponent<TextMeshProUGUI>();

            // Setup Data
            if (p1Tray != null)
            {
                trayVisuals.Add(CreateTrayVisual(
                    PlayerType.Player1,
                    p1Tray,
                    new Color(1f, 0.32f, 0.25f),
                    "<b><color=#FFD700><size=115%>YOUR TURN</size></color></b>",
                    p1Label
                ));
            }

            if (p2Tray != null)
            {
                trayVisuals.Add(CreateTrayVisual(
                    PlayerType.Player2,
                    p2Tray,
                    new Color(0.35f, 0.70f, 1f),
                    "<b><color=#55AAFF><size=115%>MAMA'S TURN</size></color></b>",
                    p2Label
                ));
            }

            if (botTray != null)
            {
                trayVisuals.Add(CreateTrayVisual(
                    PlayerType.Bot,
                    botTray,
                    new Color(0.25f, 0.95f, 0.55f),
                    "<b><color=#44FF88><size=115%>GRANDMA'S TURN</size></color></b>",
                    p3Label
                ));
            }

            if (p4Tray != null)
            {
                trayVisuals.Add(CreateTrayVisual(
                    PlayerType.Player4,
                    p4Tray,
                    new Color(1f, 0.88f, 0.2f),
                    "<b><color=#FFDD44><size=115%>DADA'S TURN</size></color></b>",
                    p4Label
                ));
            }
        }

        private TrayVisualData CreateTrayVisual(PlayerType player, GameObject trayGO, Color color, string badgeTitle, TextMeshProUGUI hudLabel)
        {
            var data = new TrayVisualData
            {
                player = player,
                trayGO = trayGO,
                playerColor = color,
                badgeTitle = badgeTitle,
                hudLabel = hudLabel
            };

            if (hudLabel != null)
            {
                Vector3 s = hudLabel.transform.localScale;
                if (float.IsNaN(s.x) || float.IsNaN(s.y) || float.IsNaN(s.z) ||
                    float.IsInfinity(s.x) || float.IsInfinity(s.y) || float.IsInfinity(s.z) ||
                    s.sqrMagnitude < 0.001f)
                {
                    s = new Vector3(4f, 4f, 4f);
                }
                data.initialLabelScale = s;
            }

            // 1. Create Glowing Halo Rim around the tray
            Transform existingHalo = trayGO.transform.Find("ActiveTurnHalo");
            if (existingHalo != null) DestroyImmediate(existingHalo.gameObject);

            GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            halo.name = "ActiveTurnHalo";
            halo.transform.SetParent(trayGO.transform, false);
            halo.transform.localPosition = new Vector3(0f, -0.02f, 0f);
            halo.transform.localScale = new Vector3(1.08f, 0.35f, 1.14f);

            var col = halo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            data.haloMaterial = new Material(shader);
            data.haloMaterial.color = new Color(color.r, color.g, color.b, 0.85f);
            halo.GetComponent<Renderer>().sharedMaterial = data.haloMaterial;
            data.haloGO = halo;
            halo.SetActive(false);

            // 2. Create Floating 3D Turn Badge above the tray
            Transform existingBadge = trayGO.transform.Find("FloatingTurnBadge");
            if (existingBadge != null) DestroyImmediate(existingBadge.gameObject);

            GameObject badgeGO = new GameObject("FloatingTurnBadge");
            badgeGO.transform.SetParent(trayGO.transform, false);
            badgeGO.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            TextMeshPro tmp = badgeGO.AddComponent<TextMeshPro>();
            tmp.text = badgeTitle;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 1.45f;
            tmp.color = Color.white;
            tmp.enableWordWrapping = false;
            if (badgeFont != null) tmp.font = badgeFont;

            data.badgeGO = badgeGO;
            data.badgeText = tmp;
            badgeGO.SetActive(false);

            return data;
        }

        private void Update()
        {
            if (trayVisuals.Count == 0) return;

            bool isGameActive = TurnAndStreakManager.Instance != null &&
                                TurnAndStreakManager.Instance.CurrentState != GameState.MatchSetup &&
                                (DayakattamDungeon.UI.GameHUD.Instance == null || !DayakattamDungeon.UI.GameHUD.Instance.IsTitleScreenActive);

            if (!isGameActive)
            {
                foreach (var tv in trayVisuals)
                {
                    if (tv.haloGO != null && tv.haloGO.activeSelf) tv.haloGO.SetActive(false);
                    if (tv.badgeGO != null && tv.badgeGO.activeSelf) tv.badgeGO.SetActive(false);
                    if (tv.hudLabel != null && tv.hudLabel.gameObject.activeInHierarchy)
                    {
                        Vector3 target = tv.initialLabelScale;
                        if (!float.IsNaN(target.x) && !float.IsNaN(target.y) && !float.IsNaN(target.z) &&
                            !float.IsInfinity(target.x) && !float.IsInfinity(target.y) && !float.IsInfinity(target.z))
                        {
                            tv.hudLabel.transform.localScale = target;
                        }
                    }
                }
                return;
            }

            PlayerType activePlayer = TurnAndStreakManager.Instance != null
                ? TurnAndStreakManager.Instance.ActivePlayer
                : PlayerType.Player1;

            Camera mainCam = Camera.main;

            foreach (var tv in trayVisuals)
            {
                if (tv.trayGO == null) continue;

                bool isTrayActiveInHierarchy = tv.trayGO.activeInHierarchy;
                bool isThisPlayerActive = isTrayActiveInHierarchy && (tv.player == activePlayer || (tv.player == PlayerType.Bot && activePlayer == PlayerType.Player3));

                // Halo pulsating effect
                if (tv.haloGO != null)
                {
                    if (tv.haloGO.activeSelf != isThisPlayerActive)
                    {
                        tv.haloGO.SetActive(isThisPlayerActive);
                    }

                    if (isThisPlayerActive)
                    {
                        float pulse = 1.0f + Mathf.Sin(Time.time * 3.8f) * 0.05f;
                        tv.haloGO.transform.localScale = new Vector3(1.08f * pulse, 0.35f, 1.14f * pulse);

                        if (tv.haloMaterial != null)
                        {
                            float alpha = 0.55f + Mathf.PingPong(Time.time * 2.5f, 0.35f);
                            Color c = tv.playerColor;
                            c.a = alpha;
                            tv.haloMaterial.color = c;
                        }
                    }
                }

                // 3D Badge Billboarding and vertical float
                if (tv.badgeGO != null)
                {
                    if (tv.badgeGO.activeSelf != isThisPlayerActive)
                    {
                        tv.badgeGO.SetActive(isThisPlayerActive);
                    }

                    if (isThisPlayerActive)
                    {
                        if (mainCam != null)
                        {
                            tv.badgeGO.transform.rotation = mainCam.transform.rotation;
                        }

                        float bob = Mathf.Sin(Time.time * 4f) * 0.06f;
                        tv.badgeGO.transform.localPosition = new Vector3(0f, 0.75f + bob, 0f);
                    }
                }

                // HUD Label scale pulse when active
                if (tv.hudLabel != null && tv.hudLabel.gameObject.activeInHierarchy)
                {
                    if (float.IsNaN(tv.initialLabelScale.x) || float.IsNaN(tv.initialLabelScale.y) || float.IsNaN(tv.initialLabelScale.z) ||
                        float.IsInfinity(tv.initialLabelScale.x) || float.IsInfinity(tv.initialLabelScale.y) || float.IsInfinity(tv.initialLabelScale.z) ||
                        tv.initialLabelScale.sqrMagnitude < 0.001f)
                    {
                        tv.initialLabelScale = new Vector3(4f, 4f, 4f);
                    }

                    if (isThisPlayerActive)
                    {
                        float speed = (labelPulseSpeed > 0.01f) ? labelPulseSpeed : 2.5f;
                        float amount = (labelPulseAmount >= 0f) ? labelPulseAmount : 0.05f;
                        float labelPulse = 1.0f + Mathf.PingPong(Time.time * speed, amount);
                        if (!float.IsNaN(labelPulse) && !float.IsInfinity(labelPulse))
                        {
                            Vector3 target = tv.initialLabelScale * labelPulse;
                            if (!float.IsNaN(target.x) && !float.IsNaN(target.y) && !float.IsNaN(target.z) &&
                                !float.IsInfinity(target.x) && !float.IsInfinity(target.y) && !float.IsInfinity(target.z))
                            {
                                tv.hudLabel.transform.localScale = target;
                            }
                        }
                    }
                    else
                    {
                        Vector3 target = tv.initialLabelScale;
                        if (!float.IsNaN(target.x) && !float.IsNaN(target.y) && !float.IsNaN(target.z) &&
                            !float.IsInfinity(target.x) && !float.IsInfinity(target.y) && !float.IsInfinity(target.z))
                        {
                            tv.hudLabel.transform.localScale = target;
                        }
                    }
                }
            }
        }

        private void HandleStateChanged(GameState state)
        {
            UpdateActiveTrayVisuals();
        }

        public void UpdateActiveTrayVisuals()
        {
            if (TurnAndStreakManager.Instance == null) return;

            bool isGameActive = TurnAndStreakManager.Instance.CurrentState != GameState.MatchSetup &&
                                (DayakattamDungeon.UI.GameHUD.Instance == null || !DayakattamDungeon.UI.GameHUD.Instance.IsTitleScreenActive);

            PlayerType activePlayer = TurnAndStreakManager.Instance.ActivePlayer;

            foreach (var tv in trayVisuals)
            {
                if (tv.trayGO == null) continue;
                bool isThisPlayerActive = isGameActive && tv.trayGO.activeInHierarchy && (tv.player == activePlayer || (tv.player == PlayerType.Bot && activePlayer == PlayerType.Player3));

                if (tv.haloGO != null) tv.haloGO.SetActive(isThisPlayerActive);
                if (tv.badgeGO != null) tv.badgeGO.SetActive(isThisPlayerActive);
                if (!isThisPlayerActive && tv.hudLabel != null && tv.hudLabel.gameObject.activeInHierarchy)
                {
                    Vector3 target = tv.initialLabelScale;
                    if (!float.IsNaN(target.x) && !float.IsNaN(target.y) && !float.IsNaN(target.z) &&
                        !float.IsInfinity(target.x) && !float.IsInfinity(target.y) && !float.IsInfinity(target.z))
                    {
                        tv.hudLabel.transform.localScale = target;
                    }
                }
            }
        }
    }
}
