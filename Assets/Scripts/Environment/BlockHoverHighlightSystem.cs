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
        Contextual = 0, // Automatically highlights quad only when holding quad block, else normal tile outline
        NormalOnly = 1, // Always normal full-tile diamond outline
        QuarterOnly = 2 // Always quarter-block quadrant outline
    }

    /// <summary>
    /// Manages the visual block hover highlight cursor in 2D Isometric world space
    /// and the progressive breaking block animation system:
    /// - Tracks mouse position, detects hovered tile, quadrant, and stacked surface elevation.
    /// - ONLY shows the quad highlight when holding a quad block tile (Grass, Dirt, Log).
    ///   - Displays directional quad highlights:
    ///     - North: 'Assets/Sprites/north quad highlight.png'
    ///     - East:  'Assets/Sprites/east quad highlight.png'
    ///     - South: 'Assets/Sprites/south quad highlight.png'
    ///     - West:  'Assets/Sprites/west quad highlight.png'
    ///   - Gently and slowly pulses to indicate placement readiness.
    ///   - Shows on the surface of quad blocks so you can build up vertically.
    /// - Displays normal block hover outline ('Assets/Sprites/normal block hover white outline.png') when not holding quad block.
    /// - Suppresses tile outlines when targeting a harvestable pine tree.
    /// - Holding Left-Click breaks blocks with the 8-stage cracking animation.
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
        [Tooltip("Whether clicking left-click (hold) breaks blocks and right-click places them")]
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
        [Tooltip("Seconds required to hold left click to completely break a block")]
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
        [SerializeField] private bool showingQuarterOutline;

        [Header("Breaking Runtime State")]
        [SerializeField] private bool isBreaking = false;
        [SerializeField] private float breakTimer = 0f;
        [SerializeField] private Vector2Int breakingGridPos;
        [SerializeField] private BlockQuadrant breakingQuadrant;
        [SerializeField] private int breakingElevation = 0;
        [SerializeField] private bool isBreakingQuarter = false;

        private GameObject normalOutlineObj;
        private SpriteRenderer normalRenderer;
        private GameObject quarterOutlineObj;
        private SpriteRenderer quarterRenderer;
        private GameObject quadHighlightObj;
        private SpriteRenderer quadHighlightRenderer;
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
            if (northQuadHighlightSprite == null || eastQuadHighlightSprite == null || southQuadHighlightSprite == null || westQuadHighlightSprite == null)
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var sp in all)
                {
                    if (northQuadHighlightSprite == null && sp.name.StartsWith("north quad highlight")) northQuadHighlightSprite = sp;
                    if (eastQuadHighlightSprite == null && sp.name.StartsWith("east quad highlight")) eastQuadHighlightSprite = sp;
                    if (southQuadHighlightSprite == null && sp.name.StartsWith("south quad highlight")) southQuadHighlightSprite = sp;
                    if (westQuadHighlightSprite == null && sp.name.StartsWith("west quad highlight")) westQuadHighlightSprite = sp;
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

            // 2. Directional Quad Highlight Cursor (N, E, S, W)
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

            // 3. Fallback Quarter Outline Object
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

            // 4. Breaking Block Cracking Overlay Object
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

            // 2. Determine whether holding quad block:
            // "only show the quad highlight when holding a quad block tile"
            bool isHoldingQuad = QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.IsHoldingQuadBlock(out QuarterBlockType heldQuadType);

            bool showQuadHighlight = false;
            if (hoverMode == HoverHighlightMode.QuarterOnly)
            {
                showQuadHighlight = true;
            }
            else if (hoverMode == HoverHighlightMode.NormalOnly)
            {
                showQuadHighlight = false;
            }
            else // Contextual: strictly only when holding a quad block tile
            {
                showQuadHighlight = isHoldingQuad;
            }

            showingQuarterOutline = showQuadHighlight;

            // 3. Slow gentle pulse
            float currentAlpha = maxAlpha;
            float pulseScale = 1.0f;
            if (enablePulse)
            {
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
                currentAlpha = Mathf.SmoothStep(minAlpha, maxAlpha, t);
                pulseScale = Mathf.Lerp(1.0f, 1.03f, t);
            }
            Color renderColor = new Color(highlightColor.r, highlightColor.g, highlightColor.b, currentAlpha);

            Vector2 tileVisualCenter = QuarterBlockManager.GetTileVisualCenter(hoveredGridPos, 0);
            float stepHeight = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuarterBlockStackStepHeight : 0.15625f;
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(hoveredGridPos.x, hoveredGridPos.y, 0, -8000 + 50);

            if (showQuadHighlight)
            {
                if (normalOutlineObj != null) normalOutlineObj.SetActive(false);
                if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);

                if (quadHighlightObj != null)
                {
                    quadHighlightObj.SetActive(true);
                    // Elevated top surface position so you can build up!
                    Vector2 surfacePos = tileVisualCenter + new Vector2(0f, hoveredElevation * stepHeight);
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
                if (quadHighlightObj != null) quadHighlightObj.SetActive(false);
                if (quarterOutlineObj != null) quarterOutlineObj.SetActive(false);

                if (normalOutlineObj != null)
                {
                    normalOutlineObj.SetActive(true);
                    normalOutlineObj.transform.position = new Vector3(tileVisualCenter.x, tileVisualCenter.y, 0f);
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

            // 1. Right Click: Place / Build Up Quarter Block when holding quad block
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

            // 2. Left Click (Hold): Breaking Block Animation & Destruction
            if (leftClickHeld && isHovering && !IsPointerOverUI())
            {
                int stackHeight = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.GetStackHeight(hoveredGridPos, hoveredQuadrant) : 0;
                bool targetIsQuarter = stackHeight > 0;

                // If targeting changed or not already breaking, start fresh break
                if (!isBreaking || hoveredGridPos != breakingGridPos || (targetIsQuarter && (hoveredQuadrant != breakingQuadrant || stackHeight != breakingElevation)))
                {
                    isBreaking = true;
                    breakTimer = 0f;
                    breakingGridPos = hoveredGridPos;
                    breakingQuadrant = hoveredQuadrant;
                    breakingElevation = stackHeight;
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
            float stepHeight = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuarterBlockStackStepHeight : 0.15625f;
            int baseSortingOrder = IsometricCoordinates.CalculateSortingOrder(breakingGridPos.x, breakingGridPos.y, 0, -8000 + 40);

            // Micro-vibration strike shake
            float shakeX = Mathf.Sin(breakTimer * 55f) * 0.007f;

            if (isBreakingQuarter)
            {
                int topElevation = Mathf.Max(0, breakingElevation - 1);
                Vector2 quadOffset = QuarterBlockManager.GetQuadrantOffset(breakingQuadrant);
                Vector2 targetPos = tileVisualCenter + quadOffset + new Vector2(shakeX, topElevation * stepHeight);

                breakingOverlayObj.transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
                breakingOverlayObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                int stackOrder = topElevation * 10;
                breakingRenderer.sortingOrder = baseSortingOrder + stackOrder + QuarterBlockManager.GetQuadrantSortingOffset(breakingQuadrant) + 12;
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
            float stepHeight = (QuarterBlockManager.Instance != null) ? QuarterBlockManager.Instance.QuarterBlockStackStepHeight : 0.15625f;

            if (isBreakingQuarter)
            {
                QuarterBlockType qType = QuarterBlockType.Grass;
                int topElevation = 0;
                if (QuarterBlockManager.Instance != null)
                {
                    topElevation = Mathf.Max(0, QuarterBlockManager.Instance.GetStackHeight(breakingGridPos, breakingQuadrant) - 1);
                    qType = QuarterBlockManager.Instance.PopQuarterBlock(breakingGridPos, breakingQuadrant);
                }

                Vector2 quadPos = tileVisualCenter + QuarterBlockManager.GetQuadrantOffset(breakingQuadrant) + new Vector2(0f, topElevation * stepHeight);

                // Drops 1 small floating quarter block
                if (QuarterBlockManager.Instance != null && qType != QuarterBlockType.None)
                {
                    QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(quadPos, qType, 1);
                }

                if (spawnBreakDebris)
                {
                    Color c = new Color(0.38f, 0.65f, 0.28f); // grass
                    if (qType == QuarterBlockType.Dirt) c = new Color(0.55f, 0.38f, 0.22f);
                    else if (qType == QuarterBlockType.Log) c = new Color(0.48f, 0.32f, 0.18f);
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
