using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IsometricGame.UI
{
    /// <summary>
    /// Clock and Calendar HUD positioned at the Top-Right of the screen above Energy Bar and XP Bar.
    /// - Sprite size: 61x14 px @ 4x scale = 244x56 px
    /// - Time speed: 1 real second = 1 game minute
    /// - AM/PM base sprite swap (with animated/steady clock icon & colon)
    /// - Days of week: MON, TUES, WED, THURS, FRI, SAT, SUN (individual sprite overlays)
    /// - Dynamic Date (1st..31st/30th/28th) and Time (HH:MM AM/PM) rendered with pixel digits 0-9
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class ClockUI : MonoBehaviour
    {
        private static ClockUI instance;
        public static ClockUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
                    if (instance == null)
                    {
                        EnsureCanvasAndMoneyUI.EnsureAllUI();
                        instance = UnityEngine.Object.FindAnyObjectByType<ClockUI>();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("Positioning and Sizing")]
        [SerializeField] private Vector2 hudAnchor = new Vector2(1f, 1f);
        [SerializeField] private Vector2 hudPivot = new Vector2(1f, 1f);
        [SerializeField] private Vector2 hudPosition = new Vector2(-24f, -22f);
        [SerializeField] private Vector2 clockPixelSize = new Vector2(244f, 56f); // 61x14 @ 4x

        [Header("Sprites")]
        [SerializeField] private Sprite clockAmSprite;
        [SerializeField] private Sprite clockPmSprite;
        [SerializeField] private Sprite[] daySprites = new Sprite[7]; // 0=Mon, 1=Tues, 2=Wed, 3=Thurs, 4=Fri, 5=Sat, 6=Sun
        [SerializeField] private Texture2D numbersTexture;

        [Header("Time & Calendar Settings")]
        [Tooltip("1 real second = 1 game minute (timeScale = 60)")]
        [SerializeField] private float timeScale = 60f;
        [SerializeField] private int startDayOfWeek = 0; // 0=Mon, 1=Tues...
        [SerializeField] private int startDate = 1;      // 1..31
        [SerializeField] private int startMonth = 1;     // 1..12
        [SerializeField] private int startHour = 8;      // 8 AM
        [SerializeField] private int startMinute = 0;

        [Header("Current State")]
        [SerializeField] private int currentDayOfWeek = 0;
        [SerializeField] private int currentDate = 1;
        [SerializeField] private int currentMonth = 1;
        [SerializeField] private int currentHour = 8;
        [SerializeField] private int currentMinute = 0;
        [SerializeField] private float secondAccumulator = 0f;

        [Header("UI Hierarchy")]
        [SerializeField] private RectTransform containerRect;
        [SerializeField] private Image baseClockImage;
        [SerializeField] private Image dayImage;
        [SerializeField] private Image dateTensImage;
        [SerializeField] private Image dateOnesImage;
        [SerializeField] private Image hourTensImage;
        [SerializeField] private Image hourOnesImage;
        [SerializeField] private Image minTensImage;
        [SerializeField] private Image minOnesImage;

        private static readonly int[] DaysInMonth = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 };
        private Dictionary<char, Sprite> digitSpriteMap = new Dictionary<char, Sprite>();

        public int CurrentDayOfWeek => currentDayOfWeek;
        public int CurrentDate => currentDate;
        public int CurrentHour => currentHour;
        public int CurrentMinute => currentMinute;
        public bool IsPM => currentHour >= 12;

        public Sprite ClockAmSprite { get => clockAmSprite; set { clockAmSprite = value; RefreshBaseSprite(); } }
        public Sprite ClockPmSprite { get => clockPmSprite; set { clockPmSprite = value; RefreshBaseSprite(); } }
        public Sprite[] DaySprites { get => daySprites; set { daySprites = value; RefreshDisplay(); } }
        public Texture2D NumbersTexture { get => numbersTexture; set { numbersTexture = value; BuildDigitSprites(); RefreshDisplay(); } }

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
            clockPixelSize = size;
            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = hudAnchor;
                containerRect.anchorMax = hudAnchor;
                containerRect.pivot = hudPivot;
                containerRect.anchoredPosition = hudPosition;
                containerRect.sizeDelta = clockPixelSize;
            }
        }

        public void InitializeComponents()
        {
            // Auto-migrate stale positions / sizes
            if (hudPosition.y < -50f || clockPixelSize.y < 50f)
            {
                hudPosition = new Vector2(-24f, -22f);
                clockPixelSize = new Vector2(244f, 56f);
            }

            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            if (containerRect != null)
            {
                containerRect.anchorMin = hudAnchor;
                containerRect.anchorMax = hudAnchor;
                containerRect.pivot = hudPivot;
                containerRect.anchoredPosition = hudPosition;
                containerRect.sizeDelta = clockPixelSize;
            }

            EnsureSpritesLoaded();
            BuildDigitSprites();
            SetupHierarchy();

            if (currentHour == 0 && currentMinute == 0 && currentDate == 0)
            {
                currentDayOfWeek = startDayOfWeek;
                currentDate = startDate;
                currentMonth = startMonth;
                currentHour = startHour;
                currentMinute = startMinute;
            }

            RefreshDisplay();
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (clockAmSprite == null) clockAmSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock am.png");
            if (clockPmSprite == null) clockPmSprite = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock pm.png");

            if (daySprites == null || daySprites.Length < 7 || daySprites[0] == null)
            {
                daySprites = new Sprite[7];
                daySprites[0] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock mon.png");
                daySprites[1] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock tues.png");
                daySprites[2] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock wed.png");
                daySprites[3] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock thurs.png");
                daySprites[4] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock fri.png");
                daySprites[5] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock sat.png");
                daySprites[6] = UISpriteUtility.LoadSprite("Assets/Sprites/GUI/Clock/clock sun.png");
            }

            if (numbersTexture == null)
            {
                numbersTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/GUI/numbers 1 - 9.png")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/numbers 1 - 9.png");
            }
#endif
        }

        public void BuildDigitSprites()
        {
            if (numbersTexture == null) return;
            digitSpriteMap.Clear();

            // Slicing 1..9 from numbers texture (digits are 3x5 px, in Unity Y=7..11)
            for (int d = 1; d <= 9; d++)
            {
                int sx = 6 + (d - 1) * 4;
                Rect rect = new Rect(sx, 7, 3, 5);
                Sprite s = Sprite.Create(numbersTexture, rect, new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                s.name = $"ClockDigit_{d}";
                digitSpriteMap[(char)('0' + d)] = s;
            }

            // Create 0 glyph (3x5 pixel font: full outline with hollow center)
            try
            {
                Texture2D zeroTex = new Texture2D(3, 5, TextureFormat.RGBA32, false);
                zeroTex.filterMode = FilterMode.Point;
                zeroTex.wrapMode = TextureWrapMode.Clamp;

                Color w = Color.white;
                Color c = Color.clear;
                Color[] zeroPixels = new Color[]
                {
                    w, w, w, // y=0 (bottom)
                    w, c, w, // y=1
                    w, c, w, // y=2 (center)
                    w, c, w, // y=3
                    w, w, w  // y=4 (top)
                };
                zeroTex.SetPixels(zeroPixels);
                zeroTex.Apply();

                Sprite zeroSprite = Sprite.Create(zeroTex, new Rect(0, 0, 3, 5), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
                zeroSprite.name = "ClockDigit_0";
                digitSpriteMap['0'] = zeroSprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClockUI] Failed to generate 0 sprite dynamically: {ex.Message}");
            }
        }

        private void SetupHierarchy()
        {
            if (containerRect == null) containerRect = transform as RectTransform ?? GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();

            // 1. Base Clock Background (AM / PM + clock icon + colon)
            baseClockImage = EnsureImageChild("Base_Clock_Image", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            
            // 2. Day Overlay (MON..SUN) - spans full 61x14 texture
            dayImage = EnsureImageChild("Day_Overlay_Image", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Digit slots setup:
            // Texture coordinates (61x14):
            // Top row (Date): Y=1..5. Date tens at X=37, ones at X=42.
            // Bottom row (Time): Y=8..12. Hour tens at X=18, ones at X=23. Colon at X=28. Min tens at X=30, ones at X=35. AM/PM at X=41..49.
            // Size of each digit = 3x5 px @ 4x scale = 12x20 px

            dateTensImage = CreateDigitSlot("Date_Tens", 37f, 1f);
            dateOnesImage = CreateDigitSlot("Date_Ones", 42f, 1f);

            hourTensImage = CreateDigitSlot("Hour_Tens", 18f, 8f);
            hourOnesImage = CreateDigitSlot("Hour_Ones", 23f, 8f);

            minTensImage = CreateDigitSlot("Min_Tens", 30f, 8f);
            minOnesImage = CreateDigitSlot("Min_Ones", 35f, 8f);
        }

        private Image EnsureImageChild(string childName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            Transform t = transform.Find(childName);
            GameObject obj = t != null ? t.gameObject : new GameObject(childName, typeof(RectTransform), typeof(Image));
            if (t == null) obj.transform.SetParent(transform, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            Image img = obj.GetComponent<Image>();
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            return img;
        }

        private Image CreateDigitSlot(string name, float pixelX, float pixelY)
        {
            Transform t = transform.Find(name);
            GameObject obj = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
            if (t == null) obj.transform.SetParent(transform, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(pixelX * 4f, -pixelY * 4f);
            rt.sizeDelta = new Vector2(12f, 20f); // 3x5 @ 4x

            Image img = obj.GetComponent<Image>();
            img.raycastTarget = false;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
            return img;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            secondAccumulator += Time.deltaTime * (timeScale / 60f);
            if (secondAccumulator >= 1f)
            {
                int minutesPassed = (int)secondAccumulator;
                secondAccumulator -= minutesPassed;
                AdvanceTime(minutesPassed);
            }
        }

        public void AdvanceTime(int minutes)
        {
            currentMinute += minutes;
            while (currentMinute >= 60)
            {
                currentMinute -= 60;
                currentHour++;

                if (currentHour >= 24)
                {
                    currentHour = 0;
                    AdvanceDay();
                }
            }
            RefreshDisplay();
        }

        public void AdvanceDay()
        {
            currentDayOfWeek = (currentDayOfWeek + 1) % 7;
            currentDate++;

            int maxDays = DaysInMonth[Mathf.Clamp(currentMonth - 1, 0, 11)];
            if (currentDate > maxDays)
            {
                currentDate = 1;
                currentMonth++;
                if (currentMonth > 12) currentMonth = 1;
            }
        }

        public void SetTime(int hour, int minute, int dayOfWeek = -1, int date = -1, int month = -1)
        {
            currentHour = Mathf.Clamp(hour, 0, 23);
            currentMinute = Mathf.Clamp(minute, 0, 59);
            if (dayOfWeek >= 0) currentDayOfWeek = dayOfWeek % 7;
            if (date >= 1) currentDate = date;
            if (month >= 1) currentMonth = month;
            secondAccumulator = 0f;
            RefreshDisplay();
        }

        public void RefreshDisplay()
        {
            RefreshBaseSprite();
            RefreshDaySprite();
            RefreshDateDigits();
            RefreshTimeDigits();
        }

        private void RefreshBaseSprite()
        {
            if (baseClockImage == null) return;
            baseClockImage.sprite = IsPM ? clockPmSprite : clockAmSprite;
        }

        private void RefreshDaySprite()
        {
            if (dayImage == null) return;
            if (daySprites != null && currentDayOfWeek >= 0 && currentDayOfWeek < daySprites.Length)
            {
                dayImage.sprite = daySprites[currentDayOfWeek];
                dayImage.enabled = dayImage.sprite != null;
            }
            else
            {
                dayImage.enabled = false;
            }
        }

        private void RefreshDateDigits()
        {
            int d = Mathf.Clamp(currentDate, 1, 31);
            int tens = d / 10;
            int ones = d % 10;

            SetDigitImage(dateTensImage, tens > 0 ? (char)('0' + tens) : ' ');
            SetDigitImage(dateOnesImage, (char)('0' + ones));
        }

        private void RefreshTimeDigits()
        {
            // 12-hour format: 12 AM, 1 AM..11 AM, 12 PM, 1 PM..11 PM
            int displayHour = currentHour % 12;
            if (displayHour == 0) displayHour = 12;

            int hTens = displayHour / 10;
            int hOnes = displayHour % 10;

            int mTens = currentMinute / 10;
            int mOnes = currentMinute % 10;

            SetDigitImage(hourTensImage, hTens > 0 ? (char)('0' + hTens) : ' ');
            SetDigitImage(hourOnesImage, (char)('0' + hOnes));

            SetDigitImage(minTensImage, (char)('0' + mTens));
            SetDigitImage(minOnesImage, (char)('0' + mOnes));
        }

        private void SetDigitImage(Image img, char c)
        {
            if (img == null) return;

            if (c == ' ')
            {
                img.enabled = false;
                return;
            }

            if (digitSpriteMap.TryGetValue(c, out Sprite sprite) && sprite != null)
            {
                img.sprite = sprite;
                img.enabled = true;
            }
            else
            {
                img.enabled = false;
            }
        }
    }
}
