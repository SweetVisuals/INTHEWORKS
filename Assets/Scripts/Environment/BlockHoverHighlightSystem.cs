using System;
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
    /// Manages the visual block hover highlight cursor in 2D Isometric world space.
    /// Tracks the mouse position, detects the hovered tile and quadrant,
    /// and displays pixel-perfect white hover outlines:
    /// - Normal Block: 'Assets/Sprites/normal block hover white outline.png'
    /// - Quarter Block: 'Assets/Sprites/quarter block white outline.png'
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
        [Tooltip("Whether clicking left-click places/cycles quarter blocks and right-click removes them")]
        [SerializeField] private bool enableBlockInteraction = true;

        [Header("Sprites")]
        [SerializeField] private Sprite normalBlockOutlineSprite;
        [SerializeField] private Sprite quarterBlockOutlineSprite;

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

        private GameObject normalOutlineObj;
        private SpriteRenderer normalRenderer;
        private GameObject quarterOutlineObj;
        private SpriteRenderer quarterRenderer;

        public HoverHighlightMode CurrentHoverMode
        {
            get => hoverMode;
            set => hoverMode = value;
        }

        public bool IsHovering => isHovering;
        public Vector2Int HoveredGridPos => hoveredGridPos;
        public BlockQuadrant HoveredQuadrant => hoveredQuadrant;

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
            CreateOutlineObjects();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureSpritesLoaded();
            CreateOutlineObjects();
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
            sys.CreateOutlineObjects();
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
#endif
        }

        private void CreateOutlineObjects()
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
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                HideHighlight();
                return;
            }

            Vector2 mouseScreen = GetMouseScreenPosition();
            // Check if within screen window
            if (mouseScreen.x < 0 || mouseScreen.x > Screen.width || mouseScreen.y < 0 || mouseScreen.y > Screen.height)
            {
                HideHighlight();
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
                // If tile has quarter blocks placed, highlight hovered quadrant
                bool hasQuarterBlocks = QuarterBlockManager.Instance != null && QuarterBlockManager.Instance.HasAnyQuarterBlocks(hoveredGridPos);
                showQuarter = hasQuarterBlocks;
            }

            showingQuarterOutline = showQuarter;

            // Pulse alpha
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
            if (!enableBlockInteraction || !isHovering) return;
            if (QuarterBlockManager.Instance == null) return;

            bool leftClick = false;
            bool rightClick = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                leftClick = Mouse.current.leftButton.wasPressedThisFrame;
                rightClick = Mouse.current.rightButton.wasPressedThisFrame;
            }
#else
            leftClick = Input.GetMouseButtonDown(0);
            rightClick = Input.GetMouseButtonDown(1);
#endif

            if (leftClick)
            {
                if (showingQuarterOutline || hoverMode == HoverHighlightMode.QuarterOnly)
                {
                    QuarterBlockManager.Instance.CycleQuarterBlock(hoveredGridPos, hoveredQuadrant);
                }
                else
                {
                    // If contextual on normal tile with no quarter blocks, place quarter dirt block on hovered quadrant
                    QuarterBlockManager.Instance.CycleQuarterBlock(hoveredGridPos, hoveredQuadrant);
                }
            }
            else if (rightClick)
            {
                QuarterBlockManager.Instance.RemoveQuarterBlock(hoveredGridPos, hoveredQuadrant);
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
