using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DayakattamDungeon.Board
{
    using DayakattamDungeon.Entities;

    /// <summary>
    /// Represents a single tile on the sacred Dayakattam board.
    /// Can be a standard tile, an auspicious Safe Tile (Malai), or the central Palace.
    /// </summary>
    public class BoardNode : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        [Header("Node Info")]
        [SerializeField] private int nodeIndex;
        [SerializeField] private int gridRow = -1;
        [SerializeField] private int gridCol = -1;
        [SerializeField] private bool isSafeTile = false; // "Malai" (Crossed square - immunity from capture)
        [SerializeField] private bool isPalace = false;   // Central goal square

        [Header("Visual Components")]
        [SerializeField] private MeshRenderer tileRenderer;
        [SerializeField] private GameObject malaiCrossVisual;
        [SerializeField] private GameObject highlightVisual;

        private readonly List<PawnController> currentOccupants = new List<PawnController>();

        public int NodeIndex => nodeIndex;
        public int GridRow => gridRow;
        public int GridCol => gridCol;
        public bool IsSafeTile => isSafeTile;
        public bool IsPalace => isPalace;
        public Vector3 WorldPosition => transform.position;
        public IReadOnlyList<PawnController> Occupants => currentOccupants;

        public void Initialize(int index, bool safeTile, bool palace, int row = -1, int col = -1)
        {
            nodeIndex = index;
            isSafeTile = safeTile;
            isPalace = palace;
            gridRow = row;
            gridCol = col;

            if (tileRenderer == null)
            {
                tileRenderer = GetComponent<MeshRenderer>();
            }

            if (malaiCrossVisual != null)
            {
                malaiCrossVisual.SetActive(isSafeTile);
            }

            if (highlightVisual != null)
            {
                highlightVisual.SetActive(false);
            }
        }

        public void SetTileVisuals(Color baseColor, Color? crossColor = null)
        {
            if (tileRenderer == null) tileRenderer = GetComponent<MeshRenderer>();
            if (tileRenderer != null)
            {
                tileRenderer.material.color = baseColor;
            }

            if (crossColor.HasValue && malaiCrossVisual != null)
            {
                foreach (var rend in malaiCrossVisual.GetComponentsInChildren<Renderer>())
                {
                    if (rend != null) rend.material.color = crossColor.Value;
                }
            }
        }

        public void SetHighlight(bool active, Color? color = null)
        {
            if (highlightVisual == null && active)
            {
                highlightVisual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                highlightVisual.name = "TileHighlight";
                highlightVisual.transform.parent = transform;
                highlightVisual.transform.localPosition = new Vector3(0, 0.55f, 0);
                highlightVisual.transform.localScale = new Vector3(0.9f, 0.04f, 0.9f);
                var col = highlightVisual.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }

            if (highlightVisual != null)
            {
                highlightVisual.SetActive(active);
                if (active && color.HasValue)
                {
                    var r = highlightVisual.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.material.color = color.Value;
                    }
                }
            }
        }

        public void RegisterOccupant(PawnController pawn)
        {
            if (!currentOccupants.Contains(pawn))
            {
                currentOccupants.Add(pawn);
            }
            UpdateOccupantVisualOffsets();
        }

        public void UnregisterOccupant(PawnController pawn)
        {
            currentOccupants.Remove(pawn);
            UpdateOccupantVisualOffsets();
        }

        /// <summary>
        /// When multiple pawns occupy a Safe Tile, spread them slightly so both are visible.
        /// </summary>
        private void UpdateOccupantVisualOffsets()
        {
            if (currentOccupants.Count <= 1) return;

            float angleStep = 360f / currentOccupants.Count;
            float radius = 0.25f;

            for (int i = 0; i < currentOccupants.Count; i++)
            {
                if (currentOccupants[i] != null)
                {
                    float rad = i * angleStep * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radius;
                    currentOccupants[i].transform.position = transform.position + offset + Vector3.up * 0.2f;
                }
            }
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            if (DayakattamDungeon.Core.TurnAndStreakManager.Instance == null) return;
            if (DayakattamDungeon.Core.TurnAndStreakManager.Instance.ActivePlayer != DayakattamDungeon.Board.PlayerType.Player1) return;

            var turnState = DayakattamDungeon.Core.TurnAndStreakManager.Instance.CurrentState;
            if (turnState != DayakattamDungeon.Core.GameState.P1_ActionPhase &&
                turnState != DayakattamDungeon.Core.GameState.TutorialIntro) return;

            var pool = DayakattamDungeon.Core.TurnAndStreakManager.Instance.CurrentRollPool;
            var pawns = DayakattamDungeon.Core.TurnAndStreakManager.Instance.Player1Pawns;

            // If clicked on Player 1's starting entry tile and pool has 1:
            bool isP1Entry = (nodeIndex == 0) || (ThayamBoardGrid.Instance != null && this == ThayamBoardGrid.Instance.GetPlayerEntryNode(PlayerType.Player1));
            if (isP1Entry && pool.Contains(1))
            {
                var reservePawn = System.Linq.Enumerable.FirstOrDefault(pawns, p => p.State == PawnState.InReserve);
                if (reservePawn != null)
                {
                    DayakattamDungeon.Core.TurnAndStreakManager.Instance.SpendRoll(1, reservePawn);
                }
            }
        }
    }
}
