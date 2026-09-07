using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using IsometricGame.Player;

namespace IsometricGame.UI
{
    /// <summary>
    /// Manages the full-screen Sleep transition sequence.
    /// Smoothly fades the screen to black, animates the 3-frame pixel-art 'Sleeping...' text,
    /// locks player movement, and gently fades back to daytime upon waking up.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SleepTransitionUI : MonoBehaviour
    {
        private static SleepTransitionUI instance;
        public static SleepTransitionUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SleepTransitionUI>();
                    if (instance == null)
                    {
                        EnsureCanvasAndMoneyUI.EnsureAllUI();
                        instance = FindAnyObjectByType<SleepTransitionUI>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Sleeping Text Animation Frames")]
        public Sprite[] sleepingFrames;

        [Header("Timing & Speed")]
        [Tooltip("Seconds to fade the screen to black.")]
        [SerializeField] private float fadeInDuration = 0.75f;
        [Tooltip("Seconds to remain asleep while text animates.")]
        [SerializeField] private float sleepDuration = 2.4f;
        [Tooltip("Seconds to fade back to daytime.")]
        [SerializeField] private float fadeOutDuration = 0.75f;
        [Tooltip("Playback speed of the 3-frame sleeping text animation in FPS.")]
        [SerializeField] private float animationFps = 2.8f;

        [Header("UI Element Sizes")]
        [SerializeField] private Vector2 sleepingTextPixelSize = new Vector2(192f, 64f); // 48x16 at 4x scale

        private CanvasGroup canvasGroup;
        private Image blackOverlayImage;
        private Image sleepingTextImage;
        private Coroutine activeSleepRoutine;
        private bool isSleeping = false;

        public bool IsSleeping => isSleeping;
        public bool IsTransitioning => isSleeping;

        private void Awake()
        {
            instance = this;
            InitializeComponents();
        }

        private void OnEnable()
        {
            instance = this;
        }

        public void InitializeComponents()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            EnsureSpritesLoaded();
            SetupHierarchy();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (sleepingFrames == null || sleepingFrames.Length < 3 || sleepingFrames[0] == null)
            {
                sleepingFrames = new Sprite[3];
                sleepingFrames[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 1.png");
                sleepingFrames[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 2.png");
                sleepingFrames[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/sleeping text frame 3.png");
            }
#endif
        }

        private void SetupHierarchy()
        {
            RectTransform rootRt = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            // 1. Fullscreen Black Overlay
            if (blackOverlayImage == null)
            {
                Transform bgTrans = transform.Find("Black_Overlay");
                GameObject bgObj;
                if (bgTrans != null)
                {
                    bgObj = bgTrans.gameObject;
                }
                else
                {
                    bgObj = new GameObject("Black_Overlay", typeof(RectTransform), typeof(Image));
                    bgObj.transform.SetParent(transform, false);
                }

                RectTransform rt = bgObj.transform as RectTransform ?? bgObj.GetComponent<RectTransform>() ?? bgObj.AddComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }

                blackOverlayImage = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
                blackOverlayImage.color = Color.black;
                blackOverlayImage.raycastTarget = false;
            }

            // 2. Centered Sleeping Text Image
            if (sleepingTextImage == null)
            {
                Transform textTrans = transform.Find("Sleeping_Text_Image");
                GameObject textObj;
                if (textTrans != null)
                {
                    textObj = textTrans.gameObject;
                }
                else
                {
                    textObj = new GameObject("Sleeping_Text_Image", typeof(RectTransform), typeof(Image));
                    textObj.transform.SetParent(transform, false);
                }

                RectTransform rt = textObj.transform as RectTransform ?? textObj.GetComponent<RectTransform>() ?? textObj.AddComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = sleepingTextPixelSize;
                }

                sleepingTextImage = textObj.GetComponent<Image>() ?? textObj.AddComponent<Image>();
                sleepingTextImage.raycastTarget = false;
                sleepingTextImage.preserveAspect = true;
                if (sleepingFrames != null && sleepingFrames.Length > 0 && sleepingFrames[0] != null)
                {
                    sleepingTextImage.sprite = sleepingFrames[0];
                }
                sleepingTextImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>
        /// Triggers the full sleep sequence.
        /// </summary>
        public void PlaySleepSequence(Action onComplete = null)
        {
            if (isSleeping) return;

            if (activeSleepRoutine != null) StopCoroutine(activeSleepRoutine);
            activeSleepRoutine = StartCoroutine(SleepRoutine(onComplete));
        }

        private IEnumerator SleepRoutine(Action onComplete)
        {
            isSleeping = true;

            // 1. Lock player input
            IsometricPlayerController player = FindAnyObjectByType<IsometricPlayerController>();
            if (player != null) player.SetInputEnabled(false);

            // 2. Enable canvas group blocking
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            // 3. Fade black overlay in (0 -> 1)
            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                if (canvasGroup != null) canvasGroup.alpha = alpha;
                yield return null;
            }
            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // 4. Fade sleeping text in (0 -> 1)
            float textFadeTime = 0.35f;
            elapsed = 0f;
            while (elapsed < textFadeTime)
            {
                elapsed += Time.deltaTime;
                float textAlpha = Mathf.Clamp01(elapsed / textFadeTime);
                if (sleepingTextImage != null) sleepingTextImage.color = new Color(1f, 1f, 1f, textAlpha);
                yield return null;
            }
            if (sleepingTextImage != null) sleepingTextImage.color = Color.white;

            // 5. Animate 3-frame sleeping text during sleep duration
            float sleepTimer = 0f;
            float frameTimer = 0f;
            int frameIndex = 0;
            float frameInterval = 1f / Mathf.Max(1f, animationFps);

            while (sleepTimer < sleepDuration)
            {
                sleepTimer += Time.deltaTime;
                frameTimer += Time.deltaTime;

                if (frameTimer >= frameInterval)
                {
                    frameTimer -= frameInterval;
                    if (sleepingFrames != null && sleepingFrames.Length > 0)
                    {
                        frameIndex = (frameIndex + 1) % sleepingFrames.Length;
                        if (sleepingTextImage != null && sleepingFrames[frameIndex] != null)
                        {
                            sleepingTextImage.sprite = sleepingFrames[frameIndex];
                        }
                    }
                }
                yield return null;
            }

            // 6. Fade sleeping text out (1 -> 0)
            elapsed = 0f;
            while (elapsed < textFadeTime)
            {
                elapsed += Time.deltaTime;
                float textAlpha = Mathf.Clamp01(1f - (elapsed / textFadeTime));
                if (sleepingTextImage != null) sleepingTextImage.color = new Color(1f, 1f, 1f, textAlpha);
                yield return null;
            }
            if (sleepingTextImage != null) sleepingTextImage.color = new Color(1f, 1f, 1f, 0f);

            // 7. Fade black overlay out (1 -> 0)
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
                if (canvasGroup != null) canvasGroup.alpha = alpha;
                yield return null;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            // 8. Restore full energy / stamina
            EnergyBarUI.Instance?.RestoreFullEnergy();

            // 9. Re-enable player input
            if (player != null) player.SetInputEnabled(true);

            isSleeping = false;
            activeSleepRoutine = null;

            onComplete?.Invoke();
        }
    }
}
