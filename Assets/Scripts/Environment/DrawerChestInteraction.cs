using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using IsometricGame.UI;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Handles mouse hover highlighting and interaction for the small wooden drawer / chest.
    /// Displays pixel-aligned pulsing outline overlay when hovered, shows the 'OPEN' popup button,
    /// and opens the 4x4 chest inventory grid with blurred background.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class DrawerChestInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Sprites & Outline")]
        [Tooltip("The direct pixel-aligned drawer hover outline sprite.")]
        public Sprite drawerHoverOutlineSprite;
        [Tooltip("Text sprite displayed inside interaction popup (OPEN).")]
        public Sprite openTextSprite;
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);

        [Header("Popup Offset")]
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.65f, 0f);

        [Header("Hover Dynamics")]
        [SerializeField] private float fadeSpeed = 16f;
        [SerializeField] private bool pulseWhileHovered = true;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseMinAlpha = 0.70f;
        [SerializeField] private float pulseMaxAlpha = 1.0f;

        [Header("Hover Trigger Area")]
        [Tooltip("Local offset from drawer pivot for hover detection.")]
        [SerializeField] private Vector2 triggerOffset = new Vector2(0f, 0.10f);
        [Tooltip("Size of hover detection box.")]
        [SerializeField] private Vector2 triggerSize = new Vector2(0.55f, 0.40f);

        [Header("Extensible Events")]
        public UnityEvent onDrawerOpened;

        private SpriteRenderer drawerRenderer;
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
            if (drawerRenderer == null) drawerRenderer = GetComponent<SpriteRenderer>();

            EnsureSpriteLoaded();
            SetupOutlineRenderer();
            SetupCollider();
            UpdateSorting();
        }

        private void EnsureSpriteLoaded()
        {
#if UNITY_EDITOR
            if (drawerHoverOutlineSprite == null)
            {
                drawerHoverOutlineSprite = UISpriteUtility.LoadSprite("Assets/Sprites/sdrawer hover outline (shift 1px to the right).png")
                                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/sdrawer hover outline (shift 1px to the right).png");
            }
            if (openTextSprite == null)
            {
                openTextSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/gui card button open text.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
            }
#endif
        }

        private void SetupOutlineRenderer()
        {
            Transform existing = transform.Find("Drawer_Hover_Outline");
            GameObject outlineObj = existing != null ? existing.gameObject : new GameObject("Drawer_Hover_Outline");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localScale = Vector3.one;

            outlineRenderer = outlineObj.GetComponent<SpriteRenderer>();
            if (outlineRenderer == null) outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();

            if (drawerHoverOutlineSprite != null)
            {
                outlineRenderer.sprite = drawerHoverOutlineSprite;
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
            if (ChestInventoryUI.IsAnyModalOpen) return false;
            var popup = WorldInteractionPopup.Instance;
            return popup != null && popup.IsButtonHovered && popup.CurrentTarget == transform;
        }

        private bool IsMouseInBounds()
        {
            if (ChestInventoryUI.IsAnyModalOpen) return false;
            Vector2 mouseWorld = IsometricGame.Core.IsometricInputHelper.GetMouseWorldPosition();
            Vector2 center = (Vector2)transform.position + triggerOffset;
            bool inBounds = (Mathf.Abs(mouseWorld.x - center.x) <= triggerSize.x * 0.5f &&
                             Mathf.Abs(mouseWorld.y - center.y) <= triggerSize.y * 0.5f);
            return inBounds || (hoverCol != null && hoverCol.OverlapPoint(mouseWorld));
        }

        private void CheckManualHover()
        {
            if (ChestInventoryUI.IsAnyModalOpen)
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
                TriggerOpenChest();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
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
            if (ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerOpenChest();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerOpenChest();
            }
        }

        private void SetHoverState(bool hovered)
        {
            isHovered = hovered;
            if (isHovered)
            {
                if (openTextSprite == null) EnsureSpriteLoaded();
                if (WorldInteractionPopup.Instance != null)
                {
                    WorldInteractionPopup.Instance.Show(transform, popupOffset, openTextSprite, TriggerOpenChest);
                }
            }
            else
            {
                if (WorldInteractionPopup.Instance != null)
                {
                    WorldInteractionPopup.Instance.Hide(transform);
                }
            }
        }

        public void TriggerOpenChest()
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;

            onDrawerOpened?.Invoke();
            if (WorldInteractionPopup.Instance != null)
            {
                WorldInteractionPopup.Instance.DismissImmediate();
            }

            if (ChestInventoryUI.Instance != null)
            {
                ChestInventoryUI.Instance.ToggleOpen();
            }
        }

        private void UpdateOutlineVisual()
        {
            if (outlineRenderer == null) SetupOutlineRenderer();
            if (outlineRenderer == null) return;

            if (outlineRenderer.sprite == null && drawerHoverOutlineSprite != null)
            {
                outlineRenderer.sprite = drawerHoverOutlineSprite;
            }

            if (ChestInventoryUI.IsAnyModalOpen)
            {
                currentAlpha = 0f;
                outlineRenderer.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, 0f);
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
            outlineRenderer.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, currentAlpha);
        }

        private void UpdateSorting()
        {
            if (drawerRenderer == null) drawerRenderer = GetComponent<SpriteRenderer>();
            if (drawerRenderer != null && outlineRenderer != null)
            {
                outlineRenderer.sortingOrder = drawerRenderer.sortingOrder + 2;
            }
        }

        private void OnMouseEnter()
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
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
            if (ChestInventoryUI.IsAnyModalOpen) return;
            TriggerOpenChest();
        }
    }
}
