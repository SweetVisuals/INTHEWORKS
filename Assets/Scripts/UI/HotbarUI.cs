using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IsometricGame.UI
{
    /// <summary>
    /// Hotbar HUD Controller located at the bottom of the screen.
    /// Manages 4 item slots with selection highlighting, keyboard shortcuts (1-4),
    /// mouse clicking, and slot punch animations.
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        public static HotbarUI Instance { get; private set; }

        [Header("Sprites")]
        [SerializeField] private Sprite slotDefaultSprite;
        [SerializeField] private Sprite slotSelectedSprite;

        [Header("Slot References")]
        [SerializeField] private Image[] slotBackgrounds = new Image[4];
        [SerializeField] private Image[] itemIcons = new Image[4];
        [SerializeField] private RectTransform[] slotRects = new RectTransform[4];

        [Header("Selection Settings")]
        [SerializeField] private int selectedSlotIndex = 0;
        [SerializeField] private bool allowKeyboardSelection = true;
        [SerializeField] private bool bounceOnSelect = true;

        [Header("Events")]
        public UnityEvent<int> onSlotSelected;

        public int SelectedSlotIndex => selectedSlotIndex;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            EnsureSpritesLoaded();
            CacheSlotReferences();
            RefreshSlotVisuals();
        }

        private void OnEnable()
        {
            EnsureSpritesLoaded();
            CacheSlotReferences();
            RefreshSlotVisuals();
        }

        private void Start()
        {
            SelectSlot(selectedSlotIndex, false);
        }

        private void Update()
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;

            if (allowKeyboardSelection)
            {
                CheckKeyboardInput();
            }
        }

        private void CheckKeyboardInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) SelectSlot(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) SelectSlot(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) SelectSlot(2);
                if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) SelectSlot(3);
            }
#else
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectSlot(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectSlot(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectSlot(2);
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectSlot(3);
#endif
        }

        public void SelectSlot(int index, bool animate = true)
        {
            if (ChestInventoryUI.IsAnyModalOpen) return;
            if (index < 0 || index >= slotBackgrounds.Length) return;

            selectedSlotIndex = index;
            RefreshSlotVisuals(animate);
            onSlotSelected?.Invoke(selectedSlotIndex);
        }

        private void RefreshSlotVisuals(bool animate = false)
        {
            EnsureSpritesLoaded();

            for (int i = 0; i < slotBackgrounds.Length; i++)
            {
                if (slotBackgrounds[i] == null) continue;

                bool isSelected = (i == selectedSlotIndex);
                Sprite targetSprite = isSelected ? slotSelectedSprite : slotDefaultSprite;
                if (targetSprite != null)
                {
                    slotBackgrounds[i].sprite = targetSprite;
                    slotBackgrounds[i].color = Color.white;
                    slotBackgrounds[i].type = Image.Type.Simple;
                }

                if (isSelected && animate && bounceOnSelect && slotRects[i] != null && gameObject.activeInHierarchy)
                {
                    StartCoroutine(AnimateSlotBounce(slotRects[i]));
                }
            }
        }

        private System.Collections.IEnumerator AnimateSlotBounce(RectTransform target)
        {
            Vector3 origScale = Vector3.one;
            Vector3 punchScale = Vector3.one * 1.18f;
            float elapsed = 0f;
            float duration = 0.2f;

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

        public void SetSlotItem(int slotIndex, Sprite itemSprite)
        {
            if (slotIndex < 0 || slotIndex >= itemIcons.Length) return;
            if (itemIcons[slotIndex] != null)
            {
                if (itemSprite != null)
                {
                    itemIcons[slotIndex].sprite = itemSprite;
                    itemIcons[slotIndex].gameObject.SetActive(true);
                }
                else
                {
                    itemIcons[slotIndex].gameObject.SetActive(false);
                }
            }
        }

        public void ClearSlot(int slotIndex)
        {
            SetSlotItem(slotIndex, null);
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

        private void CacheSlotReferences()
        {
            Transform panel = transform.Find("Hotbar_Panel");
            if (panel == null) panel = transform;

            for (int i = 0; i < 4; i++)
            {
                Transform slot = panel.Find($"Slot_{i}");
                if (slot != null)
                {
                    slotRects[i] = slot.GetComponent<RectTransform>();
                    slotBackgrounds[i] = slot.GetComponent<Image>();

                    Transform icon = slot.Find("Item_Icon");
                    if (icon != null)
                    {
                        itemIcons[i] = icon.GetComponent<Image>();
                    }
                }
            }
        }
    }
}
