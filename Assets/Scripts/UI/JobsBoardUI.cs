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
    /// Manages the full Jobs Board Modal UI triggered from the Computer Desk.
    /// Features:
    /// - Fullscreen dimmed/blurred backdrop overlay.
    /// - Plays the 7-frame pixel-art board & option grid unfolding open animation.
    /// - 3 interactive Job Card buttons with hover highlights, click punch animations, and custom task text overlay.
    /// - Smooth open and close transitions with Escape/E or backdrop click support.
    /// - Disables player locomotion and world hovers while active.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class JobsBoardUI : MonoBehaviour
    {
        public static JobsBoardUI Instance { get; private set; }

        [Header("Board Sprites (65x80)")]
        [SerializeField] private Sprite boardDefaultSprite;
        [SerializeField] private Sprite[] boardOpenFrames;

        [Header("Job Cards Overlay Sprites (65x80)")]
        [SerializeField] private Sprite jobCardsFullSprite;
        [SerializeField] private Sprite[] jobCardSprites = new Sprite[3];

        [Header("Backdrop Color")]
        [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.085f, 0.82f);

        [Header("Animation Timings")]
        [SerializeField] private float boardAnimFps = 16f;
        [SerializeField] private float cardStaggerDelay = 0.08f;
        [SerializeField] private float cardEnterDuration = 0.16f;
        [SerializeField] private float transitionDuration = 0.22f;

        [Header("Default Job Listings")]
        [SerializeField] private string[] defaultJobTitles = new string[]
        {
            "LUMBERJACK BOUNTY",
            "HARVEST WILD HERBS",
            "EXPLORE PINE FOREST"
        };

        [SerializeField] private string[] defaultJobRewards = new string[]
        {
            "+50 COINS",
            "+35 COINS",
            "+60 COINS"
        };

        [Header("Events")]
        public UnityEvent onBoardOpened;
        public UnityEvent onBoardClosed;
        public UnityEvent<int> onJobSelected;

        private CanvasGroup canvasGroup;
        private RectTransform containerRect;
        private Image backdropImage;
        private Image boardImage;
        private GameObject[] jobCardButtons = new GameObject[3];
        private CanvasGroup[] jobCardCanvasGroups = new CanvasGroup[3];
        private Text[] jobTitleTexts = new Text[3];
        private Text[] jobRewardTexts = new Text[3];

        private bool isOpen = false;
        private Coroutine activeTransitionRoutine;
        private Coroutine activeAnimRoutine;

        public bool IsOpen => isOpen;
        public static bool IsJobsBoardOpen => Instance != null && Instance.isOpen;

        // Card vertical centers and pixel metrics on 65x80 texture scaled by 4.5x
        // Card 0 (top): Y=23..34 (center 28.5) -> (40 - 28.5) * 4.5 = +51.75
        // Card 1 (mid): Y=38..49 (center 43.5) -> (40 - 43.5) * 4.5 = -15.75
        // Card 2 (bot): Y=53..64 (center 58.5) -> (40 - 58.5) * 4.5 = -83.25
        private static readonly float[] CardYOffsets = new float[] { 51.75f, -15.75f, -83.25f };
        private const float CardWidth = 220.5f; // 49 * 4.5
        private const float CardHeight = 54.0f;  // 12 * 4.5

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
            if (boardDefaultSprite == null)
            {
                boardDefaultSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/jobs board empty.png");
            }
            if (boardOpenFrames == null || boardOpenFrames.Length == 0 || boardOpenFrames[0] == null)
            {
                boardOpenFrames = UISpriteUtility.LoadSpriteFrames("Assets/Sprites/GUI/jobs board open animation empty.png", 65, 80, 7);
            }
            if (jobCardsFullSprite == null)
            {
                jobCardsFullSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/job cards for text overlay (1).png");
            }

            // Slice 3 individual cards from the 65x80 job cards texture
            // Unity Y from bottom: top=46..58, mid=31..43, bot=16..28
            Rect[] cardRects = new Rect[]
            {
                new Rect(8, 46, 49, 12), // Card 0 (top)
                new Rect(8, 31, 49, 12), // Card 1 (middle)
                new Rect(8, 16, 49, 12)  // Card 2 (bottom)
            };

            for (int i = 0; i < 3; i++)
            {
                if (jobCardSprites[i] == null)
                {
                    jobCardSprites[i] = UISpriteUtility.LoadSpriteRect("Assets/Sprites/GUI/job cards for text overlay (1).png", cardRects[i]);
                }
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
            Transform bg = transform.Find("Jobs_Backdrop");
            GameObject bgObj = bg != null ? bg.gameObject : new GameObject("Jobs_Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            if (bg == null) bgObj.transform.SetParent(transform, false);

            RectTransform rtBg = bgObj.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;

            backdropImage = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
            backdropImage.color = backdropColor;
            backdropImage.raycastTarget = true;

            Button bgBtn = bgObj.GetComponent<Button>() ?? bgObj.AddComponent<Button>();
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.RemoveAllListeners();
            bgBtn.onClick.AddListener(Close);

            // 2. Centered Jobs Board Container (65x80 @ 4.5x scale = 292.5 x 360)
            Transform container = transform.Find("Jobs_Board_Container");
            GameObject containerObj = container != null ? container.gameObject : new GameObject("Jobs_Board_Container", typeof(RectTransform));
            if (container == null) containerObj.transform.SetParent(transform, false);

            containerRect = containerObj.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(292.5f, 360f);
            containerRect.anchoredPosition = Vector2.zero;

            // Remove legacy option grid if present
            Transform oldGrid = containerObj.transform.Find("Option_Grid_Image");
            if (oldGrid != null)
            {
                if (Application.isPlaying) Destroy(oldGrid.gameObject);
                else DestroyImmediate(oldGrid.gameObject);
            }

            // 3. Board Image
            Transform boardTr = containerObj.transform.Find("Board_Image");
            GameObject boardObj = boardTr != null ? boardTr.gameObject : new GameObject("Board_Image", typeof(RectTransform), typeof(Image));
            if (boardTr == null) boardObj.transform.SetParent(containerObj.transform, false);

            RectTransform rtBoard = boardObj.GetComponent<RectTransform>();
            rtBoard.anchorMin = Vector2.zero;
            rtBoard.anchorMax = Vector2.one;
            rtBoard.offsetMin = Vector2.zero;
            rtBoard.offsetMax = Vector2.zero;

            boardImage = boardObj.GetComponent<Image>() ?? boardObj.AddComponent<Image>();
            boardImage.sprite = boardDefaultSprite;
            boardImage.type = Image.Type.Simple;
            boardImage.preserveAspect = true;
            boardImage.raycastTarget = false;

            // 4. Setup 3 Individual Job Cards
            SetupJobCardButtons(containerObj);
        }

        private void SetupJobCardButtons(GameObject parent)
        {
            Font pixelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < 3; i++)
            {
                string cardName = $"Job_Card_Button_{i}";
                Transform cardTr = parent.transform.Find(cardName);
                GameObject cardObj = cardTr != null ? cardTr.gameObject : new GameObject(cardName, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
                if (cardTr == null) cardObj.transform.SetParent(parent.transform, false);

                jobCardButtons[i] = cardObj;
                jobCardCanvasGroups[i] = cardObj.GetComponent<CanvasGroup>() ?? cardObj.AddComponent<CanvasGroup>();

                RectTransform rtCard = cardObj.GetComponent<RectTransform>();
                rtCard.anchorMin = new Vector2(0.5f, 0.5f);
                rtCard.anchorMax = new Vector2(0.5f, 0.5f);
                rtCard.pivot = new Vector2(0.5f, 0.5f);
                rtCard.sizeDelta = new Vector2(CardWidth, CardHeight);
                rtCard.anchoredPosition = new Vector2(0f, CardYOffsets[i]);

                Image cardImg = cardObj.GetComponent<Image>() ?? cardObj.AddComponent<Image>();
                if (i < jobCardSprites.Length && jobCardSprites[i] != null)
                {
                    cardImg.sprite = jobCardSprites[i];
                }
                cardImg.type = Image.Type.Simple;
                cardImg.preserveAspect = true;
                cardImg.color = Color.white;
                cardImg.raycastTarget = true;

                Button cardBtn = cardObj.GetComponent<Button>() ?? cardObj.AddComponent<Button>();
                cardBtn.transition = Selectable.Transition.ColorTint;
                ColorBlock colors = cardBtn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.15f, 1.15f, 1.25f, 1f);
                colors.pressedColor = new Color(0.85f, 0.85f, 0.90f, 1f);
                colors.fadeDuration = 0.08f;
                cardBtn.colors = colors;

                int jobIndex = i;
                cardBtn.onClick.RemoveAllListeners();
                cardBtn.onClick.AddListener(() => OnJobCardClicked(jobIndex));

                // Title Text
                Transform textTr = cardObj.transform.Find("Job_Title_Text");
                GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("Job_Title_Text", typeof(RectTransform), typeof(Text));
                if (textTr == null) textObj.transform.SetParent(cardObj.transform, false);

                RectTransform rtText = textObj.GetComponent<RectTransform>();
                rtText.anchorMin = Vector2.zero;
                rtText.anchorMax = Vector2.one;
                rtText.offsetMin = new Vector2(10f, 2f);
                rtText.offsetMax = new Vector2(-10f, -2f);

                Text titleText = textObj.GetComponent<Text>() ?? textObj.AddComponent<Text>();
                if (pixelFont != null) titleText.font = pixelFont;
                titleText.text = i < defaultJobTitles.Length ? defaultJobTitles[i] : $"JOB #{i + 1}";
                titleText.fontSize = 12;
                titleText.fontStyle = FontStyle.Bold;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.color = new Color(0.92f, 0.96f, 1.0f, 1.0f);
                titleText.raycastTarget = false;
                jobTitleTexts[i] = titleText;
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

            onBoardOpened?.Invoke();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            if (IsometricPlayerController.Instance != null)
            {
                IsometricPlayerController.Instance.SetInputEnabled(true);
            }

            if (activeTransitionRoutine != null) StopCoroutine(activeTransitionRoutine);
            activeTransitionRoutine = StartCoroutine(AnimateClose());

            onBoardClosed?.Invoke();
        }

        private IEnumerator AnimateOpen()
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            // Reset cards to hidden initially
            for (int i = 0; i < 3; i++)
            {
                if (jobCardCanvasGroups[i] != null)
                {
                    jobCardCanvasGroups[i].alpha = 0f;
                    jobCardCanvasGroups[i].interactable = false;
                    jobCardCanvasGroups[i].blocksRaycasts = false;
                }
                if (jobCardButtons[i] != null)
                {
                    jobCardButtons[i].transform.localScale = Vector3.one * 0.40f;
                }
            }

            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.88f;
            Vector3 peakScale = Vector3.one * 1.04f;
            Vector3 normalScale = Vector3.one;

            // Start playing the opening frame animation in parallel
            if (activeAnimRoutine != null) StopCoroutine(activeAnimRoutine);
            activeAnimRoutine = StartCoroutine(PlayOpenAnimationAndCards());

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

        private IEnumerator PlayOpenAnimationAndCards()
        {
            // 1. Play 7-frame board unfolding animation
            if (boardOpenFrames != null && boardOpenFrames.Length > 0)
            {
                float frameDelay = 1f / boardAnimFps;
                for (int i = 0; i < boardOpenFrames.Length; i++)
                {
                    if (boardImage != null && boardOpenFrames[i] != null)
                    {
                        boardImage.sprite = boardOpenFrames[i];
                    }
                    yield return new WaitForSecondsRealtime(frameDelay);
                }
            }

            if (boardImage != null && boardDefaultSprite != null)
            {
                boardImage.sprite = boardDefaultSprite;
            }

            // 2. Animate each job card in sequentially with elastic spring bounce
            for (int i = 0; i < 3; i++)
            {
                if (jobCardButtons[i] != null && jobCardCanvasGroups[i] != null)
                {
                    StartCoroutine(AnimateSingleCardIn(jobCardButtons[i].GetComponent<RectTransform>(), jobCardCanvasGroups[i]));
                }
                yield return new WaitForSecondsRealtime(cardStaggerDelay);
            }
        }

        private IEnumerator AnimateSingleCardIn(RectTransform cardRt, CanvasGroup cardCg)
        {
            if (cardRt == null || cardCg == null) yield break;

            float elapsed = 0f;
            Vector3 startScale = Vector3.one * 0.40f;
            Vector3 peakScale = Vector3.one * 1.12f;
            Vector3 endScale = Vector3.one;

            cardCg.blocksRaycasts = true;
            cardCg.interactable = true;

            while (elapsed < cardEnterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cardEnterDuration);
                cardCg.alpha = t;

                if (t < 0.65f)
                {
                    cardRt.localScale = Vector3.Lerp(startScale, peakScale, t / 0.65f);
                }
                else
                {
                    cardRt.localScale = Vector3.Lerp(peakScale, endScale, (t - 0.65f) / 0.35f);
                }
                yield return null;
            }

            cardCg.alpha = 1f;
            cardRt.localScale = endScale;
        }

        private IEnumerator AnimateClose()
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            Vector3 startScale = containerRect != null ? containerRect.localScale : Vector3.one;
            Vector3 endScale = Vector3.one * 0.90f;

            while (elapsed < transitionDuration * 0.70f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / (transitionDuration * 0.70f));
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

                if (containerRect != null)
                {
                    containerRect.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private void OnJobCardClicked(int index)
        {
            if (index < 0 || index >= jobCardButtons.Length) return;
            if (jobCardButtons[index] != null)
            {
                StartCoroutine(PunchCard(jobCardButtons[index].GetComponent<RectTransform>()));
            }

            onJobSelected?.Invoke(index);
        }

        private IEnumerator PunchCard(RectTransform target)
        {
            if (target == null) yield break;
            Vector3 origScale = Vector3.one;
            Vector3 punchScale = Vector3.one * 1.10f;
            float elapsed = 0f;
            float duration = 0.12f;

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
    }
}
