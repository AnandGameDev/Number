using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DayakattamDungeon.Audio;
using DayakattamDungeon.Board;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Tutorial;
using DayakattamDungeon.UI;

namespace DayakattamDungeon.Core
{
    public enum GameState
    {
        TutorialIntro,
        MatchSetup,
        P1_RollingPhase,
        P1_ActionPhase,
        Bot_RollingPhase,
        Bot_ActionPhase,
        Victory,
        Defeat
    }

    /// <summary>
    /// Core Turn & Auspicious Streak State Machine for Dayakattam Dungeon.
    /// Manages the RollPool, ComboMultiplier, Pawn spawning rules, and Win/Loss conditions.
    /// </summary>
    public class TurnAndStreakManager : MonoBehaviour
    {
        private static TurnAndStreakManager instance;
        public static TurnAndStreakManager Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<TurnAndStreakManager>();
                return instance;
            }
            private set => instance = value;
        }

        public const string PREF_KEY_4P_UNLOCKED = "Dayakattam_4P_Unlocked";

        public static bool IsFourPlayerUnlocked()
        {
            return PlayerPrefs.GetInt(PREF_KEY_4P_UNLOCKED, 0) == 1;
        }

        public static void SetFourPlayerUnlocked(bool unlocked)
        {
            PlayerPrefs.SetInt(PREF_KEY_4P_UNLOCKED, unlocked ? 1 : 0);
            PlayerPrefs.Save();
        }

        [Header("Match Settings")]
        [SerializeField] private int maxSpiritHealth = 100;

        [Header("Current Match State")]
        [SerializeField] private GameState currentState = GameState.MatchSetup;
        [SerializeField] private PlayerType activePlayer = PlayerType.Player1;
        [SerializeField] private int p1Health;
        [SerializeField] private int botHealth;

        [Header("Streak & Combo Data")]
        [SerializeField] private List<int> currentRollPool = new List<int>();
        [SerializeField] private int currentStreakCount = 0;
        [SerializeField] private float currentComboMultiplier = 1.0f;
        [SerializeField] private float peakComboMultiplier = 1.0f;

        [Header("Pawn Lists")]
        [SerializeField] private List<PawnController> player1Pawns = new List<PawnController>();
        [SerializeField] private List<PawnController> player2Pawns = new List<PawnController>();
        [SerializeField] private List<PawnController> botPawns = new List<PawnController>(); // P3 / Grandma
        [SerializeField] private List<PawnController> player4Pawns = new List<PawnController>();

        // Events
        public event Action<GameState> OnStateChanged;
        public event Action<List<int>> OnRollPoolUpdated;
        public event Action<int, float> OnStreakUpdated; // streakCount, multiplier
        public event Action<PlayerType, int, int> OnHealthUpdated; // player, currentHp, maxHp (kept for API compatibility)
        public event Action<PlayerType, int, int> OnPalaceProgressUpdated; // player, pawnsInPalace, totalPawns
        public event Action<PlayerType> OnMatchEnded; // winner
        public event Action<DayakattamRollResult> OnTurnDiceRolled;

        public GameState CurrentState => currentState;
        public PlayerType ActivePlayer => activePlayer;
        public IReadOnlyList<int> CurrentRollPool => currentRollPool;
        public int StreakCount => currentStreakCount;
        public float ComboMultiplier => currentComboMultiplier;
        public float PeakComboMultiplier => peakComboMultiplier;
        public int P1Health => p1Health;
        public int BotHealth => botHealth;
        public int MaxHealth => maxSpiritHealth;

        public int P1PalaceCount => player1Pawns.FindAll(p => p != null && p.State == PawnState.InPalace).Count;
        public int P2PalaceCount => player2Pawns.FindAll(p => p != null && p.State == PawnState.InPalace).Count;
        public int BotPalaceCount => botPawns.FindAll(p => p != null && p.State == PawnState.InPalace).Count;
        public int P3PalaceCount => BotPalaceCount;
        public int P4PalaceCount => player4Pawns.FindAll(p => p != null && p.State == PawnState.InPalace).Count;
        public int TotalPawnsPerPlayer => player1Pawns.Count > 0 ? player1Pawns.Count : 3;

        public IReadOnlyList<PawnController> Player1Pawns => player1Pawns;
        public IReadOnlyList<PawnController> Player2Pawns => player2Pawns;
        public IReadOnlyList<PawnController> BotPawns => botPawns;
        public IReadOnlyList<PawnController> Player3Pawns => botPawns;
        public IReadOnlyList<PawnController> Player4Pawns => player4Pawns;

        public List<PawnController> GetPawnsForPlayer(PlayerType player)
        {
            switch (player)
            {
                case PlayerType.Player1:
                    return player1Pawns;
                case PlayerType.Player2:
                    return player2Pawns;
                case PlayerType.Bot:
                case PlayerType.Player3:
                    return botPawns;
                case PlayerType.Player4:
                    return player4Pawns;
                default:
                    return player1Pawns;
            }
        }

        public int GetPalaceCountForPlayer(PlayerType player)
        {
            var pawns = GetPawnsForPlayer(player);
            return pawns.FindAll(p => p != null && p.State == PawnState.InPalace).Count;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            SubscribeEvents();
        }

        private void Start()
        {
            SubscribeEvents();
        }

        public void SubscribeEvents()
        {
            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.OnRollCompleted -= HandleDiceRolled;
                DayakattamRoller.Instance.OnRollCompleted += HandleDiceRolled;
            }

            if (ThayamBoardGrid.Instance != null)
            {
                ThayamBoardGrid.Instance.OnPlayerModeChanged -= SetPlayerMode;
                ThayamBoardGrid.Instance.OnPlayerModeChanged += SetPlayerMode;
            }
        }

        private void OnDestroy()
        {
            if (DayakattamRoller.Instance != null)
            {
                DayakattamRoller.Instance.OnRollCompleted -= HandleDiceRolled;
            }

            if (ThayamBoardGrid.Instance != null)
            {
                ThayamBoardGrid.Instance.OnPlayerModeChanged -= SetPlayerMode;
            }
        }

        public void InitializeMatch(bool startWithTutorial = false)
        {
            // If 4-player mode is locked, enforce 2-player mode
            if (!IsFourPlayerUnlocked() && ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode != BoardPlayerMode.TwoPlayers)
            {
                ThayamBoardGrid.Instance.SetPlayerMode(BoardPlayerMode.TwoPlayers);
                return;
            }

            ResetAllPawnsToReserve();

            // Clear board highlights & move previews
            if (ThayamBoardGrid.Instance != null)
            {
                ThayamBoardGrid.Instance.ClearHighlights();
            }

            SubscribeEvents();
            if (VoiceOverManager.Instance != null)
            {
                VoiceOverManager.Instance.ResetMatchPrompts();
            }
            FirstThayamPawnIndicator.ResetMatchFlag();
            p1Health = maxSpiritHealth;
            botHealth = maxSpiritHealth;
            currentRollPool.Clear();
            currentStreakCount = 0;
            currentComboMultiplier = 1.0f;
            peakComboMultiplier = 1.0f;

            OnHealthUpdated?.Invoke(PlayerType.Player1, p1Health, maxSpiritHealth);
            OnHealthUpdated?.Invoke(PlayerType.Bot, botHealth, maxSpiritHealth);

            int total = TotalPawnsPerPlayer;
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player1, P1PalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player2, P2PalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Bot, BotPalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player3, BotPalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player4, P4PalaceCount, total);

            OnStreakUpdated?.Invoke(currentStreakCount, currentComboMultiplier);
            OnRollPoolUpdated?.Invoke(currentRollPool);

            // Hook up pawn capture callbacks for all players
            void HookList(List<PawnController> list)
            {
                if (list == null) return;
                foreach (var p in list)
                {
                    if (p == null) continue;
                    p.OnOpponentCaptured -= HandlePawnCapture;
                    p.OnOpponentCaptured += HandlePawnCapture;
                    p.OnPawnReachedPalace -= HandlePalaceArrival;
                    p.OnPawnReachedPalace += HandlePalaceArrival;
                }
            }

            HookList(player1Pawns);
            HookList(player2Pawns);
            HookList(botPawns);
            HookList(player4Pawns);

            if (startWithTutorial)
            {
                SetState(GameState.TutorialIntro);
            }
            else
            {
                StartPlayerTurn();
            }
        }

        /// <summary>
        /// Fully resets the current match, bringing all pawns back to reserve,
        /// clearing banked dice rolls, and resetting the turn cycle to Player 1.
        /// </summary>
        public void RestartMatch()
        {
            Debug.Log("[TurnAndStreakManager] Restarting match...");
            ResetAllPawnsToReserve();
            if (ThayamBoardGrid.Instance != null)
            {
                ThayamBoardGrid.Instance.ClearHighlights();
            }
            InitializeMatch(startWithTutorial: false);
        }

        public void SetState(GameState newState)
        {
            currentState = newState;
            Debug.Log($"[TurnAndStreakManager] State changed to: {currentState} (ActivePlayer: {activePlayer})");

            // Automatically switch waypoint path to active participant's trajectory
            if (ThayamBoardGrid.Instance != null)
            {
                ThayamBoardGrid.Instance.ShowPathForPlayer(activePlayer);
            }

            OnStateChanged?.Invoke(currentState);
        }

        /// <summary>
        /// Starts Player 1's turn.
        /// </summary>
        public void StartPlayerTurn()
        {
            StartTurnForPlayer(PlayerType.Player1);
        }

        /// <summary>
        /// Starts Bot / Grandma's turn (2P opponent).
        /// </summary>
        public void StartBotTurn()
        {
            StartTurnForPlayer(PlayerType.Bot);
        }

        /// <summary>
        /// Starts turn for any specified player in 2P or 4P rotation.
        /// </summary>
        public void StartTurnForPlayer(PlayerType player)
        {
            activePlayer = player;
            ResetTurnPoolAndStreak();
            if (player == PlayerType.Player1)
            {
                SetState(GameState.P1_RollingPhase);
                bool isTutorial = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive;
                bool isTitleScreen = GameHUD.Instance != null && GameHUD.Instance.IsTitleScreenActive;
                if (!isTitleScreen)
                {
                    var titlePanel = GameObject.Find("TitleScreenPanel");
                    if (titlePanel != null && titlePanel.activeInHierarchy)
                    {
                        isTitleScreen = true;
                    }
                }

                if (!isTutorial && !isTitleScreen)
                {
                    if (VoiceOverManager.Instance != null)
                    {
                        VoiceOverManager.Instance.PlayPlayerTurnPrompt();
                    }
                }
            }
            else
            {
                SetState(GameState.Bot_RollingPhase);
            }
        }

        private void ResetTurnPoolAndStreak()
        {
            currentRollPool.Clear();
            currentStreakCount = 0;
            currentComboMultiplier = 1.0f;
            OnRollPoolUpdated?.Invoke(currentRollPool);
            OnStreakUpdated?.Invoke(currentStreakCount, currentComboMultiplier);
        }

        /// <summary>
        /// Request a dice roll for the active participant.
        /// </summary>
        public bool RequestRoll()
        {
            if (currentState != GameState.P1_RollingPhase && currentState != GameState.Bot_RollingPhase && currentState != GameState.TutorialIntro)
            {
                return false;
            }

            DayakattamRoller.Instance.RollDice();
            return true;
        }

        private void HandleDiceRolled(DayakattamRollResult roll)
        {
            OnTurnDiceRolled?.Invoke(roll);

            // Add the rolled number to the banked pool
            currentRollPool.Add(roll.total);
            OnRollPoolUpdated?.Invoke(currentRollPool);

            if (roll.isLucky)
            {
                // Auspicious Streak!
                currentStreakCount++;
                currentComboMultiplier = 1.0f + (currentStreakCount * 0.5f);
                peakComboMultiplier = Mathf.Max(peakComboMultiplier, currentComboMultiplier);
                OnStreakUpdated?.Invoke(currentStreakCount, currentComboMultiplier);

                if (activePlayer == PlayerType.Player1 || currentState == GameState.TutorialIntro)
                {
                    if (VoiceOverManager.Instance != null)
                    {
                        VoiceOverManager.Instance.PlayLuckyRollVoice(roll.total);
                    }
                }

                // Remains in current rolling phase (Player or Bot rolls again)
                Debug.Log($"[TurnAndStreakManager] Auspicious Streak! Rolled {roll.rollName}. Multiplier is now {currentComboMultiplier}x!");
            }
            else
            {
                // Standard roll (2, 3, 4) ends the rolling streak and triggers Action Phase!
                Debug.Log($"[TurnAndStreakManager] Standard roll {roll.rollName} ends streak. Moving to Action Phase.");
                if (activePlayer == PlayerType.Player1)
                {
                    SetState(GameState.P1_ActionPhase);

                    // Check if player has NO 1 in pool AND all pawns are locked in reserve
                    bool allPawnsInReserve = player1Pawns.Count > 0 && player1Pawns.TrueForAll(p => p == null || p.State == PawnState.InReserve);
                    bool hasThayam = currentRollPool.Contains(1);

                    if (allPawnsInReserve && !hasThayam)
                    {
                        if (VoiceOverManager.Instance != null)
                        {
                            VoiceOverManager.Instance.PlayNeedThayamToEnter();
                        }
                    }
                    else if (currentRollPool.Count > 0)
                    {
                        if (VoiceOverManager.Instance != null && (TutorialManager.Instance == null || !TutorialManager.Instance.IsTutorialActive))
                        {
                            VoiceOverManager.Instance.PlayClickPawnPrompt();
                        }
                    }

                    CheckForPlayableMoves(PlayerType.Player1);
                }
                else
                {
                    SetState(GameState.Bot_ActionPhase);
                }
            }
        }

        /// <summary>
        /// Execute spending a roll from the pool onto a pawn.
        /// </summary>
        public bool SpendRoll(int rollValue, PawnController targetPawn, Action onMoveComplete = null)
        {
            if (!currentRollPool.Contains(rollValue))
            {
                return false;
            }

            if (!targetPawn.CanMove(rollValue))
            {
                return false;
            }

            // Remove roll from pool
            currentRollPool.Remove(rollValue);
            OnRollPoolUpdated?.Invoke(currentRollPool);

            // Advance pawn
            targetPawn.AdvanceSteps(rollValue, currentComboMultiplier, () =>
            {
                onMoveComplete?.Invoke();
                CheckActionPhaseContinuation();
            });

            return true;
        }

        /// <summary>
        /// Check if the current player can continue spending rolls or if their turn ends.
        /// </summary>
        public void CheckActionPhaseContinuation()
        {
            if (currentState == GameState.Victory || currentState == GameState.Defeat) return;

            if (currentRollPool.Count == 0 || !HasAnyLegalMoves(activePlayer))
            {
                AdvanceToNextPlayerTurn();
            }
        }

        /// <summary>
        /// Advances turn clockwise:
        /// 2P mode: Player 1 (South) <-> Grandma (North)
        /// 4P mode: Player 1 (South) -> Player 2 (East) -> Player 3 / Grandma (North) -> Player 4 (West) -> Player 1 (South)
        /// </summary>
        public void AdvanceToNextPlayerTurn()
        {
            if (currentState == GameState.Victory || currentState == GameState.Defeat) return;

            bool is4P = (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);

            PlayerType nextPlayer;
            if (!is4P)
            {
                // 2-Player mode: Player 1 <-> Grandma (Bot/Player3)
                nextPlayer = (activePlayer == PlayerType.Player1) ? PlayerType.Bot : PlayerType.Player1;
            }
            else
            {
                // 4-Player mode: Clockwise rotation
                switch (activePlayer)
                {
                    case PlayerType.Player1:
                        nextPlayer = PlayerType.Player2;
                        break;
                    case PlayerType.Player2:
                        nextPlayer = PlayerType.Player3;
                        break;
                    case PlayerType.Bot:
                    case PlayerType.Player3:
                        nextPlayer = PlayerType.Player4;
                        break;
                    case PlayerType.Player4:
                    default:
                        nextPlayer = PlayerType.Player1;
                        break;
                }
            }

            Debug.Log($"[TurnAndStreakManager] Advancing turn: {activePlayer} -> {nextPlayer} (Mode: {(is4P ? "4P" : "2P")})");
            StartTurnForPlayer(nextPlayer);
        }

        public bool HasAnyLegalMoves(PlayerType player)
        {
            List<PawnController> pawns = GetPawnsForPlayer(player);

            foreach (int roll in currentRollPool)
            {
                foreach (var pawn in pawns)
                {
                    if (pawn != null && pawn.CanMove(roll)) return true;
                }
            }
            return false;
        }

        private void CheckForPlayableMoves(PlayerType player)
        {
            if (!HasAnyLegalMoves(player))
            {
                Debug.Log($"[TurnAndStreakManager] No legal moves available for {player}. Forfeiting turn.");
                StartCoroutine(ForfeitTurnDelayed());
            }
        }

        private IEnumerator ForfeitTurnDelayed()
        {
            yield return new WaitForSeconds(1.2f);
            CheckActionPhaseContinuation();
        }

        private void HandlePawnCapture(PawnController attacker, PawnController victim, int damage)
        {
            Debug.Log($"[TurnAndStreakManager] {attacker.Owner} pawn captured and cut {victim.Owner} pawn back to reserve!");
            // Pure Dayakattam rules: Pawn is banished to reserve. No HP deduction.

            if (attacker.Owner == PlayerType.Player1 && victim.Owner != PlayerType.Player1)
            {
                if (VoiceOverManager.Instance != null)
                {
                    VoiceOverManager.Instance.PlayPlayerCapture(); // nicecut.ogg
                }
            }
            else if (victim.Owner == PlayerType.Player1)
            {
                if (VoiceOverManager.Instance != null)
                {
                    VoiceOverManager.Instance.PlayOpponentCapture(); // 50/50 ah ohh.ogg / ayyo.ogg
                }
            }
        }

        private void HandlePalaceArrival(PawnController pawn)
        {
            Debug.Log($"[TurnAndStreakManager] {pawn.Owner} pawn reached the Central Palace!");

            int total = TotalPawnsPerPlayer;
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player1, P1PalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player2, P2PalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Bot, BotPalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player3, BotPalaceCount, total);
            OnPalaceProgressUpdated?.Invoke(PlayerType.Player4, P4PalaceCount, total);

            var list = GetPawnsForPlayer(pawn.Owner);
            int inPalaceCount = list.FindAll(p => p != null && p.State == PawnState.InPalace).Count;

            Debug.Log($"[TurnAndStreakManager] {pawn.Owner} has {inPalaceCount}/{list.Count} pawns in the Palace.");

            // Win or loose panel appears once ALL pawns of any participant reach the Central Palace!
            if (list.Count > 0 && inPalaceCount >= list.Count)
            {
                TriggerMatchEnd(pawn.Owner);
            }
            else
            {
                // Reached center Palace, but match is not won yet
                if (pawn.Owner == PlayerType.Player1 && VoiceOverManager.Instance != null)
                {
                    VoiceOverManager.Instance.PlayPalamOneStepCloser(); // palam one step closer to victory.ogg
                }
            }
        }

        public void TriggerMatchEnd(PlayerType winner)
        {
            if (currentState == GameState.Victory || currentState == GameState.Defeat) return;

            bool is4P = (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);
            if (winner == PlayerType.Player1 && !is4P)
            {
                SetFourPlayerUnlocked(true);
                Debug.Log("[TurnAndStreakManager] Player 1 defeated Grandma in 2-Player mode! 4-Player mode is now UNLOCKED!");
            }

            if (winner == PlayerType.Player1)
            {
                SetState(GameState.Victory);
                if (VoiceOverManager.Instance != null)
                {
                    VoiceOverManager.Instance.PlayVictorySequence(); // you won.ogg + youhave conqured the thayam game.ogg
                }
            }
            else
            {
                SetState(GameState.Defeat);
            }

            OnMatchEnded?.Invoke(winner);
        }

        public void RegisterPawns(List<PawnController> p1, List<PawnController> bot)
        {
            player1Pawns = p1;
            botPawns = bot;
        }

        public void RegisterAllPawns(List<PawnController> p1, List<PawnController> p2, List<PawnController> p3Bot, List<PawnController> p4)
        {
            player1Pawns = p1;
            player2Pawns = p2;
            botPawns = p3Bot;
            player4Pawns = p4;
        }

        public void SetPlayerMode(BoardPlayerMode mode)
        {
            bool is4P = (mode == BoardPlayerMode.FourPlayers);

            if (player2Pawns != null)
            {
                foreach (var p in player2Pawns) if (p != null) p.gameObject.SetActive(is4P);
            }
            if (player4Pawns != null)
            {
                foreach (var p in player4Pawns) if (p != null) p.gameObject.SetActive(is4P);
            }

            var pawnsContainer = GameObject.Find("Pawns_Container");
            if (pawnsContainer != null)
            {
                var p2Tray = pawnsContainer.transform.Find("P2_Sanctuary_Tray");
                if (p2Tray != null) p2Tray.gameObject.SetActive(is4P);

                var p4Tray = pawnsContainer.transform.Find("P4_Sanctuary_Tray");
                if (p4Tray != null) p4Tray.gameObject.SetActive(is4P);
            }

            ResetAllPawnsToReserve();
            InitializeMatch(false);
        }

        public void ResetAllPawnsToReserve()
        {
            void ResetList(List<PawnController> pawns)
            {
                if (pawns == null) return;
                foreach (var p in pawns) if (p != null) p.ResetToReserve();
            }

            ResetList(player1Pawns);
            ResetList(player2Pawns);
            ResetList(botPawns);
            ResetList(player4Pawns);
        }
    }
}
