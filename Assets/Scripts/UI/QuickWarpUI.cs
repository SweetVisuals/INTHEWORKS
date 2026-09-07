using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IsometricGame.Environment;
using IsometricGame.Tilemap;
using IsometricGame.Player;

namespace IsometricGame.UI
{
    /// <summary>
    /// Displays a circular B button loading animation in the top-right of the HUD
    /// when the user holds the 'B' key/button.
    /// Holding B for 3 seconds plays the animation once from 0% to 100%,
    /// and upon completion teleports the player back to the spawn point (the house).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class QuickWarpUI : MonoBehaviour
    {
        private static QuickWarpUI instance;
        public static QuickWarpUI Instance => instance;

        [Header("Timing Settings")]
        [Tooltip("Hold duration in seconds required to complete the warp.")]
        [SerializeField] private float holdDuration = 3.0f;

        [Tooltip("Fade transition speed when showing or hiding the HUD icon.")]
        [SerializeField] private float fadeSpeed = 12.0f;

        [Header("Sprites")]
        [Tooltip("The 10 frames of the b press loading animation.")]
        [SerializeField] private Sprite[] warpFrames;

        [Header("UI References")]
        [SerializeField] private Image buttonImage;
        [SerializeField] private CanvasGroup canvasGroup;

        private float currentHoldTime = 0f;
        private bool hasWarped = false;
        private float targetAlpha = 0f;

        public float HoldDuration { get => holdDuration; set => holdDuration = Mathf.Max(0.5f, value); }
        public Sprite[] WarpFrames { get => warpFrames; set => warpFrames = value; }
        public float CurrentProgress => Mathf.Clamp01(currentHoldTime / holdDuration);

        private void Awake()
        {
            if (instance == null) instance = this;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            EnsureComponents();
            EnsureSpritesLoaded();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
            EnsureSpritesLoaded();
        }

        public void InitializeComponents(Sprite[] frames = null)
        {
            if (frames != null && frames.Length > 0)
            {
                warpFrames = frames;
            }
            EnsureComponents();
            EnsureSpritesLoaded();
        }

        private void EnsureComponents()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            if (buttonImage == null)
            {
                buttonImage = GetComponent<Image>();
                if (buttonImage == null)
                {
                    Transform child = transform.Find("Button_Icon");
                    if (child != null)
                    {
                        buttonImage = child.GetComponent<Image>();
                    }
                    else
                    {
                        GameObject iconObj = new GameObject("Button_Icon", typeof(RectTransform), typeof(Image));
                        iconObj.transform.SetParent(transform, false);
                        RectTransform rt = iconObj.GetComponent<RectTransform>();
                        rt.anchorMin = Vector2.zero;
                        rt.anchorMax = Vector2.one;
                        rt.offsetMin = Vector2.zero;
                        rt.offsetMax = Vector2.zero;
                        buttonImage = iconObj.GetComponent<Image>();
                    }
                }
            }

            if (buttonImage != null)
            {
                buttonImage.preserveAspect = true;
                buttonImage.raycastTarget = false;
            }
        }

        public void EnsureSpritesLoaded()
        {
            if (warpFrames == null || warpFrames.Length == 0 || warpFrames[0] == null)
            {
                warpFrames = UISpriteUtility.LoadSpriteFrames("Assets/Sprites/GUI/b press animation.png", 32, 32, 10);
                if (warpFrames == null || warpFrames.Length == 0 || warpFrames[0] == null)
                {
#if UNITY_EDITOR
                    var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/GUI/b press animation.png");
                    var list = new System.Collections.Generic.List<Sprite>();
                    foreach (var a in allAssets)
                    {
                        if (a is Sprite s) list.Add(s);
                    }
                    if (list.Count > 0)
                    {
                        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                        warpFrames = list.ToArray();
                    }
#endif
                }
            }

            if (buttonImage != null && warpFrames != null && warpFrames.Length > 0 && buttonImage.sprite == null)
            {
                buttonImage.sprite = warpFrames[0];
            }
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            bool canWarp = CanProcessWarpInput();
            bool isHoldingB = canWarp && IsHoldingBKey();

            if (isHoldingB)
            {
                if (!hasWarped)
                {
                    targetAlpha = 1.0f;
                    currentHoldTime += Time.deltaTime;
                    float progress = Mathf.Clamp01(currentHoldTime / holdDuration);

                    UpdateAnimationFrame(progress);

                    if (progress >= 1.0f)
                    {
                        ExecuteHomeWarp();
                    }
                }
            }
            else
            {
                // Released or cannot warp
                currentHoldTime = 0f;
                hasWarped = false;
                targetAlpha = 0f;
                UpdateAnimationFrame(0f);
            }

            // Smooth fade transition
            if (canvasGroup != null)
            {
                if (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
                {
                    canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                }
            }
        }

        private void UpdateAnimationFrame(float progress)
        {
            if (buttonImage == null || warpFrames == null || warpFrames.Length == 0) return;

            int frameCount = warpFrames.Length;
            int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * frameCount), 0, frameCount - 1);

            if (warpFrames[frameIndex] != null)
            {
                buttonImage.sprite = warpFrames[frameIndex];
            }
        }

        private void ExecuteHomeWarp()
        {
            hasWarped = true;
            targetAlpha = 0f;
            currentHoldTime = 0f;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }

            // Target room spawn center
            Vector2 targetSpawnWorld = Vector2.zero;
            var worldMap = FindAnyObjectByType<IsometricWorldMap>();
            if (worldMap != null)
            {
                targetSpawnWorld = worldMap.GetRoomCenterWorld();
            }

            // Smooth transition through ZoneTransitionManager
            if (ZoneTransitionManager.Instance != null)
            {
                ZoneTransitionManager.Instance.TransitionTo(targetSpawnWorld, isOutdoors: false);
            }
            else if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.TeleportTo(targetSpawnWorld);
                if (worldMap != null)
                {
                    worldMap.SetZoneActive(false);
                }
            }
        }

        private bool CanProcessWarpInput()
        {
            // Do not process while already transitioning or sleeping
            if (ZoneTransitionManager.Instance != null && ZoneTransitionManager.Instance.IsTransitioning)
            {
                return false;
            }

            if (SleepTransitionUI.Instance != null && (SleepTransitionUI.Instance.IsSleeping || SleepTransitionUI.Instance.IsTransitioning))
            {
                return false;
            }

            // Do not trigger if typing in a text field
            if (EventSystem.current != null)
            {
                GameObject selected = EventSystem.current.currentSelectedGameObject;
                if (selected != null)
                {
                    if (selected.GetComponent<InputField>() != null) return false;
                    var tmpInput = selected.GetComponent("TMP_InputField");
                    if (tmpInput != null) return false;
                }
            }

            return true;
        }

        private bool IsHoldingBKey()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.bKey.isPressed)
            {
                return true;
            }
            if (UnityEngine.InputSystem.Gamepad.current != null && UnityEngine.InputSystem.Gamepad.current.bButton.isPressed)
            {
                return true;
            }
#endif
            try
            {
                return Input.GetKey(KeyCode.B);
            }
            catch
            {
                return false;
            }
        }
    }
}
