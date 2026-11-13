using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DayakattamDungeon.Audio;

namespace DayakattamDungeon.EditorTools
{
    [InitializeOnLoad]
    public static class VoiceOverVerifier
    {
        static VoiceOverVerifier()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    VerifyAndWire();
                }
            };
        }

        [MenuItem("Dayakattam/Verify and Wire VoiceOverManager")]
        public static void VerifyAndWire()
        {
            Debug.Log("[VoiceOverVerifier] Verifying VoiceOverManager in active scene...");

            VoiceOverManager voManager = UnityEngine.Object.FindFirstObjectByType<VoiceOverManager>();
            if (voManager == null)
            {
                GameObject voGO = new GameObject("VoiceOverManager");
                voManager = voGO.AddComponent<VoiceOverManager>();
                Undo.RegisterCreatedObjectUndo(voGO, "Create VoiceOverManager");
                Debug.Log("[VoiceOverVerifier] Created VoiceOverManager GameObject.");
            }

            AudioSource src = voManager.GetComponent<AudioSource>();
            if (src == null)
            {
                src = voManager.gameObject.AddComponent<AudioSource>();
            }
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;

            // Auto-assign all 16 clips
            voManager.AutoAssignClips();

            // Verify all 16 fields
            FieldInfo[] fields = typeof(VoiceOverManager).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            int clipCount = 0;
            int assignedCount = 0;

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(AudioClip))
                {
                    clipCount++;
                    AudioClip clip = field.GetValue(voManager) as AudioClip;
                    if (clip != null)
                    {
                        assignedCount++;
                        Debug.Log($"[VoiceOverVerifier] Assigned: {field.Name} -> {clip.name}");
                    }
                    else
                    {
                        Debug.LogError($"[VoiceOverVerifier] MISSING CLIP for field: {field.Name}");
                    }
                }
            }

            // Save scene
            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[VoiceOverVerifier] Result: {assignedCount}/{clipCount} voice clips assigned and scene saved!");
        }
    }
}
