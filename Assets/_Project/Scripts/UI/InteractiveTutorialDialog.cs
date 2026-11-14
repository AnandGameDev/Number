using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DayakattamDungeon.UI
{
    [Serializable]
    public class TutorialSlideData
    {
        public string title;
        public string category;
        public Sprite image;
        [TextArea(4, 10)] public string description;
        public string bulletPoints;
        public string tip;
    }

    /// <summary>
    /// Interactive visual tutorial onboarding modal that replaces the static lore text.
    /// Provides multi-step paginated slides with image illustration placeholders,
    /// previous/next navigation, step indicators, and clean category badges.
    /// </summary>
    public class InteractiveTutorialDialog : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI categoryBadgeText;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;
        [SerializeField] private Image illustrationImage;
        [SerializeField] private TextMeshProUGUI imagePlaceholderText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI bulletsText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform dotsContainer;
        [SerializeField] private GameObject dotPrefab;

        [Header("Slide Content")]
        [SerializeField] private List<TutorialSlideData> slides = new List<TutorialSlideData>();

        private int currentSlideIndex = 0;
        private List<Image> dotImages = new List<Image>();

        private static readonly Color DotActiveColor = new Color(1f, 0.78f, 0.2f, 1f);
        private static readonly Color DotInactiveColor = new Color(0.35f, 0.38f, 0.45f, 0.6f);

        private void Awake()
        {
            if (slides == null || slides.Count == 0)
            {
                PopulateDefaultSlides();
            }

            if (prevButton != null) prevButton.onClick.AddListener(OnPrevClicked);
            if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void Start()
        {
            BuildDots();
            ShowSlide(0);
        }

        private void OnEnable()
        {
            ShowSlide(currentSlideIndex);
        }

        public void BindUI(
            TextMeshProUGUI title,
            TextMeshProUGUI category,
            TextMeshProUGUI pageIndicator,
            Image illustration,
            TextMeshProUGUI imagePlaceholder,
            TextMeshProUGUI description,
            TextMeshProUGUI bullets,
            TextMeshProUGUI tip,
            Button prev,
            Button next,
            Button close,
            Transform dots)
        {
            titleText = title;
            categoryBadgeText = category;
            pageIndicatorText = pageIndicator;
            illustrationImage = illustration;
            imagePlaceholderText = imagePlaceholder;
            descriptionText = description;
            bulletsText = bullets;
            tipText = tip;
            prevButton = prev;
            nextButton = next;
            closeButton = close;
            dotsContainer = dots;

            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(OnPrevClicked);
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(OnNextClicked);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }

            if (slides == null || slides.Count == 0)
            {
                PopulateDefaultSlides();
            }

            BuildDots();
            ShowSlide(0);
        }

        public void PopulateDefaultSlides()
        {
            slides = new List<TutorialSlideData>
            {
                new TutorialSlideData
                {
                    title = "The Sacred Tamil Dice Duel",
                    category = "STEP 1: HERITAGE & GOAL",
                    description = "Thayam is an ancient tactical board game played across Tamil Nadu for millennia during lucky festive gatherings.\n\nFour players sit at the corners of a sacred 7x7 board. Your goal is to guide all 3 Kalasam spirit pawns clockwise around the outer loop, spiral through the inner rings, and ascend into the Central Sacred Palace.",
                    bulletPoints = "• <b>Objective:</b> First player with all 3 pawns in the Palace wins.\n• <b>Match Size:</b> 2-Player (You vs Grandma) or 4-Player (You, Mama, Grandma, Dada).\n• <b>Spiritual Theme:</b> Awaken spirit pawns from your altar into the sacred mandala.",
                    tip = "You must enter through your own colored threshold (South Red) before spiraling inward!"
                },
                new TutorialSlideData
                {
                    title = "Rolling The Brass Thayam",
                    category = "STEP 2: DICE & LUCKY NUMBERS",
                    description = "Thayam is played with two oblong brass stick dice with numbered long faces: 0, 1, 2, and 3.\n\nThe dice sum gives your movement value (1 to 6). But if both sticks land on blank (0 + 0), that roll triggers the fabled <b>12 (Pannirendu)!</b>",
                    bulletPoints = "• <b>Lucky Rolls (1, 5, 6, 12):</b> Grants an immediate <b>+1 Extra Roll</b>!\n• <b>Combo Multiplier:</b> Chaining lucky rolls powers up your combo (1x → 1.5x → 2x → 3x).\n• <b>Ending Rolls (2, 3, 4):</b> Standard rolls end your rolling turn and begin the Action Phase.",
                    tip = "Bank multiple rolls during a lucky streak, then spend them strategically across your pawns!"
                },
                new TutorialSlideData
                {
                    title = "Awakening & Board Movement",
                    category = "STEP 3: THAYAM & ACTION PHASE",
                    description = "Your pawns begin resting in your team's sanctuary tray. Before a pawn can step foot onto the board, it must be awakened by the sacred number <b>1 (Thayam)</b>.",
                    bulletPoints = "• <b>Awakening:</b> Spend a banked roll of <b>1</b> to place a pawn onto your colored Entry tile.\n• <b>Movement:</b> Click a banked roll, then click any active pawn (or drag it) to move clockwise along its spiral route.\n• <b>Flexible Spending:</b> You can spend different banked rolls on different pawns in any order.",
                    tip = "Rolling Thayam (1) gives an extra roll AND lets you enter a new pawn simultaneously!"
                },
                new TutorialSlideData
                {
                    title = "Sanctuary Tiles & Combat Cuts",
                    category = "STEP 4: TACTICS & CUTTING",
                    description = "The board has 8 crossed <b>Sanctuary</b> tiles (the 4 entry midpoints + 4 inner corners). These tiles grant divine immunity from opponent strikes.",
                    bulletPoints = "• <b>Sanctuary Immunity:</b> Pawns on crossed tiles can co-exist safely without being captured.\n• <b>Striking / Cutting:</b> Landing exactly on an opponent pawn on an uncrossed tile cuts them back to their reserve!\n• <b>Cutting Bonus:</b> Successfully cutting an opponent pawn awards a massive Lucky Multiplier boost.",
                    tip = "Always prioritize parking vulnerable pawns on crossed Sanctuary tiles when ending your turn!"
                },
                new TutorialSlideData
                {
                    title = "Ascension Into The Palace",
                    category = "STEP 5: VICTORY & ADVANCEMENT",
                    description = "After completing the outer perimeter loop, your pawns turn inward into the concentric middle and inner rings, spiraling toward the Central Palace.",
                    bulletPoints = "• <b>Inner Spiral:</b> Follow your illuminated trail to navigate inward.\n• <b>Palace Goal:</b> Reaching the center requires an exact or valid entrance count.\n• <b>Victory & Unlocks:</b> Defeat Grandma in 2-Player mode to unlock the chaos of 4-Player Mode with Mama and Dada!",
                    tip = "Click '? Rules & Lore' at any time to reopen this guide, or press 'Interactive Tutorial' on the Title Screen!"
                }
            };
        }

        public void ShowSlide(int index)
        {
            if (slides == null || slides.Count == 0) return;

            currentSlideIndex = Mathf.Clamp(index, 0, slides.Count - 1);
            TutorialSlideData slide = slides[currentSlideIndex];

            if (titleText != null) titleText.text = slide.title;
            if (categoryBadgeText != null) categoryBadgeText.text = slide.category;
            if (pageIndicatorText != null) pageIndicatorText.text = $"{currentSlideIndex + 1} / {slides.Count}";

            if (illustrationImage != null)
            {
                if (slide.image != null)
                {
                    illustrationImage.sprite = slide.image;
                    illustrationImage.color = Color.white;
                    if (imagePlaceholderText != null) imagePlaceholderText.gameObject.SetActive(false);
                }
                else
                {
                    illustrationImage.sprite = null;
                    illustrationImage.color = new Color(0.12f, 0.15f, 0.22f, 1f);
                    if (imagePlaceholderText != null)
                    {
                        imagePlaceholderText.gameObject.SetActive(true);
                        imagePlaceholderText.text = $"<b>[ {slide.category} ]</b>\n<size=13><color=#A0B0C8>Image Placeholder\n(Drop your custom illustration sprite here)</color></size>";
                    }
                }
            }

            if (descriptionText != null) descriptionText.text = slide.description;
            if (bulletsText != null) bulletsText.text = slide.bulletPoints;
            if (tipText != null) tipText.text = $"<color=#FFD700><b>TIP:</b></color> {slide.tip}";

            if (prevButton != null) prevButton.interactable = currentSlideIndex > 0;
            if (nextButton != null)
            {
                var nextLabel = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (nextLabel != null)
                {
                    nextLabel.text = (currentSlideIndex == slides.Count - 1) ? "Got It!" : "Next >";
                }
            }

            UpdateDots();
        }

        private void BuildDots()
        {
            if (dotsContainer == null) return;

            for (int i = dotsContainer.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(dotsContainer.GetChild(i).gameObject);
            }
            dotImages.Clear();

            for (int i = 0; i < slides.Count; i++)
            {
                GameObject dotGO = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
                dotGO.transform.SetParent(dotsContainer, false);
                var rt = dotGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(10, 10);
                var img = dotGO.GetComponent<Image>();
                img.color = DotInactiveColor;
                dotImages.Add(img);

                int targetIndex = i;
                var btn = dotGO.AddComponent<Button>();
                btn.onClick.AddListener(() => ShowSlide(targetIndex));
            }
        }

        private void UpdateDots()
        {
            for (int i = 0; i < dotImages.Count; i++)
            {
                if (dotImages[i] != null)
                {
                    bool active = (i == currentSlideIndex);
                    dotImages[i].color = active ? DotActiveColor : DotInactiveColor;
                    dotImages[i].rectTransform.sizeDelta = active ? new Vector2(24, 10) : new Vector2(10, 10);
                }
            }
        }

        public void OnNextClicked()
        {
            if (currentSlideIndex < slides.Count - 1)
            {
                ShowSlide(currentSlideIndex + 1);
            }
            else
            {
                Close();
            }
        }

        public void OnPrevClicked()
        {
            if (currentSlideIndex > 0)
            {
                ShowSlide(currentSlideIndex - 1);
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            ShowSlide(0);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Allows assigning custom illustration sprites at runtime or in editor.
        /// </summary>
        public void SetSlideImage(int slideIndex, Sprite sprite)
        {
            if (slideIndex >= 0 && slideIndex < slides.Count)
            {
                slides[slideIndex].image = sprite;
                if (slideIndex == currentSlideIndex)
                {
                    ShowSlide(currentSlideIndex);
                }
            }
        }
    }
}
