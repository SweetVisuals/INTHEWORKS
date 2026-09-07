using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace IsometricGame.UI
{
    /// <summary>
    /// Ensures that the GUI Canvas, Money HUD, Hotbar HUD, Energy Bar HUD,
    /// World Interaction Popup, and Sleep Transition UI are always present in the scene.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public class EnsureCanvasAndMoneyUI : MonoBehaviour
    {
        private static EnsureCanvasAndMoneyUI instance;

#pragma warning disable CS0649
        [SerializeField] private Sprite moneyCardSprite;
        [SerializeField] private Sprite moneySprite;
        [SerializeField] private Texture2D numbersTexture;
        [SerializeField] private Sprite hotbarSlotSprite;
        [SerializeField] private Sprite hotbarSelectedSprite;
        [SerializeField] private Sprite energyBarEmptySprite;
        [SerializeField] private Sprite energyBarFillSprite;
        [SerializeField] private Sprite xpBarEmptySprite;
        [SerializeField] private Sprite xpBarFillSprite;
        [SerializeField] private Sprite clockAmSprite;
        [SerializeField] private Sprite clockPmSprite;
        [SerializeField] private Sprite[] daySprites;
        [SerializeField] private Sprite popupBackgroundSprite;
        [SerializeField] private Sprite popupHoverOutlineSprite;
        [SerializeField] private Sprite sleepTextSprite;
        [SerializeField] private Sprite openTextSprite;
        [SerializeField] private Sprite[] sleepingFrames;
        [SerializeField] private Sprite[] warpFrames;
#pragma warning restore CS0649

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitAfter()
        {
            EnsureAllUI();
        }

        private void Awake()
        {
            instance = this;
            EnsureAllUI();
        }

        private void Start()
        {
            EnsureAllUI();
        }

        private void OnEnable()
        {
            instance = this;
            EnsureAllUI();
        }

        public static void EnsureAllUI()
        {
            Canvas canvas = EnsureCanvasAndEventSystem();
            EnsureCanvasAndMoneyUI helper = Object.FindAnyObjectByType<EnsureCanvasAndMoneyUI>();
            if (helper == null)
            {
                helper = canvas.gameObject.GetComponent<EnsureCanvasAndMoneyUI>() ?? canvas.gameObject.AddComponent<EnsureCanvasAndMoneyUI>();
            }

            helper.EnsureSpritesLoaded();

            if (Object.FindAnyObjectByType<MoneyUI>() == null)
            {
                helper.CreateMoneyHUD(canvas);
            }

            if (Object.FindAnyObjectByType<HotbarUI>() == null)
            {
                helper.CreateHotbarHUD(canvas);
            }

            ClockUI clockUI = Object.FindAnyObjectByType<ClockUI>();
            if (clockUI == null)
            {
                helper.CreateClockHUD(canvas);
            }
            else
            {
                if (helper.clockAmSprite != null) clockUI.ClockAmSprite = helper.clockAmSprite;
                if (helper.clockPmSprite != null) clockUI.ClockPmSprite = helper.clockPmSprite;
                if (helper.daySprites != null) clockUI.DaySprites = helper.daySprites;
                if (helper.numbersTexture != null) clockUI.NumbersTexture = helper.numbersTexture;
                clockUI.SetLayout(new Vector2(-24f, -22f), new Vector2(244f, 56f));
                clockUI.InitializeComponents();
            }

            XpBarUI xpUI = Object.FindAnyObjectByType<XpBarUI>();
            if (xpUI == null)
            {
                helper.CreateXpBarHUD(canvas);
            }
            else
            {
                if (helper.xpBarEmptySprite != null) xpUI.XpBarEmptySprite = helper.xpBarEmptySprite;
                if (helper.xpBarFillSprite != null) xpUI.XpBarFillSprite = helper.xpBarFillSprite;
                xpUI.SetLayout(new Vector2(-24f, -82f), new Vector2(244f, 28f));
                xpUI.InitializeComponents();
            }

            EnergyBarUI energyUI = Object.FindAnyObjectByType<EnergyBarUI>();
            if (energyUI == null)
            {
                helper.CreateEnergyBarHUD(canvas);
            }
            else
            {
                if (helper.moneyCardSprite != null) energyUI.CardBackgroundSprite = helper.moneyCardSprite;
                if (helper.energyBarEmptySprite != null) energyUI.BarEmptySprite = helper.energyBarEmptySprite;
                if (helper.energyBarFillSprite != null) energyUI.BarFillSprite = helper.energyBarFillSprite;
                energyUI.SetLayout(new Vector2(-24f, -114f), new Vector2(244f, 28f));
                energyUI.InitializeComponents();
            }

            WorldInteractionPopup popup = Object.FindAnyObjectByType<WorldInteractionPopup>();
            if (popup == null)
            {
                helper.CreateInteractionPopupUI(canvas);
            }
            else
            {
                popup.InitializeComponents();
            }

            if (Object.FindAnyObjectByType<SleepTransitionUI>() == null)
            {
                helper.CreateSleepTransitionUI(canvas);
            }

            if (Object.FindAnyObjectByType<ChestInventoryUI>() == null)
            {
                helper.CreateChestInventoryUI(canvas);
            }

            if (Object.FindAnyObjectByType<JobsBoardUI>() == null)
            {
                helper.CreateJobsBoardUI(canvas);
            }

            if (Object.FindAnyObjectByType<QuickWarpUI>() == null)
            {
                helper.CreateQuickWarpHUD(canvas);
            }

            CustomGameCursor.EnsureCursorActive();
        }

        public static Canvas EnsureCanvasAndEventSystem()
        {
            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                var module = esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                module.AssignDefaultActions();
#else
                esObj.AddComponent<StandaloneInputModule>();
#endif
            }

            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null && cam.GetComponent<Physics2DRaycaster>() == null)
            {
                cam.gameObject.AddComponent<Physics2DRaycaster>();
            }

            return canvas;
        }

        public void CreateMoneyHUD(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            // 1. Container Card Panel (Top Center)
            GameObject panelObj = new GameObject("Money_HUD_Panel", typeof(RectTransform), typeof(Image), typeof(Shadow));
            panelObj.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1.0f);
            panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.anchoredPosition = new Vector2(0, -22f);
            panelRect.sizeDelta = new Vector2(230f, 52f);

            Image bgImage = panelObj.GetComponent<Image>();
            if (moneyCardSprite != null)
            {
                bgImage.sprite = moneyCardSprite;
                bgImage.type = Image.Type.Sliced;
                bgImage.color = Color.white;
            }
            else
            {
                bgImage.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            }

            Shadow shadow = panelObj.GetComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.5f);
            shadow.effectDistance = new Vector2(0f, -3f);

            // 2. Centered Content Container
            GameObject contentRowObj = new GameObject("Content_Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            contentRowObj.transform.SetParent(panelObj.transform, false);

            RectTransform rowRect = contentRowObj.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = Vector2.zero;

            HorizontalLayoutGroup layout = contentRowObj.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = contentRowObj.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 3. Money Icon
            GameObject iconObj = new GameObject("Money_Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconObj.transform.SetParent(contentRowObj.transform, false);

            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(30f, 30f);

            LayoutElement iconLe = iconObj.GetComponent<LayoutElement>();
            iconLe.minWidth = 30f;
            iconLe.preferredWidth = 30f;
            iconLe.minHeight = 30f;
            iconLe.preferredHeight = 30f;

            Image iconImage = iconObj.GetComponent<Image>();
            iconImage.raycastTarget = false;
            if (moneySprite != null)
            {
                iconImage.sprite = moneySprite;
                iconImage.preserveAspect = true;
            }
            else
            {
                iconImage.color = new Color(0.35f, 0.85f, 0.45f);
            }

            // 4. Pixel Number Display
            GameObject digitsObj = new GameObject("Money_Digits_Container", typeof(RectTransform));
            digitsObj.transform.SetParent(contentRowObj.transform, false);

            RectTransform digitsRect = digitsObj.GetComponent<RectTransform>();
            digitsRect.sizeDelta = new Vector2(80f, 20f);

            PixelNumberDisplay numDisplay = digitsObj.AddComponent<PixelNumberDisplay>();
            var texField = typeof(PixelNumberDisplay).GetField("numbersTexture", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (texField != null && numbersTexture != null)
            {
                texField.SetValue(numDisplay, numbersTexture);
            }
            numDisplay.Initialize();

            // 5. MoneyUI Component
            MoneyUI moneyUI = panelObj.AddComponent<MoneyUI>();
            var fields = typeof(MoneyUI).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "moneyIconImage") f.SetValue(moneyUI, iconImage);
                if (f.Name == "pixelNumberDisplay") f.SetValue(moneyUI, numDisplay);
                if (f.Name == "containerRect") f.SetValue(moneyUI, panelRect);
            }

            moneyUI.SetMoney(25000);
        }

        public void CreateHotbarHUD(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            // 1. Hotbar Root Container (Bottom Center)
            GameObject hotbarObj = new GameObject("Hotbar_Panel", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            hotbarObj.transform.SetParent(canvas.transform, false);

            RectTransform hotbarRect = hotbarObj.GetComponent<RectTransform>();
            hotbarRect.anchorMin = new Vector2(0.5f, 0f);
            hotbarRect.anchorMax = new Vector2(0.5f, 0f);
            hotbarRect.pivot = new Vector2(0.5f, 0f);
            hotbarRect.anchoredPosition = new Vector2(0, 20f);
            hotbarRect.sizeDelta = new Vector2(236f, 56f);

            HorizontalLayoutGroup layout = hotbarObj.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image[] slotImages = new Image[4];
            RectTransform[] slotRects = new RectTransform[4];
            Image[] itemIcons = new Image[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject slotObj = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                slotObj.transform.SetParent(hotbarObj.transform, false);

                RectTransform slotRt = slotObj.GetComponent<RectTransform>();
                slotRt.sizeDelta = new Vector2(50f, 50f);
                slotRects[i] = slotRt;

                Image slotBg = slotObj.GetComponent<Image>();
                slotBg.sprite = (i == 0 && hotbarSelectedSprite != null) ? hotbarSelectedSprite : hotbarSlotSprite;
                slotBg.type = Image.Type.Simple;
                slotImages[i] = slotBg;

                Button slotBtn = slotObj.GetComponent<Button>();
                slotBtn.targetGraphic = slotBg;

                // Child icon for item
                GameObject iconObj = new GameObject("Item_Icon", typeof(RectTransform), typeof(Image));
                iconObj.transform.SetParent(slotObj.transform, false);

                RectTransform iconRt = iconObj.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(6f, 6f);
                iconRt.offsetMax = new Vector2(-6f, -6f);

                Image itemImg = iconObj.GetComponent<Image>();
                itemImg.raycastTarget = false;
                itemImg.preserveAspect = true;
                itemImg.gameObject.SetActive(false);
                itemIcons[i] = itemImg;
            }

            HotbarUI hotbarUI = hotbarObj.AddComponent<HotbarUI>();
            var fields = typeof(HotbarUI).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "slotDefaultSprite") f.SetValue(hotbarUI, hotbarSlotSprite);
                if (f.Name == "slotSelectedSprite") f.SetValue(hotbarUI, hotbarSelectedSprite);
                if (f.Name == "slotBackgrounds") f.SetValue(hotbarUI, slotImages);
                if (f.Name == "slotRects") f.SetValue(hotbarUI, slotRects);
                if (f.Name == "itemIcons") f.SetValue(hotbarUI, itemIcons);
            }

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                slotImages[i].GetComponent<Button>().onClick.AddListener(() => hotbarUI.SelectSlot(idx));
            }

            hotbarUI.SelectSlot(0, false);
        }

        public void CreateClockHUD(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            Transform existing = canvas.transform.Find("Clock_HUD");
            GameObject clockObj = existing != null ? existing.gameObject : new GameObject("Clock_HUD", typeof(RectTransform));
            if (existing == null) clockObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = clockObj.GetComponent<RectTransform>() ?? clockObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -22f);
            rt.sizeDelta = new Vector2(244f, 56f);

            ClockUI clockUI = clockObj.GetComponent<ClockUI>() ?? clockObj.AddComponent<ClockUI>();
            if (clockAmSprite != null) clockUI.ClockAmSprite = clockAmSprite;
            if (clockPmSprite != null) clockUI.ClockPmSprite = clockPmSprite;
            if (daySprites != null) clockUI.DaySprites = daySprites;
            if (numbersTexture != null) clockUI.NumbersTexture = numbersTexture;
            clockUI.InitializeComponents();
        }

        public void CreateXpBarHUD(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            Transform existing = canvas.transform.Find("XP_Bar_HUD");
            GameObject barObj = existing != null ? existing.gameObject : new GameObject("XP_Bar_HUD", typeof(RectTransform));
            if (existing == null) barObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = barObj.GetComponent<RectTransform>() ?? barObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -82f);
            rt.sizeDelta = new Vector2(244f, 28f);

            XpBarUI barUI = barObj.GetComponent<XpBarUI>() ?? barObj.AddComponent<XpBarUI>();
            if (xpBarEmptySprite != null) barUI.XpBarEmptySprite = xpBarEmptySprite;
            if (xpBarFillSprite != null) barUI.XpBarFillSprite = xpBarFillSprite;
            barUI.InitializeComponents();
        }

        public void CreateEnergyBarHUD(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            Transform existing = canvas.transform.Find("Energy_Bar_HUD");
            GameObject barObj = existing != null ? existing.gameObject : new GameObject("Energy_Bar_HUD", typeof(RectTransform));
            if (existing == null) barObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = barObj.GetComponent<RectTransform>() ?? barObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -114f);
            rt.sizeDelta = new Vector2(244f, 28f);

            EnergyBarUI barUI = barObj.GetComponent<EnergyBarUI>() ?? barObj.AddComponent<EnergyBarUI>();
            if (energyBarEmptySprite != null) barUI.BarEmptySprite = energyBarEmptySprite;
            if (energyBarFillSprite != null) barUI.BarFillSprite = energyBarFillSprite;
            barUI.InitializeComponents();
        }

        public void CreateInteractionPopupUI(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            GameObject popupObj = new GameObject("Interaction_Popup", typeof(RectTransform), typeof(CanvasGroup));
            popupObj.transform.SetParent(canvas.transform, false);

            CanvasGroup cg = popupObj.GetComponent<CanvasGroup>();
            WorldInteractionPopup popup = popupObj.AddComponent<WorldInteractionPopup>();
            var fields = typeof(WorldInteractionPopup).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name == "buttonBackgroundSprite" && popupBackgroundSprite != null) f.SetValue(popup, popupBackgroundSprite);
                if (f.Name == "buttonHoverOutlineSprite" && popupHoverOutlineSprite != null) f.SetValue(popup, popupHoverOutlineSprite);
            }
            popup.InitializeComponents();
        }

        public void CreateSleepTransitionUI(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            GameObject sleepObj = new GameObject("Sleep_Transition_UI", typeof(RectTransform), typeof(CanvasGroup));
            sleepObj.transform.SetParent(canvas.transform, false);

            CanvasGroup cg = sleepObj.GetComponent<CanvasGroup>();
            SleepTransitionUI sleepUI = sleepObj.AddComponent<SleepTransitionUI>();
            if (sleepingFrames != null && sleepingFrames.Length > 0)
            {
                sleepUI.sleepingFrames = sleepingFrames;
            }
            sleepUI.InitializeComponents();
        }

        public void CreateChestInventoryUI(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            GameObject chestObj = new GameObject("Chest_Inventory_UI", typeof(RectTransform), typeof(CanvasGroup));
            chestObj.transform.SetParent(canvas.transform, false);

            ChestInventoryUI chestUI = chestObj.AddComponent<ChestInventoryUI>();
            chestUI.InitializeComponents();
        }

        public void CreateJobsBoardUI(Canvas canvas)
        {
            if (canvas == null) canvas = EnsureCanvasAndEventSystem();

            EnsureSpritesLoaded();

            GameObject jobsObj = new GameObject("Jobs_Board_UI", typeof(RectTransform), typeof(CanvasGroup));
            jobsObj.transform.SetParent(canvas.transform, false);

            JobsBoardUI jobsUI = jobsObj.AddComponent<JobsBoardUI>();
            jobsUI.InitializeComponents();
        }

        private void CreateQuickWarpHUD(Canvas canvas)
        {
            Transform existing = canvas.transform.Find("Quick_Warp_HUD");
            if (existing != null) return;

            GameObject warpObj = new GameObject("Quick_Warp_HUD", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            warpObj.transform.SetParent(canvas.transform, false);

            RectTransform rt = warpObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.one;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -150f);
            rt.sizeDelta = new Vector2(64f, 64f);

            CanvasGroup cg = warpObj.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image img = warpObj.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (warpFrames != null && warpFrames.Length > 0 && warpFrames[0] != null)
            {
                img.sprite = warpFrames[0];
            }

            QuickWarpUI warpUI = warpObj.AddComponent<QuickWarpUI>();
            warpUI.InitializeComponents(warpFrames);
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (moneyCardSprite == null)
            {
                moneyCardSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/money card.png");
            }
            if (moneySprite == null)
            {
                moneySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/money.png");
            }
            if (numbersTexture == null)
            {
                numbersTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GUI/numbers 1 - 9.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/numbers 1 - 9.png");
            }
            if (hotbarSlotSprite == null)
            {
                hotbarSlotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/hotbar_slot.png");
            }
            if (hotbarSelectedSprite == null)
            {
                hotbarSelectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/hotbar_selected.png");
            }
            if (clockAmSprite == null)
            {
                clockAmSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock am.png");
            }
            if (clockPmSprite == null)
            {
                clockPmSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock pm.png");
            }
            if (daySprites == null || daySprites.Length < 7 || daySprites[0] == null)
            {
                daySprites = new Sprite[7];
                daySprites[0] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock mon.png");
                daySprites[1] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock tues.png");
                daySprites[2] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock wed.png");
                daySprites[3] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock thurs.png");
                daySprites[4] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock fri.png");
                daySprites[5] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock sat.png");
                daySprites[6] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock sun.png");
            }
            if (energyBarEmptySprite == null || energyBarEmptySprite.texture == null)
            {
                energyBarEmptySprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/energy bar empty.png");
            }
            if (energyBarFillSprite == null || energyBarFillSprite.texture == null)
            {
                energyBarFillSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/energy bar yellow fill.png");
            }
            if (xpBarEmptySprite == null || xpBarEmptySprite.texture == null)
            {
                xpBarEmptySprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar empty new.png")
                                ?? UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar empty.png");
            }
            if (xpBarFillSprite == null || xpBarFillSprite.texture == null)
            {
                xpBarFillSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar green fill.png")
                               ?? UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar filling green.png");
            }
            if (popupBackgroundSprite == null)
            {
                popupBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button popup.png");
            }
            if (popupHoverOutlineSprite == null)
            {
                popupHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button popup hover outline.png");
            }
            if (sleepTextSprite == null)
            {
                sleepTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button sleep text.png");
            }
            if (openTextSprite == null)
            {
                openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
            }
            if (sleepingFrames == null || sleepingFrames.Length < 3 || sleepingFrames[0] == null)
            {
                sleepingFrames = new Sprite[3];
                sleepingFrames[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 1.png");
                sleepingFrames[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 2.png");
                sleepingFrames[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 3.png");
            }
            if (warpFrames == null || warpFrames.Length < 10 || warpFrames[0] == null)
            {
                warpFrames = UISpriteUtility.LoadSpriteFrames("Assets/Sprites/GUI/b press animation.png", 32, 32, 10);
            }
#endif
        }
    }
}
