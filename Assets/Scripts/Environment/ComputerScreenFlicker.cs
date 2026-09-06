using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Animates the computer monitor with dynamic, unpredictable CRT glow and flicker effects.
    /// Eliminates the rigid off-state, operating purely on atmospheric pixel emission overlays,
    /// soft radial bloom halos, and gentle floating digital dust motes.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [ExecuteAlways]
    public class ComputerScreenFlicker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
    {
        [Header("Desk & Glow Sprites")]
        [Tooltip("The main isometric desk sprite that remains active at all times.")]
        public Sprite defaultSprite;
        [Tooltip("Direct pixel-aligned glowing screen overlay (e.g. just screen glow.png).")]
        public Sprite screenPixelGlowSprite;
        [Tooltip("Soft atmospheric CRT radial halo light (e.g. monitor_glow.png).")]
        public Sprite ambientHaloSprite;

        // Legacy field aliases for backwards compatibility
        [HideInInspector] public Sprite flickerSprite;
        [HideInInspector] public Sprite offSprite;
        [HideInInspector] public Sprite glowSprite;

        [Header("Screen Glow Appearance")]
        [SerializeField] private bool enableGlow = true;
        [SerializeField] private Color glowColor = new Color(0.24f, 0.82f, 0.98f, 1f); // Vibrant CRT Cyan #3DD1FA
        [Range(0f, 1f)] [SerializeField] private float idleScreenAlpha = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float surgeScreenAlpha = 1.0f;

        [Header("Unpredictable Flicker Dynamics")]
        [Tooltip("Higher values increase the frequency and chaos of spontaneous electrical surges and stutters.")]
        [Range(0.1f, 1.0f)] [SerializeField] private float unpredictability = 0.85f;
        [Tooltip("Minimum seconds between main flicker events")]
        [SerializeField] private float minInterval = 0.6f;
        [Tooltip("Maximum seconds between main flicker events")]
        [SerializeField] private float maxInterval = 3.2f;
        [Tooltip("Continuous subtle analog CRT scanline hum")]
        [SerializeField] private bool enableContinuousHum = true;
        [SerializeField] private float humFrequency = 9.5f;
        [SerializeField] private float humIntensity = 0.08f;

        [Header("Floating Computer Electricity Sparks & Particles")]
        [SerializeField] private bool enableParticles = true;
        [SerializeField] private Color particleColor = new Color(0.40f, 0.95f, 1.0f, 0.95f);
        [SerializeField] private float particleRate = 2.5f;
        [SerializeField] private Vector2 particleEmitterOffset = new Vector2(0.38f, 1.15f);

        [Header("Hover Outline & Interaction")]
        [Tooltip("Direct pixel-aligned computer screen outline sprite.")]
        public Sprite screenHoverOutlineSprite;
        [Tooltip("Text sprite displayed inside interaction popup (OPEN).")]
        public Sprite openTextSprite;
        [SerializeField] private bool enableHoverOutline = true;
        [SerializeField] private Color hoverOutlineColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float hoverFadeSpeed = 14f;
        [SerializeField] private bool pulseOutlineWhileHovered = true;
        [SerializeField] private float pulseMinAlpha = 0.70f;
        [SerializeField] private float pulseMaxAlpha = 1.0f;
        [SerializeField] private float pulseSpeed = 4.5f;
        [Tooltip("Local offset from desk base pivot to the computer screen center.")]
        [SerializeField] private Vector2 screenTriggerOffset = new Vector2(0.36f, 1.11f);
        [Tooltip("Hover bounding box size around the monitor screen.")]
        [SerializeField] private Vector2 screenTriggerSize = new Vector2(0.55f, 0.55f);

        [Header("Interaction Events")]
        public UnityEngine.Events.UnityEvent onComputerClicked;

        private SpriteRenderer deskRenderer;
        private SpriteRenderer screenGlowRenderer;
        private SpriteRenderer hoverOutlineRenderer;
        private ParticleSystem floatingParticles;
        private Coroutine flickerRoutine;

        private float currentDynamicMultiplier = 1.0f;
        private bool isHovered = false;
        private bool isPointerOver = false;
        private float currentOutlineAlpha = 0f;
        private static Sprite cachedParticleSprite;

        public bool IsHovered => isHovered;

        private void Awake()
        {
            InitializeComponents();
        }

        private void OnEnable()
        {
            InitializeComponents();
            if (Application.isPlaying)
            {
                if (flickerRoutine != null) StopCoroutine(flickerRoutine);
                flickerRoutine = StartCoroutine(UnpredictableFlickerLoop());
            }
        }

        private void OnDisable()
        {
            if (flickerRoutine != null)
            {
                StopCoroutine(flickerRoutine);
                flickerRoutine = null;
            }
            ResetToIdle();
        }

        private void Update()
        {
            UpdateSorting();
            ApplyContinuousGlow();
            UpdateHoverOutline();
        }

        public void InitializeComponents()
        {
            if (deskRenderer == null) deskRenderer = GetComponent<SpriteRenderer>();

            EnsureSpritesLoaded();

            if (defaultSprite != null && deskRenderer != null)
            {
                deskRenderer.sprite = defaultSprite;
            }

            SetupPixelGlowRenderer();
            RemoveAmbientHalo();
            SetupParticleSystem();
            SetupHoverOutlineRenderer();
            UpdateSorting();
            ResetToIdle();
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (defaultSprite == null)
            {
                defaultSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/isometric desk fixed (1).png")
                             ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/isometric desk fixed.png");
            }
            if (screenPixelGlowSprite == null)
            {
                screenPixelGlowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/just screen glow.png");
            }
            if (ambientHaloSprite == null)
            {
                ambientHaloSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/monitor_glow.png")
                                 ?? glowSprite;
            }
            if (screenHoverOutlineSprite == null)
            {
                screenHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/computer scree hover outline.png")
                                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/computer screen hover outline.png");
            }
            if (openTextSprite == null)
            {
                openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
            }
#endif
        }

        private void SetupHoverOutlineRenderer()
        {
            if (!enableHoverOutline)
            {
                Transform existing = transform.Find("Desk_Screen_Hover_Outline");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }
                return;
            }

            Transform existingTrans = transform.Find("Desk_Screen_Hover_Outline");
            GameObject outlineObj = existingTrans != null ? existingTrans.gameObject : new GameObject("Desk_Screen_Hover_Outline");
            outlineObj.transform.SetParent(transform, false);
            outlineObj.transform.localPosition = Vector3.zero;
            outlineObj.transform.localScale = Vector3.one;

            hoverOutlineRenderer = outlineObj.GetComponent<SpriteRenderer>();
            if (hoverOutlineRenderer == null) hoverOutlineRenderer = outlineObj.AddComponent<SpriteRenderer>();

            if (screenHoverOutlineSprite != null)
            {
                hoverOutlineRenderer.sprite = screenHoverOutlineSprite;
            }

            Color c = hoverOutlineColor;
            c.a = 0f;
            hoverOutlineRenderer.color = c;
        }

        private void SetupPixelGlowRenderer()
        {
            if (!enableGlow) return;

            Transform existing = transform.Find("Desk_Screen_Pixel_Glow");
            GameObject glowObj = existing != null ? existing.gameObject : new GameObject("Desk_Screen_Pixel_Glow");
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.localPosition = Vector3.zero;
            glowObj.transform.localScale = Vector3.one;

            screenGlowRenderer = glowObj.GetComponent<SpriteRenderer>();
            if (screenGlowRenderer == null) screenGlowRenderer = glowObj.AddComponent<SpriteRenderer>();

            if (screenPixelGlowSprite != null)
            {
                screenGlowRenderer.sprite = screenPixelGlowSprite;
            }
        }

        private void RemoveAmbientHalo()
        {
            Transform halo = transform.Find("Monitor_Ambient_Halo");
            if (halo != null)
            {
                if (Application.isPlaying) Destroy(halo.gameObject);
                else DestroyImmediate(halo.gameObject);
            }
            Transform stray = transform.Find("Monitor_Screen_Glow");
            if (stray != null)
            {
                if (Application.isPlaying) Destroy(stray.gameObject);
                else DestroyImmediate(stray.gameObject);
            }
        }

        private void SetupParticleSystem()
        {
            if (!enableParticles)
            {
                Transform existing = transform.Find("Computer_Floating_Particles");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }
                return;
            }

            if (particleRate > 4.0f) particleRate = 2.5f;

            Transform pTrans = transform.Find("Computer_Floating_Particles");
            if (pTrans != null)
            {
                if (Application.isPlaying) Destroy(pTrans.gameObject);
                else DestroyImmediate(pTrans.gameObject);
            }

            GameObject pObj = new GameObject("Computer_Floating_Particles");
            pObj.transform.SetParent(transform, false);
            pObj.transform.localPosition = new Vector3(particleEmitterOffset.x, particleEmitterOffset.y, 0f);
            pObj.transform.localScale = Vector3.one;

            floatingParticles = pObj.AddComponent<ParticleSystem>();

            var main = floatingParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.40f, 0.85f); // Quick, lively pixel twinkle
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);     // Subtle micro-drift, NOT a rising chimney plume
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.060f);   // 1-2 pixels in 32 PPU world
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 8;

            var emission = floatingParticles.emission;
            emission.rateOverTime = particleRate;

            var shape = floatingParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.16f; // Clustered right around the monitor screen
            shape.rotation = Vector3.zero;

            var vel = floatingParticles.velocityOverLifetime;
            vel.enabled = false;

            var col = floatingParticles.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.0f),       // Core white spark
                    new GradientColorKey(new Color(0.40f, 0.95f, 1.0f), 0.30f),     // Vibrant electric cyan
                    new GradientColorKey(new Color(0.20f, 0.70f, 1.0f), 0.70f),     // Digital aqua
                    new GradientColorKey(new Color(0.10f, 0.40f, 0.90f), 1.0f)      // Fade out
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.15f), // Quick pop in
                    new GradientAlphaKey(0.9f, 0.75f), // Crisp sustain (no puffing)
                    new GradientAlphaKey(0.0f, 1.0f)  // Sharp fade out
                }
            );
            col.color = grad;

            var sizeOverLifetime = floatingParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.7f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(0.75f, 1.0f); // Steady size (does NOT expand into a cloud)
            sizeCurve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var psr = pObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = deskRenderer != null ? deskRenderer.sortingOrder + 10 : 30;
                Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    Material pMat = new Material(spriteShader);
                    pMat.name = "Mat_Computer_Particles";
                    pMat.mainTexture = GetParticleTexture();
                    psr.sharedMaterial = pMat;
                }
            }
        }

        private static Texture2D GetParticleTexture()
        {
            if (cachedParticleSprite != null && cachedParticleSprite.texture != null) return cachedParticleSprite.texture;

            int size = 4;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            float[,] pattern = new float[4, 4]
            {
                { 0.0f,  0.75f, 0.75f, 0.0f },
                { 0.75f, 1.0f,  1.0f,  0.75f },
                { 0.75f, 1.0f,  1.0f,  0.75f },
                { 0.0f,  0.75f, 0.75f, 0.0f }
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = pattern[y, x];
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            cachedParticleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return tex;
        }

        private void UpdateSorting()
        {
            if (deskRenderer != null)
            {
                int baseOrder = deskRenderer.sortingOrder;
                if (screenGlowRenderer != null) screenGlowRenderer.sortingOrder = baseOrder + 1;
                if (floatingParticles != null)
                {
                    var psr = floatingParticles.GetComponent<ParticleSystemRenderer>();
                    if (psr != null) psr.sortingOrder = baseOrder + 2;
                }
                if (hoverOutlineRenderer != null) hoverOutlineRenderer.sortingOrder = baseOrder + 3;
            }
        }

        private void UpdateHoverOutline()
        {
            if (!enableHoverOutline) return;
            if (hoverOutlineRenderer == null) SetupHoverOutlineRenderer();
            if (hoverOutlineRenderer == null) return;

            if (screenHoverOutlineSprite == null) EnsureSpritesLoaded();
            if (hoverOutlineRenderer.sprite == null && screenHoverOutlineSprite != null)
            {
                hoverOutlineRenderer.sprite = screenHoverOutlineSprite;
            }

            CheckCursorHover();

            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen)
            {
                currentOutlineAlpha = 0f;
                hoverOutlineRenderer.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            float targetAlpha = 0f;
            if (isHovered)
            {
                targetAlpha = pulseOutlineWhileHovered
                    ? Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f)
                    : 1.0f;
            }

            currentOutlineAlpha = Mathf.MoveTowards(currentOutlineAlpha, targetAlpha, hoverFadeSpeed * Time.deltaTime);
            hoverOutlineRenderer.color = new Color(1f, 1f, 1f, currentOutlineAlpha);
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
            Vector2 worldPoint = IsometricGame.Core.IsometricInputHelper.GetMouseWorldPosition();
            Vector2 screenCenter = (Vector2)transform.position + screenTriggerOffset;
            float halfW = screenTriggerSize.x * 0.5f;
            float halfH = screenTriggerSize.y * 0.5f;

            return (Mathf.Abs(worldPoint.x - screenCenter.x) <= halfW &&
                    Mathf.Abs(worldPoint.y - screenCenter.y) <= halfH);
        }

        private void CheckCursorHover()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen)
            {
                if (isHovered) SetHoverState(false);
                return;
            }

            bool hoverNow = IsMouseInBounds() || IsPopupActiveForThis();

            if (hoverNow)
            {
                if (!isHovered) SetHoverState(true);
            }
            else if (!isPointerOver && isHovered)
            {
                SetHoverState(false);
            }

            if (isHovered && IsMouseInBounds() && IsometricGame.Core.IsometricInputHelper.IsLeftMouseButtonDown())
            {
                TriggerOpen();
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
                TriggerOpen();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TriggerOpen();
            }
        }

        private void SetHoverState(bool hovered)
        {
            isHovered = hovered;
            if (isHovered)
            {
                if (openTextSprite == null) EnsureSpritesLoaded();
                if (IsometricGame.UI.WorldInteractionPopup.Instance != null)
                {
                    IsometricGame.UI.WorldInteractionPopup.Instance.Show(transform, new Vector3(screenTriggerOffset.x, screenTriggerOffset.y + 0.35f, 0f), openTextSprite, TriggerOpen);
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

        public void TriggerOpen()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen) return;
            if (IsometricGame.UI.JobsBoardUI.IsJobsBoardOpen) return;

            onComputerClicked?.Invoke();

            if (IsometricGame.UI.WorldInteractionPopup.Instance != null)
            {
                IsometricGame.UI.WorldInteractionPopup.Instance.DismissImmediate();
            }

            if (IsometricGame.UI.JobsBoardUI.Instance != null)
            {
                IsometricGame.UI.JobsBoardUI.Instance.ToggleOpen();
            }
        }

        private void ApplyContinuousGlow()
        {
            float hum = 1.0f;
            if (enableContinuousHum && Application.isPlaying)
            {
                float n = Mathf.PerlinNoise(Time.time * humFrequency, 0.1984f);
                hum += (n - 0.5f) * humIntensity;
            }

            float finalMultiplier = currentDynamicMultiplier * hum;

            if (screenGlowRenderer != null)
            {
                float baseA = finalMultiplier >= 1.0f
                    ? Mathf.Lerp(idleScreenAlpha, surgeScreenAlpha, finalMultiplier - 1.0f)
                    : idleScreenAlpha * finalMultiplier;
                float targetAlpha = Mathf.Clamp01(baseA);
                Color c = glowColor;
                c.a = targetAlpha;
                screenGlowRenderer.color = c;
            }
        }

        private void ResetToIdle()
        {
            currentDynamicMultiplier = 1.0f;
            ApplyContinuousGlow();
        }

        private IEnumerator UnpredictableFlickerLoop()
        {
            while (true)
            {
                float delay = Random.Range(minInterval, maxInterval);
                if (Random.value < unpredictability * 0.45f)
                {
                    delay *= Random.Range(0.12f, 0.40f);
                }

                yield return new WaitForSeconds(delay);

                float roll = Random.value;

                if (roll < 0.32f)
                {
                    yield return StartCoroutine(EventVoltageSurge());
                }
                else if (roll < 0.62f)
                {
                    yield return StartCoroutine(EventRapidMicroStutter());
                }
                else if (roll < 0.80f)
                {
                    yield return StartCoroutine(EventDoublePulse());
                }
                else if (roll < 0.92f)
                {
                    yield return StartCoroutine(EventPhosphorDip());
                }
                else
                {
                    yield return StartCoroutine(EventAtmosphericBreath());
                }
            }
        }

        private IEnumerator EventVoltageSurge()
        {
            float peak = Random.Range(1.35f, 1.85f);
            float duration = Random.Range(0.18f, 0.38f);
            float elapsed = 0f;

            if (floatingParticles != null && floatingParticles.isPlaying)
            {
                floatingParticles.Emit(Random.Range(1, 3));
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                currentDynamicMultiplier = Mathf.Lerp(peak, 1.0f, t * t);
                yield return null;
            }

            ResetToIdle();
        }

        private IEnumerator EventRapidMicroStutter()
        {
            int bursts = Random.Range(3, 7);
            for (int i = 0; i < bursts; i++)
            {
                currentDynamicMultiplier = Random.Range(0.55f, 1.65f);
                yield return new WaitForSeconds(Random.Range(0.025f, 0.055f));

                currentDynamicMultiplier = Random.Range(0.85f, 1.15f);
                yield return new WaitForSeconds(Random.Range(0.015f, 0.035f));
            }

            ResetToIdle();
        }

        private IEnumerator EventDoublePulse()
        {
            currentDynamicMultiplier = 1.5f;
            yield return new WaitForSeconds(0.06f);

            currentDynamicMultiplier = 0.9f;
            yield return new WaitForSeconds(0.05f);

            currentDynamicMultiplier = 1.4f;
            yield return new WaitForSeconds(0.05f);

            ResetToIdle();
        }

        private IEnumerator EventPhosphorDip()
        {
            currentDynamicMultiplier = 0.45f;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.09f));

            currentDynamicMultiplier = 1.25f;
            yield return new WaitForSeconds(0.06f);

            ResetToIdle();
        }

        private IEnumerator EventAtmosphericBreath()
        {
            float duration = Random.Range(1.0f, 1.8f);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float sinFactor = Mathf.Sin(progress * Mathf.PI);
                currentDynamicMultiplier = 1.0f + sinFactor * 0.4f;
                yield return null;
            }

            ResetToIdle();
        }
    }
}
