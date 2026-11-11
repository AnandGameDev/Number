using System.Collections;
using UnityEngine;

namespace DayakattamDungeon.Dice
{
    /// <summary>
    /// Visual and procedural tumbling controller for a single 4-sided Dayakattam brass stick.
    /// Manages engraved face pips (0, 1, 2, 3), 3D tumbling arc, and settling animation.
    /// Supports the authentic 3D modeled Dayam_Dice_Prefab mesh.
    /// </summary>
    public class DayakattamStickVisual : MonoBehaviour
    {
        [Header("Stick Configuration")]
        [SerializeField] private int stickIndex = 0; // 0 for Stick A, 1 for Stick B
        public int StickIndex { get => stickIndex; set => stickIndex = value; }
        [SerializeField] private Vector3 restLocalPosition;
        [SerializeField] private float tumbleDuration = 0.55f;
        [SerializeField] private float tossArcHeight = 1.3f;
        [SerializeField] private bool useModeledMeshPips = true;

        [Header("Pips & Embellishments (Fallback)")]
        [SerializeField] private Transform[] facePipRoots = new Transform[4]; // 0..3

        private Quaternion targetRestRotation;
        private Coroutine activeTumbleRoutine;

        public bool IsTumbling { get; private set; }

        private void Awake()
        {
            if (restLocalPosition == Vector3.zero)
            {
                restLocalPosition = transform.localPosition;
            }

            if (!useModeledMeshPips)
            {
                CreateEngravedFacePips();
            }
            else
            {
                // Initialize to face 1 (Thayam pip) facing up
                SetFaceImmediate(1);
            }
        }

        public void SetRestPosition(Vector3 pos)
        {
            restLocalPosition = pos;
            transform.localPosition = pos;
        }

        public void SetFaceImmediate(int targetFace)
        {
            float targetZAngle = GetTargetZAngle(targetFace);
            targetRestRotation = Quaternion.Euler(0f, 0f, targetZAngle);
            transform.localRotation = targetRestRotation;
        }

        /// <summary>
        /// Calculates the settling Z rotation for each face.
        /// When useModeledMeshPips is true (Dayam_Dice_Prefab):
        /// - Face 0: 0 pips (Blank face) -> 270°
        /// - Face 1: 1 pip (Center dot)  -> 0°
        /// - Face 2: 2 pips              -> 90°
        /// - Face 3: 3 pips              -> 180°
        /// </summary>
        public float GetTargetZAngle(int faceValue)
        {
            if (useModeledMeshPips)
            {
                switch (faceValue)
                {
                    case 0: return 270f;
                    case 1: return 0f;
                    case 2: return 90f;
                    case 3: return 180f;
                    default: return 0f;
                }
            }
            return faceValue * 90f;
        }

        /// <summary>
        /// Executes a 3D procedural toss and tumble animation, settling on the rolled face (0..3).
        /// </summary>
        public void TumbleToFace(int faceValue, System.Action onComplete = null)
        {
            if (activeTumbleRoutine != null) StopCoroutine(activeTumbleRoutine);
            activeTumbleRoutine = StartCoroutine(TumbleRoutine(faceValue, onComplete));
        }

        private IEnumerator TumbleRoutine(int targetFace, System.Action onComplete)
        {
            IsTumbling = true;
            Vector3 startPos = transform.localPosition;
            Quaternion startRot = transform.localRotation;

            // Target rotation for face
            float targetZAngle = GetTargetZAngle(targetFace);
            targetRestRotation = Quaternion.Euler(0f, Random.Range(-4f, 4f), targetZAngle);

            // Additional random spin revolutions during the toss
            float spinX = Random.Range(2, 4) * 360f;
            float spinZ = Random.Range(3, 5) * 360f;

            float elapsed = 0f;

            while (elapsed < tumbleDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tumbleDuration);

                // Parabolic vertical toss
                float arc = 4f * t * (1f - t) * tossArcHeight;
                Vector3 currentPos = Vector3.Lerp(startPos, restLocalPosition, t);
                currentPos.y += arc;
                transform.localPosition = currentPos;

                // Rotational tumble
                float rotProgress = Mathf.SmoothStep(0f, 1f, t);
                Quaternion tumbleSpin = Quaternion.Euler(
                    Mathf.Lerp(0f, spinX, rotProgress),
                    0f,
                    Mathf.Lerp(0f, spinZ + targetZAngle, rotProgress)
                );
                transform.localRotation = Quaternion.Slerp(startRot, tumbleSpin, rotProgress);

                yield return null;
            }

            // Settle with a tiny bounce
            transform.localPosition = restLocalPosition;
            transform.localRotation = targetRestRotation;

            // Small bounce
            float bounceTime = 0.12f;
            float bElapsed = 0f;
            while (bElapsed < bounceTime)
            {
                bElapsed += Time.deltaTime;
                float bt = Mathf.Sin((bElapsed / bounceTime) * Mathf.PI);
                transform.localPosition = restLocalPosition + Vector3.up * (bt * 0.15f);
                yield return null;
            }
            transform.localPosition = restLocalPosition;

            IsTumbling = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Procedurally generates engraved dark dot pips on the 4 faces of the stick (fallback only).
        /// </summary>
        private void CreateEngravedFacePips()
        {
            // If already created, return
            if (transform.Find("Pips_Face_1") != null) return;

            // Face 1: 1 center pip (+Y face)
            CreatePip("Pips_Face_1", new Vector3(0, 0.26f, 0), 1, Vector3.up);

            // Face 2: 2 pips (+X face)
            CreatePip("Pips_Face_2", new Vector3(0.26f, 0, 0), 2, Vector3.right);

            // Face 3: 3 pips (-Y face)
            CreatePip("Pips_Face_3", new Vector3(0, -0.26f, 0), 3, -Vector3.up);

            // Face 0: Auspicious Cross / X (-X face)
            CreateCrossPip("Pips_Face_0", new Vector3(-0.26f, 0, 0), -Vector3.right);
        }

        private void CreatePip(string parentName, Vector3 rootPos, int dotCount, Vector3 normal)
        {
            GameObject root = new GameObject(parentName);
            root.transform.parent = transform;
            root.transform.localPosition = rootPos;
            root.transform.localRotation = Quaternion.LookRotation(normal);

            float spacing = 0.5f;
            float startOffset = -(dotCount - 1) * spacing * 0.5f;

            for (int i = 0; i < dotCount; i++)
            {
                GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pip.name = $"Dot_{i + 1}";
                pip.transform.parent = root.transform;
                pip.transform.localPosition = new Vector3(0, startOffset + i * spacing, 0);
                pip.transform.localScale = new Vector3(0.12f, 0.02f, 0.12f);
                pip.GetComponent<Renderer>().material.color = new Color(0.12f, 0.12f, 0.15f);
            }
        }

        private void CreateCrossPip(string parentName, Vector3 rootPos, Vector3 normal)
        {
            GameObject root = new GameObject(parentName);
            root.transform.parent = transform;
            root.transform.localPosition = rootPos;
            root.transform.localRotation = Quaternion.LookRotation(normal);

            // Small crossed bars for Face 0
            GameObject bar1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar1.transform.parent = root.transform;
            bar1.transform.localPosition = Vector3.zero;
            bar1.transform.localScale = new Vector3(0.08f, 0.45f, 0.02f);
            bar1.transform.localRotation = Quaternion.Euler(0, 0, 45f);
            bar1.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.2f);

            GameObject bar2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar2.transform.parent = root.transform;
            bar2.transform.localPosition = Vector3.zero;
            bar2.transform.localScale = new Vector3(0.08f, 0.45f, 0.02f);
            bar2.transform.localRotation = Quaternion.Euler(0, 0, -45f);
            bar2.GetComponent<Renderer>().material.color = new Color(0.15f, 0.15f, 0.2f);
        }
    }
}
