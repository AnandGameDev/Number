using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DayakattamDungeon.Board;
using DayakattamDungeon.Core;
using DayakattamDungeon.Entities;

namespace DayakattamDungeon.AI
{
    public enum BotDifficulty
    {
        Normal,
        Hard
    }

    /// <summary>
    /// Automated AI Opponent ("Grandma" / "Patti").
    /// Plays with the exact same Dayakattam rules, utilizing its RollPool with Normal or Hard tactical evaluation.
    /// </summary>
    public class BotAIController : MonoBehaviour
    {
        [Header("Difficulty")]
        [SerializeField] private BotDifficulty difficulty = BotDifficulty.Normal;

        [Header("Timing & Pacing")]
        [SerializeField] private float rollDelay = 0.85f;
        [SerializeField] private float actionDelay = 0.9f;

        private Coroutine activeBotRoutine;

        public BotDifficulty Difficulty
        {
            get => difficulty;
            set => difficulty = value;
        }

        private void Start()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (TurnAndStreakManager.Instance != null)
            {
                TurnAndStreakManager.Instance.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(GameState newState)
        {
            if (activeBotRoutine != null)
            {
                StopCoroutine(activeBotRoutine);
                activeBotRoutine = null;
            }

            if (newState == GameState.Bot_RollingPhase)
            {
                activeBotRoutine = StartCoroutine(BotRollingRoutine());
            }
            else if (newState == GameState.Bot_ActionPhase)
            {
                activeBotRoutine = StartCoroutine(BotActionRoutine());
            }
        }

        private IEnumerator BotRollingRoutine()
        {
            yield return new WaitForSeconds(rollDelay);

            // Keep requesting rolls while still in Bot_RollingPhase (streak allows extra rolls)
            while (TurnAndStreakManager.Instance.CurrentState == GameState.Bot_RollingPhase)
            {
                TurnAndStreakManager.Instance.RequestRoll();
                yield return new WaitForSeconds(rollDelay);
            }
        }

        private IEnumerator BotActionRoutine()
        {
            yield return new WaitForSeconds(actionDelay);

            var activeBot = TurnAndStreakManager.Instance.ActivePlayer;

            while (TurnAndStreakManager.Instance.CurrentState == GameState.Bot_ActionPhase &&
                   TurnAndStreakManager.Instance.CurrentRollPool.Count > 0 &&
                   TurnAndStreakManager.Instance.HasAnyLegalMoves(activeBot))
            {
                // Choose best move depending on difficulty
                BotMove bestMove = (difficulty == BotDifficulty.Hard)
                    ? EvaluateBestMoveHard(activeBot)
                    : EvaluateBestMoveNormal(activeBot);

                if (bestMove.pawn != null && bestMove.rollValue > 0)
                {
                    bool moveFinished = false;
                    TurnAndStreakManager.Instance.SpendRoll(bestMove.rollValue, bestMove.pawn, () =>
                    {
                        moveFinished = true;
                    });

                    // Wait for the pawn hop animation and delay
                    while (!moveFinished)
                    {
                        yield return null;
                    }
                    yield return new WaitForSeconds(actionDelay);
                }
                else
                {
                    // No valid move can be chosen, exit loop
                    break;
                }
            }

            TurnAndStreakManager.Instance.CheckActionPhaseContinuation();
        }

        private struct BotMove
        {
            public PawnController pawn;
            public int rollValue;
            public float score;

            public BotMove(PawnController p, int roll, float s)
            {
                pawn = p;
                rollValue = roll;
                score = s;
            }
        }

        private BotMove EvaluateBestMoveNormal()
        {
            var p = (TurnAndStreakManager.Instance != null) ? TurnAndStreakManager.Instance.ActivePlayer : PlayerType.Bot;
            return EvaluateBestMoveNormal(p);
        }

        private BotMove EvaluateBestMoveHard()
        {
            var p = (TurnAndStreakManager.Instance != null) ? TurnAndStreakManager.Instance.ActivePlayer : PlayerType.Bot;
            return EvaluateBestMoveHard(p);
        }

        /// <summary>
        /// Normal Mode: Evaluates rolls in order rolled.
        /// Prioritizes spawning if 1 is present, capturing vulnerable opponent pawns, or advancing furthest pawn.
        /// Has slight randomness for human-like fallibility.
        /// </summary>
        private BotMove EvaluateBestMoveNormal(PlayerType botPlayer)
        {
            var pool = TurnAndStreakManager.Instance.CurrentRollPool;
            var pawns = TurnAndStreakManager.Instance.GetPawnsForPlayer(botPlayer);

            List<BotMove> validMoves = new List<BotMove>();

            foreach (int roll in pool)
            {
                foreach (var pawn in pawns)
                {
                    if (pawn == null || !pawn.CanMove(roll)) continue;

                    float score = 10f;

                    // Priority 1: Awakening a pawn from reserve
                    if (pawn.State == PawnState.InReserve && roll == 1)
                    {
                        score += 50f;
                    }

                    // Priority 2: Immediate Capture of any opponent pawn
                    if (pawn.State == PawnState.OnBoard)
                    {
                        int targetStep = pawn.CurrentPathStep + roll;
                        BoardNode targetNode = ThayamBoardGrid.Instance.GetNodeAtStep(botPlayer, targetStep);

                        if (targetNode != null && !targetNode.IsSafeTile)
                        {
                            foreach (var occ in targetNode.Occupants)
                            {
                                if (occ != null && occ.Owner != botPlayer)
                                {
                                    score += 100f; // High priority to capture!
                                    break;
                                }
                            }
                        }

                        // Advancement
                        score += targetStep * 2f;
                    }

                    // Slight randomness
                    score += UnityEngine.Random.Range(-5f, 5f);
                    validMoves.Add(new BotMove(pawn, roll, score));
                }
            }

            if (validMoves.Count == 0) return default;

            validMoves.Sort((a, b) => b.score.CompareTo(a.score));
            return validMoves[0];
        }

        /// <summary>
        /// Hard Mode: Tactical Master.
        /// Evaluates board state with deep utility weighting:
        /// +Capture Score * ComboMultiplier, +Safe Tile (Malai), -Danger Exposure, +Palace Distance.
        /// </summary>
        private BotMove EvaluateBestMoveHard(PlayerType botPlayer)
        {
            var pool = TurnAndStreakManager.Instance.CurrentRollPool;
            var pawns = TurnAndStreakManager.Instance.GetPawnsForPlayer(botPlayer);
            float multiplier = TurnAndStreakManager.Instance.ComboMultiplier;

            List<BotMove> evaluatedMoves = new List<BotMove>();

            foreach (int roll in pool)
            {
                foreach (var pawn in pawns)
                {
                    if (pawn == null || !pawn.CanMove(roll)) continue;

                    float utility = 0f;

                    if (pawn.State == PawnState.InReserve && roll == 1)
                    {
                        utility += 60f;
                    }
                    else if (pawn.State == PawnState.OnBoard)
                    {
                        int targetStep = pawn.CurrentPathStep + roll;
                        BoardNode targetNode = ThayamBoardGrid.Instance.GetNodeAtStep(botPlayer, targetStep);

                        if (targetNode != null)
                        {
                            // 1. Capture Opportunity against any opponent pawn
                            if (!targetNode.IsSafeTile)
                            {
                                foreach (var occ in targetNode.Occupants)
                                {
                                    if (occ != null && occ.Owner != botPlayer)
                                    {
                                        utility += 120f * multiplier;
                                        break;
                                    }
                                }
                            }

                            // 2. Safe Tile (Malai) Securing
                            if (targetNode.IsSafeTile)
                            {
                                utility += 45f;
                            }
                            else
                            {
                                // 3. Danger Exposure (Check if any opponent pawn can strike this tile next turn)
                                bool threatened = IsPositionThreatenedByOpponents(targetNode, botPlayer);
                                if (threatened)
                                {
                                    utility -= 40f;
                                }
                            }

                            // 4. Palace Goal Progress
                            if (targetNode.IsPalace)
                            {
                                utility += 150f;
                            }
                            else
                            {
                                utility += (targetStep / (float)ThayamBoardGrid.TOTAL_PATH_STEPS) * 30f;
                            }
                        }
                    }

                    evaluatedMoves.Add(new BotMove(pawn, roll, utility));
                }
            }

            if (evaluatedMoves.Count == 0) return default;

            evaluatedMoves.Sort((a, b) => b.score.CompareTo(a.score));
            return evaluatedMoves[0];
        }

        private bool IsPositionThreatenedByOpponents(BoardNode node, PlayerType botPlayer)
        {
            if (node == null || node.IsSafeTile) return false;

            PlayerType[] allPlayers = (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers)
                ? new PlayerType[] { PlayerType.Player1, PlayerType.Player2, PlayerType.Player3, PlayerType.Player4 }
                : new PlayerType[] { PlayerType.Player1, PlayerType.Bot };

            foreach (var pType in allPlayers)
            {
                if (pType == botPlayer) continue;

                var oppPawns = TurnAndStreakManager.Instance.GetPawnsForPlayer(pType);
                if (oppPawns == null) continue;

                foreach (var oppPawn in oppPawns)
                {
                    if (oppPawn == null || oppPawn.State != PawnState.OnBoard) continue;

                    for (int diceReach = 1; diceReach <= 6; diceReach++)
                    {
                        int step = oppPawn.CurrentPathStep + diceReach;
                        BoardNode reachable = ThayamBoardGrid.Instance.GetNodeAtStep(pType, step);
                        if (reachable == node)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
