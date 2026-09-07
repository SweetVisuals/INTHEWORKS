using System;
using System.Collections;
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
        Contextual = 0, // Automatically highlights quad only when holding quad block or hovering over placed quad
        NormalOnly = 1, // Always normal full-tile diamond outline
        QuarterOnly = 2 // Always quarter-block quadrant outline
    }

    /// <summary>
    /// Manages the visual block hover highlight cursor in 2D Isometric world space
    /// and the progressive breaking block animation system:
    /// - Tracks mouse position, detects hovered tile, quadrant, and stacked surface elevation.
    /// - When hovering over an ALREADY PLACED quad block:
    ///   - Displays 'quarter block white outline.png' around that quad block.
    ///   - Left-clicking breaks it in ONE HIT with a solid white damage flash overlay and drops 1 quad item.
    /// - When hovering over an EMPTY quadrant while holding a quad block tile:
    ///   - Displays directional quad highlight ('north', 'east', 'south', 'west quad highlight.png') pulsing slowly.
    ///   - Right-clicking places a quad block (or stacks vertically to build up).
    /// - When not holding a quad block and on an empty floor tile:
    ///   - Displays normal block hover outline ('normal block hover white outline.png').
    ///   - Holding left-click breaks full floor tile into 4 quad grass blocks.
    /// - Suppresses tile outlines when targeting a harvestable pine tree.
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
        [Tooltip("Whether clicking left-click breaks blocks and right-click places them")]
        [SerializeField] private bool enableBlockInteraction = true;

        [Header("Hover Outline Sprites")]
        [SerializeField] private Sprite normalBlockOutlineSprite;
        [SerializeField] private Sprite quarterBlockOutlineSprite;

        [Header("Directional Quad Highlight Sprites (N, E, S, W)")]
        [SerializeField] private Sprite northQuadHighlightSprite;
        [SerializeField] private Sprite eastQuadHighlightSprite;
        [SerializeField] private Sprite southQuadHighlightSprite;
        [SerializeField] private Sprite westQuadHighlightSprite;

        [Header("Breaking Block Settings")]
        [Tooltip("Seconds required to hold left click to break a full ground tile")]
        [SerializeField] private float breakDuration = 0.75f;
        [Tooltip("8 progressive cracking frames from breaking block animation 32x16.png")]
        [SerializeField] private Sprite[] breakFrames = new Sprite[8];
        [Tooltip("Whether to spawn crunchy debris particles when a block breaks")]
        [SerializeField] private bool spawnBreakDebris = true;

        [Header("Visual Styling & Slow Pulse")]
        [SerializeField] private Color highlightColor = Color.white;
        [SerializeField] private bool enablePulse = true;
        [Tooltip("Slow, gentle breathing pulse speed")]
        [SerializeField] private float pulseSpeed = 2.2f;
        [SerializeField] private float minAlpha = 0.45f;
        [SerializeField] private float maxAlpha = 1.0f;

        [Header("Runtime State")]
        [SerializeField] private bool isHovering = false;
        [SerializeField] private Vector2Int hoveredGridPos;
        [SerializeField] private BlockQuadrant hoveredQuadrant;
        [SerializeField] private int hoveredElevation = 0;
        [SerializeField] private bool hoveringPlacedQuad = false;

        [Header("Breaking Runtime State")]
        [SerializeField] private bool isBreaking = false;
        [SerializeField] private float breakTimer = 0f;
        [SerializeField] private Vector2Int breakingGridPos;
        [SerializeField] private BlockQuadrant breakingQuadrant;

        private GameObject normalOutlineObj;
        private SpriteRenderer normalRenderer;
        private GameObject quarterOutlineObj;
        private SpriteRenderer quarterRenderer;
        private GameObject quadHighlightObj;
        private SpriteRenderer quadHighlightRenderer;
        private GameObject quadWhiteFlashObj;
        private SpriteRenderer quadWhiteFlashRenderer;
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
        public int HoveredElevation => hoveredElevation;
        public bool HoveringPlacedQuad => hoveringPlacedQuad;
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
            if (northQuadHighlightSprite == null)
            {
                northQuadHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/north quad highlight.png");
            }
            if (eastQuadHighlightSprite == null)
            {
                eastQuadHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/east quad highlight.png");
            }
            if (southQuadHighlightSprite == null)
            {
                southQuadHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/south quad highlight.png");
            }
            if (westQuadHighlightSprite == null)
            {
                westQuadHighlightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/west quad highlight.png");
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
            if (northQuadHighlightSprite == null || eastQuadHighlightSprite == null || southQuadHighlightSprite == null || westQuadHighlightSprite == null || quarterBlockOutlineSprite == null)
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var sp in all)
                {
                    if (northQuadHighlightSprite == null && sp.name.StartsWith("north quad highlight")) northQuadHighlightSprite = sp;
                    if (eastQuadHighlightSprite == null && sp.name.StartsWith("east quad highlight")) eastQuadHighlightSprite = sp;
                    if (southQuadHighlightSprite == null && sp.name.StartsWith("south quad highlight")) southQuadHighlightSprite = sp;
                    if (westQuadHighlightSprite == null && sp.name.StartsWith("west quad highlight")) westQuadHighlightSprite = sp;
                    if (quarterBlockOutlineSprite == null && sp.name.StartsWith("quarter block white outline")) quarterBlockOutlineSprite = sp;
                }
            }
        }

        public Sprite GetQuadHighlightSprite(BlockQuadrant quadrant)
        {
            switch (quadrant)
            {
                case BlockQuadrant.North: return northQuadHighlightSprite;
                case BlockQuadrant.East:  return eastQuadHighlightSprite;
                case BlockQuadrant.South: return southQuadHighlightSprite;
                case BlockQuadrant.West:  return westQuadHighlightSprite;
                default: return northQuadHighlightSprite;
            }
        }

        private void CreateVisualObjects()
        {
            EnsureSpritesLoaded();

            // 1. Normal Full-Tile Diamond Outline
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

            // 2. Placed Quarter Block Outline ('quarter block white outline.png')
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

            // 3. Directional Quad Highlight Cursor (N, E, S, W)
            if (quadHighlightObj == null)
            {
                Transform existing = transform.Find("Quad_Highlight_Cursor");
                quadHighlightObj = existing != null ? existing.gameObject : new GameObject("Quad_Highlight_Cursor");
                quadHighlightObj.transform.SetParent(transform, false);
            }
            quadHighlightRenderer = quadHighlightObj.GetComponent<SpriteRenderer>();
            if (quadHighlightRenderer == null) quadHighlightRenderer = quadHighlightObj.AddComponent<SpriteRenderer>();
            quadHighlightRenderer.sprite = northQuadHighlightSprite;
            quadHighlightRenderer.color = highlightColor;

            // 4. White Damage Flash Overlay for Quads
            if (quadWhiteFlashObj == null)
            {
                Transform existing = transform.Find("Quad_White_Flash_Overlay");
                quadWhiteFlashObj = existing != null ? existing.gameObject : new GameObject("Quad_White_Flash_Overlay");
                quadWhiteFlashObj.transform.SetParent(transform, false);
            }
            quadWhiteFlashRenderer = quadWhiteFlashObj.GetComponent<SpriteRenderer>();
            if (quadWhiteFlashRenderer == null) quadWhiteFlashRenderer = quadWhiteFlashObj.AddComponent<SpriteRenderer>();
            quadWhiteFlashRenderer.color = new Color(1f, 1f, 1f, 0.95f);
            quadWhiteFlashObj.SetActive(false);

            // 5. Breaking Full Block Cracking Overlay Object
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

            // If hovering over a tree, suppress block outline
            if (HarvestableTree.HoveredTree != null)
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

            // 1. Placement Target with Elevation / Surface Stacking
            if (QuarterBlockManager.Instance != null)
            {
                var target = QuarterBlockManager.Instance.GetHoveredPlacementTarget(mouseWorld);
                hoveredGridPos = target.gridPos;
                hoveredQuadrant = target.quadrant;
                hoveredElevation = target.elevation;
            }
            else
            {
                hoveredGridPos = QuarterBlockManager.WorldToTileCoord(mouseWorld);
                hoveredQuadrant = QuarterBlockManager.GetQuadrantFromWorld(mouseWorld, hoveredGridPos);
                hoveredElevation = 0;
            }
            isHovering = true;

            // Check if hovered quadrant has an existing placed quad block
            int stackHeight = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.GetStackHeight(hoveredGridPos, hoveredQuadrant) : 0;
            hoveringPlacedQuad = (stackHeight > 0);

            // Determine whether holding quad block
            bool isHoldingQuad = QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.IsHoldingQuadBlock(out QuarterBlockType heldQuadType);

            // 2. Slow gentle pulse
            float currentAlpha = maxAlpha;
            float pulseScale = 1.0f;
            if (enablePulse)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                currentAlpha = Mathf.SmoothStep(minAlpha, maxAlpha, t);
                pulseScale = Mathf.Lerp(1.0f, 1.03f, t);
            }
            Color renderColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, currentAlpha);

            bool isTileBroken = OutdoorInfiniteTerrain.Instance != null && OutdoorInfiniteTerrain.Instance.IsTileBroken(hoveredGridPos.x, hoveredGridPos.y);
            int groundElevation = isTileBroken ? -1 : 0;
            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(hoveredGridPos, groundElevation);
            float quadVertAdjust = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuadVerticalAdjustment : 0f;
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(hoveredGridPos.x, hoveredGridPos.y, groundElevation, -8000 + 50);

            // 3. Highlight Mode Execution:
            if (hoveringPlacedQuad && hoverMode != HoverHighlightMode.NormalOnly)
            {
                // CASE A: Hovering over an existing placed quad block!
                // "just use the quad white outline to highlight the quads once they are placed to break them"
                if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
                if (quadHighlightObj != null) quadHighlightObj.SetActive(false);

                if (quarterOutlineObj != null)
                {
                    quarterOutlineObj.SetActive(true);
                    int topElevation = stackHeight - 1;
                    Vector2 quadPos = QuarterBlockManager.Instance.GetQuarterBlockWorldPosition(hoveredGridPos, hoveredQuadrant, topElevation);
                    quarterOutlineObj.transform.position = new Vector3(quadPos.x, quadPos.y, 0f);
                    quarterOutlineObj.transform.localScale = Vector3.one;

                    if (quarterRenderer != null)
                    {
                        quarterRenderer.sprite = quarterBlockOutlineSprite;
                        quarterRenderer.color = renderColor;
                        int stackOrder = topElevation * 10;
                        quarterRenderer.sortingOrder = baseSortingOrder + stackOrder + QuarterBlockManager.GetQuadrantSortingOffset(hoveredQuadrant) + 4;
                    }
                }
            }
            else if (isHoldingQuad || hoverMode == HoverHighlightMode.QuarterOnly)
            {
                // CASE B: Empty quadrant + holding a quad block tile (or QuarterOnly mode)!
                // Show directional placement highlight (N, E, S, W) pulsing slowly
                if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
                if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);

                if (quadHighlightObj != null)
                {
                    quadHighlightObj.SetActive(true);
                    Vector2 surfacePos = QuarterBlockManager.Instance.GetQuarterBlockWorldPosition(hoveredGridPos, hoveredQuadrant, hoveredElevation);
                    quadHighlightObj.transform.position = new Vector3(surfacePos.x, surfacePos.y, 0f);
                    quadHighlightObj.transform.localScale = new Vector3(pulseScale, pulseScale, 1f);

                    if (quadHighlightRenderer != null)
                    {
                        quadHighlightRenderer.sprite = GetQuadHighlightSprite(hoveredQuadrant);
                        quadHighlightRenderer.color = renderColor;
                        int stackOrder = hoveredElevation * 10;
                        quadHighlightRenderer.sortingOrder = baseSortingOrder + stackOrder + QuarterBlockManager.GetQuadrantSortingOffset(hoveredQuadrant) + 15;
                    }
                }
            }
            else
            {
                // CASE C: Empty quadrant + NOT holding quad block:
                // Show normal full-tile diamond outline
                if (quadHighlightObj != null) quadHighlightObj.SetActive(false);
                if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);

                if (normalOutlineObj != null)
                {
                    normalOutlineObj.SetActive(true);
                    normalOutlineObj.transform.position = new Vector3(tileVisualCenter.x, tileVisualCenter.y + quadVertAdjust, 0f);
                    normalOutlineObj.transform.localScale = Vector3.one;
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

            // If hovering over a harvestable tree, let HarvestableTree process clicks!
            if (HarvestableTree.HoveredTree != null)
            {
                CancelBreaking();
                return;
            }

            bool leftClickDown = false;
            bool leftClickHeld = false;
            bool rightClickDown = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                leftClickDown = Mouse.current.leftButton.wasPressedThisFrame;
                leftClickHeld = Mouse.current.leftButton.isPressed;
                rightClickDown = Mouse.current.rightButton.wasPressedThisFrame;
            }
#else
            leftClickDown = Input.GetMouseButtonDown(0);
            leftClickHeld = Input.GetMouseButton(0);
            rightClickDown = Input.GetMouseButtonDown(1);
#endif

            // 1. LEFT CLICK ON PLACED QUAD: ONE-HIT BREAK WITH WHITE OVERLAY FLASH!
            // "Use the same tree mechanic with the quads, one hit can break them, just use the white outline and white overlay when they are hit."
            if (leftClickDown && isHovering && !IsPointerOverUI() && hoveringPlacedQuad)
            {
                CancelBreaking();
                BreakQuadBlockInstant(hoveredGridPos, hoveredQuadrant);
                return;
            }

            // 2. RIGHT CLICK: Place / Build Up Quarter Block when holding quad block
            if (rightClickDown && isHovering && !IsPointerOverUI())
            {
                if (QuarterBlockManager.Instance != null)
                {
                    if (QuarterBlockManager.Instance.IsHoldingQuadBlock(out QuarterBlockType heldType))
                    {
                        if (QuarterBlockManager.Instance.PushQuarterBlock(hoveredGridPos, hoveredQuadrant, heldType))
                        {
                            QuarterBlockManager.Instance.ConsumeFromInventory(heldType, 1);
                            if (IsometricGame.UI.HotbarUI.Instance != null)
                            {
                                IsometricGame.UI.HotbarUI.Instance.SyncWithInventory();
                            }
                        }
                    }
                }
            }

            // 3. LEFT CLICK (HOLD): Break Full Ground Tile (when not targeting a quad and not already broken)
            bool isTileAlreadyBroken = OutdoorInfiniteTerrain.Instance != null && OutdoorInfiniteTerrain.Instance.IsTileBroken(hoveredGridPos.x, hoveredGridPos.y);
            if (leftClickHeld && isHovering && !IsPointerOverUI() && !hoveringPlacedQuad && !isTileAlreadyBroken)
            {
                if (!isBreaking || hoveredGridPos != breakingGridPos)
                {
                    isBreaking = true;
                    breakTimer = 0f;
                    breakingGridPos = hoveredGridPos;
                    breakingQuadrant = hoveredQuadrant;
                }

                breakTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(breakTimer / breakDuration);
                int frameIndex = Mathf.Clamp(Mathf.FloorToInt(progress * 8f), 0, 7);

                UpdateBreakingOverlayVisual(frameIndex);

                if (breakTimer >= breakDuration)
                {
                    ExecuteFullTileBreak();
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

        public void BreakQuadBlockInstant(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            if (QuarterBlockManager.Instance == null) return;
            int stackHeight = QuarterBlockManager.Instance.GetStackHeight(gridPos, quadrant);
            if (stackHeight <= 0) return;

            int topElevation = stackHeight - 1;
            QuarterBlockType qType = QuarterBlockManager.Instance.GetTopQuarterBlock(gridPos, quadrant);
            Vector2 quadPos = QuarterBlockManager.Instance.GetQuarterBlockWorldPosition(gridPos, quadrant, topElevation);

            // 1. Pop the block from manager
            QuarterBlockManager.Instance.PopQuarterBlock(gridPos, quadrant);

            // 2. White damage overlay flash with impact micro-shake!
            StartCoroutine(FlashQuadWhiteOverlayRoutine(quadPos, qType, topElevation, quadrant));

            // 3. Spawn 1 floating dropped item on floor
            QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(quadPos, qType, 1);

            // 4. Crunchy debris particles
            Color c = new Color(0.38f, 0.65f, 0.28f); // grass green
            if (qType == QuarterBlockType.Dirt) c = new Color(0.55f, 0.38f, 0.22f);
            else if (qType == QuarterBlockType.Log) c = new Color(0.48f, 0.32f, 0.18f);
            SpawnDebrisParticles(quadPos, c);

            if (IsometricGame.UI.HotbarUI.Instance != null)
            {
                IsometricGame.UI.HotbarUI.Instance.SyncWithInventory();
            }
        }

        private IEnumerator FlashQuadWhiteOverlayRoutine(Vector2 quadPos, QuarterBlockType qType, int elevation, BlockQuadrant quadrant)
        {
            if (quadWhiteFlashObj == null)
            {
                Transform existing = transform.Find("Quad_White_Flash_Overlay");
                quadWhiteFlashObj = existing != null ? existing.gameObject : new GameObject("Quad_White_Flash_Overlay");
                quadWhiteFlashObj.transform.SetParent(transform, false);
                quadWhiteFlashRenderer = quadWhiteFlashObj.GetComponent<SpriteRenderer>();
                if (quadWhiteFlashRenderer == null) quadWhiteFlashRenderer = quadWhiteFlashObj.AddComponent<SpriteRenderer>();
            }

            Sprite targetSprite = null;
            if (QuarterBlockManager.Instance != null)
            {
                if (qType == QuarterBlockType.Dirt) targetSprite = QuarterBlockManager.Instance.QuarterDirtSprite;
                else if (qType == QuarterBlockType.Log) targetSprite = QuarterBlockManager.Instance.QuarterLogSprite;
                else targetSprite = QuarterBlockManager.Instance.QuarterGrassSprite;
            }

            if (quadWhiteFlashRenderer != null && targetSprite != null)
            {
                quadWhiteFlashRenderer.sprite = targetSprite;
                quadWhiteFlashRenderer.color = new Color(1f, 1f, 1f, 0.95f);
                int baseOrder = IsometricCoordinates.CalculateSortingOrder(hoveredGridPos.x, hoveredGridPos.y, 0, -8000 + 4);
                quadWhiteFlashRenderer.sortingOrder = baseOrder + (elevation * 10) + QuarterBlockManager.GetQuadrantSortingOffset(quadrant) + 8;
                quadWhiteFlashObj.transform.position = new Vector3(quadPos.x, quadPos.y, 0f);
                quadWhiteFlashObj.SetActive(true);
            }

            float elapsed = 0f;
            float duration = 0.10f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float shakeX = Mathf.Sin(elapsed * 75f) * 0.02f * (1f - elapsed / duration);
                if (quadWhiteFlashObj != null)
                {
                    quadWhiteFlashObj.transform.position = new Vector3(quadPos.x + shakeX, quadPos.y, 0f);
                }
                yield return null;
            }

            if (quadWhiteFlashObj != null) quadWhiteFlashObj.SetActive(false);
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
            float quadVertAdjust = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuadVerticalAdjustment : 0f;
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(breakingGridPos.x, breakingGridPos.y, 0, -8000 + 40);

            // Micro-vibration strike shake
            float shakeX = Mathf.Sin(breakTimer * 55f) * 0.007f;

            breakingOverlayObj.transform.position = new Vector3(tileVisualCenter.x + shakeX, tileVisualCenter.y + quadVertAdjust, 0f);
            breakingOverlayObj.transform.localScale = Vector3.one;
            breakingRenderer.sortingOrder = baseSortingOrder;
        }

        private void ExecuteFullTileBreak()
        {
            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(breakingGridPos, 0);
            float quadVertAdjust = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuadVerticalAdjustment : 0f;
            Vector2 breakPos = tileVisualCenter + new Vector2(0f, quadVertAdjust);

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
                QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(breakPos, QuarterBlockType.Grass, 4);
            }

            if (spawnBreakDebris)
            {
                SpawnDebrisParticles(breakPos, new Color(0.38f, 0.65f, 0.28f));
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

        public void CancelBreaking()
        {
            isBreaking = false;
            breakTimer = 0f;
            if (breakingOverlayObj != null) breakingOverlayObj.SetActive(false);
        }

        public void HideHighlight()
        {
            isHovering = false;
            if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
            if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);
            if (quadHighlightObj != null) quadHighlightObj.SetActive(false);
        }

        private static bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

        private static Vector2 GetMouseScreenPosition()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
            return Input.mousePosition;
        }

        private void OnDisable()
        {
            HideHighlight();
            CancelBreaking();
        }
    }
}
