using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DayakattamDungeon.AI;
using DayakattamDungeon.Audio;
using DayakattamDungeon.Board;
using DayakattamDungeon.Core;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Tutorial;

namespace DayakattamDungeon.UI
{
    /// <summary>
    /// Jam-ready interactive HUD for Dayakattam Dungeon.
    /// Manages Turn Banners, Roll Action, Banked Roll Pool buttons, Combo Multipliers,
    /// Bot Difficulty Selector, and the Grandmother Voice Tutorial Dialog with Skip button.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        private static GameHUD instance;
        public static GameHUD Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<GameHUD>();
                return instance;
            }
            private set => instance = value;
        }

        [Header("Turn & Status Display")]
        [SerializeField] private TextMeshProUGUI turnBannerText;
        [SerializeField] private TextMeshProUGUI p1HealthText;
        [SerializeField] private TextMeshProUGUI botHealthText;
        [SerializeField] private TextMeshProUGUI p2GoalText;
        [SerializeField] private TextMeshProUGUI p4GoalText;
        [SerializeField] private Slider p1HealthSlider;
        [SerializeField] private Slider botHealthSlider;

        public TextMeshProUGUI P2GoalText { get => p2GoalText; set => p2GoalText = value; }
        public TextMeshProUGUI P4GoalText { get => p4GoalText; set => p4GoalText = value; }

        [Header("Dice & Streak Display")]
        [SerializeField] private Button rollDiceButton;
        [SerializeField] private TextMeshProUGUI lastRollText;
        [SerializeField] private TextMeshProUGUI luckyCalloutText;
        [SerializeField] private TextMeshProUGUI comboMultiplierText;

        [Header("Roll Pool (Banked Numbers)")]
        [SerializeField] private RectTransform rollPoolContainer;
        [SerializeField] private GameObject rollChipPrefab;
        [SerializeField] private Button endTurnButton;

        [Header("Difficulty Controls")]
        [SerializeField] private Button difficultyToggleButton;
        [SerializeField] private TextMeshProUGUI difficultyText;

        [Header("Tutorial Overlay")]
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private TextMeshProUGUI tutorialDialogueText;
        [SerializeField] private Button skipTutorialButton;
        [SerializeField] private Button restartMatchButton;

        [Header("Pawn Quick Action Bar")]
        [SerializeField] private GameObject pawnActionPanel;
        [SerializeField] private TextMeshProUGUI pawnActionPromptText;
        [SerializeField] private Button[] pawnActionButtons;

        [Header("Game Over Modal")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI gameOverTitleText;
        [SerializeField] private TextMeshProUGUI gameOverSubtitleText;
        [SerializeField] private TextMeshProUGUI gameOverStatsText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button challengeHardBotButton;

        [Header("Audio Settings")]
        [SerializeField] private Button audioToggleButton;
        [SerializeField] private TextMeshProUGUI audioToggleText;
        private bool isAudioMuted = false;

        [Header("Player Count & Path Controls")]
        [SerializeField] private Button playerModeButton;
        [SerializeField] private TextMeshProUGUI playerModeText;
        [SerializeField] private Button waypointToggleButton;
        [SerializeField] private TextMeshProUGUI waypointToggleText;

        public int SelectedRoll => selectedRollFromPool;

        private int selectedRollFromPool = -1;
        private readonly List<GameObject> activeRollChips = new List<GameObject>();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            InitializeHUD();
        }

        public void InitializeHUD()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged -= UpdateStateUI;
                TurnAndStreakManager.Instance.OnStateChanged += UpdateStateUI;
                TurnAndStreakManager.Instance.OnRollPoolUpdated -= UpdateRollPoolUI;
                TurnAndStreakManager.Instance.OnRollPoolUpdated += UpdateRollPoolUI;
                TurnAndStreakManager.Instance.OnStreakUpdated -= UpdateStreakUI;
                TurnAndStreakManager.Instance.OnStreakUpdated += UpdateStreakUI;
                TurnAndStreakManager.Instance.OnHealthUpdated -= UpdateHealthUI;
                TurnAndStreakManager.Instance.OnHealthUpdated += UpdateHealthUI;
                TurnAndStreakManager.Instance.OnPalaceProgressUpdated -= UpdatePalaceProgressUI;
                TurnAndStreakManager.Instance.OnPalaceProgressUpdated += UpdatePalaceProgressUI;
                TurnAndStreakManager.Instance.OnTurnDiceRolled -= HandleDiceRolled;
                TurnAndStreakManager.Instance.OnTurnDiceRolled += HandleDiceRolled;
                TurnAndStreakManager.Instance.OnMatchEnded -= HandleMatchEnded;
                TurnAndStreakManager.Instance.OnMatchEnded += HandleMatchEnded;
            }

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnTutorialDialogueUpdated -= UpdateTutorialUI;
                TutorialManager.Instance.OnTutorialDialogueUpdated += UpdateTutorialUI;
                TutorialManager.Instance.OnTutorialCompleted -= HandleTutorialCompleted;
                TutorialManager.Instance.OnTutorialCompleted += HandleTutorialCompleted;
            }

            if (rollDiceButton != null)
            {
                rollDiceButton.onClick.RemoveListener(OnRollButtonClicked);
                rollDiceButton.onClick.AddListener(OnRollButtonClicked);
            }

            if (skipTutorialButton != null)
            {
                skipTutorialButton.onClick.RemoveListener(OnSkipTutorialClicked);
                skipTutorialButton.onClick.AddListener(OnSkipTutorialClicked);
            }

            if (difficultyToggleButton != null)
            {
                difficultyToggleButton.onClick.RemoveListener(OnToggleDifficultyClicked);
                difficultyToggleButton.onClick.AddListener(OnToggleDifficultyClicked);
            }

            if (endTurnButton != null)
            {
                endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
                endTurnButton.gameObject.SetActive(false);
            }

            if (restartMatchButton != null)
            {
                restartMatchButton.onClick.RemoveListener(OnRestartMatchClicked);
                restartMatchButton.onClick.AddListener(OnRestartMatchClicked);
            }

            if (playAgainButton != null)
            {
                playAgainButton.onClick.RemoveListener(OnPlayAgainClicked);
                playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            }

            if (challengeHardBotButton != null)
            {
                challengeHardBotButton.onClick.RemoveListener(OnChallengeHardBotClicked);
                challengeHardBotButton.onClick.AddListener(OnChallengeHardBotClicked);
            }

            if (audioToggleButton != null)
            {
                audioToggleButton.onClick.RemoveListener(ToggleAudio);
                audioToggleButton.onClick.AddListener(ToggleAudio);
            }

            if (playerModeButton != null)
            {
                playerModeButton.onClick.RemoveListener(OnPlayerModeToggleClicked);
                playerModeButton.onClick.AddListener(OnPlayerModeToggleClicked);
            }

            if (waypointToggleButton != null)
            {
                waypointToggleButton.onClick.RemoveListener(OnWaypointToggleClicked);
                waypointToggleButton.onClick.AddListener(OnWaypointToggleClicked);
            }

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pawnActionPanel != null) pawnActionPanel.SetActive(false);
            if (luckyCalloutText != null) luckyCalloutText.gameObject.SetActive(false);

            UpdateDifficultyUI();
            UpdateAudioUI();
            UpdatePlayerModeUI();
            UpdateWaypointToggleUI();
        }

        public void OnPlayerModeToggleClicked()
        {
            if (ThayamBoardGrid.Instance == null) return;

            bool isUnlocked = TurnAndStreakManager.IsFourPlayerUnlocked();
            if (!isUnlocked && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.TwoPlayers)
            {
                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.SpawnFloatingText(Vector3.up * 1.5f, "Defeat Grandma in 2P mode first!", new Color(1f, 0.45f, 0.45f), 2.2f);
                }
                if (turnBannerText != null)
                {
                    turnBannerText.text = "LOCKED: DEFEAT GRANDMA IN 2-PLAYER MODE TO UNLOCK 4-PLAYER MODE!";
                    turnBannerText.color = new Color(1f, 0.45f, 0.45f);
                }
                return;
            }

            var newMode = (ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.TwoPlayers)
                ? BoardPlayerMode.FourPlayers
                : BoardPlayerMode.TwoPlayers;

            ThayamBoardGrid.Instance.SetPlayerMode(newMode);
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.SetPlayerMode(newMode);
            }
            UpdatePlayerModeUI();
            RefreshAllPalaceProgressUI();
        }

        public void UpdatePlayerModeUI()
        {
            if (playerModeText != null && ThayamBoardGrid.Instance != null)
            {
                bool is4P = (ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);
                bool isUnlocked = TurnAndStreakManager.IsFourPlayerUnlocked();

                if (is4P)
                {
                    playerModeText.text = "Mode: 4P";
                    playerModeText.color = new Color(0.4f, 0.85f, 1f);
                }
                else if (!isUnlocked)
                {
                    playerModeText.text = "Mode: 2P [4P Locked]";
                    playerModeText.color = new Color(0.85f, 0.85f, 0.85f);
                }
                else
                {
                    playerModeText.text = "Mode: 2P";
                    playerModeText.color = Color.white;
                }
            }
        }

        public void OnWaypointToggleClicked()
        {
            if (ThayamBoardGrid.Instance == null) return;
            ThayamBoardGrid.Instance.ToggleWaypointPath();
            UpdateWaypointToggleUI();
        }

        public void UpdateWaypointToggleUI()
        {
            if (waypointToggleText != null && ThayamBoardGrid.Instance != null)
            {
                bool isShown = ThayamBoardGrid.Instance.ShowWaypointPath;
                waypointToggleText.text = isShown ? "Path: ON [P]" : "Path: OFF [P]";
                waypointToggleText.color = isShown ? new Color(0.4f, 0.9f, 1f) : new Color(0.75f, 0.75f, 0.8f);
            }
        }

        public void ToggleAudio()
        {
            isAudioMuted = !isAudioMuted;
            AudioListener.volume = isAudioMuted ? 0f : 1f;
            UpdateAudioUI();
        }

        private void UpdateAudioUI()
        {
            if (audioToggleText != null)
            {
                audioToggleText.text = isAudioMuted ? "Sound: MUTED [M]" : "Sound: ON [M]";
                audioToggleText.color = isAudioMuted ? new Color(1f, 0.45f, 0.45f) : Color.white;
            }
        }

        private void OnDestroy()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged -= UpdateStateUI;
                TurnAndStreakManager.Instance.OnRollPoolUpdated -= UpdateRollPoolUI;
                TurnAndStreakManager.Instance.OnStreakUpdated -= UpdateStreakUI;
                TurnAndStreakManager.Instance.OnHealthUpdated -= UpdateHealthUI;
                TurnAndStreakManager.Instance.OnPalaceProgressUpdated -= UpdatePalaceProgressUI;
                TurnAndStreakManager.Instance.OnTurnDiceRolled -= HandleDiceRolled;
                TurnAndStreakManager.Instance.OnMatchEnded -= HandleMatchEnded;
            }

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnTutorialDialogueUpdated -= UpdateTutorialUI;
                TutorialManager.Instance.OnTutorialCompleted -= HandleTutorialCompleted;
            }
        }

        private void UpdateStateUI(GameState state)
        {
            bool isP1Rolling = (state == GameState.P1_RollingPhase ||
                               (state == GameState.TutorialIntro && TutorialManager.Instance.IsTutorialActive &&
                               (TutorialManager.Instance.CurrentStep == TutorialStep.Step1_RollThayam ||
                                TutorialManager.Instance.CurrentStep == TutorialStep.Step2_RollStreak)));

            if (rollDiceButton != null)
            {
                rollDiceButton.interactable = isP1Rolling;
            }

            if (turnBannerText != null)
            {
                switch (state)
                {
                    case GameState.TutorialIntro:
                        turnBannerText.text = "SACRED INITIATION - TUTORIAL";
                        turnBannerText.color = new Color(1f, 0.85f, 0.3f);
                        break;
                    case GameState.P1_RollingPhase:
                        turnBannerText.text = "YOUR TURN: ROLL THE DAYAKATTAM!";
                        turnBannerText.color = new Color(0.95f, 0.35f, 0.35f);
                        break;
                    case GameState.P1_ActionPhase:
                        bool canAwaken = TurnAndStreakManager.Instance != null &&
                                         TurnAndStreakManager.Instance.CurrentRollPool.Contains(1);
                        bool anyInReserve = TurnAndStreakManager.Instance != null &&
                                            TurnAndStreakManager.Instance.Player1Pawns.Any(p => p.State == PawnState.InReserve);
                        if (canAwaken && anyInReserve)
                        {
                            turnBannerText.text = "ACTION: CLICK [1] OR YOUR RED PAWN TO ENTER BOARD!";
                            turnBannerText.color = new Color(1f, 0.40f, 0.40f);
                        }
                        else
                        {
                            turnBannerText.text = "ACTION: CLICK A BANKED ROLL [X] OR YOUR PAWN TO MOVE!";
                            turnBannerText.color = new Color(0.2f, 0.95f, 0.4f);
                        }
                        break;
                    case GameState.Bot_RollingPhase:
                        var curRollingBot = TurnAndStreakManager.Instance != null ? TurnAndStreakManager.Instance.ActivePlayer : PlayerType.Bot;
                        switch (curRollingBot)
                        {
                            case PlayerType.Player2:
                                turnBannerText.text = "PLAYER 2 IS ROLLING...";
                                turnBannerText.color = new Color(0.35f, 0.70f, 1.0f);
                                break;
                            case PlayerType.Player4:
                                turnBannerText.text = "PLAYER 4 IS ROLLING...";
                                turnBannerText.color = new Color(1.0f, 0.90f, 0.25f);
                                break;
                            case PlayerType.Bot:
                            case PlayerType.Player3:
                            default:
                                turnBannerText.text = "GRANDMA IS ROLLING...";
                                turnBannerText.color = new Color(0.25f, 0.95f, 0.55f);
                                break;
                        }
                        break;
                    case GameState.Bot_ActionPhase:
                        var curActingBot = TurnAndStreakManager.Instance != null ? TurnAndStreakManager.Instance.ActivePlayer : PlayerType.Bot;
                        switch (curActingBot)
                        {
                            case PlayerType.Player2:
                                turnBannerText.text = "PLAYER 2 IS MOVING...";
                                turnBannerText.color = new Color(0.35f, 0.70f, 1.0f);
                                break;
                            case PlayerType.Player4:
                                turnBannerText.text = "PLAYER 4 IS MOVING...";
                                turnBannerText.color = new Color(1.0f, 0.90f, 0.25f);
                                break;
                            case PlayerType.Bot:
                            case PlayerType.Player3:
                            default:
                                turnBannerText.text = "GRANDMA IS MOVING...";
                                turnBannerText.color = new Color(0.35f, 0.90f, 0.65f);
                                break;
                        }
                        break;
                    case GameState.Victory:
                        turnBannerText.text = "VICTORY! YOU BESTED ALL OPPONENTS!";
                        turnBannerText.color = Color.green;
                        break;
                    case GameState.Defeat:
                        string winnerTitle = "GRANDMA";
                        if (TurnAndStreakManager.Instance != null)
                        {
                            int totalReq = TurnAndStreakManager.Instance.TotalPawnsPerPlayer;
                            if (TurnAndStreakManager.Instance.P2PalaceCount >= totalReq) winnerTitle = "PLAYER 2";
                            else if (TurnAndStreakManager.Instance.P4PalaceCount >= totalReq) winnerTitle = "PLAYER 4";
                        }
                        turnBannerText.text = $"DEFEAT! {winnerTitle} WON THE MATCH!";
                        turnBannerText.color = new Color(1f, 0.35f, 0.35f);
                        break;
                }
            }

            if (endTurnButton != null)
            {
                endTurnButton.gameObject.SetActive(state == GameState.P1_ActionPhase);
            }

            // Hide quick action panel on state changes
            if (pawnActionPanel != null && state != GameState.P1_ActionPhase && state != GameState.TutorialIntro)
            {
                pawnActionPanel.SetActive(false);
            }
        }

        private void HandleDiceRolled(DayakattamRollResult roll)
        {
            if (lastRollText != null)
            {
                lastRollText.text = $"Stick A: [{roll.stickA}] | Stick B: [{roll.stickB}] => {roll.rollName}";
            }

            if (roll.isLucky && luckyCalloutText != null)
            {
                luckyCalloutText.gameObject.SetActive(true);
                luckyCalloutText.text = roll.total == 12 ? "PANNIRENDU! (12)\nEXTRA ROLL!" :
                                        roll.total == 1  ? "THAYAM! (1)\nSPIRIT AWAKENS!" :
                                        $"AUSPICIOUS {roll.total}!\nEXTRA ROLL!";
                CancelInvoke(nameof(HideLuckyCallout));
                Invoke(nameof(HideLuckyCallout), 1.8f);
            }
        }

        private void HideLuckyCallout()
        {
            if (luckyCalloutText != null) luckyCalloutText.gameObject.SetActive(false);
        }

        private void UpdateStreakUI(int streak, float multiplier)
        {
            if (comboMultiplierText != null)
            {
                comboMultiplierText.text = $"Streak: {streak} | Combo: {multiplier:F1}x";
                comboMultiplierText.color = streak > 0 ? Color.yellow : Color.white;
            }
        }

        public void UpdatePalaceProgressUI(PlayerType player, int inPalace, int total)
        {
            bool is4P = (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);

            if (is4P)
            {
                if (p2GoalText != null) p2GoalText.gameObject.SetActive(true);
                if (p4GoalText != null) p4GoalText.gameObject.SetActive(true);

                switch (player)
                {
                    case PlayerType.Player1:
                        if (p1HealthText != null)
                        {
                            p1HealthText.text = $"P1: {inPalace}/{total}";
                            p1HealthText.rectTransform.anchoredPosition = new Vector2(-260, -40);
                            p1HealthText.color = new Color(1.0f, 0.40f, 0.40f);
                        }
                        break;
                    case PlayerType.Player2:
                        if (p2GoalText != null)
                        {
                            p2GoalText.text = $"P2: {inPalace}/{total}";
                            p2GoalText.rectTransform.anchoredPosition = new Vector2(260, -40);
                            p2GoalText.color = new Color(0.35f, 0.70f, 1f);
                        }
                        break;
                    case PlayerType.Bot:
                    case PlayerType.Player3:
                        if (botHealthText != null)
                        {
                            botHealthText.text = $"P3: {inPalace}/{total}";
                            botHealthText.rectTransform.anchoredPosition = new Vector2(360, -40);
                            botHealthText.color = new Color(0.25f, 0.95f, 0.55f);
                        }
                        break;
                    case PlayerType.Player4:
                        if (p4GoalText != null)
                        {
                            p4GoalText.text = $"P4: {inPalace}/{total}";
                            p4GoalText.rectTransform.anchoredPosition = new Vector2(-360, -40);
                            p4GoalText.color = new Color(1f, 0.88f, 0.2f);
                        }
                        break;
                }
            }
            else
            {
                if (p2GoalText != null) p2GoalText.gameObject.SetActive(false);
                if (p4GoalText != null) p4GoalText.gameObject.SetActive(false);

                if (player == PlayerType.Player1)
                {
                    if (p1HealthText != null)
                    {
                        p1HealthText.text = $"Player: {inPalace}/{total} Home";
                        p1HealthText.rectTransform.anchoredPosition = new Vector2(-310, -40);
                        p1HealthText.color = new Color(1.0f, 0.40f, 0.40f);
                    }
                    if (p1HealthSlider != null) p1HealthSlider.value = (total > 0) ? (float)inPalace / total : 0f;
                }
                else if (player == PlayerType.Bot || player == PlayerType.Player3)
                {
                    if (botHealthText != null)
                    {
                        botHealthText.text = $"Grandma: {inPalace}/{total} Home";
                        botHealthText.rectTransform.anchoredPosition = new Vector2(310, -40);
                        botHealthText.color = new Color(0.25f, 0.95f, 0.55f);
                    }
                    if (botHealthSlider != null) botHealthSlider.value = (total > 0) ? (float)inPalace / total : 0f;
                }
            }
        }

        public void RefreshAllPalaceProgressUI()
        {
            if (TurnAndStreakManager.Instance == null) return;
            int total = TurnAndStreakManager.Instance.TotalPawnsPerPlayer;
            UpdatePalaceProgressUI(PlayerType.Player1, TurnAndStreakManager.Instance.P1PalaceCount, total);
            UpdatePalaceProgressUI(PlayerType.Player2, TurnAndStreakManager.Instance.P2PalaceCount, total);
            UpdatePalaceProgressUI(PlayerType.Player3, TurnAndStreakManager.Instance.BotPalaceCount, total);
            UpdatePalaceProgressUI(PlayerType.Player4, TurnAndStreakManager.Instance.P4PalaceCount, total);
        }

        private void UpdateHealthUI(PlayerType player, int hp, int maxHp)
        {
            RefreshAllPalaceProgressUI();
        }

        private void Update()
        {
            HandleKeyboardInput();
        }

        private void HandleKeyboardInput()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;

            // Space or R -> Roll Dayakattam
            if (kb.spaceKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)
            {
                if (rollDiceButton != null && rollDiceButton.interactable)
                {
                    OnRollButtonClicked();
                    return;
                }
            }

            // Number keys 1..6 -> Spend roll from pool!
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(1);
            }
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(2);
            }
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(3);
            }
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(4);
            }
            else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(5);
            }
            else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame)
            {
                TrySpendRollFromKeyboard(6);
            }

            // Enter or E -> End Turn
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            {
                if (endTurnButton != null && endTurnButton.gameObject.activeInHierarchy)
                {
                    OnEndTurnClicked();
                }
            }

            // M -> Toggle Audio / Mute
            if (kb.mKey.wasPressedThisFrame)
            {
                ToggleAudio();
            }

            // P -> Toggle Waypoint Path Trail
            if (kb.pKey.wasPressedThisFrame)
            {
                OnWaypointToggleClicked();
            }
        }

        private void TrySpendRollFromKeyboard(int rollValue)
        {
            if (TurnAndStreakManager.Instance == null) return;
            if (TurnAndStreakManager.Instance.ActivePlayer != PlayerType.Player1) return;
            if (TurnAndStreakManager.Instance.CurrentState != GameState.P1_ActionPhase &&
                TurnAndStreakManager.Instance.CurrentState != GameState.TutorialIntro) return;

            if (TurnAndStreakManager.Instance.CurrentRollPool.Contains(rollValue))
            {
                OnRollChipClicked(rollValue);
            }
        }

        private void UpdateRollPoolUI(List<int> pool)
        {
            if (rollPoolContainer == null) return;

            // Clear previous chips immediately
            for (int c = rollPoolContainer.childCount - 1; c >= 0; c--)
            {
                DestroyImmediate(rollPoolContainer.GetChild(c).gameObject);
            }
            activeRollChips.Clear();

            bool anyInReserve = TurnAndStreakManager.Instance != null &&
                                TurnAndStreakManager.Instance.Player1Pawns.Any(p => p.State == PawnState.InReserve);

            // Generate a chip button for each banked roll
            for (int i = 0; i < pool.Count; i++)
            {
                int rollValue = pool[i];
                GameObject chipGO;

                if (rollChipPrefab != null)
                {
                    chipGO = Instantiate(rollChipPrefab, rollPoolContainer);
                }
                else
                {
                    // Fallback greybox button
                    chipGO = new GameObject($"RollChip_{rollValue}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    chipGO.transform.SetParent(rollPoolContainer, false);
                    var img = chipGO.GetComponent<Image>();
                    img.enabled = true;
                    img.raycastTarget = true;

                    var textGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    textGO.transform.SetParent(chipGO.transform, false);
                    var txt = textGO.GetComponent<TextMeshProUGUI>();
                    if (TMP_Settings.defaultFontAsset != null)
                    {
                        txt.font = TMP_Settings.defaultFontAsset;
                    }
                    txt.raycastTarget = false; // Never block button click!

                    if (rollValue == 1 && anyInReserve)
                    {
                        txt.text = "<b>1</b>\n<size=11><color=#331800>ENTER</color></size>";
                    }
                    else
                    {
                        txt.text = rollValue.ToString();
                    }
                    txt.fontSize = 24;
                    txt.fontStyle = FontStyles.Bold;
                    txt.alignment = TextAlignmentOptions.Center;
                    txt.color = Color.black;
                    var rt = textGO.GetComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;

                    var chipRT = chipGO.GetComponent<RectTransform>();
                    chipRT.sizeDelta = new Vector2(72, 72);
                    if (rollValue == 1)
                    {
                        img.color = new Color(1.0f, 0.85f, 0.2f); // Auspicious Bright Gold for Thayam!
                    }
                    else if (DayakattamRoller.IsLuckyNumber(rollValue))
                    {
                        img.color = new Color(0.9f, 0.72f, 0.25f);
                    }
                    else
                    {
                        img.color = new Color(0.48f, 0.48f, 0.52f);
                    }
                }

                Button btn = chipGO.GetComponent<Button>();
                int capturedVal = rollValue;
                btn.onClick.AddListener(() => OnRollChipClicked(capturedVal));

                activeRollChips.Add(chipGO);
            }
        }

        private void OnRollChipClicked(int rollValue)
        {
            if (TurnAndStreakManager.Instance.ActivePlayer != PlayerType.Player1) return;

            selectedRollFromPool = rollValue;
            var pawns = TurnAndStreakManager.Instance.Player1Pawns;

            List<PawnController> legalPawns = new List<PawnController>();
            foreach (var p in pawns)
            {
                if (p.CanMove(rollValue)) legalPawns.Add(p);
            }

            if (legalPawns.Count == 0)
            {
                Debug.Log($"[GameHUD] No legal moves for roll [{rollValue}].");
                if (GameJuiceManager.Instance != null && rollPoolContainer != null)
                {
                    GameJuiceManager.Instance.SpawnFloatingText(rollPoolContainer.position, "NO LEGAL MOVES", Color.gray, 0.9f);
                }
                return;
            }

            // Case 1: If all legal pawns are in reserve (Awakening), automatically awaken the first available reserve pawn!
            bool allInReserve = legalPawns.TrueForAll(p => p.State == PawnState.InReserve);
            if (allInReserve)
            {
                if (ThayamBoardGrid.Instance != null)
                {
                    ThayamBoardGrid.Instance.HighlightMovePreview(PlayerType.Player1, rollValue, legalPawns[0]);
                }
                SpendSelectedRollOnPawn(legalPawns[0]);
                return;
            }

            // Case 2: If only 1 legal pawn is active on board, move it directly!
            var onBoardPawns = legalPawns.FindAll(p => p.State == PawnState.OnBoard);
            if (onBoardPawns.Count == 1 && (rollValue != 1 || legalPawns.Count == 1))
            {
                if (ThayamBoardGrid.Instance != null)
                {
                    ThayamBoardGrid.Instance.HighlightMovePreview(PlayerType.Player1, rollValue, onBoardPawns[0]);
                }
                SpendSelectedRollOnPawn(onBoardPawns[0]);
                return;
            }

            if (legalPawns.Count == 1)
            {
                if (ThayamBoardGrid.Instance != null)
                {
                    ThayamBoardGrid.Instance.HighlightMovePreview(PlayerType.Player1, rollValue, legalPawns[0]);
                }
                SpendSelectedRollOnPawn(legalPawns[0]);
                return;
            }

            // Case 3: Multiple legal options exist (e.g. choose between awakening a new pawn or advancing an active pawn)
            ShowPawnActionPanel(legalPawns, rollValue);
        }

        private void ShowPawnActionPanel(List<PawnController> legalPawns, int rollValue)
        {
            if (pawnActionPanel == null)
            {
                // Fallback: if panel not configured in Inspector, pick first legal pawn so player is never stuck!
                SpendSelectedRollOnPawn(legalPawns[0]);
                return;
            }
            pawnActionPanel.SetActive(true);

            if (pawnActionPromptText != null)
            {
                pawnActionPromptText.text = $"Choose Spirit Pawn to move [{rollValue}] steps:";
            }

            if (pawnActionButtons != null)
            {
                for (int i = 0; i < pawnActionButtons.Length; i++)
                {
                    if (pawnActionButtons[i] == null) continue;

                    if (i < legalPawns.Count)
                    {
                        pawnActionButtons[i].gameObject.SetActive(true);
                        PawnController p = legalPawns[i];
                        TextMeshProUGUI btnText = pawnActionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                        if (btnText != null)
                        {
                            btnText.text = (p.State == PawnState.InReserve) ? $"Awaken Reserve Spirit #{p.PawnId}" : $"Advance Spirit #{p.PawnId} ({rollValue} steps)";
                        }

                        if (ThayamBoardGrid.Instance != null && i == 0)
                        {
                            ThayamBoardGrid.Instance.HighlightMovePreview(PlayerType.Player1, rollValue, p);
                        }

                        pawnActionButtons[i].onClick.RemoveAllListeners();
                        pawnActionButtons[i].onClick.AddListener(() =>
                        {
                            SpendSelectedRollOnPawn(p);
                            pawnActionPanel.SetActive(false);
                        });
                    }
                    else
                    {
                        pawnActionButtons[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void SpendSelectedRollOnPawn(PawnController pawn)
        {
            if (selectedRollFromPool > 0)
            {
                if (ThayamBoardGrid.Instance != null)
                {
                    ThayamBoardGrid.Instance.ClearHighlights();
                }

                TurnAndStreakManager.Instance.SpendRoll(selectedRollFromPool, pawn);
                selectedRollFromPool = -1;
            }
        }

        private void OnRollButtonClicked()
        {
            TurnAndStreakManager.Instance.RequestRoll();
        }

        private void OnSkipTutorialClicked()
        {
            TutorialManager.Instance.SkipTutorial();
        }

        private void OnEndTurnClicked()
        {
            if (TurnAndStreakManager.Instance.ActivePlayer == PlayerType.Player1)
            {
                TurnAndStreakManager.Instance.StartBotTurn();
            }
        }

        private void OnRestartMatchClicked()
        {
            TurnAndStreakManager.Instance.InitializeMatch(startWithTutorial: false);
        }

        private void OnToggleDifficultyClicked()
        {
            var bot = FindFirstObjectByType<BotAIController>();
            if (bot != null)
            {
                bot.Difficulty = (bot.Difficulty == BotDifficulty.Normal) ? BotDifficulty.Hard : BotDifficulty.Normal;
                UpdateDifficultyUI();
            }
        }

        private void UpdateDifficultyUI()
        {
            var bot = FindFirstObjectByType<BotAIController>();
            if (difficultyText != null && bot != null)
            {
                difficultyText.text = $"Grandma: {bot.Difficulty.ToString().ToUpper()}";
                difficultyText.color = (bot.Difficulty == BotDifficulty.Hard) ? new Color(1f, 0.4f, 0.4f) : Color.white;
            }
        }

        private void UpdateTutorialUI(TutorialStep step, string dialogue)
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(true);
            }
            if (tutorialDialogueText != null)
            {
                tutorialDialogueText.text = dialogue;
            }
        }

        private void HandleTutorialCompleted()
        {
            if (tutorialPanel != null)
            {
                tutorialPanel.SetActive(false);
            }
        }

        private void HandleMatchEnded(PlayerType winner)
        {
            bool isP1Winner = (winner == PlayerType.Player1);
            bool is4P = (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);

            string winnerDisplayName = isP1Winner ? "YOU" :
                                      (winner == PlayerType.Player2) ? "PLAYER 2" :
                                      (winner == PlayerType.Player4) ? "PLAYER 4" : "GRANDMA";

            if (turnBannerText != null)
            {
                turnBannerText.text = isP1Winner
                    ? "VICTORY! ALL 3 SPIRIT PAWNS ATTAINED THE PALACE!"
                    : $"DEFEAT! {winnerDisplayName} HAS WON THE MATCH!";
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }

            if (gameOverTitleText != null)
            {
                gameOverTitleText.text = isP1Winner
                    ? "<color=#FFD700>VICTORY!</color>\n<size=22>ALL PAWNS IN SACRED PALACE</size>"
                    : $"<color=#FF6666>DEFEAT</color>\n<size=22>{winnerDisplayName} REACHED PALACE FIRST</size>";
            }

            if (gameOverSubtitleText != null)
            {
                if (isP1Winner)
                {
                    if (!is4P)
                    {
                        gameOverSubtitleText.text = "Grandma beams with pride: 'Aha! All your spirit pawns have reached the Sacred Palace! You have mastered Dayakattam, kanna!'\n\n<color=#FFD700><b>*** UNLOCKED: 4-PLAYER MODE! ***</b></color>\n<size=15><i>You can now toggle to 4-Player Mode on the top bar to challenge all 4 quadrants!</i></size>";
                    }
                    else
                    {
                        gameOverSubtitleText.text = "Grandma beams with pride: 'Incredible! You have conquered the 4-Player realm and guided all your spirit pawns into the Central Palace!'";
                    }
                }
                else
                {
                    gameOverSubtitleText.text = $"{winnerDisplayName} guided all 3 pawns home first! Study the sacred spiral paths and challenge again!";
                }
            }

            UpdatePlayerModeUI();

            if (gameOverStatsText != null && TurnAndStreakManager.Instance != null)
            {
                int total = TurnAndStreakManager.Instance.TotalPawnsPerPlayer;
                if (is4P)
                {
                    gameOverStatsText.text = $"<b>Match Result (4-Player):</b>\n" +
                        $"• P1 (You): <color=#66FF66>{TurnAndStreakManager.Instance.P1PalaceCount}/{total}</color>\n" +
                        $"• P2 (East): <color=#55AAFF>{TurnAndStreakManager.Instance.P2PalaceCount}/{total}</color>\n" +
                        $"• P3 (Grandma): <color=#44FF88>{TurnAndStreakManager.Instance.BotPalaceCount}/{total}</color>\n" +
                        $"• P4 (West): <color=#FFDD44>{TurnAndStreakManager.Instance.P4PalaceCount}/{total}</color>\n" +
                        $"• Peak Auspicious Multiplier: <color=#FFD700>{TurnAndStreakManager.Instance.PeakComboMultiplier:F1}x</color>";
                }
                else
                {
                    gameOverStatsText.text = $"<b>Match Result (2-Player):</b>\n" +
                        $"• Player Pawns in Palace: <color=#66FF66>{TurnAndStreakManager.Instance.P1PalaceCount}/{total}</color>\n" +
                        $"• Grandma Pawns in Palace: <color=#FF6666>{TurnAndStreakManager.Instance.BotPalaceCount}/{total}</color>\n" +
                        $"• Peak Auspicious Multiplier: <color=#FFD700>{TurnAndStreakManager.Instance.PeakComboMultiplier:F1}x</color>";
                }
            }

            if (DayakattamAudioPlayer.Instance != null)
            {
                if (isP1Winner) DayakattamAudioPlayer.Instance.PlayVictoryFanfare();
                else DayakattamAudioPlayer.Instance.PlayDefeatSting();
            }
        }

        private void OnPlayAgainClicked()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            TurnAndStreakManager.Instance.InitializeMatch(startWithTutorial: false);
        }

        private void OnChallengeHardBotClicked()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            var bot = FindFirstObjectByType<BotAIController>();
            if (bot != null)
            {
                bot.Difficulty = BotDifficulty.Hard;
                UpdateDifficultyUI();
            }
            TurnAndStreakManager.Instance.InitializeMatch(startWithTutorial: false);
        }
    }
}
