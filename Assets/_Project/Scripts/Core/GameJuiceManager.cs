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
        private Coroutine hitStopRoutine;
        private Material sharedJuiceMaterial;
        private MaterialPropertyBlock propBlock;

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
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
            }
            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            hitStopRoutine = null;
        }

        private Material GetSharedJuiceMaterial()
        {
            if (sharedJuiceMaterial == null)
            {
                Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                sharedJuiceMaterial = new Material(s);
            }
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }
            return sharedJuiceMaterial;
        }

        private void ApplyRendererColor(Renderer r, Color c)
        {
            if (r == null) return;
            r.sharedMaterial = GetSharedJuiceMaterial();
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", c);
            propBlock.SetColor("_BaseColor", c);
            r.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Spawns a floating combat text billboard in 3D world space over a target position.
        /// </summary>
        public void SpawnFloatingText(Vector3 worldPos, string message, Color color, float scale = 1.0f)
        {
            StartCoroutine(FloatingTextRoutine(worldPos, message, color, scale));
        }

        private TMP_FontAsset shivarajaFont;

        private TMP_FontAsset GetFloatingTextFont()
        {
            if (shivarajaFont == null)
            {
                shivarajaFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/Shivaraja SDF");
            }
            return shivarajaFont != null ? shivarajaFont : TMP_Settings.defaultFontAsset;
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
            TMP_FontAsset fontAsset = GetFloatingTextFont();
            if (fontAsset != null) tmp.font = fontAsset;

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

        /// <summary>
        /// Spawns an auspicious golden lotus beacon beam rising from the Sacred Palace
        /// with ascending radiant energy rings and celebration sparks.
        /// </summary>
        public void SpawnPalaceBeacon(Vector3 worldPos, Color beaconColor)
        {
            StartCoroutine(PalaceBeaconRoutine(worldPos, beaconColor));
        }

        /// <summary>
        /// Spawns an expanding, fading dust ring on the board surface (landing / awakening juice).
        /// </summary>
        public void SpawnDustRing(Vector3 worldPos, Color ringColor, float maxRadius = 0.75f, float duration = 0.30f)
        {
            StartCoroutine(DustRingRoutine(worldPos, ringColor, maxRadius, duration));
        }

        /// <summary>
        /// Spawns a snappy expanding emissive flash sphere for cuts and Palace arrivals.
        /// </summary>
        public void SpawnImpactFlash(Vector3 worldPos, Color flashColor, float maxScale = 1.6f, float duration = 0.22f)
        {
            StartCoroutine(ImpactFlashRoutine(worldPos, flashColor, maxScale, duration));
        }

        private IEnumerator DustRingRoutine(Vector3 worldPos, Color color, float maxRadius, float duration)
        {
            const int segments = 20;
            GameObject go = new GameObject("DustRing");
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 0.06f, worldPos.z);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startWidth = 0.09f;
            lr.endWidth = 0.09f;
            lr.sharedMaterial = GetSharedJuiceMaterial();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(0.18f, maxRadius, 1f - (1f - t) * (1f - t));
                float alpha = color.a * (1f - t);

                Color c = new Color(color.r, color.g, color.b, alpha);
                lr.startColor = c;
                lr.endColor = c;
                lr.startWidth = Mathf.Lerp(0.10f, 0.02f, t);
                lr.endWidth = lr.startWidth;

                for (int i = 0; i < segments; i++)
                {
                    float ang = (i / (float)segments) * Mathf.PI * 2f;
                    lr.SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                }
                yield return null;
            }

            Destroy(go);
        }

        private IEnumerator ImpactFlashRoutine(Vector3 worldPos, Color color, float maxScale, float duration)
        {
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ImpactFlash";
            Destroy(flash.GetComponent<Collider>());
            flash.transform.position = worldPos;
            Renderer r = flash.GetComponent<Renderer>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.25f, maxScale, 1f - (1f - t) * (1f - t));
                flash.transform.localScale = Vector3.one * scale;
                ApplyRendererColor(r, new Color(color.r, color.g, color.b, (1f - t) * 0.85f));
                yield return null;
            }

            Destroy(flash);
        }

        private IEnumerator PalaceBeaconRoutine(Vector3 worldPos, Color color)
        {
            GameObject container = new GameObject("PalaceBeacon");
            container.transform.position = worldPos;

            // 1. Rising vertical golden light pillar
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "BeaconColumn";
            Destroy(beam.GetComponent<Collider>());
            beam.transform.parent = container.transform;
            beam.transform.position = worldPos + Vector3.up * 4.5f;
            beam.transform.localScale = new Vector3(0.85f, 4.5f, 0.85f);
            Renderer beamR = beam.GetComponent<Renderer>();
            ApplyRendererColor(beamR, new Color(color.r, color.g, color.b, 0.45f));

            // 2. Point light bloom at palace dais
            GameObject lightGO = new GameObject("PalaceBeaconLight", typeof(Light));
            lightGO.transform.parent = container.transform;
            lightGO.transform.position = worldPos + Vector3.up * 1.5f;
            Light pl = lightGO.GetComponent<Light>();
            pl.type = LightType.Point;
            pl.color = color;
            pl.range = 8.5f;
            pl.intensity = 2.4f;

            // 3. Ground Lotus Flash & Sparks
            SpawnImpactFlash(worldPos + Vector3.up * 0.2f, color, 2.4f, 0.35f);
            SpawnImpactSparks(worldPos + Vector3.up * 0.2f, color, 28);

            // 4. Ascending Lotus Energy Rings
            const int ringCount = 3;
            const int segments = 24;
            GameObject[] rings = new GameObject[ringCount];
            LineRenderer[] ringLRs = new LineRenderer[ringCount];
            for (int r = 0; r < ringCount; r++)
            {
                rings[r] = new GameObject($"LotusRing_{r + 1}");
                rings[r].transform.parent = container.transform;
                rings[r].transform.position = worldPos + Vector3.up * 0.2f;

                LineRenderer lr = rings[r].AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = segments;
                lr.startWidth = 0.12f;
                lr.endWidth = 0.12f;
                lr.sharedMaterial = GetSharedJuiceMaterial();
                ringLRs[r] = lr;
            }

            float duration = 1.35f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Beam scale pulse and alpha fade
                float beamScaleX = Mathf.Lerp(0.95f, 0.15f, t);
                beam.transform.localScale = new Vector3(beamScaleX, 4.5f, beamScaleX);
                ApplyRendererColor(beamR, new Color(color.r, color.g, color.b, (1f - t) * 0.5f));

                if (pl != null)
                {
                    pl.intensity = Mathf.Lerp(2.4f, 0f, t);
                }

                // Animate ascending rings with staggered delay
                for (int r = 0; r < ringCount; r++)
                {
                    float ringDelay = r * 0.18f;
                    if (elapsed >= ringDelay)
                    {
                        float ringT = Mathf.Clamp01((elapsed - ringDelay) / (duration - ringDelay));
                        float ringY = Mathf.Lerp(0.2f, 7.5f, ringT);
                        float radius = Mathf.Lerp(0.7f, 1.8f, ringT);
                        float ringAlpha = (1f - ringT) * 0.85f;

                        Color rc = new Color(color.r, color.g, color.b, ringAlpha);
                        ringLRs[r].startColor = rc;
                        ringLRs[r].endColor = rc;
                        ringLRs[r].startWidth = Mathf.Lerp(0.14f, 0.02f, ringT);
                        ringLRs[r].endWidth = ringLRs[r].startWidth;

                        rings[r].transform.position = worldPos + Vector3.up * ringY;
                        for (int i = 0; i < segments; i++)
                        {
                            float ang = (i / (float)segments) * Mathf.PI * 2f;
                            ringLRs[r].SetPosition(i, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                        }
                    }
                }

                yield return null;
            }

            Destroy(container);
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
                Destroy(spark.GetComponent<Collider>());
                spark.transform.parent = container.transform;
                spark.transform.position = worldPos + Vector3.up * 0.2f;
                spark.transform.localScale = Vector3.one * Random.Range(0.08f, 0.16f);

                ApplyRendererColor(spark.GetComponent<Renderer>(), color);

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
