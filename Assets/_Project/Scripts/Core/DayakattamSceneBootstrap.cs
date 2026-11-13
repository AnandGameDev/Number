using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DayakattamDungeon.AI;
using DayakattamDungeon.Audio;
using DayakattamDungeon.Board;
using DayakattamDungeon.Dice;
using DayakattamDungeon.Entities;
using DayakattamDungeon.Environment;
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
        [SerializeField] private bool rebuildExistingSceneOnStart = false;
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

        [Header("Atmosphere Materials")]
        [SerializeField] private Material teakMaterial;
        [SerializeField] private Material stoneFloorMaterial;
        [SerializeField] private Material stonePlinthMaterial;
        [SerializeField] private Material brassMaterial;
        [SerializeField] private Material diyaFlameMaterial;

        [Header("Prefabs & Mesh Assets")]
        [SerializeField] private GameObject dicePrefab;

        private void Start()
        {
            if (!autoBuildOnStart) return;

            if (rebuildExistingSceneOnStart || !IsSceneAlreadyBuilt())
            {
                BuildScene();
            }
            else
            {
                InitializeExistingSceneForPlay();
            }
        }

        private bool IsSceneAlreadyBuilt()
        {
            return GameObject.Find("TempleCourtyardEnvironment") != null
                && Object.FindFirstObjectByType<ThayamBoardGrid>() != null
                && Object.FindFirstObjectByType<GameHUD>() != null
                && Object.FindFirstObjectByType<TurnAndStreakManager>() != null;
        }

        private void InitializeExistingSceneForPlay()
        {
            SetupAudio();

            // Re-wire LorePanel / Interactive Tutorial buttons (runtime AddListener callbacks are not serialized in scene files)
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                Transform loreT = canvas.transform.Find("LorePanel");
                if (loreT != null)
                {
                    GameObject lorePanel = loreT.gameObject;
                    var tutorialDialog = lorePanel.GetComponent<InteractiveTutorialDialog>();

                    Transform loreBtnT = canvas.transform.Find("LoreButton");
                    if (loreBtnT != null && loreBtnT.TryGetComponent<Button>(out Button loreBtn))
                    {
                        loreBtn.onClick.RemoveAllListeners();
                        loreBtn.onClick.AddListener(() =>
                        {
                            if (tutorialDialog != null)
                            {
                                if (lorePanel.activeSelf) tutorialDialog.Close();
                                else tutorialDialog.Open();
                            }
                            else
                            {
                                lorePanel.SetActive(!lorePanel.activeSelf);
                            }
                        });
                    }

                    Transform closeT = loreT.Find("CloseLoreButton");
                    if (closeT == null) closeT = loreT.Find("FooterContainer/CloseLoreButton");
                    if (closeT != null && closeT.TryGetComponent<Button>(out Button closeBtn))
                    {
                        closeBtn.onClick.RemoveAllListeners();
                        closeBtn.onClick.AddListener(() =>
                        {
                            if (tutorialDialog != null) tutorialDialog.Close();
                            else lorePanel.SetActive(false);
                        });
                    }
                }
            }

            if (TurnAndStreakManager.Instance != null)
            {
                // Ensure Player 1 pawns have the FirstThayamPawnIndicator
                if (TurnAndStreakManager.Instance.Player1Pawns != null)
                {
                    foreach (var p in TurnAndStreakManager.Instance.Player1Pawns)
                    {
                        if (p != null && p.GetComponent<DayakattamDungeon.Entities.FirstThayamPawnIndicator>() == null)
                        {
                            p.gameObject.AddComponent<DayakattamDungeon.Entities.FirstThayamPawnIndicator>();
                        }
                    }
                }

                // Ensure PlayerTrayTurnIndicator is present and active
                if (Object.FindFirstObjectByType<DayakattamDungeon.Environment.PlayerTrayTurnIndicator>() == null)
                {
                    new GameObject("PlayerTrayTurnIndicator", typeof(DayakattamDungeon.Environment.PlayerTrayTurnIndicator));
                }

                TurnAndStreakManager.Instance.InitializeMatch(false);
            }

            GameHUD hud = Object.FindFirstObjectByType<GameHUD>();
            if (hud != null)
            {
                hud.InitializeHUD();
                hud.RefreshAllPalaceProgressUI();
                hud.ShowTitleScreen();
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

            // 3. Setup Temple Courtyard Environment, Board Rim & Diyas
            SetupEnvironment();

            // 4. Setup Dice & Roller
            DayakattamRoller roller = SetupDice();

            // 5. Setup Audio Player
            SetupAudio();

            // 6. Setup Pawns (4-Player Sanctuary Trays and 12 Kalasam pawns)
            var (p1Pawns, p2Pawns, botPawns, p4Pawns) = SetupPawns();

            // 7. Setup Managers (Turn & Streak, Bot AI, Tutorial)
            SetupManagers(p1Pawns, p2Pawns, botPawns, p4Pawns);

            // 8. Setup UI Canvas
            SetupUI();

            Debug.Log("[DayakattamSceneBootstrap] Scene construction complete! Ready to play.");

            if (Application.isPlaying)
            {
                // Prepare board & managers, then present atmospheric Title Screen
                if (TurnAndStreakManager.Instance != null)
                {
                    TurnAndStreakManager.Instance.InitializeMatch(false);
                }

                GameHUD hud = Object.FindFirstObjectByType<GameHUD>();
                if (hud != null)
                {
                    hud.ShowTitleScreen();
                }
            }
#if UNITY_EDITOR
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                );
            }
#endif
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
            if (teakMaterial == null)
            {
                teakMaterial = AssetDatabaseLoadMaterial("Assets/_Unity Essentials/Materials/Interior/Environment/WoodPlanksBrown.mat") ??
                    CreatePBRMaterial(new Color(0.24f, 0.12f, 0.06f), 0.05f, 0.45f);
            }
            if (stoneFloorMaterial == null)
            {
                stoneFloorMaterial = CreatePBRMaterial(new Color(0.18f, 0.17f, 0.16f), 0.04f, 0.25f);
            }
            if (stonePlinthMaterial == null)
            {
                stonePlinthMaterial = CreatePBRMaterial(new Color(0.12f, 0.11f, 0.11f), 0.08f, 0.35f);
            }
            if (brassMaterial == null)
            {
                brassMaterial = goldMaterial ?? CreatePBRMaterial(new Color(0.92f, 0.74f, 0.24f), 0.88f, 0.85f);
            }
            if (diyaFlameMaterial == null)
            {
                diyaFlameMaterial = CreateGlowMaterial(new Color(1.0f, 0.65f, 0.12f));
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
            cam.backgroundColor = new Color(0.045f, 0.04f, 0.055f);
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
            dirLight.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            dirLight.color = new Color(1.0f, 0.94f, 0.84f);
            dirLight.intensity = 1.15f;

            // Warm atmospheric dusk temple ambient
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.15f, 0.13f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.06f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
        }

        private void SetupEnvironment()
        {
            GameObject envRoot = GameObject.Find("TempleCourtyardEnvironment");
            if (envRoot != null)
            {
                Object.DestroyImmediate(envRoot);
            }
            envRoot = new GameObject("TempleCourtyardEnvironment");

            // 1. Temple Courtyard Stone Floor
            GameObject floorRoot = new GameObject("Courtyard_Stone_Floor");
            floorRoot.transform.parent = envRoot.transform;

            // Broad ground base slab (36m x 32m)
            GameObject baseFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseFloor.name = "Courtyard_Base_Slab";
            baseFloor.transform.parent = floorRoot.transform;
            baseFloor.transform.position = new Vector3(0.8f, -0.06f, 0f);
            baseFloor.transform.localScale = new Vector3(36f, 0.12f, 32f);
            Object.DestroyImmediate(baseFloor.GetComponent<Collider>());
            if (stoneFloorMaterial != null) baseFloor.GetComponent<Renderer>().sharedMaterial = stoneFloorMaterial;

            // Tiled courtyard flagstone slabs with subtle stone seam styling
            int gridX = 8;
            int gridZ = 7;
            float slabW = 4.2f;
            float slabH = 4.2f;
            float startX = 0.8f - (gridX - 1) * slabW * 0.5f;
            float startZ = 0.0f - (gridZ - 1) * slabH * 0.5f;

            for (int x = 0; x < gridX; x++)
            {
                for (int z = 0; z < gridZ; z++)
                {
                    GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slab.name = $"Stone_Slab_{x}_{z}";
                    slab.transform.parent = floorRoot.transform;
                    float posX = startX + x * slabW;
                    float posZ = startZ + z * slabH;
                    float heightOffset = ((x + z) % 3) * 0.004f;
                    slab.transform.position = new Vector3(posX, -0.015f + heightOffset, posZ);
                    slab.transform.localScale = new Vector3(slabW - 0.12f, 0.04f, slabH - 0.12f);
                    Object.DestroyImmediate(slab.GetComponent<Collider>());
                    if (stoneFloorMaterial != null) slab.GetComponent<Renderer>().sharedMaterial = stoneFloorMaterial;
                }
            }

            // 2. Raised Temple Plinth (Mandapam Platform beneath 7x7 board)
            GameObject plinthRoot = new GameObject("Board_Stone_Plinth");
            plinthRoot.transform.parent = envRoot.transform;

            GameObject lowerPlinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lowerPlinth.name = "Plinth_Lower";
            lowerPlinth.transform.parent = plinthRoot.transform;
            lowerPlinth.transform.position = new Vector3(0f, 0.02f, 0f);
            lowerPlinth.transform.localScale = new Vector3(12.0f, 0.06f, 12.0f);
            Object.DestroyImmediate(lowerPlinth.GetComponent<Collider>());
            if (stonePlinthMaterial != null) lowerPlinth.GetComponent<Renderer>().sharedMaterial = stonePlinthMaterial;

            GameObject upperPlinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            upperPlinth.name = "Plinth_Upper";
            upperPlinth.transform.parent = plinthRoot.transform;
            upperPlinth.transform.position = new Vector3(0f, 0.06f, 0f);
            upperPlinth.transform.localScale = new Vector3(11.4f, 0.06f, 11.4f);
            Object.DestroyImmediate(upperPlinth.GetComponent<Collider>());
            if (stonePlinthMaterial != null) upperPlinth.GetComponent<Renderer>().sharedMaterial = stonePlinthMaterial;

            // 3. Antique Carved Teakwood Board Rim (Bounding the 7x7 board)
            GameObject rimRoot = new GameObject("Board_Teakwood_Rim");
            rimRoot.transform.parent = envRoot.transform;

            float rimDist = 5.25f;
            float rimLength = 11.1f;
            float rimThickness = 0.6f;
            float rimHeight = 0.18f;
            float rimY = 0.13f;

            // North, South, East, West rim beams
            CreateRimBeam(rimRoot.transform, "Rim_North", new Vector3(0f, rimY, rimDist), new Vector3(rimLength, rimHeight, rimThickness));
            CreateRimBeam(rimRoot.transform, "Rim_South", new Vector3(0f, rimY, -rimDist), new Vector3(rimLength, rimHeight, rimThickness));
            CreateRimBeam(rimRoot.transform, "Rim_East", new Vector3(rimDist, rimY, 0f), new Vector3(rimThickness, rimHeight, rimLength));
            CreateRimBeam(rimRoot.transform, "Rim_West", new Vector3(-rimDist, rimY, 0f), new Vector3(rimThickness, rimHeight, rimLength));

            // 4 Ornate Brass Corner Brackets & Rivets
            Vector3[] corners = {
                new Vector3(-rimDist, rimY + 0.015f, -rimDist),
                new Vector3(rimDist, rimY + 0.015f, -rimDist),
                new Vector3(-rimDist, rimY + 0.015f, rimDist),
                new Vector3(rimDist, rimY + 0.015f, rimDist)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                GameObject cornerCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cornerCap.name = $"Brass_Corner_{i + 1}";
                cornerCap.transform.parent = rimRoot.transform;
                cornerCap.transform.position = corners[i];
                cornerCap.transform.localScale = new Vector3(0.72f, rimHeight + 0.03f, 0.72f);
                Object.DestroyImmediate(cornerCap.GetComponent<Collider>());
                if (brassMaterial != null) cornerCap.GetComponent<Renderer>().sharedMaterial = brassMaterial;

                // Center brass stud
                GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                stud.name = "Stud";
                stud.transform.parent = cornerCap.transform;
                stud.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                stud.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                Object.DestroyImmediate(stud.GetComponent<Collider>());
                if (brassMaterial != null) stud.GetComponent<Renderer>().sharedMaterial = brassMaterial;
            }

            // 4. Traditional South Indian Brass Kuthuvilakku / Diyas
            GameObject diyasRoot = new GameObject("Temple_Diyas");
            diyasRoot.transform.parent = envRoot.transform;

            // 4 Diya locations around courtyard
            CreateDiyaLamp(diyasRoot.transform, "Diya_SW", new Vector3(-5.8f, 0.06f, -4.6f), enableLight: true, intensity: 1.35f, range: 6.0f);
            CreateDiyaLamp(diyasRoot.transform, "Diya_SE", new Vector3(5.5f, 0.06f, -4.6f), enableLight: true, intensity: 1.35f, range: 6.0f);
            CreateDiyaLamp(diyasRoot.transform, "Diya_NW", new Vector3(-5.8f, 0.06f, 4.8f), enableLight: true, intensity: 0.95f, range: 4.5f);
            CreateDiyaLamp(diyasRoot.transform, "Diya_NE", new Vector3(5.5f, 0.06f, 4.8f), enableLight: true, intensity: 0.95f, range: 4.5f);
        }

        private void CreateRimBeam(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = name;
            beam.transform.parent = parent;
            beam.transform.localPosition = pos;
            beam.transform.localScale = scale;
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            if (teakMaterial != null) beam.GetComponent<Renderer>().sharedMaterial = teakMaterial;
        }

        private void CreateDiyaLamp(Transform parent, string name, Vector3 worldPos, bool enableLight, float intensity, float range)
        {
            GameObject diyaGO = new GameObject(name);
            diyaGO.transform.parent = parent;
            diyaGO.transform.position = worldPos;

            // Tier 1: Wide stepped pedestal
            GameObject tier1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tier1.name = "Pedestal_Base";
            tier1.transform.parent = diyaGO.transform;
            tier1.transform.localPosition = new Vector3(0, 0.05f, 0);
            tier1.transform.localScale = new Vector3(0.68f, 0.05f, 0.68f);
            Object.DestroyImmediate(tier1.GetComponent<Collider>());
            if (brassMaterial != null) tier1.GetComponent<Renderer>().sharedMaterial = brassMaterial;

            // Tier 2: Upper pedestal
            GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tier2.name = "Pedestal_Upper";
            tier2.transform.parent = diyaGO.transform;
            tier2.transform.localPosition = new Vector3(0, 0.12f, 0);
            tier2.transform.localScale = new Vector3(0.48f, 0.04f, 0.48f);
            Object.DestroyImmediate(tier2.GetComponent<Collider>());
            if (brassMaterial != null) tier2.GetComponent<Renderer>().sharedMaterial = brassMaterial;

            // Stem: Slender turned brass pillar
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.parent = diyaGO.transform;
            stem.transform.localPosition = new Vector3(0, 0.28f, 0);
            stem.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
            Object.DestroyImmediate(stem.GetComponent<Collider>());
            if (brassMaterial != null) stem.GetComponent<Renderer>().sharedMaterial = brassMaterial;

            // Saucer: Oil basin dish
            GameObject dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dish.name = "Oil_Saucer";
            dish.transform.parent = diyaGO.transform;
            dish.transform.localPosition = new Vector3(0, 0.46f, 0);
            dish.transform.localScale = new Vector3(0.62f, 0.05f, 0.62f);
            Object.DestroyImmediate(dish.GetComponent<Collider>());
            if (brassMaterial != null) dish.GetComponent<Renderer>().sharedMaterial = brassMaterial;

            // Central wick cone
            GameObject wick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wick.name = "Wick";
            wick.transform.parent = diyaGO.transform;
            wick.transform.localPosition = new Vector3(0, 0.52f, 0);
            wick.transform.localScale = new Vector3(0.12f, 0.04f, 0.12f);
            Object.DestroyImmediate(wick.GetComponent<Collider>());
            if (brassMaterial != null) wick.GetComponent<Renderer>().sharedMaterial = brassMaterial;

            // Glowing teardrop flame
            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            flame.transform.parent = diyaGO.transform;
            flame.transform.localPosition = new Vector3(0, 0.62f, 0);
            flame.transform.localScale = new Vector3(0.14f, 0.26f, 0.14f);
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            if (diyaFlameMaterial != null) flame.GetComponent<Renderer>().sharedMaterial = diyaFlameMaterial;

            Light pLight = null;
            if (enableLight)
            {
                GameObject lightGO = new GameObject("FlameLight", typeof(Light));
                lightGO.transform.parent = diyaGO.transform;
                lightGO.transform.localPosition = new Vector3(0, 0.72f, 0);
                pLight = lightGO.GetComponent<Light>();
                pLight.type = LightType.Point;
                pLight.color = new Color(1.0f, 0.72f, 0.35f);
                pLight.range = range;
                pLight.intensity = intensity;
            }

            var diyaComp = diyaGO.AddComponent<DayakattamDungeon.Environment.DiyaLamp>();
            diyaComp.Initialize(flame.transform, pLight, intensity, range);
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

            // Create or update wooden tray with antique teakwood border under the sticks
            Transform tray = roller.transform.Find("DiceTray");
            if (tray != null)
            {
                Object.DestroyImmediate(tray.gameObject);
            }

            GameObject diceTrayGO = new GameObject("DiceTray");
            diceTrayGO.transform.parent = roller.transform;
            diceTrayGO.transform.position = new Vector3(7.0f, 0.05f, -1.0f);
            var trayCol = diceTrayGO.AddComponent<BoxCollider>();
            trayCol.center = new Vector3(0f, 0.3f, 0f);
            trayCol.size = new Vector3(2.8f, 0.8f, 3.8f);
            diceTrayGO.AddComponent<DayakattamDiceTapHandler>();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.parent = diceTrayGO.transform;
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(2.5f, 0.08f, 3.5f);
            if (floor.GetComponent<BoxCollider>() == null) floor.AddComponent<BoxCollider>();
            floor.AddComponent<DayakattamDiceTapHandler>();
            if (blackMaterial != null) floor.GetComponent<Renderer>().sharedMaterial = blackMaterial;

            float tw = 2.5f;
            float th = 3.5f;
            float rW = 0.16f;
            float rH = 0.18f;
            float rY = 0.08f;
            CreateRimBeam(diceTrayGO.transform, "DiceTray_RimN", new Vector3(0, rY, th * 0.5f), new Vector3(tw + rW * 2f, rH, rW));
            CreateRimBeam(diceTrayGO.transform, "DiceTray_RimS", new Vector3(0, rY, -th * 0.5f), new Vector3(tw + rW * 2f, rH, rW));
            CreateRimBeam(diceTrayGO.transform, "DiceTray_RimE", new Vector3(tw * 0.5f, rY, 0), new Vector3(rW, rH, th));
            CreateRimBeam(diceTrayGO.transform, "DiceTray_RimW", new Vector3(-tw * 0.5f, rY, 0), new Vector3(rW, rH, th));

            if (stickA.GetComponent<Collider>() == null) stickA.gameObject.AddComponent<BoxCollider>();
            if (stickA.GetComponent<DayakattamDiceTapHandler>() == null) stickA.gameObject.AddComponent<DayakattamDiceTapHandler>();

            if (stickB.GetComponent<Collider>() == null) stickB.gameObject.AddComponent<BoxCollider>();
            if (stickB.GetComponent<DayakattamDiceTapHandler>() == null) stickB.gameObject.AddComponent<DayakattamDiceTapHandler>();

            if (roller.GetComponent<DayakattamDiceTapHandler>() == null) roller.gameObject.AddComponent<DayakattamDiceTapHandler>();

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

            roller.EnsureCollidersAndTapHandlers();

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

            if (Object.FindFirstObjectByType<DayakattamDungeon.Environment.PlayerTrayTurnIndicator>() == null)
            {
                new GameObject("PlayerTrayTurnIndicator", typeof(DayakattamDungeon.Environment.PlayerTrayTurnIndicator));
            }

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

            if (owner == PlayerType.Player1)
            {
                pawnGO.AddComponent<DayakattamDungeon.Entities.FirstThayamPawnIndicator>();
            }

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

            // Load custom SDF fonts from Resources/Fonts & Materials
            TMP_FontAsset titleFont = LoadCustomFont("Beyond Wonderland SDF");
            TMP_FontAsset bannerItalicFont = LoadCustomFont("Bilderberg Italic OTF SDF");
            TMP_FontAsset uiDisplayFont = LoadCustomFont("Bilderberg OTF SDF");
            TMP_FontAsset shivarajaFont = LoadCustomFont("Shivaraja SDF");
            TMP_FontAsset bodyFont = LoadCustomFont("CAROBTN_ SDF");

            // 1. Top Bar Elements (Anchored to Top-Center, perfectly spaced across 1920 width)
            Vector2 topAnchor = new Vector2(0.5f, 1f);
            TextMeshProUGUI turnBanner = CreateTMPText(canvas.transform, "TurnBanner", "DAYAKATTAM DUNGEON", 25.5f, new Vector2(0, -40), new Vector2(420, 44), TextAlignmentOptions.Center, topAnchor, topAnchor, bannerItalicFont);
            turnBanner.fontStyle = FontStyles.Bold | FontStyles.Italic;
            turnBanner.enableAutoSizing = true;
            turnBanner.fontSizeMin = 17f;
            turnBanner.fontSizeMax = 25.5f;
            turnBanner.textWrappingMode = TextWrappingModes.NoWrap;
            turnBanner.color = new Color(1f, 0.76f, 0.15f);
            turnBanner.enableVertexGradient = true;
            turnBanner.colorGradient = new VertexGradient(
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f)
            );

            TextMeshProUGUI p1HealthText = CreateTMPText(canvas.transform, "P1HealthText", "Player: 0/3 Home", 17, new Vector2(-310, -40), new Vector2(180, 35), TextAlignmentOptions.Center, topAnchor, topAnchor, uiDisplayFont);
            p1HealthText.color = new Color(1.0f, 0.40f, 0.40f);

            TextMeshProUGUI p4GoalText = CreateTMPText(canvas.transform, "P4GoalText", "Dada: 0/3", 16, new Vector2(-360, -40), new Vector2(100, 35), TextAlignmentOptions.Center, topAnchor, topAnchor, uiDisplayFont);
            p4GoalText.color = new Color(1f, 0.88f, 0.2f);
            p4GoalText.gameObject.SetActive(false);

            TextMeshProUGUI p2GoalText = CreateTMPText(canvas.transform, "P2GoalText", "Mama: 0/3", 16, new Vector2(260, -40), new Vector2(100, 35), TextAlignmentOptions.Center, topAnchor, topAnchor, uiDisplayFont);
            p2GoalText.color = new Color(0.35f, 0.70f, 1f);
            p2GoalText.gameObject.SetActive(false);

            TextMeshProUGUI botHealthText = CreateTMPText(canvas.transform, "BotHealthText", "Grandma: 0/3 Home", 17, new Vector2(310, -40), new Vector2(180, 35), TextAlignmentOptions.Center, topAnchor, topAnchor, uiDisplayFont);
            botHealthText.color = new Color(0.25f, 0.95f, 0.55f);

            Button diffBtn = CreateButton(canvas.transform, "DifficultyButton", "GRANDMA: NORMAL", new Vector2(-790, -40), new Vector2(145, 40), 13, new Color(0.18f, 0.28f, 0.24f), topAnchor, topAnchor, uiDisplayFont);
            TextMeshProUGUI diffText = diffBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Rules & Lore Button
            Button loreBtn = CreateButton(canvas.transform, "LoreButton", "? Rules & Lore", new Vector2(-650, -40), new Vector2(120, 40), 14, new Color(0.18f, 0.3f, 0.45f), topAnchor, topAnchor, uiDisplayFont);

            // Audio Mute Toggle Button
            Button audioBtn = CreateButton(canvas.transform, "AudioToggleButton", "Sound: ON [M]", new Vector2(-520, -40), new Vector2(125, 40), 14, new Color(0.22f, 0.28f, 0.35f), topAnchor, topAnchor, uiDisplayFont);
            TextMeshProUGUI audioText = audioBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Waypoint Path Toggle Button
            Button waypointBtn = CreateButton(canvas.transform, "WaypointToggleButton", "Path: ON [P]", new Vector2(480, -40), new Vector2(120, 40), 14, new Color(0.15f, 0.35f, 0.45f), topAnchor, topAnchor, uiDisplayFont);
            TextMeshProUGUI waypointText = waypointBtn.GetComponentInChildren<TextMeshProUGUI>();

            // 2P / 4P Mode Toggle Button
            Button modeBtn = CreateButton(canvas.transform, "PlayerModeButton", "Mode: 2P", new Vector2(615, -40), new Vector2(140, 40), 14, new Color(0.3f, 0.22f, 0.45f), topAnchor, topAnchor, uiDisplayFont);
            TextMeshProUGUI modeText = modeBtn.GetComponentInChildren<TextMeshProUGUI>();

            Button restartBtn = CreateButton(canvas.transform, "RestartButton", "Restart Match", new Vector2(760, -40), new Vector2(130, 40), 14, new Color(0.35f, 0.15f, 0.15f), topAnchor, topAnchor, uiDisplayFont);

            Button menuBtn = CreateButton(canvas.transform, "MenuButton", "Menu [ESC]", new Vector2(888, -40), new Vector2(105, 40), 14, new Color(0.24f, 0.24f, 0.34f), topAnchor, topAnchor, uiDisplayFont);

            // Rules & Lore / Interactive Tutorial Modal Panel (Initially hidden)
            InteractiveTutorialDialog tutDialog = DayakattamDungeon.EditorUtils.TutorialModalBuilder.BuildInteractiveTutorialModal(
                canvas.transform,
                titleFont,
                bodyFont,
                uiDisplayFont
            );

            GameObject lorePanelGO = tutDialog.gameObject;
            loreBtn.onClick.AddListener(() =>
            {
                if (lorePanelGO.activeSelf) tutDialog.Close();
                else tutDialog.Open();
            });

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

            TextMeshProUGUI tutText = CreateTMPText(tutPanelGO.transform, "TutorialDialogueText", "Grandmother's Voice: ...", 18, new Vector2(0, 10), new Vector2(850, 90), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bodyFont);
            tutText.color = new Color(1f, 0.95f, 0.8f);

            Button skipBtn = CreateButton(tutPanelGO.transform, "SkipTutorialButton", "Skip Tutorial", new Vector2(360, -40), new Vector2(130, 32), 15, new Color(0.6f, 0.2f, 0.2f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);

            // 3. Bottom Control Elements (Anchored to Bottom-Center)
            Vector2 bottomAnchor = new Vector2(0.5f, 0f);
            Button rollBtn = CreateButton(canvas.transform, "RollDiceButton", "ROLL [SPACE]", new Vector2(0, 52), new Vector2(240, 52), 19, new Color(0.85f, 0.65f, 0.15f), bottomAnchor, bottomAnchor, uiDisplayFont);
            Button endTurnBtn = CreateButton(canvas.transform, "EndTurnButton", "END [E]", new Vector2(190, 52), new Vector2(105, 46), 15, new Color(0.5f, 0.5f, 0.5f), bottomAnchor, bottomAnchor, uiDisplayFont);

            TextMeshProUGUI comboText = CreateTMPText(canvas.transform, "ComboText", "Streak: 0 | Combo: 1.0x", 18, new Vector2(0, 16), new Vector2(400, 28), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor, shivarajaFont);
            TextMeshProUGUI lastRoll = CreateTMPText(canvas.transform, "LastRollText", "Roll to Begin", 18, new Vector2(0, 158), new Vector2(540, 28), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor, shivarajaFont);
            TextMeshProUGUI luckyText = CreateTMPText(canvas.transform, "LuckyCalloutText", "THAYAM! (1)", 26, new Vector2(0, 192), new Vector2(600, 40), TextAlignmentOptions.Center, bottomAnchor, bottomAnchor, bannerItalicFont);
            luckyText.color = Color.yellow;
            luckyText.fontStyle = FontStyles.Bold | FontStyles.Italic;

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

            TextMeshProUGUI promptTxt = CreateTMPText(actionPanelGO.transform, "ActionPromptText", "Choose Spirit Pawn to move:", 18, new Vector2(0, 85), new Vector2(480, 40), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            promptTxt.color = new Color(1f, 0.85f, 0.25f);
            promptTxt.fontStyle = FontStyles.Bold;

            Button[] actButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                actButtons[i] = CreateButton(actionPanelGO.transform, $"PawnChoiceBtn_{i + 1}", $"Pawn #{i + 1}", new Vector2(0, 25 - (i * 55)), new Vector2(440, 46), 16, new Color(0.18f, 0.42f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
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

            TextMeshProUGUI goTitle = CreateTMPText(gameOverPanelGO.transform, "GameOverTitle", "VICTORY!", 42, new Vector2(0, 195), new Vector2(660, 76), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), titleFont);
            goTitle.fontStyle = FontStyles.Bold;

            TextMeshProUGUI goSubtitle = CreateTMPText(gameOverPanelGO.transform, "GameOverSubtitle", "You unlocked the Sacred Palace!", 15, new Vector2(0, 95), new Vector2(640, 110), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bodyFont);
            goSubtitle.color = new Color(0.85f, 0.85f, 0.9f);

            TextMeshProUGUI goStats = CreateTMPText(gameOverPanelGO.transform, "GameOverStats", "Match Statistics...", 16, new Vector2(0, -45), new Vector2(620, 130), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), bodyFont);

            Button fourPlayerBtn = CreateButton(gameOverPanelGO.transform, "FourPlayerModeButton", "4-Player Mode", new Vector2(-150, -195), new Vector2(250, 50), 17, new Color(0.18f, 0.55f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button twoPlayerHardBtn = CreateButton(gameOverPanelGO.transform, "TwoPlayerHardButton", "2-Player Hard Mode", new Vector2(150, -195), new Vector2(250, 50), 17, new Color(0.65f, 0.25f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);

            gameOverPanelGO.SetActive(false);

            // 7. Atmospheric Title Screen Overlay
            GameObject titlePanelGO = new GameObject("TitleScreenPanel", typeof(RectTransform), typeof(Image));
            titlePanelGO.transform.SetParent(canvas.transform, false);
            RectTransform titlePanelRT = titlePanelGO.GetComponent<RectTransform>();
            titlePanelRT.anchorMin = Vector2.zero;
            titlePanelRT.anchorMax = Vector2.one;
            titlePanelRT.offsetMin = Vector2.zero;
            titlePanelRT.offsetMax = Vector2.zero;
            titlePanelGO.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.07f, 0.88f);

            GameObject titleCardGO = new GameObject("TitleCard", typeof(RectTransform), typeof(Image));
            titleCardGO.transform.SetParent(titlePanelGO.transform, false);
            RectTransform titleCardRT = titleCardGO.GetComponent<RectTransform>();
            titleCardRT.anchorMin = new Vector2(0.5f, 0.5f);
            titleCardRT.anchorMax = new Vector2(0.5f, 0.5f);
            titleCardRT.anchoredPosition = Vector2.zero;
            titleCardRT.sizeDelta = new Vector2(680, 490);
            titleCardGO.GetComponent<Image>().color = new Color(0.06f, 0.05f, 0.10f, 0.96f);

            TextMeshProUGUI mainTitle = CreateTMPText(titleCardGO.transform, "MainTitle", "DAYAKATTAM DUNGEON", 44, new Vector2(0, 168), new Vector2(640, 68), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), titleFont);
            mainTitle.color = Color.white;
            mainTitle.fontStyle = FontStyles.Bold;
            mainTitle.enableAutoSizing = true;
            mainTitle.fontSizeMin = 28f;
            mainTitle.fontSizeMax = 44f;
            mainTitle.textWrappingMode = TextWrappingModes.NoWrap;
            mainTitle.enableVertexGradient = true;
            mainTitle.colorGradient = new VertexGradient(
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f)
            );

            TextMeshProUGUI mainSub = CreateTMPText(titleCardGO.transform, "MainSubtitle", "The Sacred Tamil Dice Duel\nGuide all 3 Kalasam spirit pawns into the Central Palace.", 18, new Vector2(0, 80), new Vector2(580, 64), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            mainSub.color = new Color(0.88f, 0.88f, 0.94f);

            Button playDuelBtn = CreateButton(titleCardGO.transform, "PlayDuelButton", "Play Duel", new Vector2(0, -10), new Vector2(320, 56), 20, new Color(0.16f, 0.54f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button titleTutorialBtn = CreateButton(titleCardGO.transform, "InteractiveTutorialButton", "Interactive Tutorial", new Vector2(0, -82), new Vector2(320, 52), 18, new Color(0.18f, 0.38f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button titleSettingsBtn = CreateButton(titleCardGO.transform, "TitleSettingsButton", "Settings", new Vector2(0, -152), new Vector2(320, 48), 18, new Color(0.28f, 0.28f, 0.38f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);

            // 8. Pause & Settings Modal Panel
            GameObject pausePanelGO = new GameObject("PauseMenuPanel", typeof(RectTransform), typeof(Image));
            pausePanelGO.transform.SetParent(canvas.transform, false);
            RectTransform pauseOverlayRT = pausePanelGO.GetComponent<RectTransform>();
            pauseOverlayRT.anchorMin = Vector2.zero;
            pauseOverlayRT.anchorMax = Vector2.one;
            pauseOverlayRT.offsetMin = Vector2.zero;
            pauseOverlayRT.offsetMax = Vector2.zero;
            pausePanelGO.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.05f, 0.78f);

            GameObject pauseCardGO = new GameObject("PauseCard", typeof(RectTransform), typeof(Image));
            pauseCardGO.transform.SetParent(pausePanelGO.transform, false);
            RectTransform pauseCardRT = pauseCardGO.GetComponent<RectTransform>();
            pauseCardRT.anchorMin = new Vector2(0.5f, 0.5f);
            pauseCardRT.anchorMax = new Vector2(0.5f, 0.5f);
            pauseCardRT.anchoredPosition = Vector2.zero;
            pauseCardRT.sizeDelta = new Vector2(560, 520);
            pauseCardGO.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.10f, 0.97f);

            TextMeshProUGUI pauseTitle = CreateTMPText(pauseCardGO.transform, "PauseTitle", "PAUSED & SETTINGS", 36, new Vector2(0, 210), new Vector2(520, 56), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), titleFont);
            pauseTitle.color = Color.white;
            pauseTitle.fontStyle = FontStyles.Bold;
            pauseTitle.enableVertexGradient = true;
            pauseTitle.colorGradient = new VertexGradient(
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f)
            );

            TextMeshProUGUI pauseVolLabel = CreateTMPText(pauseCardGO.transform, "PauseVolumeLabel", "Master Volume: 100%", 17, new Vector2(0, 148), new Vector2(420, 30), TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Slider pauseVolSlider = CreateVolumeSlider(pauseCardGO.transform, new Vector2(0, 112), new Vector2(380, 26));

            Button pauseMusicBtn = CreateButton(pauseCardGO.transform, "PauseMusicButton", "Music : on", new Vector2(0, 50), new Vector2(380, 44), 16, new Color(0.18f, 0.30f, 0.36f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button pauseDiffBtn = CreateButton(pauseCardGO.transform, "PauseDifficultyButton", "Grandma Difficulty: NORMAL", new Vector2(0, -6), new Vector2(380, 44), 16, new Color(0.22f, 0.32f, 0.26f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button pausePathBtn = CreateButton(pauseCardGO.transform, "PausePathButton", "Path Trail: ON", new Vector2(0, -62), new Vector2(380, 44), 16, new Color(0.16f, 0.34f, 0.44f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button pauseRestartBtn = CreateButton(pauseCardGO.transform, "PauseRestartButton", "Restart Match", new Vector2(-98, -126), new Vector2(182, 46), 16, new Color(0.45f, 0.18f, 0.18f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button pauseTitleBtn = CreateButton(pauseCardGO.transform, "PauseTitleButton", "Title Screen", new Vector2(98, -126), new Vector2(182, 46), 16, new Color(0.32f, 0.26f, 0.44f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            Button pauseResumeBtn = CreateButton(pauseCardGO.transform, "PauseResumeButton", "Resume", new Vector2(0, -196), new Vector2(380, 50), 18, new Color(0.16f, 0.54f, 0.32f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), uiDisplayFont);
            pausePanelGO.SetActive(false);

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
            SetField(hud, "menuButton", menuBtn);
            SetField(hud, "titleScreenPanel", titlePanelGO);
            SetField(hud, "titlePlayDuelButton", playDuelBtn);
            SetField(hud, "titleTutorialButton", titleTutorialBtn);
            SetField(hud, "titleSettingsButton", titleSettingsBtn);
            SetField(hud, "pauseMenuPanel", pausePanelGO);
            SetField(hud, "pauseResumeButton", pauseResumeBtn);
            SetField(hud, "pauseVolumeSlider", pauseVolSlider);
            SetField(hud, "pauseVolumeText", pauseVolLabel);
            SetField(hud, "pauseMusicToggleButton", pauseMusicBtn);
            SetField(hud, "pauseMusicToggleText", pauseMusicBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(hud, "pauseDifficultyButton", pauseDiffBtn);
            SetField(hud, "pauseDifficultyText", pauseDiffBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(hud, "pausePathButton", pausePathBtn);
            SetField(hud, "pausePathText", pausePathBtn.GetComponentInChildren<TextMeshProUGUI>());
            SetField(hud, "pauseRestartButton", pauseRestartBtn);
            SetField(hud, "pauseTitleButton", pauseTitleBtn);

            hud.InitializeHUD();
            hud.RefreshAllPalaceProgressUI();
        }

        private Slider CreateVolumeSlider(Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            GameObject sliderGO = new GameObject("VolumeSlider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(parent, false);
            RectTransform rt = sliderGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(sliderGO.transform, false);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.26f);

            GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            RectTransform fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRT.offsetMin = Vector2.zero;
            fillAreaRT.offsetMax = Vector2.zero;

            GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillGO.GetComponent<Image>().color = new Color(0.90f, 0.72f, 0.20f);

            GameObject handleAreaGO = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            RectTransform handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            RectTransform handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(22f, size.y + 4f);
            handleGO.GetComponent<Image>().color = Color.white;

            Slider slider = sliderGO.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleGO.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        private TMP_FontAsset LoadCustomFont(string fontAssetName)
        {
            TMP_FontAsset loaded = Resources.Load<TMP_FontAsset>($"Fonts & Materials/{fontAssetName}");
            return loaded != null ? loaded : TMP_Settings.defaultFontAsset;
        }

        private TextMeshProUGUI CreateTMPText(Transform parent, string name, string content, float fontSize, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align, Vector2? aMin = null, Vector2? aMax = null, TMP_FontAsset customFont = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (aMin.HasValue) rt.anchorMin = aMin.Value;
            if (aMax.HasValue) rt.anchorMax = aMax.Value;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            TextMeshProUGUI txt = go.GetComponent<TextMeshProUGUI>();
            TMP_FontAsset chosenFont = customFont != null ? customFont : TMP_Settings.defaultFontAsset;
            if (chosenFont != null)
            {
                txt.font = chosenFont;
            }
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = Color.white;
            return txt;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, float fontSize, Color bgColor, Vector2? aMin = null, Vector2? aMax = null, TMP_FontAsset customFont = null)
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

            TextMeshProUGUI txt = CreateTMPText(go.transform, "Label", label, fontSize, Vector2.zero, size, TextAlignmentOptions.Center, new Vector2(0, 0), new Vector2(1, 1), customFont);
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
