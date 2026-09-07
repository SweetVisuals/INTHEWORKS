using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsometricGame.Tilemap
{
    public enum BlockQuadrant
    {
        North = 0, // Top (+Y screen offset = +0.125f / +4px)
        South = 1, // Bottom (-Y screen offset = -0.125f / -4px)
        East = 2,  // Right (+X screen offset = +0.25f / +8px)
        West = 3   // Left (-X screen offset = -0.25f / -8px)
    }

    public enum QuarterBlockType
    {
        None = 0,
        Grass = 1,
        Dirt = 2,
        Log = 3
    }

    /// <summary>
    /// Manages sub-tile quarter blocks and vertical stacking/building up:
    /// - Exactly 4 quarter blocks fit pixel-perfect inside 1 standard isometric tile:
    ///   - North: dx =   0px (+0.000 units), dy = +4px (+0.125 units)
    ///   - South: dx =   0px (+0.000 units), dy = -4px (-0.125 units)
    ///   - East:  dx =  +8px (+0.250 units), dy =  0px (+0.000 units)
    ///   - West:  dx =  -8px (-0.250 units), dy =  0px (+0.000 units)
    /// - Supports vertical stacking / building up along each quadrant (step height = 5px / 0.15625 units).
    /// - Manages Grass, Dirt, and Log quad blocks with inventory sync.
    /// </summary>
    [ExecuteAlways]
    public class QuarterBlockManager : MonoBehaviour
    {
        public static QuarterBlockManager Instance { get; private set; }

        [Header("Quarter Block Sprites")]
        [SerializeField] private Sprite quarterGrassSprite;
        [SerializeField] private Sprite quarterDirtSprite;
        [SerializeField] private Sprite quarterLogSprite;

        [Header("Stacking / Building Up")]
        [Tooltip("Vertical world step height for stacking quarter blocks on top of each other (5px = 0.15625f at 32 PPU)")]
        [SerializeField] private float quarterBlockStackStepHeight = 0.15625f;
        [SerializeField] private int maxStackHeight = 16;
        [SerializeField] private bool enableSortingDepth = true;

        // Tile -> 4 Quadrants -> Stack of types (North, South, East, West)
        private readonly Dictionary<Vector2Int, List<QuarterBlockType>[]> tileQuarterStacks = new Dictionary<Vector2Int, List<QuarterBlockType>[]>();
        // Tile -> 4 Quadrants -> Stack of GameObjects
        private readonly Dictionary<Vector2Int, List<GameObject>[]> tileQuarterObjectStacks = new Dictionary<Vector2Int, List<GameObject>[]>();

        [Header("Inventory")]
        [SerializeField] private int quarterGrassInventory = 4;
        [SerializeField] private int quarterDirtInventory = 4;
        [SerializeField] private int quarterLogInventory = 0;

        public int QuarterGrassInventory => quarterGrassInventory;
        public int QuarterDirtInventory => quarterDirtInventory;
        public int QuarterLogInventory => quarterLogInventory;

        public Sprite QuarterGrassSprite => quarterGrassSprite;
        public Sprite QuarterDirtSprite => quarterDirtSprite;
        public Sprite QuarterLogSprite => quarterLogSprite;
        public float QuarterBlockStackStepHeight => quarterBlockStackStepHeight;
        public int MaxStackHeight => maxStackHeight;

        /// <summary>
        /// Returns true if the player currently has a quad block tile selected in the hotbar (Slot 0 for Grass, Slot 1 for Dirt, Slot 2 for Log)
        /// and has at least 1 in inventory.
        /// </summary>
        public bool IsHoldingQuadBlock(out QuarterBlockType heldType)
        {
            heldType = QuarterBlockType.None;
            if (IsometricGame.UI.HotbarUI.Instance == null) return false;

            int slot = IsometricGame.UI.HotbarUI.Instance.SelectedSlotIndex;
            if (slot == 0 && quarterGrassInventory > 0)
            {
                heldType = QuarterBlockType.Grass;
                return true;
            }
            if (slot == 1 && quarterDirtInventory > 0)
            {
                heldType = QuarterBlockType.Dirt;
                return true;
            }
            if (slot == 2 && quarterLogInventory > 0)
            {
                heldType = QuarterBlockType.Log;
                return true;
            }
            return false;
        }

        public void AddToInventory(QuarterBlockType type, int count = 1)
        {
            if (type == QuarterBlockType.Grass) quarterGrassInventory += count;
            else if (type == QuarterBlockType.Dirt) quarterDirtInventory += count;
            else if (type == QuarterBlockType.Log) quarterLogInventory += count;
        }

        public bool HasInInventory(QuarterBlockType type, int count = 1)
        {
            if (type == QuarterBlockType.Grass) return quarterGrassInventory >= count;
            if (type == QuarterBlockType.Dirt) return quarterDirtInventory >= count;
            if (type == QuarterBlockType.Log) return quarterLogInventory >= count;
            return false;
        }

        public bool ConsumeFromInventory(QuarterBlockType type, int count = 1)
        {
            if (type == QuarterBlockType.Grass && quarterGrassInventory >= count)
            {
                quarterGrassInventory -= count;
                return true;
            }
            if (type == QuarterBlockType.Dirt && quarterDirtInventory >= count)
            {
                quarterDirtInventory -= count;
                return true;
            }
            if (type == QuarterBlockType.Log && quarterLogInventory >= count)
            {
                quarterLogInventory -= count;
                return true;
            }
            return false;
        }

        public void SpawnDroppedQuarterBlocks(Vector2 tileOrigin, QuarterBlockType type, int count = 4)
        {
            EnsureSpritesLoaded();
            Sprite sprite = quarterGrassSprite;
            if (type == QuarterBlockType.Dirt) sprite = quarterDirtSprite;
            else if (type == QuarterBlockType.Log) sprite = quarterLogSprite;

            Vector2[] scatterOffsets = new Vector2[]
            {
                new Vector2(0f, 0.22f),
                new Vector2(0f, -0.22f),
                new Vector2(0.28f, 0f),
                new Vector2(-0.28f, 0f)
            };

            for (int i = 0; i < count; i++)
            {
                Vector2 targetFloor = tileOrigin + scatterOffsets[i % scatterOffsets.Length] + (Vector2)UnityEngine.Random.insideUnitCircle * 0.05f;
                IsometricGame.Environment.DroppedQuarterBlock.Spawn(tileOrigin, targetFloor, type, sprite);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInit()
        {
            EnsureManagerActive();
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
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureSpritesLoaded();
        }

        public static void EnsureManagerActive()
        {
            if (Instance != null) return;

            QuarterBlockManager mgr = FindAnyObjectByType<QuarterBlockManager>();
            if (mgr == null)
            {
                GameObject obj = new GameObject("QuarterBlockManager");
                mgr = obj.AddComponent<QuarterBlockManager>();
            }
            Instance = mgr;
            mgr.EnsureSpritesLoaded();
        }

        public void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (quarterGrassSprite == null)
            {
                quarterGrassSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/quarter grass block.png");
            }
            if (quarterDirtSprite == null)
            {
                quarterDirtSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/quarter dirt block.png");
            }
            if (quarterLogSprite == null)
            {
                quarterLogSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/quarter block log block.png");
            }
#endif
            if (quarterGrassSprite == null || quarterDirtSprite == null || quarterLogSprite == null)
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in all)
                {
                    if (quarterGrassSprite == null && s.name.StartsWith("quarter grass block")) quarterGrassSprite = s;
                    if (quarterDirtSprite == null && s.name.StartsWith("quarter dirt block")) quarterDirtSprite = s;
                    if (quarterLogSprite == null && s.name.StartsWith("quarter block log block")) quarterLogSprite = s;
                }
            }
        }

        /// <summary>
        /// Gets the pixel-perfect 2D world offset for each sub-quadrant relative to the tile's visual diamond center.
        /// </summary>
        public static Vector2 GetQuadrantOffset(BlockQuadrant quadrant)
        {
            switch (quadrant)
            {
                case BlockQuadrant.North: return new Vector2(0f, 0.125f);   // dx=0, dy=+4px
                case BlockQuadrant.South: return new Vector2(0f, -0.125f);  // dx=0, dy=-4px
                case BlockQuadrant.East:  return new Vector2(0.25f, 0f);    // dx=+8px, dy=0
                case BlockQuadrant.West:  return new Vector2(-0.25f, 0f);   // dx=-8px, dy=0
                default: return Vector2.zero;
            }
        }

        /// <summary>
        /// Returns depth sorting offset so North (back) renders first, West/East middle, and South (front) on top.
        /// </summary>
        public static int GetQuadrantSortingOffset(BlockQuadrant quadrant)
        {
            switch (quadrant)
            {
                case BlockQuadrant.North: return 0;
                case BlockQuadrant.West:  return 1;
                case BlockQuadrant.East:  return 1;
                case BlockQuadrant.South: return 2;
                default: return 0;
            }
        }

        /// <summary>
        /// Returns the visual diamond center in world coordinates for a tile at gridPos.
        /// (In this project, 32x32 floor tiles have their visual diamond centered 8px / 0.25 units below tile center).
        /// </summary>
        public static Vector2 GetTileVisualCenter(Vector2Int gridPos, int elevation = 0)
        {
            Vector2 baseWorld = IsometricCoordinates.GridToWorld(gridPos.x, gridPos.y, elevation);
            return baseWorld + new Vector2(0f, -0.25f);
        }

        /// <summary>
        /// Converts world position to the nearest isometric tile grid coordinate.
        /// </summary>
        public static Vector2Int WorldToTileCoord(Vector2 worldPos)
        {
            Vector2 relative = worldPos - new Vector2(0f, -0.25f);
            return IsometricCoordinates.WorldToGrid(relative);
        }

        /// <summary>
        /// Determines which of the 4 sub-quadrants (North, South, East, West) a world point falls into within a tile.
        /// </summary>
        public static BlockQuadrant GetQuadrantFromWorld(Vector2 worldPos, Vector2Int tileGridPos)
        {
            Vector2 relative = worldPos - new Vector2(0f, -0.25f);
            float halfW = IsometricCoordinates.DefaultTileWidth * 0.5f;  // 0.5f
            float halfH = IsometricCoordinates.DefaultTileHeight * 0.5f; // 0.25f

            float fx = (relative.x / halfW + relative.y / halfH) * 0.5f - tileGridPos.x;
            float fy = (relative.y / halfH - relative.x / halfW) * 0.5f - tileGridPos.y;

            if (fx >= 0f && fy >= 0f) return BlockQuadrant.North;
            if (fx < 0f && fy < 0f) return BlockQuadrant.South;
            if (fx >= 0f && fy < 0f) return BlockQuadrant.East;
            return BlockQuadrant.West;
        }

        public int GetStackHeight(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            if (tileQuarterStacks.TryGetValue(gridPos, out var stacks))
            {
                int idx = (int)quadrant;
                if (idx >= 0 && idx < stacks.Length && stacks[idx] != null)
                {
                    return stacks[idx].Count;
                }
            }
            return 0;
        }

        public QuarterBlockType GetTopQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            if (tileQuarterStacks.TryGetValue(gridPos, out var stacks))
            {
                int idx = (int)quadrant;
                if (idx >= 0 && idx < stacks.Length && stacks[idx] != null && stacks[idx].Count > 0)
                {
                    return stacks[idx][stacks[idx].Count - 1];
                }
            }
            return QuarterBlockType.None;
        }

        public QuarterBlockType GetQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            return GetTopQuarterBlock(gridPos, quadrant);
        }

        public bool HasAnyQuarterBlocks(Vector2Int gridPos)
        {
            if (tileQuarterStacks.TryGetValue(gridPos, out var stacks))
            {
                for (int i = 0; i < stacks.Length; i++)
                {
                    if (stacks[i] != null && stacks[i].Count > 0) return true;
                }
            }
            return false;
        }

        public bool PushQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant, QuarterBlockType type)
        {
            if (type == QuarterBlockType.None) return false;
            EnsureSpritesLoaded();

            if (!tileQuarterStacks.TryGetValue(gridPos, out var typeStacks))
            {
                typeStacks = new List<QuarterBlockType>[4];
                for (int i = 0; i < 4; i++) typeStacks[i] = new List<QuarterBlockType>();
                tileQuarterStacks[gridPos] = typeStacks;
            }

            if (!tileQuarterObjectStacks.TryGetValue(gridPos, out var objStacks))
            {
                objStacks = new List<GameObject>[4];
                for (int i = 0; i < 4; i++) objStacks[i] = new List<GameObject>();
                tileQuarterObjectStacks[gridPos] = objStacks;
            }

            int idx = (int)quadrant;
            if (typeStacks[idx].Count >= maxStackHeight) return false;

            int elevation = typeStacks[idx].Count;
            typeStacks[idx].Add(type);

            Sprite targetSprite = quarterGrassSprite;
            if (type == QuarterBlockType.Dirt) targetSprite = quarterDirtSprite;
            else if (type == QuarterBlockType.Log) targetSprite = quarterLogSprite;

            if (targetSprite != null)
            {
                GameObject obj = new GameObject($"QuarterBlock_{gridPos.x}_{gridPos.y}_{quadrant}_L{elevation}");
                obj.transform.SetParent(transform, false);

                Vector2 center = GetTileVisualCenter(gridPos, 0);
                Vector2 quadPos = center + GetQuadrantOffset(quadrant) + new Vector2(0f, elevation * quarterBlockStackStepHeight);
                obj.transform.position = new Vector3(quadPos.x, quadPos.y, 0f);

                SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = targetSprite;

                int baseOrder = IsometricCoordinates.CalculateSortingOrder(gridPos.x, gridPos.y, 0, -8000 + 4);
                int stackSort = elevation * 10;
                sr.sortingOrder = enableSortingDepth ? (baseOrder + stackSort + GetQuadrantSortingOffset(quadrant)) : baseOrder + stackSort;

                objStacks[idx].Add(obj);
            }
            return true;
        }

        public QuarterBlockType PopQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            if (tileQuarterStacks.TryGetValue(gridPos, out var typeStacks) && tileQuarterObjectStacks.TryGetValue(gridPos, out var objStacks))
            {
                int idx = (int)quadrant;
                if (typeStacks[idx] != null && typeStacks[idx].Count > 0)
                {
                    int topIndex = typeStacks[idx].Count - 1;
                    QuarterBlockType topType = typeStacks[idx][topIndex];
                    typeStacks[idx].RemoveAt(topIndex);

                    if (objStacks[idx] != null && objStacks[idx].Count > topIndex)
                    {
                        GameObject topObj = objStacks[idx][topIndex];
                        objStacks[idx].RemoveAt(topIndex);
                        if (topObj != null)
                        {
                            if (Application.isPlaying) Destroy(topObj);
                            else DestroyImmediate(topObj);
                        }
                    }
                    return topType;
                }
            }
            return QuarterBlockType.None;
        }

        public void SetQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant, QuarterBlockType type)
        {
            if (type == QuarterBlockType.None)
            {
                PopQuarterBlock(gridPos, quadrant);
            }
            else
            {
                PushQuarterBlock(gridPos, quadrant, type);
            }
        }

        public void RemoveQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            PopQuarterBlock(gridPos, quadrant);
        }

        public void CycleQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            QuarterBlockType current = GetTopQuarterBlock(gridPos, quadrant);
            if (current == QuarterBlockType.None)
            {
                PushQuarterBlock(gridPos, quadrant, QuarterBlockType.Grass);
            }
            else if (current == QuarterBlockType.Grass)
            {
                PopQuarterBlock(gridPos, quadrant);
                PushQuarterBlock(gridPos, quadrant, QuarterBlockType.Dirt);
            }
            else if (current == QuarterBlockType.Dirt)
            {
                PopQuarterBlock(gridPos, quadrant);
                PushQuarterBlock(gridPos, quadrant, QuarterBlockType.Log);
            }
            else
            {
                PopQuarterBlock(gridPos, quadrant);
            }
        }

        /// <summary>
        /// Finds the target tile, quadrant, and top surface elevation where the cursor is pointing.
        /// Prioritizes existing elevated stacks so aiming at a raised surface directly targets it for stacking!
        /// </summary>
        public (Vector2Int gridPos, BlockQuadrant quadrant, int elevation) GetHoveredPlacementTarget(Vector2 mouseWorld)
        {
            Vector2Int groundPos = WorldToTileCoord(mouseWorld);

            // 1. Check nearby existing elevated stacks
            int bestElevation = -1;
            Vector2Int bestGrid = groundPos;
            BlockQuadrant bestQuad = BlockQuadrant.North;

            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    Vector2Int checkPos = new Vector2Int(groundPos.x + dx, groundPos.y + dy);
                    if (!tileQuarterStacks.TryGetValue(checkPos, out var stacks)) continue;

                    for (int q = 0; q < 4; q++)
                    {
                        int height = (stacks[q] != null) ? stacks[q].Count : 0;
                        if (height <= 0) continue;

                        BlockQuadrant bq = (BlockQuadrant)q;
                        Vector2 surfaceCenter = GetTileVisualCenter(checkPos, 0) + GetQuadrantOffset(bq) + new Vector2(0f, height * quarterBlockStackStepHeight);

                        float diffX = Mathf.Abs(mouseWorld.x - surfaceCenter.x);
                        float diffY = Mathf.Abs(mouseWorld.y - surfaceCenter.y);

                        // 2:1 isometric diamond hit-test for top surface
                        if ((diffX / 0.22f + diffY / 0.11f) <= 1.0f)
                        {
                            if (height > bestElevation)
                            {
                                bestElevation = height;
                                bestGrid = checkPos;
                                bestQuad = bq;
                            }
                        }
                    }
                }
            }

            if (bestElevation >= 0)
            {
                return (bestGrid, bestQuad, bestElevation);
            }

            // 2. Default to ground level tile
            BlockQuadrant groundQuad = GetQuadrantFromWorld(mouseWorld, groundPos);
            int groundStack = GetStackHeight(groundPos, groundQuad);
            return (groundPos, groundQuad, groundStack);
        }

        public void ClearAllQuarterBlocks()
        {
            foreach (var kvp in tileQuarterObjectStacks)
            {
                if (kvp.Value == null) continue;
                for (int q = 0; q < kvp.Value.Length; q++)
                {
                    if (kvp.Value[q] == null) continue;
                    for (int i = 0; i < kvp.Value[q].Count; i++)
                    {
                        if (kvp.Value[q][i] != null)
                        {
                            if (Application.isPlaying) Destroy(kvp.Value[q][i]);
                            else DestroyImmediate(kvp.Value[q][i]);
                        }
                    }
                    kvp.Value[q].Clear();
                }
            }
            tileQuarterObjectStacks.Clear();
            tileQuarterStacks.Clear();
        }
    }
}
