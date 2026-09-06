using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IsometricGame.Player;

namespace IsometricGame.UI
{
    /// <summary>
    /// Manages the 4x4 Chest / Drawer Inventory Modal UI.
    /// Features:
    /// - Fullscreen dimmed/blurred background overlay.
    /// - Centered 4x4 grid (16 hotbar-styled slots) with hover highlights & punch animations.
    /// - Smooth open/close scale & fade transitions.
    /// - Closes when pressing Escape, 'E', or clicking anywhere outside the 4x4 grid.
    /// - Disables player locomotion while open.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class ChestInventoryUI : MonoBehaviour
    {
        public static ChestInventoryUI Instance { get; private set; }

        [Header("Sprites")]
        [SerializeField] private Sprite slotDefaultSprite;
        [SerializeField] private Sprite slotSelectedSprite;

        [Header("Grid Configuration")]
        [SerializeField] private int gridColumns = 4;
        [SerializeField] private int gridRows = 4;
        [SerializeField] private Vector2 slotPixelSize = new Vector2(50f, 50f);
        [SerializeField] private Vector2 slotSpacing = new Vector2(6f, 6f);

        [Header("Backdrop / Blur Color")]
        [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.085f, 0.80f);

        [Header("Animation")]
        [SerializeField] private float transitionDuration = 0.20f;
        [SerializeField] private float bouncePunchScale = 1.06f;

        [Header("Events")]
        public UnityEvent onChestOpened;
        public UnityEvent onChestClosed;

        private CanvasGroup canvasGroup;
        private RectTransform containerRect;
        private Image backdropImage;
        private Image[] slotBackgrounds;
        private Image[] slotItemIcons;
        private RectTransform[] slotRects;
        private int hoveredSlotIndex = -1;
        private bool isOpen = false;
        private Coroutine activeTransitionRoutine;

        public bool IsOpen => isOpen;
        public static bool IsChestOpen => Instance != null && Instance.isOpen;
        public static bool IsAnyModalOpen => IsChestOpen || JobsBoardUI.IsJobsBoardOpen;
        public int TotalSlots => gridColumns * gridRows;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            InitializeComponents();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
        }

        public void InitializeComponents()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

            EnsureSpritesLoaded();
            SetupHierarchy();

            if (!isOpen)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (slotDefaultSprite == null)
            {
                slotDefaultSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/hotbar_slot.png", new Vector4(3, 3, 3, 3));
            }
            if (slotSelectedSprite == null)
            {
                slotSelectedSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/hotbar_selected.png", new Vector4(3, 3, 3, 3));
            }
#endif
        }

        public void SetupHierarchy()
        {
            RectTransform rootRt = transform as RectTransform ?? GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            EnsureSpritesLoaded();

            // 1. Fullscreen Blurred/Dimmed Backdrop
            Transform bg = transform.Find("Chest_Backdrop");
            GameObject bgObj;
            if (bg != null)
            {
                bgObj = bg.gameObject;
            }
            else
            {
                bgObj = new GameObject("Chest_Backdrop", typeof(RectTransform), typeof(Image), typeof(EventTrigger));
                bgObj.transform.SetParent(transform, false);
            }

            RectTransform rtBg = bgObj.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;

            backdropImage = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
            backdropImage.color = backdropColor;
            backdropImage.raycastTarget = true;

            // Backdrop click handler to close chest
            Button bgBtn = bgObj.GetComponent<Button>();
            if (bgBtn == null) bgBtn = bgObj.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.RemoveAllListeners();
            bgBtn.onClick.AddListener(Close);

            // 2. Centered 4x4 Grid Container
            Transform container = transform.Find("Chest_Grid_Container");
            GameObject containerObj;
            if (container != null)
            {
                containerObj = container.gameObject;
            }
            else
            {
                containerObj = new GameObject("Chest_Grid_Container", typeof(RectTransform), typeof(GridLayoutGroup));
                containerObj.transform.SetParent(transform, false);
            }

            containerRect = containerObj.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);

            float totalWidth = gridColumns * slotPixelSize.x + (gridColumns - 1) * slotSpacing.x;
            float totalHeight = gridRows * slotPixelSize.y + (gridRows - 1) * slotSpacing.y;
            containerRect.sizeDelta = new Vector2(totalWidth, totalHeight);
            containerRect.anchoredPosition = Vector2.zero;

            GridLayoutGroup grid = containerObj.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = containerObj.AddComponent<GridLayoutGroup>();
            grid.cellSize = slotPixelSize;
            grid.spacing = slotSpacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = gridColumns;
            grid.childAlignment = TextAnchor.MiddleCenter;

            // 3. Create 16 Hotbar Slots
            int total = TotalSlots;
            slotBackgrounds = new Image[total];
            slotItemIcons = new Image[total];
            slotRects = new RectTransform[total];

            for (int i = 0; i < total; i++)
            {
                string slotName = $"Chest_Slot_{i}";
                Transform slotTr = containerObj.transform.Find(slotName);
                GameObject slotObj;
                if (slotTr != null)
                {
                    slotObj = slotTr.gameObject;
                }
                else
                {
                    slotObj = new GameObject(slotName, typeof(RectTransform), typeof(Image), typeof(EventTrigger));
                    slotObj.transform.SetParent(containerObj.transform, false);
                }

                slotRects[i] = slotObj.GetComponent<RectTransform>();
                slotBackgrounds[i] = slotObj.GetComponent<Image>();
                if (slotBackgrounds[i] == null) slotBackgrounds[i] = slotObj.AddComponent<Image>();

                slotBackgrounds[i].sprite = slotDefaultSprite;
                slotBackgrounds[i].color = Color.white;
                slotBackgrounds[i].type = Image.Type.Simple;
                slotBackgrounds[i].raycastTarget = true;

                // Item Icon child
                Transform iconTr = slotObj.transform.Find("Item_Icon");
                GameObject iconObj;
                if (iconTr != null)
                {
                    iconObj = iconTr.gameObject;
                }
                else
                {
                    iconObj = new GameObject("Item_Icon", typeof(RectTransform), typeof(Image));
                    iconObj.transform.SetParent(slotObj.transform, false);
                }

                RectTransform rtIcon = iconObj.GetComponent<RectTransform>();
                rtIcon.anchorMin = new Vector2(0.15f, 0.15f);
                rtIcon.anchorMax = new Vector2(0.85f, 0.85f);
                rtIcon.offsetMin = Vector2.zero;
                rtIcon.offsetMax = Vector2.zero;

                slotItemIcons[i] = iconObj.GetComponent<Image>();
                slotItemIcons[i].raycastTarget = false;
                slotItemIcons[i].preserveAspect = true;
                slotItemIcons[i].gameObject.SetActive(false);

                // Add pointer enter / exit / click handlers
                int slotIndex = i;
                EventTrigger trigger = slotObj.GetComponent<EventTrigger>();
                if (trigger == null) trigger = slotObj.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) => OnSlotHover(slotIndex, true));
                trigger.triggers.Add(enterEntry);

                EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exitEntry.callback.AddListener((data) => OnSlotHover(slotIndex, false));
                trigger.triggers.Add(exitEntry);

                EventTrigger.Entry clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                clickEntry.callback.AddListener((data) => OnSlotClicked(slotIndex));
                trigger.triggers.Add(clickEntry);
            }
        }

        private void Update()
        {
            if (!isOpen) return;

            CheckInput();
        }

        private void CheckInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame)
                {
                    Close();
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                Close();
            }
#endif
        }

        public void ToggleOpen()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;

            EnsureSpritesLoaded();
            SetupHierarchy();

            // Lock player locomotion
            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.SetInputEnabled(false);
            }

            if (WorldInteractionPopup.Instance != null)
            {
                WorldInteractionPopup.Instance.DismissImmediate();
            }

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(AnimateOpen());

            onChestOpened?.Invoke();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            // Restore player locomotion
            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.SetInputEnabled(true);
            }

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(AnimateClose());

            onChestClosed?.Invoke();
        }

        private IEnumerator AnimateOpen()
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.88f;
            Vector3 peakScale = Vector3.one * bouncePunchScale;
            Vector3 normalScale = Vector3.one;

            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                canvasGroup.alpha = t;

                if (containerRect != null)
                {
                    if (t < 0.70f)
                    {
                        containerRect.localScale = Vector3.Lerp(startScale, peakScale, t / 0.70f);
                    }
                    else
                    {
                        containerRect.localScale = Vector3.Lerp(peakScale, normalScale, (t - 0.70f) / 0.30f);
                    }
                }
                yield return null;
            }

            canvasGroup.alpha = 1f;
            if (containerRect != null) containerRect.localScale = normalScale;
        }

        private IEnumerator AnimateClose()
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = containerRect != null ? containerRect.localScale : Vector3.one;
            Vector3 endScale = Vector3.one * 0.90f;

            while (elapsed < transitionDuration * 0.75f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / (transitionDuration * 0.75f));
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (containerRect != null)
                {
                    containerRect.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private void OnSlotHover(int index, bool hovered)
        {
            if (index < 0 || index >= slotBackgrounds.Length) return;
            if (slotBackgrounds[index] == null) return;

            hoveredSlotIndex = hovered ? index : -1;
            slotBackgrounds[index].sprite = hovered ? slotSelectedSprite : slotDefaultSprite;

            if (hovered && slotRects[index] != null)
            {
                StartCoroutine(PunchSlot(slotRects[index]));
            }
        }

        private void OnSlotClicked(int index)
        {
            if (index < 0 || index >= slotBackgrounds.Length) return;
            if (slotRects[index] != null)
            {
                StartCoroutine(PunchSlot(slotRects[index]));
            }
        }

        private IEnumerator PunchSlot(RectTransform target)
        {
            Vector3 origScale = Vector3.one;
            Vector3 punchScale = Vector3.one * 1.15f;
            float elapsed = 0f;
            float duration = 0.14f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                if (t < 0.5f)
                {
                    target.localScale = Vector3.Lerp(origScale, punchScale, t * 2f);
                }
                else
                {
                    target.localScale = Vector3.Lerp(punchScale, origScale, (t - 0.5f) * 2f);
                }
                yield return null;
            }
            target.localScale = origScale;
        }

        public void SetSlotItem(int index, Sprite itemSprite)
        {
            if (index < 0 || index >= slotItemIcons.Length) return;
            if (slotItemIcons[index] != null)
            {
                if (itemSprite != null)
                {
                    slotItemIcons[index].sprite = itemSprite;
                    slotItemIcons[index].gameObject.SetActive(true);
                }
                else
                {
                    slotItemIcons[index].gameObject.SetActive(false);
                }
            }
        }
    }
}
