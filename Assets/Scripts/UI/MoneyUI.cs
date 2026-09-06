using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if TMPro
using TMPro;
#endif

namespace IsometricGame.UI
{
    /// <summary>
    /// Clean and stylish Money HUD Controller.
    /// Handles money balance display, smooth counting animation, and punch bounce effects.
    /// </summary>
    public class MoneyUI : MonoBehaviour
    {
        public static MoneyUI Instance { get; private set; }

        [Header("Starting Balance")]
        [SerializeField] private long currentMoney = 25000;

        [Header("UI References")]
        [Tooltip("The Image component displaying the money icon.")]
        [SerializeField] private Image moneyIconImage;

        [Tooltip("Standard UI Text (if not using TextMeshPro).")]
        [SerializeField] private Text moneyText;

#if TMPro
        [Tooltip("TextMeshPro text component.")]
        [SerializeField] private TMP_Text moneyTMPText;
#endif

        [Tooltip("Container transform for punch/bounce animation.")]
        [SerializeField] private RectTransform containerRect;

        [Header("Animation Settings")]
        [Tooltip("Smoothly count numbers when balance changes.")]
        [SerializeField] private bool animateCounting = true;
        [SerializeField] private float countDuration = 0.5f;
        [SerializeField] private bool bounceOnChange = true;

        [Header("Formatting")]
        [SerializeField] private string currencySymbol = "$";
        [SerializeField] private string format = "#,##0";

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

            displayedMoney = currentMoney;
            UpdateTextInstant(displayedMoney);
        }

        private void Start()
        {
            UpdateTextInstant(currentMoney);
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
            currentMoney = Mathf.Max(0, (int)newAmount);

            if (bounceOnChange)
            {
                if (bounceCoroutine != null) StopCoroutine(bounceCoroutine);
                bounceCoroutine = StartCoroutine(AnimateBounce(newAmount >= oldAmount ? Color.green : Color.red));
            }

            if (animateCounting && gameObject.activeInHierarchy)
            {
                if (countCoroutine != null) StopCoroutine(countCoroutine);
                countCoroutine = StartCoroutine(AnimateCount(displayedMoney, currentMoney));
            }
            else
            {
                displayedMoney = currentMoney;
                UpdateTextInstant(displayedMoney);
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
                UpdateTextInstant(displayedMoney);
                yield return null;
            }

            displayedMoney = targetVal;
            UpdateTextInstant(displayedMoney);
        }

        private IEnumerator AnimateBounce(Color flashColor)
        {
            if (containerRect == null) yield break;

            float elapsed = 0f;
            float duration = 0.25f;
            Vector3 punchScale = originalScale * 1.15f;

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
                yield return null;
            }

            containerRect.localScale = originalScale;
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

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offset = Mathf.Sin(elapsed * 50f) * strength * (1f - elapsed / duration);
                containerRect.anchoredPosition = new Vector2(originalPos.x + offset, originalPos.y);
                yield return null;
            }

            containerRect.anchoredPosition = originalPos;
        }

        private void UpdateTextInstant(long amount)
        {
            string formatted = currencySymbol + amount.ToString(format);

#if TMPro
            if (moneyTMPText != null)
            {
                moneyTMPText.text = formatted;
                return;
            }
#endif
            if (moneyText != null)
            {
                moneyText.text = formatted;
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
