using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace IsometricGame.UI
{
    /// <summary>
    /// Ensures that the Money HUD Canvas is present in the scene on play.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EnsureCanvasAndMoneyUI : MonoBehaviour
    {
        [SerializeField] private Sprite moneySprite;
        [SerializeField] private Font customFont;

        private void Awake()
        {
            if (FindAnyObjectByType<MoneyUI>() != null) return;

            CreateMoneyHUD();
        }

        public void CreateMoneyHUD()
        {
            // 1. Find or create Canvas
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 2. Ensure EventSystem
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            // 3. Container Card Panel (Top Center)
            GameObject panelObj = new GameObject("Money_HUD_Panel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1.0f);
            panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.anchoredPosition = new Vector2(0, -28f);
            panelRect.sizeDelta = new Vector2(250f, 56f);

            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.32f, 0.42f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            Shadow shadow = panelObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(0f, -3f);

            // 4. Money Icon
            GameObject iconObj = new GameObject("Money_Icon");
            iconObj.transform.SetParent(panelObj.transform, false);

            RectTransform iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(34f, 0);
            iconRect.sizeDelta = new Vector2(40f, 40f);

            Image iconImage = iconObj.AddComponent<Image>();
            if (moneySprite != null)
            {
                iconImage.sprite = moneySprite;
                iconImage.preserveAspect = true;
            }
            else
            {
                iconImage.color = new Color(0.95f, 0.8f, 0.2f);
            }

            // 5. Money Text
            GameObject textObj = new GameObject("Money_Text");
            textObj.transform.SetParent(panelObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(64f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            Text label = textObj.AddComponent<Text>();
            if (customFont != null)
            {
                label.font = customFont;
                if (customFont.material != null && customFont.material.mainTexture != null)
                {
                    customFont.material.mainTexture.filterMode = FilterMode.Point;
                }
            }
            label.fontSize = 26;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(0.35f, 1.0f, 0.65f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "$25,000";

            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.85f);
            textShadow.effectDistance = new Vector2(1f, -1f);

            // 6. MoneyUI Component
            MoneyUI moneyUI = panelObj.AddComponent<MoneyUI>();
            var fields = typeof(MoneyUI).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "moneyIconImage") f.SetValue(moneyUI, iconImage);
                if (f.Name == "moneyText") f.SetValue(moneyUI, label);
                if (f.Name == "containerRect") f.SetValue(moneyUI, panelRect);
            }
        }
    }
}
