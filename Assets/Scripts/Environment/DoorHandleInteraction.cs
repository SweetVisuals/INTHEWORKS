using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Handles mouse hover highlighting for the door handle.
    /// Shows a clean pixel-perfect outline when hovering over the door/handle,
    /// with an extensible click event hook for future interactions.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class DoorHandleInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Sprites & Outline")]
        [Tooltip("Direct pixel-aligned door handle outline sprite.")]
        public Sprite handleOutlineSprite;
        [Tooltip("Text sprite displayed inside interaction popup (OPEN).")]
        public Sprite openTextSprite;
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 1f);

        [Header("Popup Offset")]
        [SerializeField] private Vector3 popupOffset = new Vector3(0f, 0.95f, 0f);

        [Header("Hover Dynamics")]
        [SerializeField] private float fadeSpeed = 16f;
        [SerializeField] private bool pulseWhileHovered = true;
        [SerializeField] private float pulseSpeed = 4.5f;
        [SerializeField] private float pulseMinAlpha = 0.70f;
        [SerializeField] private float pulseMaxAlpha = 1.0f;

        [Header("Door Target Type")]
        [Tooltip("If true, clicking this door transitions indoors. If false, transitions outdoors.")]
        public bool isOutdoorDoor = false;

        [Header("Extensible Events (Click / Interact)")]
        public UnityEvent onDoorClicked;

        private SpriteRenderer doorRenderer;
        private SpriteRenderer outlineRenderer;
        private BoxCollider2D triggerCol;
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
            doorRenderer = GetComponent<SpriteRenderer>();

            EnsureSpriteLoaded();
            SetupOutlineRenderer();
            SetupCollider();
            UpdateSorting();
        }

        private void EnsureSpriteLoaded()
        {
#if UNITY_EDITOR
            if (handleOutlineSprite == null)
            {
                handleOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door handle outline.png");
            }
            if (openTextSprite == null)
            {
                openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
            }
#endif
        }

        private void SetupOutlineRenderer()
        {
            Transform existing = transform.Find("Door_Handle_Outline");
            GameObject outlineObj = existing != null ? existing.gameObject : new GameObject("Door_Handle_Outline");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localScale = Vector3.one;

            outlineRenderer = outlineObj.GetComponent<SpriteRenderer>();
            if (outlineRenderer == null) outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();

            if (handleOutlineSprite != null)
            {
                outlineRenderer.sprite = handleOutlineSprite;
            }

            Color c = outlineColor;
            c.a = 0f;
            outlineRenderer.color = c;
        }

        private void SetupCollider()
        {
            triggerCol = GetComponent<BoxCollider2D>();
            if (triggerCol == null) triggerCol = gameObject.AddComponent<BoxCollider2D>();

            triggerCol.isTrigger = true;
            triggerCol.size = new Vector2(0.70f, 1.25f);
            triggerCol.offset = new Vector2(0f, 0.45f);
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
            Vector2 center = (Vector2)transform.position + new Vector2(0f, 0.45f);
            bool inBounds = (Mathf.Abs(mouseWorld.x - center.x) <= 0.45f &&
                             Mathf.Abs(mouseWorld.y - center.y) <= 0.70f);
            return inBounds || (triggerCol != null && triggerCol.OverlapPoint(mouseWorld));
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
                TriggerOpenDoor();
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
                TriggerOpenDoor();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerOpenDoor();
            }
        }

        private void SetHoverState(bool hovered)
        {
            isHovered = hovered;
            if (isHovered)
            {
                if (openTextSprite == null) EnsureSpriteLoaded();
                if (IsometricGame.UI.WorldInteractionPopup.Instance != null)
                {
                    IsometricGame.UI.WorldInteractionPopup.Instance.Show(transform, popupOffset, openTextSprite, TriggerOpenDoor);
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

        public void TriggerOpenDoor()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;

            onDoorClicked?.Invoke();
            if (ZoneTransitionManager.Instance != null)
            {
                if (isOutdoorDoor)
                {
                    ZoneTransitionManager.Instance.TransitionToIndoors();
                }
                else
                {
                    ZoneTransitionManager.Instance.TransitionToOutdoors();
                }
            }
        }

        private void UpdateOutlineVisual()
        {
            if (outlineRenderer == null) SetupOutlineRenderer();
            if (outlineRenderer == null) return;

            if (outlineRenderer.sprite == null && handleOutlineSprite != null)
            {
                outlineRenderer.sprite = handleOutlineSprite;
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
            if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();
            if (doorRenderer != null && outlineRenderer != null)
            {
                outlineRenderer.sortingOrder = doorRenderer.sortingOrder + 2;
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
            TriggerOpenDoor();
        }
    }
}
