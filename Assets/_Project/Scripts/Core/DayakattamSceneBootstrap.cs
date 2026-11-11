using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DayakattamDungeon.AI;
using DayakattamDungeon.Audio;
using DayakattamDungeon.Board;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Tutorial;
using DayakattamDungeon.UI;

namespace DayakattamDungeon.Core
{
    /// <summary>
    /// Bootstraps and generates the Dayakattam Dungeon scene:
    /// - Board Grid (24 perimeter tiles + Malai markers + Central Palace)
    /// - Oblong brass stick dice (Dayakattam)
    /// - Player 1 Bronze Pawns & Bot Obsidian Pawns in reserves
    /// - Top-down angled Camera
    /// - Interactive Game Jam UI Canvas
    /// </summary>
    public class DayakattamSceneBootstrap : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [SerializeField] private bool autoBuildOnStart = true;
        [SerializeField] private bool startWithTutorial = true;

        [Header("Materials (Optional overrides)")]
        [SerializeField] private Material redMaterial;
        [SerializeField] private Material blueMaterial;
        [SerializeField] private Material greenMaterial;
        [SerializeField] private Material yellowMaterial;
        [SerializeField] private Material goldMaterial;
        [SerializeField] private Material blackMaterial;
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material emissiveMaterial;

        [Header("Prefabs & Mesh Assets")]
        [SerializeField] private GameObject dicePrefab;

        private void Start()
        {
            if (autoBuildOnStart)
            {
                BuildScene();
            }
        }

        [ContextMenu("Build Scene Now")]
        public void BuildScene()
        {
            FindExistingMaterials();

            // 1. Setup Camera & Lighting
            SetupCamera();
            SetupLighting();

            // 2. Setup Board
            ThayamBoardGrid board = SetupBoard();

            // 3. Setup Dice & Roller
            DayakattamRoller roller = SetupDice();

            // 4. Setup Audio Player
            SetupAudio();

            // 5. Setup Pawns (4-Player Sanctuary Trays and 12 Kalasam pawns)
            var (p1Pawns, p2Pawns, botPawns, p4Pawns) = SetupPawns();

            // 6. Setup Managers (Turn & Streak, Bot AI, Tutorial)
            SetupManagers(p1Pawns, p2Pawns, botPawns, p4Pawns);

            // 7. Setup UI Canvas
            SetupUI();

            Debug.Log("[DayakattamSceneBootstrap] Scene construction complete! Ready to play.");

            if (Application.isPlaying)
            {
                // Start Match / Tutorial
                if (TurnAndStreakManager.Instance != null)
                {
                    TurnAndStreakManager.Instance.InitializeMatch(startWithTutorial);
                }

                if (startWithTutorial && TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.StartTutorial();
                }
            }
        }

        private void FindExistingMaterials()
        {
            if (redMaterial == null)
            {
                redMaterial = AssetDatabaseLoadMaterial("Assets/_Unity Essentials/Materials/Outdoor/Tangram_Puzzle_Materials/Red.mat") ??
                    CreatePBRMaterial(new Color(0.92f, 0.15f, 0.15f), 0.75f, 0.85f);
            }
            if (blueMaterial == null)
            {
                blueMaterial = AssetDatabaseLoadMaterial("Assets/_Unity Essentials/Materials/Outdoor/Tangram_Puzzle_Materials/Blue.mat") ??
                    CreatePBRMaterial(new Color(0.18f, 0.45f, 0.95f), 0.75f, 0.85f);
            }
            if (greenMaterial == null)
            {
                greenMaterial = AssetDatabaseLoadMaterial("Assets/_Unity Essentials/Materials/Outdoor/Tangram_Puzzle_Materials/Green.mat") ??
                    CreatePBRMaterial(new Color(0.15f, 0.75f, 0.35f), 0.75f, 0.85f);
            }
            if (blackMaterial == null)
            {
                blackMaterial = Resources.Load<Material>("Metal_Black") ??
                    AssetDatabaseLoadMaterial("Assets/GameJam/Scenes/MAIN/Metal_Black.mat");
            }
            if (yellowMaterial == null)
            {
                yellowMaterial = AssetDatabaseLoadMaterial("Assets/_Unity Essentials/Materials/Outdoor/Tangram_Puzzle_Materials/Yellow.mat") ??
                    CreatePBRMaterial(new Color(0.95f, 0.82f, 0.15f), 0.75f, 0.85f);
            }
            if (goldMaterial == null)
            {
                goldMaterial = Resources.Load<Material>("Gold") ??
                    AssetDatabaseLoadMaterial("Assets/GameJam/Scenes/MAIN/Gold.mat");
            }
            if (groundMaterial == null)
            {
                groundMaterial = Resources.Load<Material>("Ground") ??
                    AssetDatabaseLoadMaterial("Assets/GameJam/Scenes/MAIN/Ground.mat");
            }
            if (emissiveMaterial == null)
            {
                emissiveMaterial = Resources.Load<Material>("Emissive") ??
                    AssetDatabaseLoadMaterial("Assets/GameJam/Scenes/MAIN/Emissive.mat");
            }
#if UNITY_EDITOR
            if (dicePrefab == null)
            {
                dicePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Mesh/Dayam_Dice_Prefab.prefab");
            }
#endif
        }

        private Material CreatePBRMaterial(Color color, float metallic = 0.8f, float smoothness = 0.85f)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(litShader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private Material AssetDatabaseLoadMaterial(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
#else
            return null;
#endif
        }

        private Font AssetDatabaseLoadFont(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(path);
#else
            return null;
#endif
        }

        private void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cam = camGO.GetComponent<Camera>();
                camGO.tag = "MainCamera";
            }

            // Ensure PhysicsRaycaster is attached for 3D clicks & drag-and-drop with New Input System!
            if (cam.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null)
            {
                cam.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            }

            // Top-down angled perspective overlooking the 7x7 board and dice tray
            cam.transform.position = new Vector3(0.8f, 13.8f, -9.2f);
            cam.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            cam.fieldOfView = 52f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.06f, 0.09f);
        }

        private void SetupLighting()
        {
            Light dirLight = Object.FindFirstObjectByType<Light>();
            if (dirLight == null)
            {
                GameObject lightGO = new GameObject("Directional Light", typeof(Light));
                dirLight = lightGO.GetComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.color = new Color(1.0f, 0.95f, 0.85f);
            dirLight.intensity = 1.2f;
        }

        private ThayamBoardGrid SetupBoard()
        {
            ThayamBoardGrid grid = Object.FindFirstObjectByType<ThayamBoardGrid>();
            if (grid == null)
            {
                GameObject boardGO = new GameObject("ThayamBoardGrid");
                grid = boardGO.AddComponent<ThayamBoardGrid>();
            }

            // Pass materials if available
            var fieldStandard = typeof(ThayamBoardGrid).GetField("tileStandardMat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldMalai = typeof(ThayamBoardGrid).GetField("tileMalaiMat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldPalace = typeof(ThayamBoardGrid).GetField("tilePalaceMat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldStandard != null) fieldStandard.SetValue(grid, groundMaterial);
            if (fieldMalai != null) fieldMalai.SetValue(grid, emissiveMaterial ?? goldMaterial);
            if (fieldPalace != null) fieldPalace.SetValue(grid, goldMaterial);

            if (!TurnAndStreakManager.IsFourPlayerUnlocked())
            {
                grid.SetPlayerMode(BoardPlayerMode.TwoPlayers);
            }

            grid.GenerateGreyboxBoard();
            return grid;
        }

        private DayakattamRoller SetupDice()
        {
            DayakattamRoller roller = Object.FindFirstObjectByType<DayakattamRoller>();
            if (roller == null)
            {
                GameObject diceHolder = new GameObject("Dayakattam_DiceHolder");
                roller = diceHolder.AddComponent<DayakattamRoller>();
            }

#if UNITY_EDITOR
            if (dicePrefab == null)
            {
                dicePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Mesh/Dayam_Dice_Prefab.prefab");
            }
#endif

            // Check if existing Stick_A needs replacement with Dayam_Dice_Prefab mesh
            Transform stickA = roller.transform.Find("Stick_A");
            if (stickA != null)
            {
                var mf = stickA.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube.001")
                {
                    Object.DestroyImmediate(stickA.gameObject);
                    stickA = null;
                }
            }

            if (stickA == null)
            {
                GameObject sa;
                if (dicePrefab != null)
                {
                    sa = Object.Instantiate(dicePrefab, roller.transform);
                }
                else
                {
                    sa = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sa.transform.parent = roller.transform;
                    sa.transform.localScale = new Vector3(0.5f, 0.5f, 2.5f);
                    if (goldMaterial != null) sa.GetComponent<Renderer>().sharedMaterial = goldMaterial;
                }
                sa.name = "Stick_A";
                sa.transform.position = new Vector3(6.4f, 0.41f, -1.0f);
                stickA = sa.transform;
            }

            // Check if existing Stick_B needs replacement with Dayam_Dice_Prefab mesh
            Transform stickB = roller.transform.Find("Stick_B");
            if (stickB != null)
            {
                var mf = stickB.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube.001")
                {
                    Object.DestroyImmediate(stickB.gameObject);
                    stickB = null;
                }
            }

            if (stickB == null)
            {
                GameObject sb;
                if (dicePrefab != null)
                {
                    sb = Object.Instantiate(dicePrefab, roller.transform);
                }
                else
                {
                    sb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sb.transform.parent = roller.transform;
                    sb.transform.localScale = new Vector3(0.5f, 0.5f, 2.5f);
                    if (goldMaterial != null) sb.GetComponent<Renderer>().sharedMaterial = goldMaterial;
                }
                sb.name = "Stick_B";
                sb.transform.position = new Vector3(7.6f, 0.41f, -1.0f);
                stickB = sb.transform;
            }

            // Create a small wooden tray under the sticks
            Transform tray = roller.transform.Find("DiceTray");
            if (tray == null)
            {
                GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cube);
                t.name = "DiceTray";
                t.transform.parent = roller.transform;
                t.transform.position = new Vector3(7.0f, 0.05f, -1.0f);
                t.transform.localScale = new Vector3(2.5f, 0.1f, 3.5f);
                if (blackMaterial != null) t.GetComponent<Renderer>().sharedMaterial = blackMaterial;
            }

            var visA = stickA.GetComponent<DayakattamStickVisual>() ?? stickA.gameObject.AddComponent<DayakattamStickVisual>();
            var visB = stickB.GetComponent<DayakattamStickVisual>() ?? stickB.gameObject.AddComponent<DayakattamStickVisual>();

            visA.SetRestPosition(stickA.localPosition);
            visB.SetRestPosition(stickB.localPosition);
            visA.SetFaceImmediate(1); // Start showing Face 1 (Thayam)
            visB.SetFaceImmediate(1);

            var fA = typeof(DayakattamRoller).GetField("stickAVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fB = typeof(DayakattamRoller).GetField("stickBVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fACtrl = typeof(DayakattamRoller).GetField("stickAController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fBCtrl = typeof(DayakattamRoller).GetField("stickBController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (fA != null) fA.SetValue(roller, stickA);
            if (fB != null) fB.SetValue(roller, stickB);
            if (fACtrl != null) fACtrl.SetValue(roller, visA);
            if (fBCtrl != null) fBCtrl.SetValue(roller, visB);

            return roller;
        }

        private void SetupAudio()
        {
            DayakattamAudioPlayer audioPlayer = Object.FindFirstObjectByType<DayakattamAudioPlayer>();
            if (audioPlayer == null)
            {
                GameObject audioGO = new GameObject("DayakattamAudioPlayer");
                audioPlayer = audioGO.AddComponent<DayakattamAudioPlayer>();
            }

            MridangamRhythmEngine rhythm = Object.FindFirstObjectByType<MridangamRhythmEngine>();
            if (rhythm == null)
            {
                GameObject rhythmGO = new GameObject("MridangamRhythmEngine");
                rhythm = rhythmGO.AddComponent<MridangamRhythmEngine>();
            }
        }

        private (List<PawnController>, List<PawnController>, List<PawnController>, List<PawnController>) SetupPawns()
        {
            List<PawnController> p1Pawns = new List<PawnController>();
            List<PawnController> p2Pawns = new List<PawnController>();
            List<PawnController> botPawns = new List<PawnController>();
            List<PawnController> p4Pawns = new List<PawnController>();

            GameObject pawnsRoot = GameObject.Find("Pawns_Container");
            if (pawnsRoot != null)
            {
                DestroyImmediate(pawnsRoot);
            }
            pawnsRoot = new GameObject("Pawns_Container");

            // Dedicated glow materials for all 4 quadrants
            Material p1Glow = CreateGlowMaterial(new Color(1.0f, 0.25f, 0.25f, 0.9f)); // Crimson Red
            Material p2Glow = CreateGlowMaterial(new Color(0.25f, 0.65f, 1.0f, 0.9f)); // Sapphire Blue
            Material p3Glow = CreateGlowMaterial(new Color(0.20f, 0.95f, 0.45f, 0.9f)); // Emerald Green
            Material p4Glow = CreateGlowMaterial(new Color(1.0f, 0.88f, 0.25f, 0.9f)); // Radiant Amber

            // 1. Player 1 Sanctuary Tray (South flank outside perimeter)
            GameObject p1Tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p1Tray.name = "P1_Sanctuary_Tray";
            p1Tray.transform.parent = pawnsRoot.transform;
            p1Tray.transform.position = new Vector3(0f, 0.05f, -5.8f);
            p1Tray.transform.localScale = new Vector3(5.4f, 0.1f, 1.8f);
            if (redMaterial != null) p1Tray.GetComponent<Renderer>().sharedMaterial = redMaterial;

            // 2. Player 2 Sanctuary Tray (East flank outside perimeter)
            GameObject p2Tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p2Tray.name = "P2_Sanctuary_Tray";
            p2Tray.transform.parent = pawnsRoot.transform;
            p2Tray.transform.position = new Vector3(6.4f, 0.05f, 2.8f);
            p2Tray.transform.localScale = new Vector3(1.8f, 0.1f, 4.4f);
            if (blueMaterial != null) p2Tray.GetComponent<Renderer>().sharedMaterial = blueMaterial;

            // 3. Bot / Grandma Sanctuary Tray (North flank outside perimeter)
            GameObject botTray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            botTray.name = "Bot_Sanctuary_Tray";
            botTray.transform.parent = pawnsRoot.transform;
            botTray.transform.position = new Vector3(0f, 0.05f, 6.4f);
            botTray.transform.localScale = new Vector3(5.4f, 0.1f, 1.8f);
            if (greenMaterial != null) botTray.GetComponent<Renderer>().sharedMaterial = greenMaterial;

            // 4. Player 4 Sanctuary Tray (West flank outside perimeter)
            GameObject p4Tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p4Tray.name = "P4_Sanctuary_Tray";
            p4Tray.transform.parent = pawnsRoot.transform;
            p4Tray.transform.position = new Vector3(-6.4f, 0.05f, 0f);
            p4Tray.transform.localScale = new Vector3(1.8f, 0.1f, 5.4f);
            if (yellowMaterial != null) p4Tray.GetComponent<Renderer>().sharedMaterial = yellowMaterial;

            // Player 1 Reserve Pawns (South: -1.6 to +1.6, 0.12, -5.8) - Crimson Red
            for (int i = 0; i < 3; i++)
            {
                Vector3 rPos = new Vector3(-1.6f + i * 1.6f, 0.12f, -5.8f);
                var p = CreateKalasamPawn($"P1_Pawn_{i + 1}", pawnsRoot.transform, PlayerType.Player1, i + 1, rPos, redMaterial, p1Glow);
                p1Pawns.Add(p);
            }

            // Player 2 Reserve Pawns (East: 6.4, 0.12, 1.5 to 4.1) - Sapphire Blue
            for (int i = 0; i < 3; i++)
            {
                Vector3 rPos = new Vector3(6.4f, 0.12f, 1.5f + i * 1.3f);
                var p = CreateKalasamPawn($"P2_Pawn_{i + 1}", pawnsRoot.transform, PlayerType.Player2, i + 1, rPos, blueMaterial, p2Glow);
                p2Pawns.Add(p);
            }

            // Bot / Grandma Reserve Pawns (North: -1.6 to +1.6, 0.12, 6.4) - Emerald Green
            for (int i = 0; i < 3; i++)
            {
                Vector3 rPos = new Vector3(-1.6f + i * 1.6f, 0.12f, 6.4f);
                var p = CreateKalasamPawn($"Bot_Pawn_{i + 1}", pawnsRoot.transform, PlayerType.Bot, i + 1, rPos, greenMaterial, p3Glow);
                botPawns.Add(p);
            }

            // Player 4 Reserve Pawns (West: -6.4, 0.12, -1.6 to +1.6) - Radiant Amber
            for (int i = 0; i < 3; i++)
            {
                Vector3 rPos = new Vector3(-6.4f, 0.12f, -1.6f + i * 1.6f);
                var p = CreateKalasamPawn($"P4_Pawn_{i + 1}", pawnsRoot.transform, PlayerType.Player4, i + 1, rPos, yellowMaterial, p4Glow);
                p4Pawns.Add(p);
            }

            // Respect 2P vs 4P initial mode
            bool is4P = TurnAndStreakManager.IsFourPlayerUnlocked() && (ThayamBoardGrid.Instance != null && ThayamBoardGrid.Instance.CurrentMode == BoardPlayerMode.FourPlayers);
            p2Tray.SetActive(is4P);
            p4Tray.SetActive(is4P);
            foreach (var p in p2Pawns) p.gameObject.SetActive(is4P);
            foreach (var p in p4Pawns) p.gameObject.SetActive(is4P);

            return (p1Pawns, p2Pawns, botPawns, p4Pawns);
        }

        private Material CreateGlowMaterial(Color color)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material mat = new Material(s);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }

        private PawnController CreateKalasamPawn(string name, Transform parent, PlayerType owner, int id, Vector3 reservePos, Material mat, Material glowMat)
        {
            GameObject pawnGO = new GameObject(name);
            pawnGO.transform.parent = parent;
            pawnGO.transform.position = reservePos;
            pawnGO.transform.localScale = Vector3.one;

            CapsuleCollider col = pawnGO.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.45f, 0);
            col.radius = 0.42f;
            col.height = 1.0f;

            // Tier 1: Base pedestal
            GameObject baseTier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseTier.name = "Pedestal";
            baseTier.transform.parent = pawnGO.transform;
            baseTier.transform.localPosition = new Vector3(0, 0.10f, 0);
            baseTier.transform.localScale = new Vector3(0.72f, 0.10f, 0.72f);
            DestroyImmediate(baseTier.GetComponent<Collider>());
            if (mat != null) baseTier.GetComponent<Renderer>().sharedMaterial = mat;

            // Tier 2: Waist Ring
            GameObject waistTier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            waistTier.name = "WaistRing";
            waistTier.transform.parent = pawnGO.transform;
            waistTier.transform.localPosition = new Vector3(0, 0.30f, 0);
            waistTier.transform.localScale = new Vector3(0.54f, 0.14f, 0.54f);
            DestroyImmediate(waistTier.GetComponent<Collider>());
            if (mat != null) waistTier.GetComponent<Renderer>().sharedMaterial = mat;

            // Tier 3: Neck
            GameObject neckTier = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            neckTier.name = "Neck";
            neckTier.transform.parent = pawnGO.transform;
            neckTier.transform.localPosition = new Vector3(0, 0.48f, 0);
            neckTier.transform.localScale = new Vector3(0.34f, 0.08f, 0.34f);
            DestroyImmediate(neckTier.GetComponent<Collider>());
            if (mat != null) neckTier.GetComponent<Renderer>().sharedMaterial = mat;

            // Tier 4: Dome / Crown Pinnacle (Kalasam)
            GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "KalasamDome";
            crown.transform.parent = pawnGO.transform;
            crown.transform.localPosition = new Vector3(0, 0.68f, 0);
            crown.transform.localScale = new Vector3(0.44f, 0.52f, 0.44f);
            DestroyImmediate(crown.GetComponent<Collider>());
            if (mat != null) crown.GetComponent<Renderer>().sharedMaterial = mat;

            // Selection Glow Aura (Ring at base)
            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "SelectionGlow";
            glow.transform.parent = pawnGO.transform;
            glow.transform.localPosition = new Vector3(0, 0.02f, 0);
            glow.transform.localScale = new Vector3(0.88f, 0.02f, 0.88f);
            DestroyImmediate(glow.GetComponent<Collider>());
            if (glowMat != null) glow.GetComponent<Renderer>().sharedMaterial = glowMat;
            glow.SetActive(false);

            PawnController pawn = pawnGO.AddComponent<PawnController>();
            SetField(pawn, "selectionGlow", glow);
            pawn.Initialize(owner, id, reservePos);

            return pawn;
        }

        private void SetupManagers(List<PawnController> p1Pawns, List<PawnController> p2Pawns, List<PawnController> botPawns, List<PawnController> p4Pawns)
        {
            TurnAndStreakManager turnMgr = Object.FindFirstObjectByType<TurnAndStreakManager>();
            if (turnMgr == null)
            {
                GameObject mgrGO = new GameObject("TurnAndStreakManager");
                turnMgr = mgrGO.AddComponent<TurnAndStreakManager>();
            }
            turnMgr.RegisterAllPawns(p1Pawns, p2Pawns, botPawns, p4Pawns);

            BotAIController bot = Object.FindFirstObjectByType<BotAIController>();
            if (bot == null)
            {
                GameObject botGO = new GameObject("BotAIController");
                bot = botGO.AddComponent<BotAIController>();
            }

            TutorialManager tut = Object.FindFirstObjectByType<TutorialManager>();
            if (tut == null)
            {
                GameObject tutGO = new GameObject("TutorialManager");
                tut = tutGO.AddComponent<TutorialManager>();
            }

            GameJuiceManager juice = Object.FindFirstObjectByType<GameJuiceManager>();
            if (juice == null)
            {
                GameObject juiceGO = new GameObject("GameJuiceManager");
                juice = juiceGO.AddComponent<GameJuiceManager>();
            }
        }

        private void SetupUI()
        {
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                GameObject esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                var inputType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputType != null) esGO.AddComponent(inputType);
                else esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGO.GetComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 10f;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            GameHUD hud = canvas.GetComponent<GameHUD>();
            if (hud == null)
            {
                hud = canvas.gameObject.AddComponent<GameHUD>();
            }

            // Clear any previous UI children to prevent duplicate stacked elements
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(canvas.transform.GetChild(i).gameObject);
            }

            // 1. Top Bar Elements (Anchored to Top-Center, perfectly spaced across 1920 width)
            Vector2 topAnchor = new Vector2(0.5f, 1f);
            TextMeshProUGUI turnBanner = CreateTMPText(canvas.transform, "TurnBanner", "DAYAKATTAM DUNGEON", 20, new Vector2(0, -40), new Vector2(360, 42), TextAlignmentOptions.Center, topAnchor, topAnchor);
            turnBanner.fontStyle = FontStyles.Bold;

            TextMeshProUGUI p1HealthText = CreateTMPText(canvas.transform, "P1HealthText", "Player: 0/3 Home", 17, new Vector2(-310, -40), new Vector2(180, 35), TextAlignmentOptions.Center, topAnchor, topAnchor);
            p1HealthText.color = new Color(1.0f, 0.40f, 0.40f);

            TextMeshProUGUI p4GoalText = CreateTMPText(canvas.transform, "P4GoalText", "P4: 0/3", 16, new Vector2(-360, -40), new Vector2(100, 35), TextAlignmentOptions.Center, topAnchor, topAnchor);
            p4GoalText.color = new Color(1f, 0.88f, 0.2f);
            p4GoalText.gameObject.SetActive(false);

            TextMeshProUGUI p2GoalText = CreateTMPText(canvas.transform, "P2GoalText", "P2: 0/3", 16, new Vector2(260, -40), new Vector2(100, 35), TextAlignmentOptions.Center, topAnchor, topAnchor);
            p2GoalText.color = new Color(0.35f, 0.70f, 1f);
            p2GoalText.gameObject.SetActive(false);

            TextMeshProUGUI botHealthText = CreateTMPText(canvas.transform, "BotHealthText", "Grandma: 0/3 Home", 17, new Vector2(310, -40), new Vector2(180, 35), TextAlignmentOptions.Center, topAnchor, topAnchor);
            botHealthText.color = new Color(0.25f, 0.95f, 0.55f);

            Button diffBtn = CreateButton(canvas.transform, "DifficultyButton", "GRANDMA: NORMAL", new Vector2(-790, -40), new Vector2(145, 40), 13, new Color(0.18f, 0.28f, 0.24f), topAnchor, topAnchor);
            TextMeshProUGUI diffText = diffBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Rules & Lore Button
            Button loreBtn = CreateButton(canvas.transform, "LoreButton", "? Rules & Lore", new Vector2(-650, -40), new Vector2(120, 40), 14, new Color(0.18f, 0.3f, 0.45f), topAnchor, topAnchor);

            // Audio Mute Toggle Button
            Button audioBtn = CreateButton(canvas.transform, "AudioToggleButton", "Sound: ON [M]", new Vector2(-520, -40), new Vector2(125, 40), 14, new Color(0.22f, 0.28f, 0.35f), topAnchor, topAnchor);
            TextMeshProUGUI audioText = audioBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Waypoint Path Toggle Button
            Button waypointBtn = CreateButton(canvas.transform, "WaypointToggleButton", "Path: ON [P]", new Vector2(480, -40), new Vector2(120, 40), 14, new Color(0.15f, 0.35f, 0.45f), topAnchor, topAnchor);
            TextMeshProUGUI waypointText = waypointBtn.GetComponentInChildren<TextMeshProUGUI>();

            // 2P / 4P Mode Toggle Button
            Button modeBtn = CreateButton(canvas.transform, "PlayerModeButton", "Mode: 2P", new Vector2(615, -40), new Vector2(140, 40), 14, new Color(0.3f, 0.22f, 0.45f), topAnchor, topAnchor);
            TextMeshProUGUI modeText = modeBtn.GetComponentInChildren<TextMeshProUGUI>();

            Button restartBtn = CreateButton(canvas.transform, "RestartButton", "Restart Match", new Vector2(765, -40), new Vector2(135, 40), 14, new Color(0.35f, 0.15f, 0.15f), topAnchor, topAnchor);

            // Rules & Lore Modal Panel (Initially hidden)
            GameObject lorePanelGO = new GameObject("LorePanel", typeof(RectTransform), typeof(Image));
            lorePanelGO.transform.SetParent(canvas.transform, false);
            RectTransform loreRT = lorePanelGO.GetComponent<RectTransform>();
            loreRT.anchorMin = new Vector2(0.5f, 0.5f);
            loreRT.anchorMax = new Vector2(0.5f, 0.5f);
            loreRT.anchoredPosition = Vector2.zero;
            loreRT.sizeDelta = new Vector2(860, 520);
            Image loreBg = lorePanelGO.GetComponent<Image>();
            loreBg.color = new Color(0.04f, 0.04f, 0.07f, 0.96f);

            string loreTextContent = "<b><size=24><color=#FFD700>DAYAKATTAM DUNGEON — THE SACRED DICE DUEL</color></size></b>\n\n" +
                "<b>Cultural Heritage:</b> Based on the traditional Tamil board game <i>Thayam</i>, played for millennia during festive gatherings with two brass stick dice.\n\n" +
                "<b>The 'Lucky Number' Math:</b>\n" +
                "• Two 4-sided oblong brass sticks (faces: 0, 1, 2, 3).\n" +
                "• Normal sum: 1..6. But rolling <b>0 + 0 = 12 (Pannirendu)!</b>\n" +
                "• <b>Lucky Numbers (1, 5, 6, 12):</b> Trigger an <i>Auspicious Streak</i>, granting +1 Extra Roll and accelerating your <i>Combo Multiplier</i> (1x -> 1.5x -> 2x -> 3x)!\n" +
                "• Standard numbers (2, 3, 4) end your rolling streak and begin the Action Phase.\n\n" +
                "<b>Tactical Board & Sanctuary:</b>\n" +
                "• <b>Thayam Entry:</b> You must spend a <b>1 (Thayam)</b> to awaken a spirit pawn from reserve.\n" +
                "• <b>Malai (Safe Squares):</b> The 8 crossed perimeter tiles grant capture immunity.\n" +
                "• <b>Striking:</b> Landing on an opponent pawn on an unsafe tile cuts them back to their reserve!\n\n" +
                "<b>Victory:</b> The first player to successfully navigate all 3 spirit pawns into the Central Palace wins the duel!";

            TextMeshProUGUI loreTxt = CreateTMPText(lorePanelGO.transform, "LoreText", loreTextContent, 16, new Vector2(0, 25), new Vector2(800, 430), TextAlignmentOptions.TopLeft, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            loreTxt.color = new Color(0.95f, 0.95f, 0.95f);

            Button closeLoreBtn = CreateButton(lorePanelGO.transform, "CloseLoreButton", "Close", new Vector2(0, -225), new Vector2(160, 42), 16, new Color(0.7f, 0.25f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            closeLoreBtn.onClick.AddListener(() => lorePanelGO.SetActive(false));
            loreBtn.onClick.AddListener(() => lorePanelGO.SetActive(!lorePanelGO.activeSelf));
            lorePanelGO.SetActive(false);

            // 2. Tutorial Panel ("Grandmother's Voice")
            GameObject tutPanelGO = new GameObject("TutorialPanel", typeof(RectTransform), typeof(Image));
            tutPanelGO.transform.SetParent(canvas.transform, false);
            RectTransform tutRT = tutPanelGO.GetComponent<RectTransform>();
            tutRT.anchorMin = topAnchor;
            tutRT.anchorMax = topAnchor;
            tutRT.anchoredPosition = new Vector2(0, -145);
            tutRT.sizeDelta = new Vector2(900, 130);
            Image tutBg = tutPanelGO.GetComponent<Image>();
            tutBg.color = new Color(0.06f, 0.06f, 0.09f, 0.94f);

            TextMeshProUGUI tutText = CreateTMPText(tutPanelGO.transform, "TutorialDialogueText", "Grandmother's Voice: ...", 18, new Vector2(0, 10), new Vector2(850, 90), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            tutText.color = new Color(1f, 0.95f, 0.8f);

            Button skipBtn = CreateButton(tutPanelGO.transform, "SkipTutorialButton", "Skip Tutorial", new Vector2(360, -40), new Vector2(130, 32), 15, new Color(0.6f, 0.2f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            // 3. Bottom Control Elements (Anchored to Bottom-Center)
            Vector2 bottomAnchor = new Vector2(0.5f, 0f);
            Button rollBtn = CreateButton(canvas.transform, "RollDiceButton", "ROLL [SPACE]", new Vector2(0, 52), new Vector2(240, 52), 19, new Color(0.85f, 0.65f, 0.15f), bottomAnchor, bottomAnchor);
            Button endTurnBtn = CreateButton(canvas.transform, "EndTurnButton", "END [E]", new Vector2(190, 52), new Vector2(105, 46), 15, new Color(0.5f, 0.5f, 0.5f), bottomAnchor, bottomAnchor);

            TextMeshProUGUI comboText = CreateTMPText(canvas.transform, "ComboText", "Streak: 0 | Combo: 1.0x", 17, new Vector2(0, 16), new Vector2(400, 26), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor);
            TextMeshProUGUI lastRoll = CreateTMPText(canvas.transform, "LastRollText", "Roll to Begin", 16, new Vector2(0, 158), new Vector2(500, 26), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor);
            TextMeshProUGUI luckyText = CreateTMPText(canvas.transform, "LuckyCalloutText", "THAYAM! (1)", 24, new Vector2(0, 190), new Vector2(600, 36), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor);
            luckyText.color = Color.yellow;
            luckyText.fontStyle = FontStyles.Bold;

            // 4. Roll Pool Container (Banked rolls positioned neatly above Roll Button)
            GameObject poolContainerGO = new GameObject("RollPoolContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            poolContainerGO.transform.SetParent(canvas.transform, false);
            RectTransform poolRT = poolContainerGO.GetComponent<RectTransform>();
            poolRT.anchorMin = bottomAnchor;
            poolRT.anchorMax = bottomAnchor;
            poolRT.anchoredPosition = new Vector2(0, 112);
            poolRT.sizeDelta = new Vector2(400, 48);
            HorizontalLayoutGroup hlg = poolContainerGO.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 15;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // 5. Pawn Action Selection Modal (when choosing between multiple pawns)
            GameObject actionPanelGO = new GameObject("PawnActionPanel", typeof(RectTransform), typeof(Image));
            actionPanelGO.transform.SetParent(canvas.transform, false);
            RectTransform actionRT = actionPanelGO.GetComponent<RectTransform>();
            actionRT.anchorMin = new Vector2(0.5f, 0.5f);
            actionRT.anchorMax = new Vector2(0.5f, 0.5f);
            actionRT.anchoredPosition = new Vector2(0, 0);
            actionRT.sizeDelta = new Vector2(520, 260);
            Image actionBg = actionPanelGO.GetComponent<Image>();
            actionBg.color = new Color(0.06f, 0.06f, 0.10f, 0.96f);

            TextMeshProUGUI promptTxt = CreateTMPText(actionPanelGO.transform, "ActionPromptText", "Choose Spirit Pawn to move:", 18, new Vector2(0, 85), new Vector2(480, 40), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            promptTxt.color = new Color(1f, 0.85f, 0.25f);
            promptTxt.fontStyle = FontStyles.Bold;

            Button[] actButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                actButtons[i] = CreateButton(actionPanelGO.transform, $"PawnChoiceBtn_{i + 1}", $"Pawn #{i + 1}", new Vector2(0, 25 - (i * 55)), new Vector2(440, 46), 16, new Color(0.18f, 0.42f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            }
            actionPanelGO.SetActive(false);

            // 6. Game Over / Match End Modal Panel (Center Modal)
            GameObject gameOverPanelGO = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
            gameOverPanelGO.transform.SetParent(canvas.transform, false);
            RectTransform goRT = gameOverPanelGO.GetComponent<RectTransform>();
            goRT.anchorMin = new Vector2(0.5f, 0.5f);
            goRT.anchorMax = new Vector2(0.5f, 0.5f);
            goRT.anchoredPosition = Vector2.zero;
            goRT.sizeDelta = new Vector2(720, 520);
            Image goBg = gameOverPanelGO.GetComponent<Image>();
            goBg.color = new Color(0.04f, 0.04f, 0.08f, 0.97f);

            TextMeshProUGUI goTitle = CreateTMPText(gameOverPanelGO.transform, "GameOverTitle", "VICTORY!", 30, new Vector2(0, 195), new Vector2(660, 70), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            goTitle.fontStyle = FontStyles.Bold;

            TextMeshProUGUI goSubtitle = CreateTMPText(gameOverPanelGO.transform, "GameOverSubtitle", "You unlocked the Sacred Palace!", 15, new Vector2(0, 95), new Vector2(640, 110), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            goSubtitle.color = new Color(0.85f, 0.85f, 0.9f);

            TextMeshProUGUI goStats = CreateTMPText(gameOverPanelGO.transform, "GameOverStats", "Match Statistics...", 16, new Vector2(0, -45), new Vector2(620, 130), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            Button fourPlayerBtn = CreateButton(gameOverPanelGO.transform, "FourPlayerModeButton", "4-Player Mode", new Vector2(-150, -195), new Vector2(250, 50), 17, new Color(0.18f, 0.55f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Button twoPlayerHardBtn = CreateButton(gameOverPanelGO.transform, "TwoPlayerHardButton", "2-Player Hard Mode", new Vector2(150, -195), new Vector2(250, 50), 17, new Color(0.65f, 0.25f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

            gameOverPanelGO.SetActive(false);

            // Wire up HUD serialized fields via reflection
            SetField(hud, "turnBannerText", turnBanner);
            SetField(hud, "p1HealthText", p1HealthText);
            SetField(hud, "botHealthText", botHealthText);
            SetField(hud, "p2GoalText", p2GoalText);
            SetField(hud, "p4GoalText", p4GoalText);
            SetField(hud, "rollDiceButton", rollBtn);
            SetField(hud, "lastRollText", lastRoll);
            SetField(hud, "luckyCalloutText", luckyText);
            SetField(hud, "comboMultiplierText", comboText);
            SetField(hud, "rollPoolContainer", poolRT);
            SetField(hud, "endTurnButton", endTurnBtn);
            SetField(hud, "difficultyToggleButton", diffBtn);
            SetField(hud, "difficultyText", diffText);
            SetField(hud, "tutorialPanel", tutPanelGO);
            SetField(hud, "tutorialDialogueText", tutText);
            SetField(hud, "skipTutorialButton", skipBtn);
            SetField(hud, "pawnActionPanel", actionPanelGO);
            SetField(hud, "pawnActionPromptText", promptTxt);
            SetField(hud, "pawnActionButtons", actButtons);
            SetField(hud, "gameOverPanel", gameOverPanelGO);
            SetField(hud, "gameOverTitleText", goTitle);
            SetField(hud, "gameOverSubtitleText", goSubtitle);
            SetField(hud, "gameOverStatsText", goStats);
            SetField(hud, "playAgainButton", fourPlayerBtn);
            SetField(hud, "challengeHardBotButton", twoPlayerHardBtn);
            SetField(hud, "fourPlayerModeButton", fourPlayerBtn);
            SetField(hud, "twoPlayerHardButton", twoPlayerHardBtn);
            SetField(hud, "audioToggleButton", audioBtn);
            SetField(hud, "audioToggleText", audioText);
            SetField(hud, "playerModeButton", modeBtn);
            SetField(hud, "playerModeText", modeText);
            SetField(hud, "waypointToggleButton", waypointBtn);
            SetField(hud, "waypointToggleText", waypointText);
            SetField(hud, "restartMatchButton", restartBtn);

            hud.InitializeHUD();
            hud.RefreshAllPalaceProgressUI();
        }

        private TextMeshProUGUI CreateTMPText(Transform parent, string name, string content, float fontSize, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align, Vector2? aMin = null, Vector2? aMax = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (aMin.HasValue) rt.anchorMin = aMin.Value;
            if (aMax.HasValue) rt.anchorMax = aMax.Value;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            TextMeshProUGUI txt = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                txt.font = TMP_Settings.defaultFontAsset;
            }
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = Color.white;
            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, float fontSize, Color bgColor, Vector2? aMin = null, Vector2? aMax = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (aMin.HasValue) rt.anchorMin = aMin.Value;
            if (aMax.HasValue) rt.anchorMax = aMax.Value;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            img.color = bgColor;

            TextMeshProUGUI txt = CreateTMPText(go.transform, "Label", label, fontSize, Vector2.zero, size, TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 1));
            txt.color = Color.white;
            txt.raycastTarget = false; // Never block click on button

            return go.GetComponent<Button>();
        }

        private void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
