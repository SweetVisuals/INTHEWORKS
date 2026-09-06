using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace IsometricGame.UI
{
    /// <summary>
    /// Reusable Floating Card Button Popup for interactables (Bed, Computer Desk, Door, etc.).
    /// Features:
    /// - Displays 'gui card button popup' background (50x16 @ 3x scale = 150x48).
    /// - Overlays contextual action text ('SLEEP', 'OPEN', etc.).
    /// - Dynamic hover state displaying 'gui card button popup hover outline'.
    /// - Follows cursor with precise offset, instantly snapping on appear.
    /// - Robust grace period preventing raycast flickering between 2D collider and UI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    [ExecuteAlways]
    public class WorldInteractionPopup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private static WorldInteractionPopup instance;
        public static WorldInteractionPopup Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindAnyObjectByType<WorldInteractionPopup>();
                    if (instance == null)
                    {
                        EnsureCanvasAndMoneyUI.EnsureAllUI();
                        instance = UnityEngine.Object.FindAnyObjectByType<WorldInteractionPopup>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Sprites")]
        [SerializeField] private Sprite buttonBackgroundSprite;
        [SerializeField] private Sprite buttonHoverOutlineSprite;

        [Header("UI References")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image textImage;
        [SerializeField] private Image hoverOutlineImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform rectTransform;

        [Header("Animation & Sizing")]
        [SerializeField] private float fadeSpeed = 24f;
        [SerializeField] private float hoverOutlineFadeSpeed = 20f;
        [SerializeField] private Vector2 defaultPixelSize = new Vector2(150f, 48f); // 50x16 at 3x scale

        [Header("World Anchoring")]
        [SerializeField] private bool followCursor = false;
        [SerializeField] private Vector2 cursorOffset = new Vector2(0f, 42f);
        [SerializeField] private float positionSmoothSpeed = 35f;

        private Transform targetWorldTransform;
        private Vector3 worldOffset = new Vector3(0f, 0.85f, 0f);
        private Action currentActionCallback;
        private bool isButtonHovered = false;
        private bool isTargetHovered = false;
        private float hideGraceTimer = 0f;
        private const float HIDE_GRACE_DURATION = 0.35f;
        private float currentHoverOutlineAlpha = 0f;
        private Camera mainCamera;
        private Canvas rootCanvas;
        private RectTransform canvasRectTransform;

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0.01f;
        public bool IsButtonHovered => isButtonHovered;
        public Transform CurrentTarget => targetWorldTransform;

        private void Awake()
        {
            instance = this;
            InitializeComponents();
        }

        private void OnEnable()
        {
            instance = this;
            CacheCanvasAndCamera();
        }

        private void CacheCanvasAndCamera()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) mainCamera = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null) rootCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (rootCanvas != null) canvasRectTransform = rootCanvas.transform as RectTransform;
        }

        public void InitializeComponents()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (rectTransform == null) rectTransform = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();

            EnsureSpritesLoaded();
            SetupHierarchy();
            CacheCanvasAndCamera();

            if (canvasGroup != null && !isTargetHovered && !isButtonHovered)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (buttonBackgroundSprite == null)
            {
                buttonBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button popup.png");
            }
            if (buttonHoverOutlineSprite == null)
            {
                buttonHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button popup hover outline.png");
            }
#endif
        }

        public void SetupHierarchy()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            }
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.sizeDelta = defaultPixelSize;
            }

            EnsureSpritesLoaded();

            // 1. Background Image
            Transform bg = transform.Find("Popup_Background");
            GameObject bgObj;
            if (bg != null)
            {
                bgObj = bg.gameObject;
            }
            else
            {
                bgObj = new GameObject("Popup_Background", typeof(RectTransform), typeof(Image));
                bgObj.transform.SetParent(transform, false);
            }

            RectTransform rtBg = bgObj.GetComponent<RectTransform>();
            if (rtBg != null)
            {
                rtBg.anchorMin = Vector2.zero;
                rtBg.anchorMax = Vector2.one;
                rtBg.offsetMin = Vector2.zero;
                rtBg.offsetMax = Vector2.zero;
            }

            backgroundImage = bgObj.GetComponent<Image>();
            if (backgroundImage == null) backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.raycastTarget = true;
            backgroundImage.type = Image.Type.Simple;
            backgroundImage.preserveAspect = false;
            if (buttonBackgroundSprite != null)
            {
                backgroundImage.sprite = buttonBackgroundSprite;
                backgroundImage.color = Color.white;
            }

            // 2. Action Text Image (SLEEP / OPEN)
            Transform txt = transform.Find("Popup_Text");
            GameObject txtObj;
            if (txt != null)
            {
                txtObj = txt.gameObject;
            }
            else
            {
                txtObj = new GameObject("Popup_Text", typeof(RectTransform), typeof(Image));
                txtObj.transform.SetParent(transform, false);
            }

            RectTransform rtTxt = txtObj.GetComponent<RectTransform>();
            if (rtTxt != null)
            {
                rtTxt.anchorMin = Vector2.zero;
                rtTxt.anchorMax = Vector2.one;
                rtTxt.offsetMin = Vector2.zero;
                rtTxt.offsetMax = Vector2.zero;
            }

            textImage = txtObj.GetComponent<Image>();
            if (textImage == null) textImage = txtObj.AddComponent<Image>();
            textImage.raycastTarget = false;
            textImage.preserveAspect = false;

            // 3. Hover Outline Image
            Transform outline = transform.Find("Popup_Hover_Outline");
            GameObject outlineObj;
            if (outline != null)
            {
                outlineObj = outline.gameObject;
            }
            else
            {
                outlineObj = new GameObject("Popup_Hover_Outline", typeof(RectTransform), typeof(Image));
                outlineObj.transform.SetParent(transform, false);
            }

            RectTransform rtOutline = outlineObj.GetComponent<RectTransform>();
            if (rtOutline != null)
            {
                rtOutline.anchorMin = Vector2.zero;
                rtOutline.anchorMax = Vector2.one;
                rtOutline.offsetMin = Vector2.zero;
                rtOutline.offsetMax = Vector2.zero;
            }

            hoverOutlineImage = outlineObj.GetComponent<Image>();
            if (hoverOutlineImage == null) hoverOutlineImage = outlineObj.AddComponent<Image>();
            hoverOutlineImage.raycastTarget = false;
            hoverOutlineImage.preserveAspect = false;
            if (buttonHoverOutlineSprite != null)
            {
                hoverOutlineImage.sprite = buttonHoverOutlineSprite;
            }
            hoverOutlineImage.color = new Color(1f, 1f, 1f, currentHoverOutlineAlpha);
        }

        private void LateUpdate()
        {
            UpdatePosition();
            UpdateVisibility();
            UpdateHoverOutline();
        }

        /// <summary>
        /// Shows the interaction popup over a world target with specific action text and click handler.
        /// </summary>
        public void Show(Transform worldTarget, Sprite textSprite, Action onAction)
        {
            Show(worldTarget, Vector3.zero, textSprite, onAction);
        }

        /// <summary>
        /// Shows the interaction popup over a world target with specific action text and click handler.
        /// </summary>
        public void Show(Transform worldTarget, Vector3 offset, Sprite textSprite, Action onAction)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;

            targetWorldTransform = worldTarget;
            worldOffset = offset != Vector3.zero ? offset : new Vector3(0f, 0.85f, 0f);
            currentActionCallback = onAction;
            isTargetHovered = true;
            hideGraceTimer = 0f;

            transform.SetAsLastSibling();
            SetupHierarchy();

            if (textImage != null && textSprite != null)
            {
                textImage.sprite = textSprite;
                textImage.gameObject.SetActive(true);
            }

            CacheCanvasAndCamera();

            if (canvasGroup != null && canvasGroup.alpha < 0.1f)
            {
                SnapToPosition();
                canvasGroup.alpha = 1.0f;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// Hides the interaction popup when the target object is no longer hovered.
        /// </summary>
        public void Hide(Transform worldTarget)
        {
            if (targetWorldTransform == worldTarget)
            {
                hideGraceTimer = HIDE_GRACE_DURATION;
            }
        }

        /// <summary>
        /// Force dismisses the popup immediately.
        /// </summary>
        public void DismissImmediate()
        {
            isTargetHovered = false;
            isButtonHovered = false;
            hideGraceTimer = 0f;
            targetWorldTransform = null;
            currentActionCallback = null;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private Vector2 GetDesiredScreenPosition()
        {
            Vector2 mousePos = IsometricGame.Core.IsometricInputHelper.GetMouseScreenPosition();
            bool hasValidMouse = (mousePos.x >= 0 && mousePos.y >= 0 && mousePos.x <= Screen.width && mousePos.y <= Screen.height && mousePos != Vector2.zero);

            if (followCursor && hasValidMouse)
            {
                return mousePos + cursorOffset;
            }

            if (mainCamera == null) CacheCanvasAndCamera();

            if (mainCamera != null && targetWorldTransform != null)
            {
                Vector3 worldPos = targetWorldTransform.position + worldOffset;
                return (Vector2)RectTransformUtility.WorldToScreenPoint(mainCamera, worldPos);
            }

            return mousePos;
        }

        public void SnapToPosition()
        {
            CacheCanvasAndCamera();
            if (rootCanvas == null || canvasRectTransform == null) return;

            Camera eventCam = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : mainCamera;
            Vector2 screenPos = GetDesiredScreenPosition();

            if (rectTransform == null) rectTransform = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (rectTransform != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPos, eventCam, out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }

        private void UpdatePosition()
        {
            if (targetWorldTransform == null && !isButtonHovered) return;
            CacheCanvasAndCamera();
            if (rootCanvas == null || canvasRectTransform == null) return;

            Camera eventCam = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : mainCamera;
            Vector2 screenPos = GetDesiredScreenPosition();

            if (rectTransform == null) rectTransform = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (rectTransform != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRectTransform, screenPos, eventCam, out Vector2 localPoint))
            {
                if (canvasGroup != null && canvasGroup.alpha < 0.1f)
                {
                    rectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    rectTransform.anchoredPosition = Vector2.Lerp(rectTransform.anchoredPosition, localPoint, Time.deltaTime * positionSmoothSpeed);
                }
            }
        }

        private void UpdateVisibility()
        {
            if (canvasGroup == null) return;

            if (ChestInventoryUI.IsAnyModalOpen)
            {
                DismissImmediate();
                return;
            }

            if (hideGraceTimer > 0f)
            {
                hideGraceTimer -= Time.deltaTime;
                if (hideGraceTimer <= 0f)
                {
                    isTargetHovered = false;
                }
            }

            bool shouldBeVisible = (isTargetHovered || isButtonHovered) && targetWorldTransform != null;
            float targetAlpha = shouldBeVisible ? 1.0f : 0.0f;

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
            bool interactive = canvasGroup.alpha > 0.25f;
            canvasGroup.interactable = interactive;
            canvasGroup.blocksRaycasts = interactive;

            if (canvasGroup.alpha <= 0.001f && !shouldBeVisible)
            {
                targetWorldTransform = null;
                currentActionCallback = null;
            }
        }

        private void UpdateHoverOutline()
        {
            if (hoverOutlineImage == null) return;

            if (ChestInventoryUI.IsAnyModalOpen)
            {
                currentHoverOutlineAlpha = 0f;
                Color clr = Color.white;
                clr.a = 0f;
                hoverOutlineImage.color = clr;
                return;
            }

            float targetAlpha = 0f;
            if (isButtonHovered)
            {
                // Pulsing hover outline on button
                float pulse = (Mathf.Sin(Time.time * 6f) + 1f) * 0.5f;
                targetAlpha = Mathf.Lerp(0.85f, 1.0f, pulse);
            }

            currentHoverOutlineAlpha = Mathf.MoveTowards(currentHoverOutlineAlpha, targetAlpha, Time.deltaTime * hoverOutlineFadeSpeed);

            Color c = Color.white;
            c.a = currentHoverOutlineAlpha;
            hoverOutlineImage.color = c;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
            isButtonHovered = true;
            hideGraceTimer = 0f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isButtonHovered = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerAction();
            }
        }

        public void TriggerAction()
        {
            Action callback = currentActionCallback;
            DismissImmediate();
            callback?.Invoke();
        }
    }
}
