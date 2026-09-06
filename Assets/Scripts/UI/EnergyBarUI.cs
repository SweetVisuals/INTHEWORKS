using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IsometricGame.UI
{
    /// <summary>
    /// Energy / Stamina Bar HUD positioned at the Top-Right of the screen.
    /// Clean side-by-side layout matching the Money HUD style:
    /// - Yellow Star Icon on the left (vertically centered).
    /// - Horizontal Orange Fill Bar on the right (centered inside the card panel).
    /// - Smooth lerped fill transitions and auto-regeneration/sleep restoration hooks.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class EnergyBarUI : MonoBehaviour
    {
        private static EnergyBarUI instance;
        public static EnergyBarUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindAnyObjectByType<EnergyBarUI>();
                    if (instance == null)
                    {
                        EnsureCanvasAndMoneyUI.EnsureAllUI();
                        instance = UnityEngine.Object.FindAnyObjectByType<EnergyBarUI>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Sprites")]
        [SerializeField] private Sprite barEmptySprite;
        [SerializeField] private Sprite barFillSprite;

        public Sprite BarEmptySprite { get => barEmptySprite; set { barEmptySprite = value; if (barEmptyImage != null && value != null) barEmptyImage.sprite = value; } }
        public Sprite BarFillSprite { get => barFillSprite; set { barFillSprite = value; if (barFillImage != null && value != null) barFillImage.sprite = value; } }

        // Backwards compatibility properties
        public Sprite CardBackgroundSprite { get => null; set { } }
        public Sprite StarIconSprite { get => barEmptySprite; set { } }
        public Sprite BarFullSprite { get => barFillSprite; set => BarFillSprite = value; }

        [Header("UI References")]
        [SerializeField] private Image barEmptyImage;
        [SerializeField] private Image barFillImage;
        [SerializeField] private RectTransform containerRect;

        [Header("Positioning and Sizing")]
        [SerializeField] private Vector2 hudAnchor = new Vector2(1f, 1f); // Top-Right
        [SerializeField] private Vector2 hudPivot = new Vector2(1f, 1f);
        [SerializeField] private Vector2 hudPosition = new Vector2(-24f, -114f);
        [SerializeField] private Vector2 barPixelSize = new Vector2(244f, 28f); // 61x7 @ 4x scale

        [Header("Energy Stats")]
        [SerializeField] private float maxEnergy = 100f;
        [SerializeField] private float currentEnergy = 100f;
        [SerializeField] private float fillLerpSpeed = 8f;

        [Header("Regeneration and Drain")]
        [SerializeField] private bool autoRegenerate = false;
        [SerializeField] private float regenRate = 5f;
        [SerializeField] private float regenDelay = 1.2f;

        private float targetFill = 1.0f;
        private float displayedFill = 1.0f;
        private float lastDrainTime = 0f;

        public float CurrentEnergy => currentEnergy;
        public float MaxEnergy => maxEnergy;
        public float NormalizedEnergy => Mathf.Clamp01(currentEnergy / Mathf.Max(1f, maxEnergy));

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
            // Auto-migrate stale positions / sizes from earlier versions
            if (hudPosition.y > -80f || barPixelSize.y > 30f)
            {
                hudPosition = new Vector2(-24f, -114f);
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

            // Remove any card background or shadow on root if present
            Image rootImage = GetComponent<Image>();
            if (rootImage != null)
            {
                if (Application.isPlaying) Destroy(rootImage);
                else DestroyImmediate(rootImage);
            }
            Shadow rootShadow = GetComponent<Shadow>();
            if (rootShadow != null)
            {
                if (Application.isPlaying) Destroy(rootShadow);
                else DestroyImmediate(rootShadow);
            }

            EnsureSpritesLoaded();
            SetupHierarchy();
            SetEnergy(currentEnergy, maxEnergy, true);
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (barEmptySprite == null)
            {
                barEmptySprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/energy bar empty.png");
            }
            if (barFillSprite == null)
            {
                barFillSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/energy bar yellow fill.png");
            }
#endif
        }

        public void SetupHierarchy()
        {
            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            EnsureSpritesLoaded();

            // Clean up any legacy children
            Transform legacyRow = transform.Find("Content_Row");
            if (legacyRow != null)
            {
                if (Application.isPlaying) Destroy(legacyRow.gameObject);
                else DestroyImmediate(legacyRow.gameObject);
            }
            Transform legacyFrame = transform.Find("Bar_Frame");
            if (legacyFrame != null)
            {
                if (Application.isPlaying) Destroy(legacyFrame.gameObject);
                else DestroyImmediate(legacyFrame.gameObject);
            }

            // 1. Layer 1: Empty Bar Sprite (Base Frame + Star Icon on left)
            Transform emptyTrans = transform.Find("Bar_Empty_Base");
            GameObject emptyObj;
            if (emptyTrans != null)
            {
                emptyObj = emptyTrans.gameObject;
            }
            else
            {
                emptyObj = new GameObject("Bar_Empty_Base", typeof(RectTransform), typeof(Image));
                emptyObj.transform.SetParent(transform, false);
            }

            RectTransform emptyRt = emptyObj.transform as RectTransform ?? emptyObj.GetComponent<RectTransform>() ?? emptyObj.AddComponent<RectTransform>();
            if (emptyRt != null)
            {
                emptyRt.anchorMin = Vector2.zero;
                emptyRt.anchorMax = Vector2.one;
                emptyRt.offsetMin = Vector2.zero;
                emptyRt.offsetMax = Vector2.zero;
            }

            barEmptyImage = emptyObj.GetComponent<Image>() ?? emptyObj.AddComponent<Image>();
            barEmptyImage.raycastTarget = false;
            barEmptyImage.type = Image.Type.Simple;
            barEmptyImage.preserveAspect = false;
            barEmptyImage.color = Color.white;
            if (barEmptySprite != null) barEmptyImage.sprite = barEmptySprite;

            // 2. Layer 2: Yellow Fill Sprite (Horizontal Fill over the frame)
            Transform fillTrans = transform.Find("Bar_Fill_Overlay");
            GameObject fillObj;
            if (fillTrans != null)
            {
                fillObj = fillTrans.gameObject;
            }
            else
            {
                fillObj = new GameObject("Bar_Fill_Overlay", typeof(RectTransform), typeof(Image));
                fillObj.transform.SetParent(transform, false);
            }

            RectTransform fillRt = fillObj.transform as RectTransform ?? fillObj.GetComponent<RectTransform>() ?? fillObj.AddComponent<RectTransform>();
            if (fillRt != null)
            {
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = Vector2.zero;
                fillRt.offsetMax = Vector2.zero;
            }

            barFillImage = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
            barFillImage.raycastTarget = false;
            barFillImage.type = Image.Type.Filled;
            barFillImage.fillMethod = Image.FillMethod.Horizontal;
            barFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFillImage.preserveAspect = false;
            barFillImage.color = Color.white;
            if (barFillSprite != null) barFillImage.sprite = barFillSprite;

            ApplyFillAmount(displayedFill);
        }

        private void Update()
        {
            if (autoRegenerate && Time.time >= lastDrainTime + regenDelay && currentEnergy < maxEnergy)
            {
                currentEnergy = Mathf.MoveTowards(currentEnergy, maxEnergy, regenRate * Time.deltaTime);
                targetFill = NormalizedEnergy;
            }

            if (barFillImage != null)
            {
                displayedFill = Mathf.MoveTowards(displayedFill, targetFill, fillLerpSpeed * Time.deltaTime);
                ApplyFillAmount(displayedFill);
            }
        }

        /// <summary>
        /// Maps normalized energy (0..1) precisely to the yellow fill track (X:13..57 of 61).
        /// </summary>
        private void ApplyFillAmount(float normalized)
        {
            if (barFillImage == null) return;

            if (normalized <= 0.001f)
            {
                barFillImage.fillAmount = 0f;
            }
            else
            {
                // Track spans from pixel 13 to 57 out of 61 total canvas width
                float mapped = (13f + normalized * 44f) / 61f;
                barFillImage.fillAmount = Mathf.Clamp01(mapped);
            }
        }

        /// <summary>
        /// Sets the energy amount and maximum value.
        /// </summary>
        public void SetEnergy(float current, float max, bool instant = false)
        {
            maxEnergy = Mathf.Max(1f, max);
            currentEnergy = Mathf.Clamp(current, 0f, maxEnergy);
            targetFill = NormalizedEnergy;

            if (instant)
            {
                displayedFill = targetFill;
                ApplyFillAmount(displayedFill);
            }
        }

        /// <summary>
        /// Consumes energy.
        /// </summary>
        public void UseEnergy(float amount)
        {
            currentEnergy = Mathf.Clamp(currentEnergy - amount, 0f, maxEnergy);
            targetFill = NormalizedEnergy;
            lastDrainTime = Time.time;
        }

        /// <summary>
        /// Restores energy.
        /// </summary>
        public void RestoreEnergy(float amount, bool instant = false)
        {
            currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);
            targetFill = NormalizedEnergy;
            if (instant)
            {
                displayedFill = targetFill;
                ApplyFillAmount(displayedFill);
            }
        }

        /// <summary>
        /// Fully restores energy to maximum (e.g. after sleeping).
        /// </summary>
        public void RestoreFullEnergy(bool instant = false)
        {
            RestoreEnergy(maxEnergy, instant);
        }
    }
}
