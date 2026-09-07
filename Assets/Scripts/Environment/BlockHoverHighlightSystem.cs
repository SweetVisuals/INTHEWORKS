using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using IsometricGame.Tilemap;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsometricGame.Environment
{
    public enum HoverHighlightMode
    {
        Contextual = 0, // Automatically highlights quarter block if hovering over one, else normal tile outline
        NormalOnly = 1, // Always normal full-tile diamond outline
        QuarterOnly = 2 // Always quarter-block quadrant outline
    }

    /// <summary>
    /// Manages the visual block hover highlight cursor in 2D Isometric world space
    /// and the progressive breaking block animation system:
    /// - Tracks mouse position, detects hovered tile and quadrant.
    /// - Displays white hover outlines:
    ///   - Normal Block: 'Assets/Sprites/normal block hover white outline.png'
    ///   - Quarter Block: 'Assets/Sprites/quarter block white outline.png'
    /// - Holding Left-Click plays the 8-stage 'breaking block animation 32x16.png'
    ///   overlay directly on top of the tile diamond.
    /// - Breaking a full block drops 4 small floating quarter blocks on the floor.
    /// - Breaking a quarter block drops 1 small floating quarter block on the floor.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-40)]
    public class BlockHoverHighlightSystem : MonoBehaviour
    {
        public static BlockHoverHighlightSystem Instance { get; private set; }

        [Header("Hover Mode & Controls")]
        [Tooltip("Contextual (switches automatically), NormalOnly, or QuarterOnly")]
        [SerializeField] private HoverHighlightMode hoverMode = HoverHighlightMode.Contextual;
        [Tooltip("Key to toggle between Normal and Quarter hover modes (Q)")]
        [SerializeField] private bool allowKeyToggle = true;
        [Tooltip("Whether clicking left-click (hold) breaks blocks and right-click places/cycles them")]
        [SerializeField] private bool enableBlockInteraction = true;

        [Header("Hover Outline Sprites")]
        [SerializeField] private Sprite normalBlockOutlineSprite;
        [SerializeField] private Sprite quarterBlockOutlineSprite;

        [Header("Breaking Block Settings")]
        [Tooltip("Seconds required to hold left click to completely break a block")]
        [SerializeField] private float breakDuration = 0.75f;
        [Tooltip("8 progressive cracking frames from breaking block animation 32x16.png")]
        [SerializeField] private Sprite[] breakFrames = new Sprite[8];
        [Tooltip("Whether to spawn crunchy debris particles when a block breaks")]
        [SerializeField] private bool spawnBreakDebris = true;

        [Header("Visual Styling")]
        [SerializeField] private Color highlightColor = Color.white;
        [SerializeField] private bool enablePulse = true;
        [SerializeField] private float pulseSpeed = 4.0f;
        [SerializeField] private float minAlpha = 0.82f;
        [SerializeField] private float maxAlpha = 1.0f;

        [Header("Runtime State")]
        [SerializeField] private bool isHovering = false;
        [SerializeField] private Vector2Int hoveredGridPos;
        [SerializeField] private BlockQuadrant hoveredQuadrant;
        [SerializeField] private bool showingQuarterOutline;

        [Header("Breaking Runtime State")]
        [SerializeField] private bool isBreaking = false;
        [SerializeField] private float breakTimer = 0f;
        [SerializeField] private Vector2Int breakingGridPos;
        [SerializeField] private BlockQuadrant breakingQuadrant;
        [SerializeField] private bool isBreakingQuarter = false;

        private GameObject normalOutlineObj;
        private SpriteRenderer normalRenderer;
        private GameObject quarterOutlineObj;
        private SpriteRenderer quarterRenderer;
        private GameObject breakingOverlayObj;
        private SpriteRenderer breakingRenderer;

        public HoverHighlightMode CurrentHoverMode
        {
            get => hoverMode;
            set => hoverMode = value;
        }

        public bool IsHovering => isHovering;
        public Vector2Int HoveredGridPos => hoveredGridPos;
        public BlockQuadrant HoveredQuadrant => hoveredQuadrant;
        public bool IsBreaking => isBreaking;
        public float BreakProgress => Mathf.Clamp01(breakTimer / breakDuration);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            EnsureSystemActive();
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this && Application.isPlaying)
            {
                Destroy(gameObject);
                return;
            }

            EnsureSpritesLoaded();
            CreateVisualObjects();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureSpritesLoaded();
            CreateVisualObjects();
        }

        public static void EnsureSystemActive()
        {
            if (Instance != null) return;

            BlockHoverHighlightSystem sys = FindAnyObjectByType<BlockHoverHighlightSystem>();
            if (sys == null)
            {
                GameObject obj = new GameObject("BlockHoverHighlightSystem");
                sys = obj.AddComponent<BlockHoverHighlightSystem>();
            }
            Instance = sys;
            sys.EnsureSpritesLoaded();
            sys.CreateVisualObjects();
        }

        public void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (normalBlockOutlineSprite == null)
            {
                normalBlockOutlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/normal block hover white outline.png");
            }
            if (quarterBlockOutlineSprite == null)
            {
                quarterBlockOutlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/quarter block white outline.png");
            }

            if (breakFrames == null || breakFrames.Length < 8 || breakFrames[0] == null)
            {
                string breakPath = "Assets/Sprites/breaking block animation 32x16.png";
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(breakPath);
                List<Sprite> loadedSlices = new List<Sprite>();
                if (allAssets != null)
                {
                    foreach (var a in allAssets)
                    {
                        if (a is Sprite sp) loadedSlices.Add(sp);
                    }
                }
                loadedSlices.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
                if (loadedSlices.Count >= 8)
                {
                    breakFrames = loadedSlices.GetRange(0, 8).ToArray();
                }
                else
                {
                    // Fallback to manual slice from Texture2D
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(breakPath);
                    if (tex != null)
                    {
                        breakFrames = new Sprite[8];
                        for (int i = 0; i < 8; i++)
                        {
                            breakFrames[i] = Sprite.Create(tex, new Rect(i * 32, 0, 32, 16), new Vector2(0.5f, 0.5f), 32f);
                            breakFrames[i].name = $"breaking block animation 32x16_{i}";
                        }
                    }
                }
            }
#endif
        }

        private void CreateVisualObjects()
        {
            EnsureSpritesLoaded();

            // 1. Normal Outline Object
            if (normalOutlineObj == null)
            {
                Transform existing = transform.Find("Normal_Hover_Outline");
                normalOutlineObj = existing != null ? existing.gameObject : new GameObject("Normal_Hover_Outline");
                normalOutlineObj.transform.SetParent(transform, false);
            }
            normalRenderer = normalOutlineObj.GetComponent<SpriteRenderer>();
            if (normalRenderer == null) normalRenderer = normalOutlineObj.AddComponent<SpriteRenderer>();
            normalRenderer.sprite = normalBlockOutlineSprite;
            normalRenderer.color = highlightColor;

            // 2. Quarter Outline Object
            if (quarterOutlineObj == null)
            {
                Transform existing = transform.Find("Quarter_Hover_Outline");
                quarterOutlineObj = existing != null ? existing.gameObject : new GameObject("Quarter_Hover_Outline");
                quarterOutlineObj.transform.SetParent(transform, false);
            }
            quarterRenderer = quarterOutlineObj.GetComponent<SpriteRenderer>();
            if (quarterRenderer == null) quarterRenderer = quarterOutlineObj.AddComponent<SpriteRenderer>();
            quarterRenderer.sprite = quarterBlockOutlineSprite;
            quarterRenderer.color = highlightColor;

            // 3. Breaking Block Cracking Overlay Object
            if (breakingOverlayObj == null)
            {
                Transform existing = transform.Find("Breaking_Block_Overlay");
                breakingOverlayObj = existing != null ? existing.gameObject : new GameObject("Breaking_Block_Overlay");
                breakingOverlayObj.transform.SetParent(transform, false);
            }
            breakingRenderer = breakingOverlayObj.GetComponent<SpriteRenderer>();
            if (breakingRenderer == null) breakingRenderer = breakingOverlayObj.AddComponent<SpriteRenderer>();
            if (breakFrames != null && breakFrames.Length > 0)
            {
                breakingRenderer.sprite = breakFrames[0];
            }
            breakingRenderer.color = Color.white;

            HideHighlight();
        }

        private void Update()
        {
            HandleModeToggleInput();
            UpdateHoverTracking();
            HandleInteractionInput();
        }

        private void HandleModeToggleInput()
        {
            if (!allowKeyToggle) return;

            bool togglePressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                togglePressed = Keyboard.current.qKey.wasPressedThisFrame;
            }
#else
            togglePressed = Input.GetKeyDown(KeyCode.Q);
#endif
            if (togglePressed)
            {
                // Cycle: Contextual -> NormalOnly -> QuarterOnly -> Contextual
                switch (hoverMode)
                {
                    case HoverHighlightMode.Contextual:
                        hoverMode = HoverHighlightMode.NormalOnly;
                        break;
                    case HoverHighlightMode.NormalOnly:
                        hoverMode = HoverHighlightMode.QuarterOnly;
                        break;
                    case HoverHighlightMode.QuarterOnly:
                        hoverMode = HoverHighlightMode.Contextual;
                        break;
                }
            }
        }

        private void UpdateHoverTracking()
        {
            if (IsPointerOverUI())
            {
                HideHighlight();
                CancelBreaking();
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                HideHighlight();
                CancelBreaking();
                return;
            }

            Vector2 mouseScreen = GetMouseScreenPosition();
            if (mouseScreen.x < 0 || mouseScreen.x > Screen.width || mouseScreen.y < 0 || mouseScreen.y > Screen.height)
            {
                HideHighlight();
                CancelBreaking();
                return;
            }

            Vector3 worldPoint3 = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
            Vector2 mouseWorld = new Vector2(worldPoint3.x, worldPoint3.y);

            // Calculate hovered tile & quadrant
            hoveredGridPos = QuarterBlockManager.WorldToTileCoord(mouseWorld);
            hoveredQuadrant = QuarterBlockManager.GetQuadrantFromWorld(mouseWorld, hoveredGridPos);
            isHovering = true;

            // Determine whether to display Normal outline or Quarter outline
            bool showQuarter = false;
            if (hoverMode == HoverHighlightMode.QuarterOnly)
            {
                showQuarter = true;
            }
            else if (hoverMode == HoverHighlightMode.NormalOnly)
            {
                showQuarter = false;
            }
            else // Contextual
            {
                bool hasQuarterBlocks = QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.HasAnyQuarterBlocks(hoveredGridPos);
                showQuarter = hasQuarterBlocks;
            }

            showingQuarterOutline = showQuarter;

            // Pulse alpha for hover outline
            float currentAlpha = maxAlpha;
            if (enablePulse)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                currentAlpha = Mathf.Lerp(minAlpha, maxAlpha, t);
            }
            Color renderColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, currentAlpha);

            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(hoveredGridPos, 0);
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(hoveredGridPos.x, hoveredGridPos.y, 0, -8000 + 50);

            if (showQuarter)
            {
                if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
                if (quarterOutlineObj != null)
                {
                    quarterOutlineObj.SetActive(true);
                    Vector2 quadOffset = QuarterBlockManager.GetQuadrantOffset(hoveredQuadrant);
                    quarterOutlineObj.transform.position = new Vector3(tileVisualCenter.x + quadOffset.x, tileVisualCenter.y + quadOffset.y, 0f);
                    if (quarterRenderer != null)
                    {
                        quarterRenderer.color = renderColor;
                        quarterRenderer.sortingOrder = baseSortingOrder + QuarterBlockManager.GetQuadrantSortingOffset(hoveredQuadrant);
                    }
                }
            }
            else
            {
                if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);
                if (normalOutlineObj != null)
                {
                    normalOutlineObj.SetActive(true);
                    normalOutlineObj.transform.position = new Vector3(tileVisualCenter.x, tileVisualCenter.y, 0f);
                    if (normalRenderer != null)
                    {
                        normalRenderer.color = renderColor;
                        normalRenderer.sortingOrder = baseSortingOrder;
                    }
                }
            }
        }

        private void HandleInteractionInput()
        {
            if (!enableBlockInteraction) return;

            bool leftClickHeld = false;
            bool rightClickDown = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                leftClickHeld = Mouse.current.leftButton.isPressed;
                rightClickDown = Mouse.current.rightButton.wasPressedThisFrame;
            }
#else
            leftClickHeld = Input.GetMouseButton(0);
            rightClickDown = Input.GetMouseButtonDown(1);
#endif

            // 1. Right Click: Place Quarter Block from Selected Hotbar Slot
            if (rightClickDown && isHovering && !IsPointerOverUI())
            {
                if (QuarterBlockManager.Instance != null)
                {
                    QuarterBlockType placeType = QuarterBlockType.Grass;
                    if (IsometricGame.UI.HotbarUI.Instance != null)
                    {
                        int selected = IsometricGame.UI.HotbarUI.Instance.SelectedSlotIndex;
                        if (selected == 1) placeType = QuarterBlockType.Dirt;
                        else if (selected == 0) placeType = QuarterBlockType.Grass;
                    }

                    if (QuarterBlockManager.Instance.HasInInventory(placeType))
                    {
                        QuarterBlockManager.Instance.ConsumeFromInventory(placeType, 1);
                        QuarterBlockManager.Instance.SetQuarterBlock(hoveredGridPos, hoveredQuadrant, placeType);
                        if (IsometricGame.UI.HotbarUI.Instance != null) IsometricGame.UI.HotbarUI.Instance.SyncWithInventory();
                    }
                    else
                    {
                        QuarterBlockManager.Instance.CycleQuarterBlock(hoveredGridPos, hoveredQuadrant);
                        if (IsometricGame.UI.HotbarUI.Instance != null) IsometricGame.UI.HotbarUI.Instance.SyncWithInventory();
                    }
                }
            }

            // 2. Left Click (Hold): Breaking Block Animation & Destruction
            if (leftClickHeld && isHovering && !IsPointerOverUI())
            {
                bool targetIsQuarter = showingQuarterOutline || hoverMode == HoverHighlightMode.QuarterOnly;
                if (QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.GetQuarterBlock(hoveredGridPos, hoveredQuadrant) != QuarterBlockType.None)
                {
                    targetIsQuarter = true;
                }

                // If targeting changed or not already breaking, start fresh break
                if (!isBreaking || hoveredGridPos != breakingGridPos || (targetIsQuarter && hoveredQuadrant != breakingQuadrant))
                {
                    isBreaking = true;
                    breakTimer = 0f;
                    breakingGridPos = hoveredGridPos;
                    breakingQuadrant = hoveredQuadrant;
                    isBreakingQuarter = targetIsQuarter;
                }

                breakTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(breakTimer / breakDuration);
                int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * 8f), 0, 7);

                UpdateBreakingOverlayVisual(frameIndex);

                // Reached 100% -> Break the block!
                if (breakTimer >= breakDuration)
                {
                    ExecuteBlockBreak();
                    CancelBreaking();
                }
            }
            else
            {
                if (isBreaking)
                {
                    CancelBreaking();
                }
            }
        }

        private void UpdateBreakingOverlayVisual(int frameIndex)
        {
            if (breakingOverlayObj == null || breakingRenderer == null) return;
            if (breakFrames == null || breakFrames.Length == 0) EnsureSpritesLoaded();

            if (breakFrames != null && frameIndex >= 0 && frameIndex < breakFrames.Length && breakFrames[frameIndex] != null)
            {
                breakingRenderer.sprite = breakFrames[frameIndex];
            }

            breakingOverlayObj.SetActive(true);

            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(breakingGridPos, 0);
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(breakingGridPos.x, breakingGridPos.y, 0, -8000 + 40);

            // Micro-vibration strike shake
            float shakeX = Mathf.Sin(breakTimer * 55f) * 0.007f;

            if (isBreakingQuarter)
            {
                Vector2 quadOffset = QuarterBlockManager.GetQuadrantOffset(breakingQuadrant);
                breakingOverlayObj.transform.position = new Vector3(tileVisualCenter.x + quadOffset.x + shakeX, tileVisualCenter.y + quadOffset.y, 0f);
                breakingOverlayObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                breakingRenderer.sortingOrder = baseSortingOrder + QuarterBlockManager.GetQuadrantSortingOffset(breakingQuadrant);
            }
            else
            {
                breakingOverlayObj.transform.position = new Vector3(tileVisualCenter.x + shakeX, tileVisualCenter.y, 0f);
                breakingOverlayObj.transform.localScale = Vector3.one;
                breakingRenderer.sortingOrder = baseSortingOrder;
            }
        }

        private void ExecuteBlockBreak()
        {
            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(breakingGridPos, 0);

            if (isBreakingQuarter)
            {
                QuarterBlockType qType = QuarterBlockType.Grass;
                if (QuarterBlockManager.Instance != null)
                {
                    QuarterBlockType existing = QuarterBlockManager.Instance.GetQuarterBlock(breakingGridPos, breakingQuadrant);
                    if (existing != QuarterBlockType.None) qType = existing;
                    QuarterBlockManager.Instance.RemoveQuarterBlock(breakingGridPos, breakingQuadrant);
                }

                Vector2 quadPos = tileVisualCenter + QuarterBlockManager.GetQuadrantOffset(breakingQuadrant);

                // Drops 1 small floating quarter block
                if (QuarterBlockManager.Instance != null)
                {
                    QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(quadPos, qType, 1);
                }

                if (spawnBreakDebris)
                {
                    Color c = (qType == QuarterBlockType.Dirt) ? new Color(0.55f, 0.38f, 0.22f) : new Color(0.38f, 0.65f, 0.28f);
                    SpawnDebrisParticles(quadPos, c);
                }
            }
            else
            {
                // Full Tile Break:
                // 1 grass block = drops 4 small floating quarter grass blocks!
                if (QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.HasAnyQuarterBlocks(breakingGridPos))
                {
                    QuarterBlockManager.Instance.ClearAllQuarterBlocks();
                }

                if (OutdoorInfiniteTerrain.Instance != null)
                {
                    OutdoorInfiniteTerrain.Instance.BreakTile(breakingGridPos.x, breakingGridPos.y);
                }

                // Drops 4 small floating quarter grass blocks on the floor
                if (QuarterBlockManager.Instance != null)
                {
                    QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(tileVisualCenter, QuarterBlockType.Grass, 4);
                }

                if (spawnBreakDebris)
                {
                    SpawnDebrisParticles(tileVisualCenter, new Color(0.38f, 0.65f, 0.28f));
                }
            }
        }

        private void SpawnDebrisParticles(Vector2 position, Color color)
        {
            GameObject debrisObj = new GameObject("Block_Break_Debris");
            debrisObj.transform.position = new Vector3(position.x, position.y, 0f);

            ParticleSystem ps = debrisObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.30f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startColor = color;
            main.gravityModifier = 2.0f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 14) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;

            var renderer = debrisObj.GetComponent<ParticleSystemRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
            renderer.material = mat;
            renderer.sortingOrder = 100;

            ps.Play();
        }

        private void CancelBreaking()
        {
            isBreaking = false;
            breakTimer = 0f;
            if (breakingOverlayObj != null)
            {
                breakingOverlayObj.SetActive(false);
            }
        }

        public void HideHighlight()
        {
            isHovering = false;
            if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
            if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}
