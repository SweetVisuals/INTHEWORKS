using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IsometricGame.UI
{
    /// <summary>
    /// Clean and stylish Money HUD Controller.
    /// Displays the money balance using the money icon and pixel art numbers (0-9, commas).
    /// Features smooth counting animations, bounce effects, and insufficient funds feedback.
    /// </summary>
    public class MoneyUI : MonoBehaviour
    {
        public static MoneyUI Instance { get; private set; }

        [Header("Starting Balance")]
        [SerializeField] private long currentMoney = 25000;

#pragma warning disable CS0649
        [Header("UI References")]
        [Tooltip("The Image component displaying the money icon.")]
        [SerializeField] private Image moneyIconImage;

        [Tooltip("The pixel art numbers display component.")]
        [SerializeField] private PixelNumberDisplay pixelNumberDisplay;

        [Tooltip("Container transform for punch/bounce animation.")]
        [SerializeField] private RectTransform containerRect;
#pragma warning restore CS0649

        [Header("Animation Settings")]
        [Tooltip("Smoothly count numbers when balance changes.")]
        [SerializeField] private bool animateCounting = true;
        [SerializeField] private float countDuration = 0.5f;
        [SerializeField] private bool bounceOnChange = true;

        [Header("Visual Styling")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color gainColor = new Color(0.35f, 1.0f, 0.65f);
        [SerializeField] private Color spendColor = new Color(1.0f, 0.4f, 0.4f);

        private long displayedMoney;
        private Coroutine countCoroutine;
        private Coroutine bounceCoroutine;
        private Vector3 originalScale = Vector3.one;

        public long CurrentMoney => currentMoney;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) Destroy(gameObject);

            if (containerRect == null)
            {
                containerRect = GetComponent<RectTransform>();
            }
            if (containerRect != null)
            {
                originalScale = containerRect.localScale;
            }

            if (pixelNumberDisplay == null)
            {
                pixelNumberDisplay = GetComponentInChildren<PixelNumberDisplay>();
            }

            displayedMoney = currentMoney;
            UpdateDisplayInstant(displayedMoney);
        }

        private void Start()
        {
            UpdateDisplayInstant(currentMoney);
        }

        /// <summary>
        /// Add money to balance.
        /// </summary>
        public void AddMoney(long amount)
        {
            SetMoney(currentMoney + amount);
        }

        /// <summary>
        /// Spend money from balance if sufficient funds exist.
        /// </summary>
        public bool TrySpendMoney(long amount)
        {
            if (currentMoney >= amount)
            {
                SetMoney(currentMoney - amount);
                return true;
            }
            TriggerInsufficientFundsEffect();
            return false;
        }

        /// <summary>
        /// Directly set the balance.
        /// </summary>
        public void SetMoney(long newAmount)
        {
            long oldAmount = currentMoney;
            currentMoney = System.Math.Max(0L, newAmount);

            if (bounceOnChange)
            {
                if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
                bounceCoroutine = StartCoroutine(AnimateBounce(newAmount >= oldAmount ? gainColor : spendColor));
            }

            if (animateCounting && gameObject.activeInHierarchy)
            {
                if (countCoroutine != null) StopCoroutine(countCoroutine);
                countCoroutine = StartCoroutine(AnimateCount(displayedMoney, currentMoney));
            }
            else
            {
                displayedMoney = currentMoney;
                UpdateDisplayInstant(displayedMoney);
            }
        }

        private IEnumerator AnimateCount(long startVal, long targetVal)
        {
            float elapsed = 0f;
            while (elapsed < countDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / countDuration);
                // Ease Out Quad
                t = 1f - (1f - t) * (1f - t);

                displayedMoney = (long)Mathf.Lerp(startVal, targetVal, t);
                UpdateDisplayInstant(displayedMoney);
                yield return null;
            }

            displayedMoney = targetVal;
            UpdateDisplayInstant(displayedMoney);
        }

        private IEnumerator AnimateBounce(Color flashColor)
        {
            if (containerRect == null) yield break;

            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 punchScale = originalScale * 1.15f;

            if (pixelNumberDisplay != null)
            {
                pixelNumberDisplay.DigitColor = flashColor;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;

                if (t < 0.5f)
                {
                    containerRect.localScale = Vector3.Lerp(originalScale, punchScale, t * 2f);
                }
                else
                {
                    containerRect.localScale = Vector3.Lerp(punchScale, originalScale, (t - 0.5f) * 2f);
                }

                if (pixelNumberDisplay != null)
                {
                    pixelNumberDisplay.DigitColor = Color.Lerp(flashColor, normalColor, t);
                }

                yield return null;
            }

            containerRect.localScale = originalScale;
            if (pixelNumberDisplay != null)
            {
                pixelNumberDisplay.DigitColor = normalColor;
            }
        }

        private void TriggerInsufficientFundsEffect()
        {
            if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
            bounceCoroutine = StartCoroutine(AnimateShake());
        }

        private IEnumerator AnimateShake()
        {
            if (containerRect == null) yield break;

            Vector2 originalPos = containerRect.anchoredPosition;
            float elapsed = 0f;
            float duration = 0.3f;
            float strength = 8f;

            if (pixelNumberDisplay != null)
            {
                pixelNumberDisplay.DigitColor = spendColor;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 50f) * strength * (1f - elapsed / duration);
                containerRect.anchoredPosition = new Vector2(originalPos.x + offset, originalPos.y);
                yield return null;
            }

            containerRect.anchoredPosition = originalPos;
            if (pixelNumberDisplay != null)
            {
                pixelNumberDisplay.DigitColor = normalColor;
            }
        }

        private void UpdateDisplayInstant(long amount)
        {
            if (pixelNumberDisplay != null)
            {
                pixelNumberDisplay.SetValue(amount);
            }
        }

        public void SetIcon(Sprite sprite)
        {
            if (moneyIconImage != null)
            {
                moneyIconImage.sprite = sprite;
            }
        }
    }
}
