using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using DayakattamDungeon.Audio;
using DayakattamDungeon.Board;
using DayakattamDungeon.Core;
using DayakattamDungeon.UI;

namespace DayakattamDungeon.Entities
{
    public enum PawnState
    {
        InReserve,
        OnBoard,
        InPalace
    }

    /// <summary>
    /// Represents a Spirit Pawn (Player Bronze or Bot Obsidian).
    /// Handles grid hopping, safe haven immunity, and opponent capture strikes.
    /// Supports direct 3D mouse click and tactile drag-and-drop onto the board.
    /// </summary>
    public class PawnController : MonoBehaviour,
        IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Pawn Identity")]
        [SerializeField] private PlayerType owner;
        [SerializeField] private int pawnId;
        [SerializeField] private PawnState state = PawnState.InReserve;

        [Header("Positioning & Path")]
        [SerializeField] private int currentPathStep = -1; // -1 = Reserve, 0..23 = Perimeter, 24 = Palace
        [SerializeField] private BoardNode currentNode;
        [SerializeField] private Vector3 reservePosition;

        [Header("Visuals & Animation")]
        [SerializeField] private Renderer pawnRenderer;
        [SerializeField] private GameObject selectionGlow;
        [SerializeField] private float hopHeight = 0.6f;
        [SerializeField] private float stepDuration = 0.18f;

        private Vector3 dragStartPosition;
        private Plane dragPlane;
        private bool isDragging = false;
        private Vector3 defaultScale = Vector3.one;

        public PlayerType Owner => owner;
        public int PawnId => pawnId;
        public PawnState State => state;
        public int CurrentPathStep => currentPathStep;
        public BoardNode CurrentNode => currentNode;
        public Vector3 ReservePosition => reservePosition;
        public bool IsMoving { get; private set; }

        public event Action<PawnController, PawnController, int> OnOpponentCaptured; // attacker, victim, spiritDamage
        public event Action<PawnController> OnPawnReachedPalace;

        private void Awake()
        {
            if (state == PawnState.InReserve)
            {
                reservePosition = transform.position;
            }
            if (defaultScale == Vector3.zero)
            {
                defaultScale = transform.localScale;
            }
        }

        public void Initialize(PlayerType playerOwner, int id, Vector3 initialReservePos)
        {
            owner = playerOwner;
            pawnId = id;
            state = PawnState.InReserve;
            currentPathStep = -1;
            reservePosition = initialReservePos;
            transform.position = reservePosition;
            defaultScale = transform.localScale;

            if (selectionGlow != null) selectionGlow.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            if (selectionGlow != null)
            {
                selectionGlow.SetActive(selected);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != PlayerType.Player1 || IsMoving || isDragging) return;
            transform.localScale = defaultScale * 1.15f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (owner != PlayerType.Player1 || IsMoving || isDragging) return;
            transform.localScale = defaultScale;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isDragging) return;
            HandlePawnAction();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (owner != PlayerType.Player1 || IsMoving) return;
            if (TurnAndStreakManager.Instance == null || TurnAndStreakManager.Instance.ActivePlayer != PlayerType.Player1) return;

            dragStartPosition = transform.position;
            dragPlane = new Plane(Vector3.up, new Vector3(0, 0.8f, 0));
            isDragging = true;
            transform.localScale = defaultScale * 1.2f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            Ray ray = eventData.pressEventCamera != null ?
                eventData.pressEventCamera.ScreenPointToRay(eventData.position) :
                Camera.main.ScreenPointToRay(eventData.position);

            if (dragPlane.Raycast(ray, out float enter))
            {
                transform.position = ray.GetPoint(enter);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;
            transform.localScale = defaultScale;

            // Check if dropped near the board
            if (TurnAndStreakManager.Instance != null && TurnAndStreakManager.Instance.ActivePlayer == PlayerType.Player1)
            {
                var pool = TurnAndStreakManager.Instance.CurrentRollPool;
                if (state == PawnState.InReserve)
                {
                    if (pool.Contains(1))
                    {
                        transform.position = dragStartPosition;
                        AwakenWithRoll();
                        return;
                    }
                    else
                    {
                        if (GameJuiceManager.Instance != null)
                        {
                            GameJuiceManager.Instance.SpawnFloatingText(dragStartPosition + Vector3.up * 1f, "Needs 1 (Thayam) to Enter!", new Color(1f, 0.4f, 0.4f), 1.2f);
                        }
                    }
                }
                else if (state == PawnState.OnBoard)
                {
                    transform.position = dragStartPosition;
                    HandlePawnAction();
                    return;
                }
            }

            transform.position = dragStartPosition;
        }

        private void OnMouseDown()
        {
            HandlePawnAction();
        }

        public void HandlePawnAction()
        {
            if (owner != PlayerType.Player1) return;
            if (TurnAndStreakManager.Instance == null) return;
            if (TurnAndStreakManager.Instance.ActivePlayer != PlayerType.Player1) return;

            var turnState = TurnAndStreakManager.Instance.CurrentState;
            if (turnState != GameState.P1_ActionPhase && turnState != GameState.TutorialIntro)
            {
                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "Roll Dayakattam first!", Color.yellow, 1.0f);
                }
                return;
            }

            var pool = TurnAndStreakManager.Instance.CurrentRollPool;
            if (pool.Count == 0)
            {
                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "No banked rolls left!", Color.gray, 1.0f);
                }
                return;
            }

            // Case A: Pawn in reserve -> Awaken
            if (state == PawnState.InReserve)
            {
                if (pool.Contains(1))
                {
                    AwakenWithRoll();
                }
                else
                {
                    if (GameJuiceManager.Instance != null)
                    {
                        GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "Needs 1 (Thayam) to Awaken!", new Color(1f, 0.45f, 0.45f), 1.3f);
                    }
                }
                return;
            }

            // Case B: Pawn on board -> Advance
            if (state == PawnState.OnBoard)
            {
                int selectedRoll = GameHUD.Instance != null ? GameHUD.Instance.SelectedRoll : -1;
                if (selectedRoll > 0 && pool.Contains(selectedRoll) && CanMove(selectedRoll))
                {
                    TurnAndStreakManager.Instance.SpendRoll(selectedRoll, this);
                    return;
                }

                // If no roll selected, find first legal roll in pool
                int legalRoll = -1;
                foreach (int r in pool)
                {
                    if (CanMove(r))
                    {
                        legalRoll = r;
                        break;
                    }
                }

                if (legalRoll > 0)
                {
                    TurnAndStreakManager.Instance.SpendRoll(legalRoll, this);
                }
                else
                {
                    if (GameJuiceManager.Instance != null)
                    {
                        GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 0.8f, "No legal move for this pawn!", Color.gray, 1.0f);
                    }
                }
            }
        }

        private void AwakenWithRoll()
        {
            TurnAndStreakManager.Instance.SpendRoll(1, this);
            if (GameJuiceManager.Instance != null)
            {
                GameJuiceManager.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.0f, "AWAKENED!", Color.yellow, 1.2f);
            }
        }

        /// <summary>
        /// Can this pawn be awakened from reserve onto the board?
        /// Only allowed when spending a 1 (Thayam).
        /// </summary>
        public bool CanAwaken(int rollValue)
        {
            return state == PawnState.InReserve && rollValue == 1;
        }

        /// <summary>
        /// Can this pawn advance by the given roll value?
        /// </summary>
        public bool CanMove(int steps)
        {
            if (IsMoving) return false;

            if (state == PawnState.InReserve)
            {
                return steps == 1; // Awakening
            }

            if (state == PawnState.InPalace)
            {
                return false; // Already reached the goal
            }

            // Cannot overshoot the central Palace
            return (currentPathStep + steps) <= (ThayamBoardGrid.TOTAL_PATH_STEPS - 1);
        }

        /// <summary>
        /// Awakens the pawn onto the starting entry Malai node.
        /// </summary>
        public void Awaken(Action onComplete = null)
        {
            if (state != PawnState.InReserve || IsMoving) return;

            IsMoving = true;
            BoardNode entryNode = ThayamBoardGrid.Instance.GetPlayerEntryNode(owner);
            state = PawnState.OnBoard;
            currentPathStep = 0;

            StartCoroutine(HopToPosition(entryNode.WorldPosition + Vector3.up * 0.2f, () =>
            {
                if (currentNode != null) currentNode.UnregisterOccupant(this);
                currentNode = entryNode;
                currentNode.RegisterOccupant(this);
                if (DayakattamAudioPlayer.Instance != null) DayakattamAudioPlayer.Instance.PlayAwakenChime();
                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.SpawnDustRing(entryNode.WorldPosition, new Color(1f, 0.9f, 0.55f, 0.85f), 0.78f, 0.30f);
                }
                IsMoving = false;
                onComplete?.Invoke();
            }));
        }

        /// <summary>
        /// Smoothly moves the pawn forward along the board by the specified number of steps.
        /// </summary>
        public void AdvanceSteps(int steps, float comboMultiplier, Action onComplete = null)
        {
            if (!CanMove(steps))
            {
                onComplete?.Invoke();
                return;
            }

            if (state == PawnState.InReserve && steps == 1)
            {
                Awaken(onComplete);
                return;
            }

            StartCoroutine(AdvanceRoutine(steps, comboMultiplier, onComplete));
        }

        private IEnumerator AdvanceRoutine(int steps, float comboMultiplier, Action onComplete)
        {
            IsMoving = true;
            if (currentNode != null)
            {
                currentNode.UnregisterOccupant(this);
            }

            for (int s = 1; s <= steps; s++)
            {
                currentPathStep++;
                BoardNode nextNode = ThayamBoardGrid.Instance.GetNodeAtStep(owner, currentPathStep);
                Vector3 targetPos = nextNode.WorldPosition + Vector3.up * 0.2f;

                yield return StartCoroutine(HopToPosition(targetPos, null));
                if (DayakattamAudioPlayer.Instance != null) DayakattamAudioPlayer.Instance.PlayHop(s - 1, steps);
                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.TriggerShake(0.04f);
                    if (s == steps)
                    {
                        GameJuiceManager.Instance.SpawnDustRing(nextNode.WorldPosition, new Color(0.95f, 0.88f, 0.7f, 0.75f), 0.72f, 0.28f);
                    }
                }
            }

            BoardNode finalNode = ThayamBoardGrid.Instance.GetNodeAtStep(owner, currentPathStep);
            currentNode = finalNode;
            finalNode.RegisterOccupant(this);

            // Check if arrived at Palace
            if (finalNode.IsPalace)
            {
                state = PawnState.InPalace;
                OnPawnReachedPalace?.Invoke(this);

                if (DayakattamAudioPlayer.Instance != null)
                {
                    DayakattamAudioPlayer.Instance.PlayPalaceChime();
                }

                if (GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.TriggerShake(0.35f);
                    GameJuiceManager.Instance.SpawnFloatingText(finalNode.WorldPosition, "PALACE UNLOCKED!", new Color(1f, 0.85f, 0.2f), 1.4f);
                    GameJuiceManager.Instance.SpawnPalaceBeacon(finalNode.WorldPosition, new Color(1f, 0.85f, 0.25f));
                }
            }
            else
            {
                if (finalNode.IsSafeTile && GameJuiceManager.Instance != null)
                {
                    GameJuiceManager.Instance.SpawnFloatingText(finalNode.WorldPosition, "SANCTUARY", new Color(0.4f, 0.9f, 1f), 1.0f);
                    GameJuiceManager.Instance.SpawnImpactSparks(finalNode.WorldPosition, new Color(0.3f, 0.85f, 1f), 12);
                }

                // Check for opponent capture on non-Safe Tiles
                CheckForCapture(finalNode, comboMultiplier);
            }

            IsMoving = false;
            onComplete?.Invoke();
        }

        private void CheckForCapture(BoardNode node, float comboMultiplier)
        {
            if (node.IsSafeTile)
            {
                // Safe Tile (Malai): Immunity! Opponent pawns can co-exist.
                return;
            }

            // Unsafe tile: check if any occupant is an opponent
            for (int i = node.Occupants.Count - 1; i >= 0; i--)
            {
                PawnController occupant = node.Occupants[i];
                if (occupant != null && occupant != this && occupant.Owner != this.owner)
                {
                    // Opponent pawn captured / cut! Returns to reserve!
                    occupant.BeCaptured();
                    OnOpponentCaptured?.Invoke(this, occupant, 0);

                    if (DayakattamAudioPlayer.Instance != null)
                    {
                        DayakattamAudioPlayer.Instance.PlayCaptureStrike();
                    }

                    if (GameJuiceManager.Instance != null)
                    {
                        GameJuiceManager.Instance.TriggerShake(0.35f);
                        GameJuiceManager.Instance.TriggerHitStop(0.065f);
                        GameJuiceManager.Instance.SpawnFloatingText(node.WorldPosition, "CUT! SENT TO RESERVE!", new Color(1f, 0.35f, 0.35f), 1.35f);
                        GameJuiceManager.Instance.SpawnImpactFlash(node.WorldPosition + Vector3.up * 0.25f, new Color(1f, 0.35f, 0.1f), 1.85f, 0.24f);
                        GameJuiceManager.Instance.SpawnImpactSparks(node.WorldPosition, new Color(1f, 0.35f, 0.1f), 24);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Called when this pawn is struck/cut by an opponent on an unsafe tile.
        /// Sends pawn back to spirit reserve.
        /// </summary>
        public void BeCaptured()
        {
            if (currentNode != null)
            {
                currentNode.UnregisterOccupant(this);
                currentNode = null;
            }

            state = PawnState.InReserve;
            currentPathStep = -1;

            StartCoroutine(HopToPosition(reservePosition, null));
        }

        /// <summary>
        /// Resets the pawn back to spirit reserve immediately, clearing board occupancy.
        /// </summary>
        public void ResetToReserve()
        {
            StopAllCoroutines();
            IsMoving = false;
            isDragging = false;
            if (currentNode != null)
            {
                currentNode.UnregisterOccupant(this);
                currentNode = null;
            }
            state = PawnState.InReserve;
            currentPathStep = -1;
            transform.position = reservePosition;
            transform.localScale = defaultScale;
            if (selectionGlow != null) selectionGlow.SetActive(false);
        }

        private IEnumerator HopToPosition(Vector3 targetPos, Action onDone)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            while (elapsed < stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / stepDuration);
                // Parabolic hop
                float arc = 4f * t * (1f - t) * hopHeight;
                Vector3 current = Vector3.Lerp(startPos, targetPos, t);
                current.y += arc;
                transform.position = current;

                // Mid-air vertical stretch
                float stretch = Mathf.Sin(t * Mathf.PI) * 0.16f;
                transform.localScale = new Vector3(
                    defaultScale.x * (1f - stretch * 0.55f),
                    defaultScale.y * (1f + stretch),
                    defaultScale.z * (1f - stretch * 0.55f)
                );
                yield return null;
            }

            transform.position = targetPos;

            // Snappy landing squash and spring back
            float squashDur = 0.09f;
            float sElapsed = 0f;
            Vector3 squashedScale = new Vector3(defaultScale.x * 1.22f, defaultScale.y * 0.78f, defaultScale.z * 1.22f);
            while (sElapsed < squashDur)
            {
                sElapsed += Time.deltaTime;
                float st = Mathf.Clamp01(sElapsed / squashDur);
                transform.localScale = Vector3.Lerp(squashedScale, defaultScale, st);
                yield return null;
            }

            transform.localScale = defaultScale;
            onDone?.Invoke();
        }
    }
}
