using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DayakattamDungeon.UI;

namespace DayakattamDungeon.EditorUtils
{
    public static class TutorialModalBuilder
    {
        public static InteractiveTutorialDialog BuildInteractiveTutorialModal(Transform parent, TMP_FontAsset titleFont, TMP_FontAsset bodyFont, TMP_FontAsset uiDisplayFont)
        {
            Transform existing = parent.Find("LorePanel");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // Modal Root Panel (Dark semi-transparent backdrop & card)
            GameObject panelGO = new GameObject("LorePanel", typeof(RectTransform), typeof(Image), typeof(InteractiveTutorialDialog));
            panelGO.transform.SetParent(parent, false);
            RectTransform rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(980, 620);

            Image bg = panelGO.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.045f, 0.08f, 0.98f);

            // Subtle border outline card
            GameObject cardBorderGO = new GameObject("CardBorder", typeof(RectTransform), typeof(Image));
            cardBorderGO.transform.SetParent(panelGO.transform, false);
            var borderRT = cardBorderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.sizeDelta = Vector2.zero;
            var borderImg = cardBorderGO.GetComponent<Image>();
            borderImg.color = new Color(0.95f, 0.72f, 0.22f, 0.25f);
            borderImg.raycastTarget = false;

            // Header Container
            GameObject headerGO = new GameObject("HeaderContainer", typeof(RectTransform));
            headerGO.transform.SetParent(panelGO.transform, false);
            var headerRT = headerGO.GetComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.anchoredPosition = new Vector2(0, -20);
            headerRT.sizeDelta = new Vector2(-60, 80);

            // Category Badge
            GameObject catGO = new GameObject("CategoryBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            catGO.transform.SetParent(headerGO.transform, false);
            var catRT = catGO.GetComponent<RectTransform>();
            catRT.anchorMin = new Vector2(0, 1);
            catRT.anchorMax = new Vector2(0, 1);
            catRT.pivot = new Vector2(0, 1);
            catRT.anchoredPosition = new Vector2(0, 0);
            catRT.sizeDelta = new Vector2(400, 24);
            var catTxt = catGO.GetComponent<TextMeshProUGUI>();
            catTxt.font = uiDisplayFont != null ? uiDisplayFont : TMP_Settings.defaultFontAsset;
            catTxt.fontSize = 13;
            catTxt.fontStyle = FontStyles.Bold;
            catTxt.color = new Color(1f, 0.78f, 0.25f);
            catTxt.text = "STEP 1: HERITAGE & GOAL";

            // Slide Title
            GameObject titleGO = new GameObject("SlideTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(headerGO.transform, false);
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0, 1);
            titleRT.anchorMax = new Vector2(1, 1);
            titleRT.pivot = new Vector2(0, 1);
            titleRT.anchoredPosition = new Vector2(0, -22);
            titleRT.sizeDelta = new Vector2(-120, 50);
            var titleTxt = titleGO.GetComponent<TextMeshProUGUI>();
            titleTxt.font = titleFont != null ? titleFont : (uiDisplayFont != null ? uiDisplayFont : TMP_Settings.defaultFontAsset);
            titleTxt.fontSize = 32;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = Color.white;
            titleTxt.enableVertexGradient = true;
            titleTxt.colorGradient = new VertexGradient(
                new Color(1f, 0.847f, 0.420f, 1f), // #FFD86B (Temple Gold)
                new Color(1f, 0.847f, 0.420f, 1f),
                new Color(0.620f, 0.357f, 0.086f, 1f), // #9E5B16 (Antique Bronze)
                new Color(0.620f, 0.357f, 0.086f, 1f)
            );
            titleTxt.text = "The Sacred Tamil Dice Duel";

            // Page Indicator
            GameObject pageGO = new GameObject("PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            pageGO.transform.SetParent(headerGO.transform, false);
            var pageRT = pageGO.GetComponent<RectTransform>();
            pageRT.anchorMin = new Vector2(1, 1);
            pageRT.anchorMax = new Vector2(1, 1);
            pageRT.pivot = new Vector2(1, 1);
            pageRT.anchoredPosition = new Vector2(0, -5);
            pageRT.sizeDelta = new Vector2(120, 30);
            var pageTxt = pageGO.GetComponent<TextMeshProUGUI>();
            pageTxt.font = uiDisplayFont != null ? uiDisplayFont : TMP_Settings.defaultFontAsset;
            pageTxt.fontSize = 16;
            pageTxt.alignment = TextAlignmentOptions.Right;
            pageTxt.color = new Color(0.7f, 0.75f, 0.85f);
            pageTxt.text = "1 / 5";

            // Content Split (Left: Image Illustration Card | Right: Instruction Details)
            // Left Image Frame
            GameObject imgFrameGO = new GameObject("IllustrationFrame", typeof(RectTransform), typeof(Image));
            imgFrameGO.transform.SetParent(panelGO.transform, false);
            var imgFrameRT = imgFrameGO.GetComponent<RectTransform>();
            imgFrameRT.anchorMin = new Vector2(0, 0.5f);
            imgFrameRT.anchorMax = new Vector2(0, 0.5f);
            imgFrameRT.pivot = new Vector2(0, 0.5f);
            imgFrameRT.anchoredPosition = new Vector2(30, 20);
            imgFrameRT.sizeDelta = new Vector2(380, 380);
            var imgFrameBg = imgFrameGO.GetComponent<Image>();
            imgFrameBg.color = new Color(0.08f, 0.10f, 0.16f, 1f);

            // Inner Illustration Image
            GameObject illuGO = new GameObject("IllustrationImage", typeof(RectTransform), typeof(Image));
            illuGO.transform.SetParent(imgFrameGO.transform, false);
            var illuRT = illuGO.GetComponent<RectTransform>();
            illuRT.anchorMin = Vector2.zero;
            illuRT.anchorMax = Vector2.one;
            illuRT.sizeDelta = new Vector2(-16, -16);
            var illuImg = illuGO.GetComponent<Image>();
            illuImg.color = new Color(0.12f, 0.15f, 0.22f, 1f);

            // Placeholder Text inside Image
            GameObject phGO = new GameObject("PlaceholderText", typeof(RectTransform), typeof(TextMeshProUGUI));
            phGO.transform.SetParent(illuGO.transform, false);
            var phRT = phGO.GetComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.sizeDelta = Vector2.zero;
            var phTxt = phGO.GetComponent<TextMeshProUGUI>();
            phTxt.font = bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            phTxt.fontSize = 15;
            phTxt.alignment = TextAlignmentOptions.Center;
            phTxt.color = new Color(0.7f, 0.8f, 0.95f);
            phTxt.text = "<b>[ STEP 1 ]</b>\n<size=12><color=#A0B0C8>Image Placeholder\n(Drop your custom illustration sprite here)</color></size>";

            // Right Instructions Container
            GameObject rightGO = new GameObject("InstructionDetails", typeof(RectTransform));
            rightGO.transform.SetParent(panelGO.transform, false);
            var rightRT = rightGO.GetComponent<RectTransform>();
            rightRT.anchorMin = new Vector2(0, 0.5f);
            rightRT.anchorMax = new Vector2(1, 0.5f);
            rightRT.pivot = new Vector2(0, 0.5f);
            rightRT.anchoredPosition = new Vector2(430, 20);
            rightRT.sizeDelta = new Vector2(-460, 380);

            // Description Text
            GameObject descGO = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGO.transform.SetParent(rightGO.transform, false);
            var descRT = descGO.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 1);
            descRT.anchorMax = new Vector2(1, 1);
            descRT.pivot = new Vector2(0, 1);
            descRT.anchoredPosition = new Vector2(0, 0);
            descRT.sizeDelta = new Vector2(0, 130);
            var descTxt = descGO.GetComponent<TextMeshProUGUI>();
            descTxt.font = bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            descTxt.fontSize = 15.5f;
            descTxt.color = new Color(0.92f, 0.93f, 0.96f);
            descTxt.text = "Thayam description...";

            // Bullet Points Box
            GameObject bulletsGO = new GameObject("BulletsText", typeof(RectTransform), typeof(TextMeshProUGUI));
            bulletsGO.transform.SetParent(rightGO.transform, false);
            var bulletsRT = bulletsGO.GetComponent<RectTransform>();
            bulletsRT.anchorMin = new Vector2(0, 1);
            bulletsRT.anchorMax = new Vector2(1, 1);
            bulletsRT.pivot = new Vector2(0, 1);
            bulletsRT.anchoredPosition = new Vector2(0, -135);
            bulletsRT.sizeDelta = new Vector2(0, 160);
            var bulletsTxt = bulletsGO.GetComponent<TextMeshProUGUI>();
            bulletsTxt.font = bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            bulletsTxt.fontSize = 14.5f;
            bulletsTxt.color = new Color(0.85f, 0.88f, 0.94f);
            bulletsTxt.text = "Bullet points...";

            // Pro Tip Box (Highlighted amber)
            GameObject tipBoxGO = new GameObject("TipBox", typeof(RectTransform), typeof(Image));
            tipBoxGO.transform.SetParent(rightGO.transform, false);
            var tipBoxRT = tipBoxGO.GetComponent<RectTransform>();
            tipBoxRT.anchorMin = new Vector2(0, 0);
            tipBoxRT.anchorMax = new Vector2(1, 0);
            tipBoxRT.pivot = new Vector2(0.5f, 0);
            tipBoxRT.anchoredPosition = new Vector2(0, 0);
            tipBoxRT.sizeDelta = new Vector2(0, 68);
            var tipBg = tipBoxGO.GetComponent<Image>();
            tipBg.color = new Color(0.18f, 0.14f, 0.05f, 0.85f);

            GameObject tipTxtGO = new GameObject("TipText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tipTxtGO.transform.SetParent(tipBoxGO.transform, false);
            var tipTxtRT = tipTxtGO.GetComponent<RectTransform>();
            tipTxtRT.anchorMin = Vector2.zero;
            tipTxtRT.anchorMax = Vector2.one;
            tipTxtRT.sizeDelta = new Vector2(-24, -12);
            var tipTxt = tipTxtGO.GetComponent<TextMeshProUGUI>();
            tipTxt.font = bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;
            tipTxt.fontSize = 13.5f;
            tipTxt.color = new Color(1f, 0.9f, 0.65f);
            tipTxt.text = "<color=#FFD700><b>PRO TIP:</b></color> ...";

            // Bottom Navigation Footer
            GameObject footerGO = new GameObject("FooterContainer", typeof(RectTransform));
            footerGO.transform.SetParent(panelGO.transform, false);
            var footerRT = footerGO.GetComponent<RectTransform>();
            footerRT.anchorMin = new Vector2(0, 0);
            footerRT.anchorMax = new Vector2(1, 0);
            footerRT.pivot = new Vector2(0.5f, 0);
            footerRT.anchoredPosition = new Vector2(0, 20);
            footerRT.sizeDelta = new Vector2(-60, 48);

            // Dots Container
            GameObject dotsGO = new GameObject("SlideDots", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            dotsGO.transform.SetParent(footerGO.transform, false);
            var dotsRT = dotsGO.GetComponent<RectTransform>();
            dotsRT.anchorMin = new Vector2(0.5f, 0.5f);
            dotsRT.anchorMax = new Vector2(0.5f, 0.5f);
            dotsRT.pivot = new Vector2(0.5f, 0.5f);
            dotsRT.anchoredPosition = Vector2.zero;
            dotsRT.sizeDelta = new Vector2(180, 24);
            var hlg = dotsGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Prev Button
            Button prevBtn = CreateFooterButton(footerGO.transform, "PrevButton", "< Previous", new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(130, 42), new Color(0.2f, 0.22f, 0.3f), uiDisplayFont);

            // Next Button
            Button nextBtn = CreateFooterButton(footerGO.transform, "NextButton", "Next >", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(130, 42), new Color(0.18f, 0.52f, 0.32f), uiDisplayFont);

            // Close Button
            Button closeBtn = CreateFooterButton(footerGO.transform, "CloseLoreButton", "Close", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(120, 42), new Color(0.6f, 0.22f, 0.22f), uiDisplayFont);

            InteractiveTutorialDialog dialog = panelGO.GetComponent<InteractiveTutorialDialog>();
            dialog.BindUI(
                titleTxt,
                catTxt,
                pageTxt,
                illuImg,
                phTxt,
                descTxt,
                bulletsTxt,
                tipTxt,
                prevBtn,
                nextBtn,
                closeBtn,
                dotsGO.transform
            );

            panelGO.SetActive(false);
            return dialog;
        }

        private static Button CreateFooterButton(Transform parent, string name, string label, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Color bgColor, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = aMin;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txtRT = txtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = font != null ? font : TMP_Settings.defaultFontAsset;
            txt.text = label;
            txt.fontSize = 15;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
            txt.raycastTarget = false;

            return go.GetComponent<Button>();
        }
    }
}
