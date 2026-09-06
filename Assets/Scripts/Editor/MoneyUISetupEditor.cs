#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IsometricGame.UI;

namespace IsometricGame.Editor
{
    public static class MoneyUISetupEditor
    {
        [MenuItem("GameObject/UI/Setup Money HUD (Center)", false, 20)]
        public static void SetupMoneyHUD()
        {
            // 1. Find or create Canvas
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // 2. Ensure EventSystem exists
            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
                Undo.RegisterCreatedObjectUndo(esObj, "Create EventSystem");
            }

            // 3. Find money sprite
            Sprite moneySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/money.png");

            // 4. Find imported font
            Font customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Xanmono-Regular.ttf");
            if (customFont == null)
            {
                customFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Xanmono-Regular.otf");
            }
            if (customFont == null)
            {
                customFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            // 5. Remove existing Money_HUD if present
            Transform existingHUD = canvas.transform.Find("Money_HUD_Panel");
            if (existingHUD != null)
            {
                Undo.DestroyObjectImmediate(existingHUD.gameObject);
            }

            // 6. Create Money HUD Container Panel (Top Middle Center)
            GameObject panelObj = new GameObject("Money_HUD_Panel");
            Undo.RegisterCreatedObjectUndo(panelObj, "Create Money HUD Panel");
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1.0f); // Top Center
            panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.anchoredPosition = new Vector2(0, -28f);
            panelRect.sizeDelta = new Vector2(250f, 56f);

            // Dark glass card background
            Image bgImage = panelObj.AddComponent<Image>();
            bgImage.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

            // Outline / Border
            Outline outline = panelObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.32f, 0.42f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Shadow
            Shadow shadow = panelObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.6f);
            shadow.effectDistance = new Vector2(0f, -3f);

            // 7. Create Money Icon Image
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

            // 8. Create Money Text
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
            label.color = new Color(0.35f, 1.0f, 0.65f); // Crisp mint/emerald currency glow
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = "$25,000";

            // Subtle text shadow
            Shadow textShadow = textObj.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0, 0, 0, 0.75f);
            textShadow.effectDistance = new Vector2(1f, -1f);

            // 9. Attach & configure MoneyUI script
            MoneyUI moneyUI = panelObj.AddComponent<MoneyUI>();

            var fields = typeof(MoneyUI).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "moneyIconImage") f.SetValue(moneyUI, iconImage);
                if (f.Name == "moneyText") f.SetValue(moneyUI, label);
                if (f.Name == "containerRect") f.SetValue(moneyUI, panelRect);
            }

            Selection.activeGameObject = panelObj;
            Debug.Log("<color=green>[Money UI]</color> Successfully configured Money HUD with custom font and money sprite!");
        }
    }
}
#endif
