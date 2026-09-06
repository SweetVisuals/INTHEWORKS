using System.Collections.Generic;
using UnityEngine;

namespace IsometricGame.Tilemap
{
    public enum TileType
    {
        Empty = 0,
        Grass = 1,
        DirtPath = 2,
        StoneCobble = 3,
        Water = 4,
        RoomFloor = 5,
        WallLeft = 6,
        WallRight = 7,
        WallDoor = 8,
        WallWindow = 9,
        BlackVoid = 10,
        Rug = 11,
        ComputerDesk = 12,
        StackingBox = 13
    }

    /// <summary>
    /// Generates and manages the 2D Isometric Tilemap and 24x24 Room Plane.
    /// Features:
    /// - 24x24 wooden floor tile plane.
    /// - 3-tile high Back-Left and Back-Right walls stacking pixel-perfectly.
    /// - Seamless corner intersection and precise depth sorting.
    /// - Perimeter boundary colliders and support for pixel-perfect stacking boxes.
    /// </summary>
    [ExecuteAlways]
    public class IsometricWorldMap : MonoBehaviour
    {
        [Header("World Mode")]
        [Tooltip("If false, only renders the 4x4 room plane with 3-tile high back walls.")]
        [SerializeField] private bool generateOpenWorld = false;
        [SerializeField] private int worldRadius = 12;

        [Header("Room Plane Settings")]
        [SerializeField] private Vector2Int roomOrigin = new Vector2Int(0, 0);
        [SerializeField] private int roomWidth = 4;
        [SerializeField] private int roomDepth = 4;
        [SerializeField] private int wallHeight = 3;

        [Header("Back-Left Wall Features")]
        [Tooltip("Grid X index on the Back-Left wall to place a door (-1 for solid continuous wall)")]
        [SerializeField] private int doorColumn = -1;
        [Tooltip("Grid X index on the Back-Left wall to place a window (-1 for solid continuous wall)")]
        [SerializeField] private int windowColumn = -1;

        [Header("Custom Sprites (32x32 Pixel Art)")]
        public Sprite customFloorSprite;
        public Sprite customWallLeftSprite;
        public Sprite customWallRightSprite;
        public Sprite customDoorSprite;
        public Sprite customWindowSprite;
        public Sprite customGrassSprite;

        [Header("Boundary Colliders")]
        [SerializeField] private bool addRoomBoundaries = true;

        [Header("Map Root")]
        [SerializeField] private GameObject mapRoot;

        private Dictionary<TileType, Sprite> spriteCache = new Dictionary<TileType, Sprite>();

        public bool GenerateOpenWorld { get => generateOpenWorld; set => generateOpenWorld = value; }
        public int RoomWidth { get => roomWidth; set => roomWidth = value; }
        public int RoomDepth { get => roomDepth; set => roomDepth = value; }
        public int WallHeight { get => wallHeight; set => wallHeight = value; }
        public int DoorColumn { get => doorColumn; set => doorColumn = value; }
        public int WindowColumn { get => windowColumn; set => windowColumn = value; }

        private void Awake()
        {
            GenerateWorldMap();
        }

        private void Start()
        {
            GenerateWorldMap();
        }

        private void OnEnable()
        {
            if (mapRoot == null || mapRoot.transform.childCount == 0)
            {
                GenerateWorldMap();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    GenerateWorldMap();
                }
            };
        }
#endif

        [ContextMenu("Regenerate 2D World Map")]
        public void GenerateWorldMap()
        {
            if (mapRoot != null)
            {
                if (Application.isPlaying) Destroy(mapRoot);
                else DestroyImmediate(mapRoot);
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Generated_") || child.name.StartsWith("Room_") || child.name.StartsWith("OpenWorld_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            mapRoot = new GameObject("Generated_2DWorldMap");
            mapRoot.transform.SetParent(transform, false);

            EnsureSpritesLoaded();

            // 1. Generate 10x10 Room Plane with 3-tile high stacked walls
            GenerateRoomPlane(mapRoot.transform);

            // 2. Open World Terrain (Optional)
            if (generateOpenWorld)
            {
                GenerateOpenWorldTerrain(mapRoot.transform);
            }
        }

        private void GenerateRoomPlane(Transform parent)
        {
            GameObject roomGroup = new GameObject("Room_10x10_Plane");
            roomGroup.transform.SetParent(parent, false);

            // 1. Floor Plane (10x10 tiles)
            // Spawned back to front (highest x+y to lowest x+y)
            for (int y = roomOrigin.y + roomDepth - 1; y >= roomOrigin.y; y--)
            {
                for (int x = roomOrigin.x + roomWidth - 1; x >= roomOrigin.x; x--)
                {
                    SpawnTile(roomGroup.transform, x, y, 0, TileType.RoomFloor, -50);
                }
            }

            // 2. Back-Left Wall (Along Y = roomOrigin.y + roomDepth - 1, for all X = 0..roomWidth-1)
            int blWallY = roomOrigin.y + roomDepth - 1;
            for (int x = roomOrigin.x + roomWidth - 1; x >= roomOrigin.x; x--)
            {
                if (x == doorColumn && customDoorSprite != null)
                {
                    // Standalone door tile (no extra wall tiles stacked above)
                    SpawnTile(roomGroup.transform, x, blWallY, 0, TileType.WallDoor, 0);
                }
                else if (x == windowColumn && customWindowSprite != null)
                {
                    // Standalone window tile (no extra wall tiles stacked above)
                    SpawnTile(roomGroup.transform, x, blWallY, 0, TileType.WallWindow, 0);
                }
                else
                {
                    // Standard solid wall stacked wallHeight tiles high
                    for (int h = 0; h < wallHeight; h++)
                    {
                        SpawnTile(roomGroup.transform, x, blWallY, h, TileType.WallLeft, 0);
                    }
                }
            }

            // 3. Back-Right Wall (Along X = roomOrigin.x + roomWidth - 1, for all Y = 0..roomDepth-1)
            // Stacked 3 tiles high (elevation = 0, 1, 2)
            int brWallX = roomOrigin.x + roomWidth - 1;
            for (int y = roomOrigin.y + roomDepth - 1; y >= roomOrigin.y; y--)
            {
                for (int h = 0; h < wallHeight; h++)
                {
                    SpawnTile(roomGroup.transform, brWallX, y, h, TileType.WallRight, 0);
                }
            }

            // 4. Boundary Colliders around the 10x10 plane so the player stays within bounds
            if (addRoomBoundaries)
            {
                CreateRoomBoundaries(roomGroup.transform);
            }
        }

        private void CreateRoomBoundaries(Transform parent)
        {
            GameObject boundsObj = new GameObject("Room_Boundaries");
            boundsObj.transform.SetParent(parent, false);

            float halfW = IsometricCoordinates.DefaultTileWidth * 0.5f;
            float halfH = IsometricCoordinates.DefaultTileHeight * 0.5f;

            // 4 Corner vertices in isometric world space:
            // Bottom Corner: (0, 0)
            // Right Corner:  (roomWidth - 1, 0)
            // Top Corner:    (roomWidth - 1, roomDepth - 1)
            // Left Corner:   (0, roomDepth - 1)
            Vector2 bottom = IsometricCoordinates.GridToWorld(roomOrigin.x, roomOrigin.y) + new Vector2(0, -halfH * 0.6f);
            Vector2 right  = IsometricCoordinates.GridToWorld(roomOrigin.x + roomWidth - 1, roomOrigin.y) + new Vector2(halfW * 0.8f, 0);
            Vector2 top    = IsometricCoordinates.GridToWorld(roomOrigin.x + roomWidth - 1, roomOrigin.y + roomDepth - 1) + new Vector2(0, halfH * 0.8f);
            Vector2 left   = IsometricCoordinates.GridToWorld(roomOrigin.x, roomOrigin.y + roomDepth - 1) + new Vector2(-halfW * 0.8f, 0);

            // Create solid edge colliders along the 4 boundaries
            CreateEdgeBarrier(boundsObj.transform, "BackLeft_Wall_Barrier", left, top);
            CreateEdgeBarrier(boundsObj.transform, "BackRight_Wall_Barrier", top, right);
            CreateEdgeBarrier(boundsObj.transform, "FrontRight_Rim_Barrier", right, bottom);
            CreateEdgeBarrier(boundsObj.transform, "FrontLeft_Rim_Barrier", bottom, left);
        }

        private void CreateEdgeBarrier(Transform parent, string name, Vector2 p1, Vector2 p2)
        {
            GameObject edgeObj = new GameObject(name);
            edgeObj.transform.SetParent(parent, false);
            EdgeCollider2D edge = edgeObj.AddComponent<EdgeCollider2D>();
            edge.points = new Vector2[] { p1, p2 };
            edge.edgeRadius = 0.08f;
        }

        private void GenerateOpenWorldTerrain(Transform parent)
        {
            GameObject terrainGroup = new GameObject("OpenWorld_Terrain");
            terrainGroup.transform.SetParent(parent, false);

            for (int x = -worldRadius; x <= roomWidth + worldRadius; x++)
            {
                for (int y = -worldRadius; y <= roomDepth + worldRadius; y++)
                {
                    if (x >= roomOrigin.x && x < roomOrigin.x + roomWidth &&
                        y >= roomOrigin.y && y < roomOrigin.y + roomDepth)
                    {
                        continue;
                    }

                    SpawnTile(terrainGroup.transform, x, y, 0, TileType.Grass, -200);
                }
            }
        }

        public GameObject SpawnTile(Transform parent, int gridX, int gridY, int elevation, TileType type, int layerOffset)
        {
            Sprite sprite = GetSpriteForType(type);
            if (sprite == null) return null;

            if (sprite.texture != null)
            {
                sprite.texture.filterMode = FilterMode.Point;
            }

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, elevation);
            string tileName = elevation > 0 ? $"Tile_{type}_{gridX}_{gridY}_H{elevation}" : $"Tile_{type}_{gridX}_{gridY}";
            GameObject tileObj = new GameObject(tileName);
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, elevation, layerOffset);

            return tileObj;
        }

        private Sprite GetSpriteForType(TileType type)
        {
            if (type == TileType.Grass && customGrassSprite != null) return customGrassSprite;
            if (type == TileType.RoomFloor && customFloorSprite != null) return customFloorSprite;
            if (type == TileType.WallLeft && customWallLeftSprite != null) return customWallLeftSprite;
            if (type == TileType.WallRight && customWallRightSprite != null) return customWallRightSprite;
            if (type == TileType.WallDoor && customDoorSprite != null) return customDoorSprite;
            if (type == TileType.WallWindow && customWindowSprite != null) return customWindowSprite;

            if (spriteCache.TryGetValue(type, out Sprite sprite))
            {
                return sprite;
            }
            return null;
        }

        private void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (customFloorSprite == null)
            {
                customFloorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden floor tile 32x32.png");
            }
            if (customWallLeftSprite == null)
            {
                customWallLeftSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/left wall tile 32x32.png");
            }
            if (customWallRightSprite == null)
            {
                customWallRightSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/right wall tile 32x32.png");
            }
            if (customDoorSprite == null)
            {
                customDoorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door black void.png");
            }
#endif
        }

        public Vector2 GetRoomCenterWorld()
        {
            float cx = roomOrigin.x + (roomWidth - 1) * 0.5f;
            float cy = roomOrigin.y + (roomDepth - 1) * 0.5f;
            return IsometricCoordinates.GridToWorld(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy));
        }
    }
}
