using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IsometricGame.UI
{
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class XpBarUI : MonoBehaviour
    {
        private static XpBarUI instance;
        public static XpBarUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindAnyObjectByType<XpBarUI>();
                    if (instance == null)
                    {
                        EnsureCanvasAndMoneyUI.EnsureAllUI();
                        instance = UnityEngine.Object.FindAnyObjectByType<XpBarUI>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Sprites")]
        [SerializeField] private Sprite xpBarEmptySprite;
        [SerializeField] private Sprite xpBarFillSprite;

        public Sprite XpBarEmptySprite
        {
            get => xpBarEmptySprite;
            set
            {
                xpBarEmptySprite = value;
                if (xpBarEmptyImage != null && value != null) xpBarEmptyImage.sprite = value;
            }
        }

        public Sprite XpBarFillSprite
        {
            get => xpBarFillSprite;
            set
            {
                xpBarFillSprite = value;
                if (xpBarFillImage != null && value != null) xpBarFillImage.sprite = value;
            }
        }

        [Header("UI References")]
        [SerializeField] private Image xpBarEmptyImage;
        [SerializeField] private Image xpBarFillImage;
        [SerializeField] private RectTransform containerRect;

        [Header("Positioning and Sizing")]
        [SerializeField] private Vector2 hudAnchor = new Vector2(1f, 1f);
        [SerializeField] private Vector2 hudPivot = new Vector2(1f, 1f);
        [SerializeField] private Vector2 hudPosition = new Vector2(-24f, -82f);
        [SerializeField] private Vector2 barPixelSize = new Vector2(244f, 28f); // 61x7 @ 4x

        [Header("XP Stats")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private float currentXp = 0f;
        [SerializeField] private float maxXp = 100f;
        [SerializeField] private float fillLerpSpeed = 8f;

        private float targetFill = 0f;
        private float displayedFill = 0f;

        public int CurrentLevel => currentLevel;
        public float CurrentXp => currentXp;
        public float MaxXp => maxXp;
        public float NormalizedXp => Mathf.Clamp01(currentXp / Mathf.Max(1f, maxXp));

        private void Awake()
        {
            instance = this;
            InitializeComponents();
        }

        private void OnEnable()
        {
            instance = this;
            InitializeComponents();
        }

        public void SetLayout(Vector2 pos, Vector2 size)
        {
            hudPosition = pos;
            barPixelSize = size;
            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = hudAnchor;
                containerRect.anchorMax = hudAnchor;
                containerRect.pivot = hudPivot;
                containerRect.anchoredPosition = hudPosition;
                containerRect.sizeDelta = barPixelSize;
            }
        }

        public void InitializeComponents()
        {
            // Auto-migrate stale positions / sizes
            if (hudPosition.y > -50f || hudPosition.y < -100f || barPixelSize.y > 30f)
            {
                hudPosition = new Vector2(-24f, -82f);
                barPixelSize = new Vector2(244f, 28f);
            }

            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = hudAnchor;
                containerRect.anchorMax = hudAnchor;
                containerRect.pivot = hudPivot;
                containerRect.anchoredPosition = hudPosition;
                containerRect.sizeDelta = barPixelSize;
            }

            EnsureSpritesLoaded();
            SetupHierarchy();
            SetXp(currentXp, maxXp, currentLevel, true);
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (xpBarEmptySprite == null)
            {
                xpBarEmptySprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar empty new.png")
                                ?? UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar empty.png");
            }
            if (xpBarFillSprite == null)
            {
                xpBarFillSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar green fill.png")
                               ?? UISpriteUtility.LoadSprite("Assets/Sprites/GUI/xp bar filling green.png");
            }
#endif
        }

        public void SetupHierarchy()
        {
            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            EnsureSpritesLoaded();

            Transform emptyTrans = transform.Find("XP_Bar_Empty_Base");
            GameObject emptyObj = emptyTrans != null ? emptyTrans.gameObject : new GameObject("XP_Bar_Empty_Base", typeof(RectTransform), typeof(Image));
            if (emptyTrans == null) emptyObj.transform.SetParent(transform, false);

            RectTransform emptyRt = emptyObj.transform as RectTransform ?? emptyObj.GetComponent<RectTransform>() ?? emptyObj.AddComponent<RectTransform>();
            if (emptyRt != null)
            {
                emptyRt.anchorMin = Vector2.zero;
                emptyRt.anchorMax = Vector2.one;
                emptyRt.offsetMin = Vector2.zero;
                emptyRt.offsetMax = Vector2.zero;
            }

            xpBarEmptyImage = emptyObj.GetComponent<Image>() ?? emptyObj.AddComponent<Image>();
            xpBarEmptyImage.raycastTarget = false;
            xpBarEmptyImage.type = Image.Type.Simple;
            xpBarEmptyImage.preserveAspect = false;
            xpBarEmptyImage.color = Color.white;
            if (xpBarEmptySprite != null) xpBarEmptyImage.sprite = xpBarEmptySprite;

            Transform fillTrans = transform.Find("XP_Bar_Fill_Overlay");
            GameObject fillObj = fillTrans != null ? fillTrans.gameObject : new GameObject("XP_Bar_Fill_Overlay", typeof(RectTransform), typeof(Image));
            if (fillTrans == null) fillObj.transform.SetParent(transform, false);

            RectTransform fillRt = fillObj.transform as RectTransform ?? fillObj.GetComponent<RectTransform>() ?? fillObj.AddComponent<RectTransform>();
            if (fillRt != null)
            {
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
            }

            xpBarFillImage = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
            xpBarFillImage.raycastTarget = false;
            xpBarFillImage.type = Image.Type.Filled;
            xpBarFillImage.fillMethod = Image.FillMethod.Horizontal;
            xpBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            xpBarFillImage.preserveAspect = false;
            xpBarFillImage.color = Color.white;
            if (xpBarFillSprite != null) xpBarFillImage.sprite = xpBarFillSprite;

            ApplyFillAmount(displayedFill);
        }

        private void Update()
        {
            if (xpBarFillImage != null)
            {
                displayedFill = Mathf.MoveTowards(displayedFill, targetFill, fillLerpSpeed * Time.deltaTime);
                ApplyFillAmount(displayedFill);
            }
        }

        private void ApplyFillAmount(float normalized)
        {
            if (xpBarFillImage == null) return;

            if (normalized <= 0.001f)
            {
                xpBarFillImage.fillAmount = 0f;
            }
            else
            {
                float mapped = (13f + normalized * 44f) / 61f;
                xpBarFillImage.fillAmount = Mathf.Clamp01(mapped);
            }
        }

        public void SetXp(float current, float max, int level = 1, bool instant = false)
        {
            maxXp = Mathf.Max(1f, max);
            currentXp = Mathf.Clamp(current, 0f, maxXp);
            currentLevel = Mathf.Max(1, level);
            targetFill = NormalizedXp;

            if (instant)
            {
                displayedFill = targetFill;
                ApplyFillAmount(displayedFill);
            }
        }

        public void AddXp(float amount)
        {
            currentXp += amount;
            while (currentXp >= maxXp)
            {
                currentXp -= maxXp;
                currentLevel++;
                maxXp = Mathf.Round(maxXp * 1.25f);
            }
            targetFill = NormalizedXp;
        }
    }
}
