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
        Dirt = 2
    }

    /// <summary>
    /// Manages sub-tile quarter blocks. Exactly 4 quarter blocks fit pixel-perfect inside 1 standard isometric tile:
    /// - North:  dx =   0px (+0.000 units), dy = +4px (+0.125 units)
    /// - South:  dx =   0px (+0.000 units), dy = -4px (-0.125 units)
    /// - East:   dx =  +8px (+0.250 units), dy =  0px (+0.000 units)
    /// - West:   dx =  -8px (-0.250 units), dy =  0px (+0.000 units)
    /// </summary>
    [ExecuteAlways]
    public class QuarterBlockManager : MonoBehaviour
    {
        public static QuarterBlockManager Instance { get; private set; }

        [Header("Quarter Block Sprites")]
        [SerializeField] private Sprite quarterGrassSprite;
        [SerializeField] private Sprite quarterDirtSprite;

        [Header("Settings")]
        [SerializeField] private bool enableSortingDepth = true;

        // Tile -> 4 Quadrant types [North, South, East, West]
        private readonly Dictionary<Vector2Int, QuarterBlockType[]> tileQuarterBlocks = new Dictionary<Vector2Int, QuarterBlockType[]>();
        // Tile -> 4 Quadrant GameObjects
        private readonly Dictionary<Vector2Int, GameObject[]> tileQuarterObjects = new Dictionary<Vector2Int, GameObject[]>();

        [Header("Inventory")]
        [SerializeField] private int quarterGrassInventory = 0;
        [SerializeField] private int quarterDirtInventory = 0;

        public int QuarterGrassInventory => quarterGrassInventory;
        public int QuarterDirtInventory => quarterDirtInventory;

        public Sprite QuarterGrassSprite => quarterGrassSprite;
        public Sprite QuarterDirtSprite => quarterDirtSprite;

        public void AddToInventory(QuarterBlockType type, int count = 1)
        {
            if (type == QuarterBlockType.Grass) quarterGrassInventory += count;
            else if (type == QuarterBlockType.Dirt) quarterDirtInventory += count;
        }

        public bool HasInInventory(QuarterBlockType type, int count = 1)
        {
            if (type == QuarterBlockType.Grass) return quarterGrassInventory >= count;
            if (type == QuarterBlockType.Dirt) return quarterDirtInventory >= count;
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
            return false;
        }

        public void SpawnDroppedQuarterBlocks(Vector2 tileOrigin, QuarterBlockType type, int count = 4)
        {
            EnsureSpritesLoaded();
            Sprite sprite = (type == QuarterBlockType.Grass) ? quarterGrassSprite : quarterDirtSprite;

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
#endif
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

        public QuarterBlockType GetQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            if (tileQuarterBlocks.TryGetValue(gridPos, out var quads))
            {
                int idx = (int)quadrant;
                if (idx >= 0 && idx < quads.Length) return quads[idx];
            }
            return QuarterBlockType.None;
        }

        public bool HasAnyQuarterBlocks(Vector2Int gridPos)
        {
            if (tileQuarterBlocks.TryGetValue(gridPos, out var quads))
            {
                for (int i = 0; i < quads.Length; i++)
                {
                    if (quads[i] != QuarterBlockType.None) return true;
                }
            }
            return false;
        }

        public void SetQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant, QuarterBlockType type)
        {
            EnsureSpritesLoaded();

            if (!tileQuarterBlocks.TryGetValue(gridPos, out var quads))
            {
                quads = new QuarterBlockType[4];
                tileQuarterBlocks[gridPos] = quads;
            }

            int idx = (int)quadrant;
            quads[idx] = type;

            UpdateQuadrantVisual(gridPos, quadrant, type);
        }

        public void RemoveQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            SetQuarterBlock(gridPos, quadrant, QuarterBlockType.None);
        }

        public void CycleQuarterBlock(Vector2Int gridPos, BlockQuadrant quadrant)
        {
            QuarterBlockType current = GetQuarterBlock(gridPos, quadrant);
            QuarterBlockType next;
            switch (current)
            {
                case QuarterBlockType.None:  next = QuarterBlockType.Dirt; break;
                case QuarterBlockType.Dirt:  next = QuarterBlockType.Grass; break;
                case QuarterBlockType.Grass: next = QuarterBlockType.None; break;
                default: next = QuarterBlockType.Dirt; break;
            }
            SetQuarterBlock(gridPos, quadrant, next);
        }

        private void UpdateQuadrantVisual(Vector2Int gridPos, BlockQuadrant quadrant, QuarterBlockType type)
        {
            if (!tileQuarterObjects.TryGetValue(gridPos, out var objs))
            {
                objs = new GameObject[4];
                tileQuarterObjects[gridPos] = objs;
            }

            int idx = (int)quadrant;
            GameObject currentObj = objs[idx];

            if (type == QuarterBlockType.None)
            {
                if (currentObj != null)
                {
                    if (Application.isPlaying) Destroy(currentObj);
                    else DestroyImmediate(currentObj);
                    objs[idx] = null;
                }
                return;
            }

            Sprite targetSprite = (type == QuarterBlockType.Grass) ? quarterGrassSprite : quarterDirtSprite;
            if (targetSprite == null) return;

            if (currentObj == null)
            {
                currentObj = new GameObject($"QuarterBlock_{gridPos.x}_{gridPos.y}_{quadrant}");
                currentObj.transform.SetParent(transform, false);
                objs[idx] = currentObj;
            }

            Vector2 center = GetTileVisualCenter(gridPos, 0);
            Vector2 quadPos = center + GetQuadrantOffset(quadrant);
            currentObj.transform.position = new Vector3(quadPos.x, quadPos.y, 0f);

            SpriteRenderer sr = currentObj.GetComponent<SpriteRenderer>();
            if (sr == null) sr = currentObj.AddComponent<SpriteRenderer>();

            sr.sprite = targetSprite;
            int baseOrder = IsometricCoordinates.CalculateSortingOrder(gridPos.x, gridPos.y, 0, -8000 + 4);
            sr.sortingOrder = enableSortingDepth ? (baseOrder + GetQuadrantSortingOffset(quadrant)) : baseOrder;
        }

        public void ClearAllQuarterBlocks()
        {
            foreach (var kvp in tileQuarterObjects)
            {
                if (kvp.Value == null) continue;
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    if (kvp.Value[i] != null)
                    {
                        if (Application.isPlaying) Destroy(kvp.Value[i]);
                        else DestroyImmediate(kvp.Value[i]);
                    }
                }
            }
            tileQuarterObjects.Clear();
            tileQuarterBlocks.Clear();
        }
    }
}
