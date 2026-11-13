using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DayakattamDungeon.EditorTools
{
    /// <summary>
    /// Applies WebGL release Player, IL2CPP, Quality, URP, and Graphics settings
    /// following Unity 6 Web optimization guidance.
    /// </summary>
    public static class WebOptimizer
    {
        private const string BaseUrpAssetPath = "Assets/Settings/URP-Asset.asset";
        private const string BaseUrpRendererPath = "Assets/Settings/URP-Renderer.asset";
        private const string WebUrpAssetPath = "Assets/Settings/URP-Asset-Web.asset";
        private const string WebUrpRendererPath = "Assets/Settings/URP-Renderer-Web.asset";
        private const string WebQualityName = "Web Balanced";

        [MenuItem("Tools/Apply Web Release Settings")]
        public static void Optimize()
        {
            var target = NamedBuildTarget.WebGL;

            // Player & IL2CPP code-size / stripping settings
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
            PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.stripUnusedMeshComponents = true;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = false;

            // WebGL-specific size & runtime settings
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.wasm2023 = true;
            PlayerSettings.WebGL.template = "PROJECT:FullscreenResponsive";

            UnityEditor.WebGL.UserBuildSettings.codeOptimization =
                UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;

            // Strip all BatchRendererGroup shader variants (Entities Graphics is unused)
            var graphicsAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphicsAsset != null)
            {
                var graphicsSo = new SerializedObject(graphicsAsset);
                var brgProp = graphicsSo.FindProperty("m_BrgStripping");
                if (brgProp != null)
                {
                    brgProp.intValue = 1; // Strip All
                    graphicsSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Ensure lightweight URP assets & dedicated WebGL quality level
            UniversalRenderPipelineAsset webUrpAsset = EnsureWebUrpAssets();
            int webQualityIndex = EnsureWebQualityLevel(webUrpAsset);
            if (webQualityIndex >= 0 && EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                QualitySettings.SetQualityLevel(webQualityIndex, true);
            }

            // Browser requestAnimationFrame pacing
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[WebOptimizer] Applied all Web release Player, Quality, URP, and Graphics settings.");
        }

        [MenuItem("Tools/Build WebGL Release")]
        public static void BuildWebGLRelease()
        {
            Optimize();

            string[] scenes = new string[] { "Assets/GameJam/Scenes/MAIN.unity" };
            string buildPath = "Builds/WebGL";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            Debug.Log($"[WebOptimizer] Starting WebGL release build to '{buildPath}'...");
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                throw new System.Exception($"WebGL build failed with {report.summary.totalErrors} errors!");
            }
            Debug.Log($"[WebOptimizer] WebGL release build completed successfully ({report.summary.totalSize} bytes)!");
        }

        private static UniversalRenderPipelineAsset EnsureWebUrpAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(WebUrpRendererPath) == null &&
                AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(BaseUrpRendererPath) != null)
            {
                AssetDatabase.CopyAsset(BaseUrpRendererPath, WebUrpRendererPath);
            }

            var webRenderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(WebUrpRendererPath);
            if (webRenderer != null)
            {
                foreach (var feature in webRenderer.rendererFeatures)
                {
                    if (feature != null && feature.GetType().Name.Contains("AmbientOcclusion"))
                    {
                        feature.SetActive(false);
                        EditorUtility.SetDirty(feature);
                    }
                }

                var rendererSo = new SerializedObject(webRenderer);
                SetInt(rendererSo, "m_IntermediateTextureMode", 0); // Auto
                SetInt(rendererSo, "m_DepthPrimingMode", 0);        // Disabled
                rendererSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(webRenderer);
            }

            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(WebUrpAssetPath) == null &&
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(BaseUrpAssetPath) != null)
            {
                AssetDatabase.CopyAsset(BaseUrpAssetPath, WebUrpAssetPath);
            }

            var webUrp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(WebUrpAssetPath);
            if (webUrp != null)
            {
                webUrp.supportsHDR = false;
                webUrp.msaaSampleCount = 2;
                webUrp.shadowDistance = 35f;
                webUrp.shadowCascadeCount = 1;
                webUrp.supportsCameraDepthTexture = false;
                webUrp.supportsCameraOpaqueTexture = false;

                var urpSo = new SerializedObject(webUrp);
                SetBool(urpSo, "m_UseAdaptivePerformance", false);
                SetInt(urpSo, "m_MainLightShadowmapResolution", 1024);
                SetInt(urpSo, "m_AdditionalLightsShadowmapResolution", 512);
                SetBool(urpSo, "m_SoftShadowsSupported", false);
                SetInt(urpSo, "m_SoftShadowQuality", 0);

                if (webRenderer != null)
                {
                    var rendererListProp = urpSo.FindProperty("m_RendererDataList");
                    if (rendererListProp != null && rendererListProp.isArray && rendererListProp.arraySize > 0)
                    {
                        rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue = webRenderer;
                    }
                }

                urpSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(webUrp);
            }

            return webUrp;
        }

        private static int EnsureWebQualityLevel(UniversalRenderPipelineAsset webUrpAsset)
        {
            var qsAsset = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/QualitySettings.asset");
            if (qsAsset == null) return -1;

            var so = new SerializedObject(qsAsset);
            var levelsProp = so.FindProperty("m_QualitySettings");
            if (levelsProp == null || !levelsProp.isArray || levelsProp.arraySize == 0) return -1;

            int webIndex = -1;
            for (int i = 0; i < levelsProp.arraySize; i++)
            {
                var nameProp = levelsProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                if (nameProp != null && nameProp.stringValue == WebQualityName)
                {
                    webIndex = i;
                    break;
                }
            }

            if (webIndex < 0)
            {
                int srcIndex = levelsProp.arraySize - 1;
                levelsProp.InsertArrayElementAtIndex(levelsProp.arraySize);
                webIndex = levelsProp.arraySize - 1;
                // Unity duplicates the last element cleanly
                if (srcIndex >= 0)
                {
                    var webElemNew = levelsProp.GetArrayElementAtIndex(webIndex);
                    SetString(webElemNew, "name", WebQualityName);
                }
            }

            var webElem = levelsProp.GetArrayElementAtIndex(webIndex);
            SetString(webElem, "name", WebQualityName);
            SetInt(webElem, "vSyncCount", 0);
            SetInt(webElem, "antiAliasing", 2);
            SetInt(webElem, "shadows", (int)UnityEngine.ShadowQuality.HardOnly);
            SetInt(webElem, "shadowResolution", (int)UnityEngine.ShadowResolution.Medium);
            SetFloat(webElem, "shadowDistance", 35f);
            SetInt(webElem, "shadowCascades", 1);

            if (webUrpAsset != null)
            {
                var rpProp = webElem.FindPropertyRelative("customRenderPipeline");
                if (rpProp != null)
                {
                    rpProp.objectReferenceValue = webUrpAsset;
                }
            }

            // Set WebGL per-platform default quality level to Web Balanced
            var platformDefaults = so.FindProperty("m_PerPlatformDefaultQuality");
            if (platformDefaults != null && platformDefaults.isArray)
            {
                bool updatedWeb = false;
                for (int i = 0; i < platformDefaults.arraySize; i++)
                {
                    var entry = platformDefaults.GetArrayElementAtIndex(i);
                    var keyProp = entry.FindPropertyRelative("first");
                    var valProp = entry.FindPropertyRelative("second");
                    if (keyProp != null && valProp != null && keyProp.stringValue == "WebGL")
                    {
                        valProp.intValue = webIndex;
                        updatedWeb = true;
                        break;
                    }
                }

                if (!updatedWeb)
                {
                    int newIdx = platformDefaults.arraySize;
                    platformDefaults.InsertArrayElementAtIndex(newIdx);
                    var entry = platformDefaults.GetArrayElementAtIndex(newIdx);
                    SetString(entry, "first", "WebGL");
                    SetInt(entry, "second", webIndex);
                }
            }

            var currentQualityProp = so.FindProperty("m_CurrentQuality");
            if (currentQualityProp != null && EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                currentQualityProp.intValue = webIndex;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qsAsset);
            return webIndex;
        }

        private static void SetInt(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p != null) p.boolValue = value;
        }

        private static void SetInt(SerializedProperty parent, string relPath, int value)
        {
            var p = parent.FindPropertyRelative(relPath);
            if (p != null) p.intValue = value;
        }

        private static void SetFloat(SerializedProperty parent, string relPath, float value)
        {
            var p = parent.FindPropertyRelative(relPath);
            if (p != null) p.floatValue = value;
        }

        private static void SetString(SerializedProperty parent, string relPath, string value)
        {
            var p = parent.FindPropertyRelative(relPath);
            if (p != null) p.stringValue = value;
        }
    }
}
