using System;
using System.Collections.Generic;
using UnityEngine;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Core;

namespace DayakattamDungeon.Board
{
    public enum PlayerType
    {
        Player1, // South (Red)
        Bot,     // Default 2P opponent (North, Green)
        Player2, // East (Blue)
        Player3, // North (Green)
        Player4  // West (Yellow)
    }

    public enum BoardPlayerMode
    {
        TwoPlayers,
        FourPlayers
    }

    /// <summary>
    /// Manages the full 7x7 (49 squares) sacred Dayakattam tactical grid:
    /// - Outer Perimeter Loop (24 tiles)
    /// - Middle Ring Loop (16 tiles)
    /// - Inner Ring Loop (8 tiles)
    /// - Central Sacred Palace (Goal)
    /// - 4 Colored Entry Midpoints (South Red, East Blue, North Green, West Yellow)
    /// - 4 Inner Corner Safe Squares (Malai with X)
    /// - Dynamic 2-Player (2 colors) vs 4-Player (4 colors) outer ring presentation
    /// - Semi-transparent way-point path line for intuitive player navigation
    /// </summary>
    public class ThayamBoardGrid : MonoBehaviour
    {
        private static ThayamBoardGrid instance;
        public static ThayamBoardGrid Instance
        {
            get
            {
                if (instance == null) instance = UnityEngine.Object.FindFirstObjectByType<ThayamBoardGrid>();
                return instance;
            }
            private set => instance = value;
        }

        public const int GRID_SIZE = 7;
        public const int TOTAL_TILES = 49;
        public const int TOTAL_PATH_STEPS = 49; // Steps 0..47 along path, step 48 = Central Palace (Goal)
        public const int PALACE_INDEX = 48;

        [Header("Player & Display Mode")]
        [SerializeField] private BoardPlayerMode currentMode = BoardPlayerMode.TwoPlayers;
        [SerializeField] private bool showWaypointPath = true;
        [SerializeField] private PlayerType currentWaypointPlayer = PlayerType.Player1;

        [Header("Procedural Materials")]
        [SerializeField] private Material tileStandardMat;
        [SerializeField] private Material tileMalaiMat;
        [SerializeField] private Material tilePalaceMat;

        [Header("Player Colors")]
        [SerializeField] private Color colorSouth = new Color(0.92f, 0.20f, 0.20f); // Red
        [SerializeField] private Color colorEast  = new Color(0.12f, 0.38f, 0.95f); // Blue
        [SerializeField] private Color colorNorth = new Color(0.18f, 0.85f, 0.25f); // Green
        [SerializeField] private Color colorWest  = new Color(0.96f, 0.88f, 0.20f); // Yellow
        [SerializeField] private Color colorPalace= new Color(0.96f, 0.52f, 0.15f); // Orange
        [SerializeField] private Color colorStone = new Color(0.12f, 0.12f, 0.14f); // Dark Stone
        [SerializeField] private Color colorCross = new Color(1.0f, 0.90f, 0.40f); // Gold Cross

        // 7x7 Grid storage: gridNodes[row, col]
        private readonly BoardNode[,] gridNodes = new BoardNode[GRID_SIZE, GRID_SIZE];
        private readonly List<BoardNode> allGridNodes = new List<BoardNode>();

        // Precomputed 49-step paths for each participant
        private readonly List<BoardNode> p1Path = new List<BoardNode>();
        private readonly List<BoardNode> p2Path = new List<BoardNode>();
        private readonly List<BoardNode> p3Path = new List<BoardNode>();
        private readonly List<BoardNode> p4Path = new List<BoardNode>();

        // Waypoint Line & Markers
        private LineRenderer waypointLine;
        private GameObject waypointMarkersContainer;

        public BoardPlayerMode CurrentMode => currentMode;
        public bool ShowWaypointPath => showWaypointPath;
        public IReadOnlyList<BoardNode> AllNodes => allGridNodes;

        #region Path Definition (7x7 Spiral from South Entry into Center Palace)

        // Player 1 Path (South Entry [6, 3] -> Outer -> Diagonal [6,2]->[5,1] -> Middle -> Inner -> Palace [3,3])
        private static readonly Vector2Int[] P1_COORDS = new Vector2Int[]
        {
            // Outer Ring (24 steps: 0..23)
            new Vector2Int(6, 3), // 0: South Red Entry (Safe Tile)
            new Vector2Int(6, 4), // 1
            new Vector2Int(6, 5), // 2
            new Vector2Int(6, 6), // 3: Bottom-Right Corner
            new Vector2Int(5, 6), // 4
            new Vector2Int(4, 6), // 5
            new Vector2Int(3, 6), // 6: East Entry (Safe Tile)
            new Vector2Int(2, 6), // 7
            new Vector2Int(1, 6), // 8
            new Vector2Int(0, 6), // 9: Top-Right Corner
            new Vector2Int(0, 5), // 10
            new Vector2Int(0, 4), // 11
            new Vector2Int(0, 3), // 12: North Green Entry (Safe Tile)
            new Vector2Int(0, 2), // 13
            new Vector2Int(0, 1), // 14
            new Vector2Int(0, 0), // 15: Top-Left Corner
            new Vector2Int(1, 0), // 16
            new Vector2Int(2, 0), // 17
            new Vector2Int(3, 0), // 18: West Yellow Entry (Safe Tile)
            new Vector2Int(4, 0), // 19
            new Vector2Int(5, 0), // 20
            new Vector2Int(6, 0), // 21: Bottom-Left Corner
            new Vector2Int(6, 1), // 22
            new Vector2Int(6, 2), // 23: Outer turning point before South

            // Diagonal Transition into Middle Ring (Step 24)
            new Vector2Int(5, 1), // 24: Bottom-Left Inner Corner (Malai Safe Tile)

            // Middle Ring (15 steps: 25..39)
            new Vector2Int(4, 1), // 25
            new Vector2Int(3, 1), // 26
            new Vector2Int(2, 1), // 27
            new Vector2Int(1, 1), // 28: Top-Left Inner Corner (Malai Safe Tile)
            new Vector2Int(1, 2), // 29
            new Vector2Int(1, 3), // 30
            new Vector2Int(1, 4), // 31
            new Vector2Int(1, 5), // 32: Top-Right Inner Corner (Malai Safe Tile)
            new Vector2Int(2, 5), // 33
            new Vector2Int(3, 5), // 34
            new Vector2Int(4, 5), // 35
            new Vector2Int(5, 5), // 36: Bottom-Right Inner Corner (Malai Safe Tile)
            new Vector2Int(5, 4), // 37
            new Vector2Int(5, 3), // 38
            new Vector2Int(5, 2), // 39: Middle turning point

            // Inner Ring (8 steps: 40..47)
            new Vector2Int(4, 2), // 40
            new Vector2Int(3, 2), // 41
            new Vector2Int(2, 2), // 42
            new Vector2Int(2, 3), // 43
            new Vector2Int(2, 4), // 44
            new Vector2Int(3, 4), // 45
            new Vector2Int(4, 4), // 46
            new Vector2Int(4, 3), // 47: Inner turning point

            // Central Sacred Palace (Step 48 = Final Goal)
            new Vector2Int(3, 3)  // 48: Central Orange Palace
        };

        // Key turning points along the path where directional waypoint arrows should appear
        private static readonly int[] ARROW_STEP_INDICES = new int[]
        {
            1, 3, 6, 9, 12, 15, 18, 21, 23, 24, 27, 31, 35, 38, 40, 42, 44, 46, 47
        };

        private static Mesh arrowMeshCache = null;
        private static Mesh GetOrCreateArrowMesh()
        {
            if (arrowMeshCache != null) return arrowMeshCache;
            var mesh = new Mesh { name = "WaypointArrowChevron" };
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, 0f, 0.38f),      // 0: Tip
                new Vector3(-0.26f, 0f, -0.20f), // 1: Left Wing
                new Vector3(0f, 0f, -0.05f),     // 2: Inner Notch
                new Vector3(0.26f, 0f, -0.20f)   // 3: Right Wing
            };
            mesh.triangles = new int[]
            {
                0, 2, 1, 0, 1, 2,
                0, 3, 2, 0, 2, 3
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            arrowMeshCache = mesh;
            return arrowMeshCache;
        }

        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildNodeCacheAndPaths();
        }

        private void Start()
        {
            if (!TurnAndStreakManager.IsFourPlayerUnlocked())
            {
                currentMode = BoardPlayerMode.TwoPlayers;
            }
            BuildNodeCacheAndPaths();
            ApplyModeVisuals();
            UpdateWaypointLine();
        }

        public event Action<BoardPlayerMode> OnPlayerModeChanged;

        public void SetPlayerMode(BoardPlayerMode mode)
        {
            if (mode == BoardPlayerMode.FourPlayers && !TurnAndStreakManager.IsFourPlayerUnlocked())
            {
                Debug.LogWarning("[ThayamBoardGrid] Cannot switch to 4-Player mode: Player must win in 2-Player mode first to unlock!");
                mode = BoardPlayerMode.TwoPlayers;
            }
            currentMode = mode;
            ApplyModeVisuals();
            OnPlayerModeChanged?.Invoke(currentMode);
            Debug.Log($"[ThayamBoardGrid] Switched to {currentMode} mode.");
        }

        public void ToggleWaypointPath()
        {
            showWaypointPath = !showWaypointPath;
            if (waypointLine != null) waypointLine.gameObject.SetActive(showWaypointPath);
            if (waypointMarkersContainer != null) waypointMarkersContainer.SetActive(showWaypointPath);
        }

        public void SetWaypointVisible(bool visible)
        {
            showWaypointPath = visible;
            if (waypointLine != null) waypointLine.gameObject.SetActive(showWaypointPath);
            if (waypointMarkersContainer != null) waypointMarkersContainer.SetActive(showWaypointPath);
        }

        public PlayerType CurrentWaypointPlayer => currentWaypointPlayer;

        public void ShowPathForPlayer(PlayerType player)
        {
            currentWaypointPlayer = player;
            UpdateWaypointLine();
        }

        #region Path & Node Queries

        public BoardNode GetNodeAtStep(PlayerType player, int stepIndex)
        {
            var path = GetPathForPlayer(player);
            if (path == null || path.Count == 0) return null;

            int clamped = Mathf.Clamp(stepIndex, 0, path.Count - 1);
            return path[clamped];
        }

        public BoardNode GetPlayerEntryNode(PlayerType player)
        {
            var path = GetPathForPlayer(player);
            return (path != null && path.Count > 0) ? path[0] : null;
        }

        public BoardNode GetPalaceNode()
        {
            return (p1Path.Count > PALACE_INDEX) ? p1Path[PALACE_INDEX] : gridNodes[3, 3];
        }

        public bool IsStepSafeTile(PlayerType player, int stepIndex)
        {
            BoardNode node = GetNodeAtStep(player, stepIndex);
            return node != null && node.IsSafeTile;
        }

        public List<BoardNode> GetPathForPlayer(PlayerType player)
        {
            switch (player)
            {
                case PlayerType.Player1: return p1Path;
                case PlayerType.Player2: return p2Path;
                case PlayerType.Bot:
                case PlayerType.Player3: return p3Path;
                case PlayerType.Player4: return p4Path;
                default: return p1Path;
            }
        }

        public void ClearHighlights()
        {
            foreach (var node in allGridNodes)
            {
                if (node != null) node.SetHighlight(false);
            }
        }

        public void HighlightMovePreview(PlayerType player, int steps, PawnController pawn)
        {
            ClearHighlights();
            if (pawn == null || !pawn.CanMove(steps)) return;

            int targetStep = (pawn.State == PawnState.InReserve) ? 0 : pawn.CurrentPathStep + steps;
            BoardNode targetNode = GetNodeAtStep(player, targetStep);
            if (targetNode == null) return;

            Color highlightColor;
            if (targetNode.IsPalace)
            {
                highlightColor = new Color(1f, 0.85f, 0.2f, 0.95f); // Radiant Gold
            }
            else if (targetNode.IsSafeTile)
            {
                highlightColor = new Color(0.2f, 0.9f, 1f, 0.95f); // Cyan Malai Sanctuary
            }
            else
            {
                bool hasOpponent = false;
                foreach (var occ in targetNode.Occupants)
                {
                    if (occ != null && occ.Owner != player)
                    {
                        hasOpponent = true;
                        break;
                    }
                }
                highlightColor = hasOpponent ? new Color(1f, 0.2f, 0.2f, 0.95f) : new Color(0.2f, 1f, 0.4f, 0.9f);
            }

            targetNode.SetHighlight(true, highlightColor);
        }

        #endregion

        #region Board Generation (Full 7x7 Grid & Waypoints)

        [ContextMenu("Generate 7x7 Thayam Board")]
        public void GenerateGreyboxBoard()
        {
            // Clear existing children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
            allGridNodes.Clear();

            float spacing = 1.4f;

            // Generate 49 tiles (7x7)
            for (int r = 0; r < GRID_SIZE; r++)
            {
                for (int c = 0; c < GRID_SIZE; c++)
                {
                    float x = (c - 3) * spacing;
                    float z = (3 - r) * spacing;
                    Vector3 worldPos = new Vector3(x, 0, z);

                    bool isCenterPalace = (r == 3 && c == 3);
                    bool isSouthEntry   = (r == 6 && c == 3);
                    bool isEastEntry    = (r == 3 && c == 6);
                    bool isNorthEntry   = (r == 0 && c == 3);
                    bool isWestEntry    = (r == 3 && c == 0);
                    bool isInnerCorner  = (r == 1 && c == 1) || (r == 1 && c == 5) || (r == 5 && c == 1) || (r == 5 && c == 5);

                    bool hasCross = isCenterPalace || isSouthEntry || isEastEntry || isNorthEntry || isWestEntry || isInnerCorner;
                    bool isSafe   = hasCross; // All crossed tiles grant sanctuary

                    GameObject tileGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tileGO.name = $"Tile_R{r}_C{c}" + (hasCross ? "_Malai" : "");
                    tileGO.transform.parent = transform;
                    tileGO.transform.position = worldPos;
                    tileGO.transform.localScale = new Vector3(1.28f, 0.2f, 1.28f);

                    // Material
                    if (tileStandardMat != null)
                    {
                        tileGO.GetComponent<Renderer>().sharedMaterial = tileStandardMat;
                    }
                    else
                    {
                        tileGO.GetComponent<Renderer>().material.color = colorStone;
                    }

                    // Add authentic crossed-X visual marker
                    GameObject crossRoot = null;
                    if (hasCross)
                    {
                        crossRoot = new GameObject("Malai_Cross_X");
                        crossRoot.transform.parent = tileGO.transform;
                        crossRoot.transform.localPosition = new Vector3(0, 0.52f, 0);

                        // Bar 1 (+45 degrees)
                        GameObject bar1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bar1.name = "Bar_Diag_1";
                        bar1.transform.parent = crossRoot.transform;
                        bar1.transform.localPosition = Vector3.zero;
                        bar1.transform.localRotation = Quaternion.Euler(0, 45f, 0);
                        bar1.transform.localScale = new Vector3(0.18f, 0.06f, 0.95f);
                        DestroyImmediate(bar1.GetComponent<Collider>());

                        // Bar 2 (-45 degrees)
                        GameObject bar2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        bar2.name = "Bar_Diag_2";
                        bar2.transform.parent = crossRoot.transform;
                        bar2.transform.localPosition = Vector3.zero;
                        bar2.transform.localRotation = Quaternion.Euler(0, -45f, 0);
                        bar2.transform.localScale = new Vector3(0.18f, 0.06f, 0.95f);
                        DestroyImmediate(bar2.GetComponent<Collider>());

                        if (tileMalaiMat != null)
                        {
                            bar1.GetComponent<Renderer>().sharedMaterial = tileMalaiMat;
                            bar2.GetComponent<Renderer>().sharedMaterial = tileMalaiMat;
                        }
                        else
                        {
                            bar1.GetComponent<Renderer>().material.color = colorCross;
                            bar2.GetComponent<Renderer>().material.color = colorCross;
                        }
                    }

                    // Central Palace extra dais
                    if (isCenterPalace)
                    {
                        GameObject palaceDais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        palaceDais.name = "Palace_Inner_Sanctum";
                        palaceDais.transform.parent = tileGO.transform;
                        palaceDais.transform.localPosition = new Vector3(0, 0.55f, 0);
                        palaceDais.transform.localScale = new Vector3(0.72f, 0.5f, 0.72f);
                        DestroyImmediate(palaceDais.GetComponent<Collider>());
                        if (tilePalaceMat != null) palaceDais.GetComponent<Renderer>().sharedMaterial = tilePalaceMat;
                        else palaceDais.GetComponent<Renderer>().material.color = colorPalace;

                        GameObject pinnacle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        pinnacle.name = "Palace_Pinnacle";
                        pinnacle.transform.parent = tileGO.transform;
                        pinnacle.transform.localPosition = new Vector3(0, 0.95f, 0);
                        pinnacle.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                        DestroyImmediate(pinnacle.GetComponent<Collider>());
                        if (tilePalaceMat != null) pinnacle.GetComponent<Renderer>().sharedMaterial = tilePalaceMat;
                        else pinnacle.GetComponent<Renderer>().material.color = colorCross;
                    }

                    BoardNode node = tileGO.AddComponent<BoardNode>();
                    int flatIndex = r * GRID_SIZE + c;
                    node.Initialize(flatIndex, isSafe, isCenterPalace, r, c);

                    // Reflection field assignment for malaiCrossVisual
                    var fCross = typeof(BoardNode).GetField("malaiCrossVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (fCross != null && crossRoot != null) fCross.SetValue(node, crossRoot);

                    gridNodes[r, c] = node;
                    allGridNodes.Add(node);
                }
            }

            BuildNodeCacheAndPaths();
            ApplyModeVisuals();
            BuildWaypointLineAndArrows();

            Debug.Log("[ThayamBoardGrid] Generated full 7x7 (49 tiles) Dayakattam board with sacred spiral paths.");
        }

        public void BuildNodeCacheAndPaths()
        {
            // Populate gridNodes from children if not populated
            if (gridNodes[0, 0] == null || allGridNodes.Count < TOTAL_TILES)
            {
                allGridNodes.Clear();
                var nodes = GetComponentsInChildren<BoardNode>();
                foreach (var n in nodes)
                {
                    if (n.GridRow >= 0 && n.GridCol >= 0 && n.GridRow < GRID_SIZE && n.GridCol < GRID_SIZE)
                    {
                        gridNodes[n.GridRow, n.GridCol] = n;
                    }
                    allGridNodes.Add(n);
                }
            }

            // Construct Player 1 Path
            p1Path.Clear();
            for (int i = 0; i < P1_COORDS.Length; i++)
            {
                var c = P1_COORDS[i];
                var n = gridNodes[c.x, c.y];
                if (n != null) p1Path.Add(n);
            }

            // Construct Player 2 (East) Path: rotated 90 deg CW
            p2Path.Clear();
            for (int i = 0; i < P1_COORDS.Length; i++)
            {
                var rot = Rotate90CW(P1_COORDS[i]);
                var n = gridNodes[rot.x, rot.y];
                if (n != null) p2Path.Add(n);
            }

            // Construct Player 3 / Bot (North) Path: rotated 180 deg
            p3Path.Clear();
            for (int i = 0; i < P1_COORDS.Length; i++)
            {
                var rot = Rotate180(P1_COORDS[i]);
                var n = gridNodes[rot.x, rot.y];
                if (n != null) p3Path.Add(n);
            }

            // Construct Player 4 (West) Path: rotated 270 deg CW
            p4Path.Clear();
            for (int i = 0; i < P1_COORDS.Length; i++)
            {
                var rot = Rotate270CW(P1_COORDS[i]);
                var n = gridNodes[rot.x, rot.y];
                if (n != null) p4Path.Add(n);
            }
        }

        private Vector2Int Rotate90CW(Vector2Int coord)
        {
            // (r, c) -> (c, 6 - r)
            return new Vector2Int(coord.y, 6 - coord.x);
        }

        private Vector2Int Rotate180(Vector2Int coord)
        {
            // (r, c) -> (6 - r, 6 - c)
            return new Vector2Int(6 - coord.x, 6 - coord.y);
        }

        private Vector2Int Rotate270CW(Vector2Int coord)
        {
            // (r, c) -> (6 - c, r)
            return new Vector2Int(6 - coord.y, coord.x);
        }

        #endregion

        #region Visual Presentation (2-Player vs 4-Player & Waypoints)

        public void ApplyModeVisuals()
        {
            BuildNodeCacheAndPaths();

            // Central Palace: Always Orange
            var palace = gridNodes[3, 3];
            if (palace != null)
            {
                palace.SetTileVisuals(colorPalace, colorCross);
            }

            // South Entry: Always Red (Player 1)
            var south = gridNodes[6, 3];
            if (south != null)
            {
                south.SetTileVisuals(colorSouth, Color.white);
            }

            // North Entry: Always Green (Bot / Player 3)
            var north = gridNodes[0, 3];
            if (north != null)
            {
                north.SetTileVisuals(colorNorth, Color.white);
            }

            // East Entry: Blue in 4P mode, Neutral Stone in 2P mode
            var east = gridNodes[3, 6];
            if (east != null)
            {
                if (currentMode == BoardPlayerMode.FourPlayers)
                {
                    east.SetTileVisuals(colorEast, Color.white);
                }
                else
                {
                    east.SetTileVisuals(colorStone, colorCross);
                }
            }

            // West Entry: Yellow in 4P mode, Neutral Stone in 2P mode
            var west = gridNodes[3, 0];
            if (west != null)
            {
                if (currentMode == BoardPlayerMode.FourPlayers)
                {
                    west.SetTileVisuals(colorWest, Color.black);
                }
                else
                {
                    west.SetTileVisuals(colorStone, colorCross);
                }
            }

            // 4 Inner Corner Safe Squares (always Stone with Gold Cross)
            Vector2Int[] innerCorners = new Vector2Int[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(1, 5),
                new Vector2Int(5, 1),
                new Vector2Int(5, 5)
            };

            foreach (var ic in innerCorners)
            {
                var n = gridNodes[ic.x, ic.y];
                if (n != null)
                {
                    n.SetTileVisuals(colorStone, colorCross);
                }
            }
        }

        private void BuildWaypointLineAndArrows()
        {
            // 1. Line Renderer
            Transform lineTr = transform.Find("Waypoint_Path_Trail");
            GameObject lineGO;
            if (lineTr != null)
            {
                lineGO = lineTr.gameObject;
            }
            else
            {
                lineGO = new GameObject("Waypoint_Path_Trail");
                lineGO.transform.parent = transform;
                lineGO.transform.localPosition = Vector3.zero;
            }

            waypointLine = lineGO.GetComponent<LineRenderer>();
            if (waypointLine == null)
            {
                waypointLine = lineGO.AddComponent<LineRenderer>();
            }
            waypointLine.useWorldSpace = true;
            waypointLine.startWidth = 0.08f;
            waypointLine.endWidth = 0.08f;
            waypointLine.positionCount = p1Path.Count;

            // Semi-transparent royal blue / cyan material
            Material lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
            lineMat.color = new Color(0.18f, 0.55f, 1.0f, 0.45f);
            waypointLine.material = lineMat;

            // 2. Waypoint Directional Arrow Markers
            Transform markersTr = transform.Find("Waypoint_Markers_Container");
            if (markersTr != null)
            {
                DestroyImmediate(markersTr.gameObject);
            }
            waypointMarkersContainer = new GameObject("Waypoint_Markers_Container");
            waypointMarkersContainer.transform.parent = transform;
            waypointMarkersContainer.transform.localPosition = Vector3.zero;

            UpdateWaypointLine();
        }

        public void UpdateWaypointLine()
        {
            if (waypointLine == null)
            {
                BuildWaypointLineAndArrows();
                return;
            }

            if (p1Path.Count == 0 || p3Path.Count == 0) BuildNodeCacheAndPaths();

            var activePath = GetPathForPlayer(currentWaypointPlayer);
            if (activePath == null || activePath.Count == 0) activePath = p1Path;

            waypointLine.positionCount = activePath.Count;
            for (int i = 0; i < activePath.Count; i++)
            {
                Vector3 pos = activePath[i].WorldPosition + Vector3.up * 0.22f;
                waypointLine.SetPosition(i, pos);
            }

            // Material colors based on active participant:
            // Player 1 (South): Crimson Red
            // Player 2 (East): Sapphire Blue
            // Player 3 / Grandma (North): Emerald Green
            // Player 4 (West): Radiant Golden Amber
            Color lineColor;
            Color arrowColor;
            switch (currentWaypointPlayer)
            {
                case PlayerType.Player1:
                    lineColor = new Color(0.95f, 0.22f, 0.22f, 0.55f);
                    arrowColor = new Color(1.0f, 0.35f, 0.35f, 0.95f);
                    break;
                case PlayerType.Player2:
                    lineColor = new Color(0.20f, 0.45f, 1.0f, 0.50f);
                    arrowColor = new Color(0.35f, 0.70f, 1.0f, 0.95f);
                    break;
                case PlayerType.Bot:
                case PlayerType.Player3:
                    lineColor = new Color(0.18f, 0.85f, 0.35f, 0.50f);
                    arrowColor = new Color(0.28f, 1.0f, 0.45f, 0.95f);
                    break;
                case PlayerType.Player4:
                    lineColor = new Color(0.95f, 0.82f, 0.15f, 0.50f);
                    arrowColor = new Color(1.0f, 0.90f, 0.25f, 0.95f);
                    break;
                default:
                    lineColor = new Color(0.95f, 0.22f, 0.22f, 0.55f);
                    arrowColor = new Color(1.0f, 0.35f, 0.35f, 0.95f);
                    break;
            }

            if (waypointLine.material != null)
            {
                waypointLine.material.color = lineColor;
            }

            // Populate directional arrow markers along the path
            if (waypointMarkersContainer != null)
            {
                for (int i = waypointMarkersContainer.transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(waypointMarkersContainer.transform.GetChild(i).gameObject);
                }

                Material arrowMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
                arrowMat.color = arrowColor;

                for (int s = 0; s < ARROW_STEP_INDICES.Length; s++)
                {
                    int step = ARROW_STEP_INDICES[s];
                    if (step < 0 || step >= activePath.Count) continue;

                    Vector3 curPos = activePath[step].WorldPosition + Vector3.up * 0.24f;
                    Vector3 forwardDir = Vector3.forward;

                    if (step < activePath.Count - 1)
                    {
                        forwardDir = (activePath[step + 1].WorldPosition - activePath[step].WorldPosition).normalized;
                    }
                    else if (step > 0)
                    {
                        forwardDir = (activePath[step].WorldPosition - activePath[step - 1].WorldPosition).normalized;
                    }

                    if (forwardDir == Vector3.zero) forwardDir = Vector3.forward;

                    GameObject arrow = new GameObject($"Arrow_Step_{step}", typeof(MeshFilter), typeof(MeshRenderer));
                    arrow.transform.parent = waypointMarkersContainer.transform;
                    arrow.transform.position = curPos;
                    arrow.transform.rotation = Quaternion.LookRotation(forwardDir);
                    arrow.transform.localScale = Vector3.one;
                    arrow.GetComponent<MeshFilter>().sharedMesh = GetOrCreateArrowMesh();
                    arrow.GetComponent<MeshRenderer>().sharedMaterial = arrowMat;
                }
            }

            // Respect toggle visibility
            if (waypointLine != null) waypointLine.gameObject.SetActive(showWaypointPath);
            if (waypointMarkersContainer != null) waypointMarkersContainer.SetActive(showWaypointPath);
        }

        #endregion
    }
}
