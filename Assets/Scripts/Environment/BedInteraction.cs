using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Handles mouse hover highlighting and interaction for the bed.
    /// Displays a pixel-aligned pulsing outline overlay when hovered,
    /// with click interaction hooks for resting/saving.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class BedInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Sprites & Outline")]
        [Tooltip("The direct pixel-aligned bed hover outline sprite.")]
        public Sprite bedHoverOutlineSprite;
        [Tooltip("Text sprite displayed inside interaction popup (SLEEP).")]
        public Sprite sleepTextSprite;
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);

        [Header("Popup Offset")]
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.75f, 0f);

        [Header("Hover Dynamics")]
        [SerializeField] private float fadeSpeed = 16f;
        [SerializeField] private bool pulseWhileHovered = true;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseMinAlpha = 0.70f;
        [SerializeField] private float pulseMaxAlpha = 1.0f;

        [Header("Hover Trigger Area")]
        [Tooltip("Local offset from bed pivot for hover detection.")]
        [SerializeField] private Vector2 triggerOffset = new Vector2(0f, 0.28f);
        [Tooltip("Size of hover detection box.")]
        [SerializeField] private Vector2 triggerSize = new Vector2(1.2f, 0.95f);

        [Header("Extensible Events")]
        public UnityEvent onBedClicked;

        private SpriteRenderer bedRenderer;
        private SpriteRenderer outlineRenderer;
        private BoxCollider2D hoverCol;
        private bool isHovered = false;
        private bool isPointerOver = false;
        private float currentAlpha = 0f;

        public bool IsHovered => isHovered;

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();
        }

        public void InitializeComponents()
        {
            if (bedRenderer == null) bedRenderer = GetComponent<SpriteRenderer>();

            EnsureSpriteLoaded();
            SetupOutlineRenderer();
            SetupCollider();
            UpdateSorting();
        }

        private void EnsureSpriteLoaded()
        {
#if UNITY_EDITOR
            if (bedHoverOutlineSprite == null)
            {
                bedHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bed hover outline.png");
            }
            if (sleepTextSprite == null)
            {
                sleepTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button sleep text.png");
            }
#endif
        }

        private void SetupOutlineRenderer()
        {
            Transform existing = transform.Find("Bed_Hover_Outline");
            GameObject outlineObj = existing != null ? existing.gameObject : new GameObject("Bed_Hover_Outline");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localScale = Vector3.one;

            outlineRenderer = outlineObj.GetComponent<SpriteRenderer>();
            if (outlineRenderer == null) outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();

            if (bedHoverOutlineSprite != null)
            {
                outlineRenderer.sprite = bedHoverOutlineSprite;
            }

            Color c = outlineColor;
            c.a = 0f;
            outlineRenderer.color = c;
        }

        private void SetupCollider()
        {
            hoverCol = GetComponent<BoxCollider2D>();
            if (hoverCol == null) hoverCol = gameObject.AddComponent<BoxCollider2D>();

            hoverCol.isTrigger = true;
            hoverCol.size = triggerSize;
            hoverCol.offset = triggerOffset;
        }

        private void Update()
        {
            UpdateSorting();

            if (Application.isPlaying)
            {
                CheckManualHover();
                UpdateOutlineVisual();
            }
        }

        private bool IsPopupActiveForThis()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return false;
            var popup = IsometricGame.UI.WorldInteractionPopup.Instance;
            return popup != null && popup.IsButtonHovered && popup.CurrentTarget == transform;
        }

        private bool IsMouseInBounds()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return false;
            Vector2 mouseWorld = IsometricGame.Core.IsometricInputHelper.GetMouseWorldPosition();
            Vector2 center = (Vector2)transform.position + triggerOffset;
            bool inBounds = (Mathf.Abs(mouseWorld.x - center.x) <= triggerSize.x * 0.5f &&
                             Mathf.Abs(mouseWorld.y - center.y) <= triggerSize.y * 0.5f);
            return inBounds || (hoverCol != null && hoverCol.OverlapPoint(mouseWorld));
        }

        private void CheckManualHover()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen)
            {
                if (isHovered) SetHoverState(false);
                return;
            }

            bool shouldHover = IsMouseInBounds() || IsPopupActiveForThis();

            if (shouldHover)
            {
                if (!isHovered) SetHoverState(true);
            }
            else if (!isPointerOver && isHovered)
            {
                SetHoverState(false);
            }

            if (isHovered && IsMouseInBounds() && IsometricGame.Core.IsometricInputHelper.IsLeftMouseButtonDown())
            {
                TriggerSleep();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            isPointerOver = true;
            SetHoverState(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            if (!IsMouseInBounds() && !IsPopupActiveForThis())
            {
                SetHoverState(false);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerSleep();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerSleep();
            }
        }

        private void SetHoverState(bool hovered)
        {
            isHovered = hovered;
            if (isHovered)
            {
                if (sleepTextSprite == null) EnsureSpriteLoaded();
                if (IsometricGame.UI.WorldInteractionPopup.Instance != null)
                {
                    IsometricGame.UI.WorldInteractionPopup.Instance.Show(transform, popupOffset, sleepTextSprite, TriggerSleep);
                }
            }
            else
            {
                if (IsometricGame.UI.WorldInteractionPopup.Instance != null)
                {
                    IsometricGame.UI.WorldInteractionPopup.Instance.Hide(transform);
                }
            }
        }

        public void TriggerSleep()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;

            onBedClicked?.Invoke();
            if (IsometricGame.UI.SleepTransitionUI.Instance != null)
            {
                IsometricGame.UI.SleepTransitionUI.Instance.PlaySleepSequence();
            }
        }

        private void UpdateOutlineVisual()
        {
            if (outlineRenderer == null) SetupOutlineRenderer();
            if (outlineRenderer == null) return;

            if (outlineRenderer.sprite == null && bedHoverOutlineSprite != null)
            {
                outlineRenderer.sprite = bedHoverOutlineSprite;
            }

            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen)
            {
                currentAlpha = 0f;
                outlineRenderer.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            float targetAlpha = 0f;
            if (isHovered)
            {
                if (pulseWhileHovered)
                {
                    float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                    targetAlpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, pulse);
                }
                else
                {
                    targetAlpha = 1.0f;
                }
            }

            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            outlineRenderer.color = new Color(1f, 1f, 1f, currentAlpha);
        }

        private void UpdateSorting()
        {
            if (bedRenderer == null) bedRenderer = GetComponent<SpriteRenderer>();
            if (bedRenderer != null && outlineRenderer != null)
            {
                outlineRenderer.sortingOrder = bedRenderer.sortingOrder + 2;
            }
        }

        private void OnMouseEnter()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            SetHoverState(true);
        }

        private void OnMouseExit()
        {
            if (!IsMouseInBounds())
            {
                SetHoverState(false);
            }
        }

        private void OnMouseDown()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            TriggerSleep();
        }
    }
}
