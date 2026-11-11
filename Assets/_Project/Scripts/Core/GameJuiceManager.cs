using System.Collections;
using UnityEngine;
using TMPro;
using DayakattamDungeon.Core;
using DayakattamDungeon.Entities;

namespace DayakattamDungeon.Core
{
    /// <summary>
    /// Game Jam Polish & Juice Manager:
    /// - Camera Screenshake (hops, lucky rolls, captures)
    /// - Micro Hit-Stop (crunchy combat freeze frames)
    /// - 3D Floating Combat Text billboarding to camera
    /// - Auspicious particle burst triggers
    /// </summary>
    public class GameJuiceManager : MonoBehaviour
    {
        public static GameJuiceManager Instance { get; private set; }

        [Header("Camera Shake Settings")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float shakeDecay = 4.5f;

        private Vector3 originalCameraPosition;
        private float currentShakeIntensity = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (cameraTransform != null)
            {
                originalCameraPosition = cameraTransform.position;
            }
        }

        private void Update()
        {
            if (currentShakeIntensity > 0.001f && cameraTransform != null)
            {
                Vector3 offset = Random.insideUnitSphere * currentShakeIntensity;
                offset.z = 0; // Keep depth stable
                cameraTransform.position = originalCameraPosition + offset;
                currentShakeIntensity = Mathf.MoveTowards(currentShakeIntensity, 0f, shakeDecay * Time.unscaledDeltaTime);
            }
            else if (cameraTransform != null && cameraTransform.position != originalCameraPosition)
            {
                cameraTransform.position = originalCameraPosition;
            }
        }

        /// <summary>
        /// Triggers a screen shake with the specified intensity (e.g. 0.08 for hops, 0.3 for captures).
        /// </summary>
        public void TriggerShake(float intensity)
        {
            currentShakeIntensity = Mathf.Max(currentShakeIntensity, intensity);
        }

        /// <summary>
        /// Micro hit-stop freeze frame for combat strikes (50-80ms).
        /// </summary>
        public void TriggerHitStop(float duration = 0.065f)
        {
            StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            float prevTimeScale = Time.timeScale;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = prevTimeScale;
        }

        /// <summary>
        /// Spawns a floating combat text billboard in 3D world space over a target position.
        /// </summary>
        public void SpawnFloatingText(Vector3 worldPos, string message, Color color, float scale = 1.0f)
        {
            StartCoroutine(FloatingTextRoutine(worldPos, message, color, scale));
        }

        private IEnumerator FloatingTextRoutine(Vector3 worldPos, string message, Color color, float targetScale)
        {
            GameObject go = new GameObject("FloatingText", typeof(TextMeshPro));
            go.transform.position = worldPos + Vector3.up * 0.8f;
            if (Camera.main != null)
            {
                go.transform.rotation = Camera.main.transform.rotation;
            }

            TextMeshPro tmp = go.GetComponent<TextMeshPro>();
            tmp.text = message;
            tmp.fontSize = 6.5f * targetScale;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;

            float duration = 0.9f;
            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + Vector3.up * 1.5f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Upward float
                go.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, t));

                // Scale pop (1 -> 1.25 -> 1)
                float pop = (t < 0.2f) ? Mathf.Lerp(0.5f, 1.2f, t / 0.2f) : Mathf.Lerp(1.2f, 1.0f, (t - 0.2f) / 0.8f);
                go.transform.localScale = Vector3.one * pop;

                // Alpha fade
                float alpha = (t > 0.6f) ? Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f) : 1f;
                tmp.color = new Color(color.r, color.g, color.b, alpha);

                yield return null;
            }

            Destroy(go);
        }

        /// <summary>
        /// Spawns celebratory golden dust or combat strike sparks.
        /// </summary>
        public void SpawnImpactSparks(Vector3 worldPos, Color sparkColor, int count = 16)
        {
            StartCoroutine(SparksRoutine(worldPos, sparkColor, count));
        }

        private IEnumerator SparksRoutine(Vector3 worldPos, Color color, int count)
        {
            GameObject container = new GameObject("ImpactSparks");
            container.transform.position = worldPos;

            Transform[] sparks = new Transform[count];
            Vector3[] velocities = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                spark.transform.parent = container.transform;
                spark.transform.position = worldPos + Vector3.up * 0.2f;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

                Renderer r = spark.GetComponent<Renderer>();
                r.material.color = color;

                sparks[i] = spark.transform;
                velocities[i] = (Random.insideUnitSphere + Vector3.up * 1.5f) * Random.Range(2.5f, 5.0f);
            }

            float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dt = Time.deltaTime;

                for (int i = 0; i < count; i++)
                {
                    if (sparks[i] != null)
                    {
                        sparks[i].position += velocities[i] * dt;
                        velocities[i] += Vector3.down * (9.8f * dt); // Gravity
                        sparks[i].localScale = Vector3.Lerp(sparks[i].localScale, Vector3.zero, elapsed / duration);
                    }
                }
                yield return null;
            }

            Destroy(container);
        }
    }
}
