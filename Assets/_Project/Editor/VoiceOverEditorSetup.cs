using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DayakattamDungeon.Audio;

namespace DayakattamDungeon.EditorTools
{
    public static class VoiceOverEditorSetup
    {
        [MenuItem("Dayakattam/Setup Voice Over Integration")]
        public static void SetupVoiceOver()
        {
            Debug.Log("[VoiceOverEditorSetup] Running VO setup and clip binding...");

            // 1. Ensure Assets/_Project/Audio folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Audio"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Audio");
            }

            // 2. Ensure Assets/_Project/Audio/VO folder exists
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Audio/VO"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Audio", "VO");
            }

            // 3. Move files from Assets/_Project/Voice over to Assets/_Project/Audio/VO if present
            if (AssetDatabase.IsValidFolder("Assets/_Project/Voice over"))
            {
                string[] files = Directory.GetFiles("Assets/_Project/Voice over", "*.ogg");
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string oldPath = "Assets/_Project/Voice over/" + fileName;
                    string newPath = "Assets/_Project/Audio/VO/" + fileName;
                    string result = AssetDatabase.MoveAsset(oldPath, newPath);
                    if (string.IsNullOrEmpty(result))
                    {
                        Debug.Log($"[VoiceOverEditorSetup] Moved {fileName} -> Assets/_Project/Audio/VO/");
                    }
                    else
                    {
                        Debug.LogWarning($"[VoiceOverEditorSetup] Move result for {fileName}: {result}");
                    }
                }

                // Delete old folder if empty
                string[] remaining = Directory.GetFiles("Assets/_Project/Voice over");
                if (remaining.Length == 0)
                {
                    AssetDatabase.DeleteAsset("Assets/_Project/Voice over");
                    Debug.Log("[VoiceOverEditorSetup] Deleted old Assets/_Project/Voice over directory.");
                }
            }

            AssetDatabase.Refresh();

            // 4. Find or Create VoiceOverManager in open scene
            VoiceOverManager voManager = UnityEngine.Object.FindFirstObjectByType<VoiceOverManager>();
            if (voManager == null)
            {
                GameObject voGO = new GameObject("VoiceOverManager");
                voManager = voGO.AddComponent<VoiceOverManager>();
                Undo.RegisterCreatedObjectUndo(voGO, "Create VoiceOverManager");
                Debug.Log("[VoiceOverEditorSetup] Created VoiceOverManager GameObject in scene.");
            }

            // 5. Ensure AudioSource
            AudioSource src = voManager.GetComponent<AudioSource>();
            if (src == null)
            {
                src = voManager.gameObject.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;

            // 6. Bind clips via AutoAssignClips
            voManager.AutoAssignClips();

            // 7. Mark scene dirty and save
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log("[VoiceOverEditorSetup] Finished successfully! VoiceOverManager is fully wired in the scene.");
        }
    }
}
