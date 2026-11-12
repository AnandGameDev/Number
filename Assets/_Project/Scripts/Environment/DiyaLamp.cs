using UnityEngine;

namespace DayakattamDungeon.Environment
{
    /// <summary>
    /// Traditional South Indian Brass Oil Lamp (Kuthuvilakku / Diya).
    /// Provides organic flame flicker and warm candlelight illumination.
    /// Lightweight and fully optimized for WebGL.
    /// </summary>
    public class DiyaLamp : MonoBehaviour
    {
        [Header("Flame Visual")]
        [SerializeField] private Transform flameVisual;
        [SerializeField] private Light flameLight;

        [Header("Flicker Settings")]
        [SerializeField] private float baseIntensity = 1.25f;
        [SerializeField] private float baseRange = 5.2f;
        [SerializeField] private Color flameColor = new Color(1.0f, 0.72f, 0.35f);

        private float seed;
        private Vector3 baseScale = Vector3.one;
        private Vector3 baseLocalPos = Vector3.zero;

        private void Awake()
        {
            seed = Random.Range(10f, 500f);

            if (flameVisual != null)
            {
                baseScale = flameVisual.localScale;
                baseLocalPos = flameVisual.localPosition;
            }

            if (flameLight != null)
            {
                flameLight.color = flameColor;
                flameLight.range = baseRange;
                flameLight.intensity = baseIntensity;
            }
        }

        public void Initialize(Transform flame, Light lightComponent, float intensity, float range)
        {
            flameVisual = flame;
            flameLight = lightComponent;
            baseIntensity = intensity;
            baseRange = range;

            if (flameVisual != null)
            {
                baseScale = flameVisual.localScale;
                baseLocalPos = flameVisual.localPosition;
            }

            if (flameLight != null)
            {
                flameLight.color = flameColor;
                flameLight.range = baseRange;
                flameLight.intensity = baseIntensity;
            }
        }

        private void Update()
        {
            float t = Time.time;
            float n1 = Mathf.PerlinNoise(t * 6.5f, seed);
            float n2 = Mathf.PerlinNoise(seed, t * 8.2f);

            if (flameLight != null)
            {
                flameLight.intensity = baseIntensity * (0.82f + 0.36f * n1);
            }

            if (flameVisual != null)
            {
                float scaleY = baseScale.y * (0.90f + 0.22f * n1);
                float scaleXZ = baseScale.x * (1.05f - 0.12f * n1);
                flameVisual.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                flameVisual.localPosition = baseLocalPos + new Vector3((n1 - 0.5f) * 0.015f, (n2 - 0.5f) * 0.025f, (n2 - 0.5f) * 0.015f);
            }
        }
    }
}
