using System.Collections.Generic;
using UnityEngine;
using IsometricGame.Environment;

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
        StackingBox = 13,
        BlueBed = 14,
        Bush = 15,
        LongGrass = 16
    }

    /// <summary>
    /// Generates and manages the 2D Isometric Tilemap:
    /// 1. Indoor Bedroom Plane with stacked walls, door, window, bed, desk, and drawer.
    /// 2. Outdoor Grass World with organic Perlin-noise bush clusters and doorway transition.
    /// </summary>
    [ExecuteAlways]
    public class IsometricWorldMap : MonoBehaviour
    {
        [Header("World Mode")]
        [SerializeField] private bool generateOutsideWorld = true;

        [Header("Room Plane Settings")]
        [SerializeField] private Vector2Int roomOrigin = new Vector2Int(0, 0);
        [SerializeField] private int roomWidth = 6;
        [SerializeField] private int roomDepth = 6;
        [SerializeField] private int wallHeight = 3;

        [Header("Back-Left Wall Features")]
        [Tooltip("Grid X index on the Back-Left wall to place a door (-1 for solid continuous wall)")]
        [SerializeField] private int doorColumn = 4;
        [Tooltip("Elevation step (in vertical wall blocks) for the door (0 = ground level)")]
        [SerializeField] private int doorElevation = 0;
        [Tooltip("Grid X index on the Back-Left wall to place a window (-1 for solid continuous wall)")]
        [SerializeField] private int windowColumn = 1;
        [Tooltip("Elevation step (in vertical wall blocks) for the window")]
        [SerializeField] private int windowElevation = 1;

        [Header("Furniture & Props: Desk & Drawer")]
        [SerializeField] private bool spawnDesk = true;
        [SerializeField] private Vector2Int deskPosition = new Vector2Int(4, 2);
        [SerializeField] private bool spawnDrawer = true;
        [SerializeField] private Vector2Int drawerPosition = new Vector2Int(5, 0);
        [SerializeField] private Vector2 drawerWorldOffset = Vector2.zero;

        [Header("Furniture & Props: Bed & Safe Zone")]
        [SerializeField] private bool spawnBed = true;
        [SerializeField] private Vector2Int bedPosition = new Vector2Int(1, 2);
        [SerializeField] private Vector2 bedWorldOffset = new Vector2(-0.405f, -0.36f);
        [SerializeField] private bool createSafeZone = true;
        [SerializeField] private float safeZoneTileRadius = 1.0f;

        [Header("Outside World Settings")]
        [SerializeField] private Vector2Int outsideOrigin = new Vector2Int(20, 0);
        [SerializeField] private Vector2Int outsideDoorOffset = new Vector2Int(4, 4);
        [SerializeField] private float bushNoiseScale = 0.18f;
        [SerializeField] private float bushThreshold = 0.70f;
        [SerializeField] private int bushSeed = 42;
        [SerializeField] private float bushClearRadius = 3.5f;

        [Header("Custom Sprites (32x32 Pixel Art)")]
        public Sprite customFloorSprite;
        public Sprite customWallLeftSprite;
        public Sprite customWallRightSprite;
        public Sprite customDoorSprite;
        public Sprite customWindowSprite;
        public Sprite customDeskSprite;
        public Sprite customDrawerSprite;
        public Sprite customDrawerHoverOutlineSprite;
        public Sprite customDeskFlickerSprite;
        public Sprite customDeskOffSprite;
        public Sprite customDeskGlowSprite;
        public Sprite customBedSprite;
        public Sprite customGrassSprite;
        public Sprite customLongGrassSprite;
        public Sprite customBushSprite;
        public Sprite customPineTreeSprite;

        [Header("Boundary Colliders")]
        [SerializeField] private bool addRoomBoundaries = true;

        [Header("Map Root")]
        [SerializeField] private GameObject mapRoot;
        [SerializeField] private GameObject roomPlane;
        [SerializeField] private GameObject outsidePlane;

        private Dictionary<TileType, Sprite> spriteCache = new Dictionary<TileType, Sprite>();

        public bool GenerateOutsideWorldFlag { get => generateOutsideWorld; set => generateOutsideWorld = value; }
        public bool GenerateOpenWorld { get => generateOutsideWorld; set => generateOutsideWorld = value; }
        public int RoomWidth { get => roomWidth; set => roomWidth = value; }
        public int RoomDepth { get => roomDepth; set => roomDepth = value; }
        public int WallHeight { get => wallHeight; set => wallHeight = value; }
        public int DoorColumn { get => doorColumn; set => doorColumn = value; }
        public int WindowColumn { get => windowColumn; set => windowColumn = value; }
        public Vector2Int OutsideOrigin => outsideOrigin;
        public GameObject RoomPlane => roomPlane;
        public GameObject OutsidePlane => outsidePlane;

        public void SetZoneActive(bool isOutdoors)
        {
            if (roomPlane == null)
            {
                var rt = transform.Find("Generated_2DWorldMap/Room_10x10_Plane") ?? transform.Find("Room_10x10_Plane");
                if (rt != null) roomPlane = rt.gameObject;
            }
            if (outsidePlane == null)
            {
                var ot = transform.Find("Generated_2DWorldMap/Outside_World_Plane") ?? transform.Find("Outside_World_Plane");
                if (ot != null) outsidePlane = ot.gameObject;
            }

            if (roomPlane != null) roomPlane.SetActive(!isOutdoors);
            if (outsidePlane != null)
            {
                outsidePlane.SetActive(isOutdoors);
                if (isOutdoors)
                {
                    var terrain = outsidePlane.GetComponent<OutdoorInfiniteTerrain>();
                    if (terrain != null)
                    {
                        terrain.RebuildAllChunks();
                    }
                }
            }
        }

        private void Awake()
        {
            GenerateWorldMap();
            IsometricGame.UI.EnsureCanvasAndMoneyUI.EnsureAllUI();
        }

        private void Start()
        {
            GenerateWorldMap();
            IsometricGame.UI.EnsureCanvasAndMoneyUI.EnsureAllUI();
        }

        private void OnEnable()
        {
            GenerateWorldMap();
            IsometricGame.UI.EnsureCanvasAndMoneyUI.EnsureAllUI();
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
                if (child.name.StartsWith("Generated_") || child.name.StartsWith("Room_") || child.name.StartsWith("OpenWorld_") || child.name.StartsWith("Outside_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            mapRoot = new GameObject("Generated_2DWorldMap");
            mapRoot.transform.SetParent(transform, false);

            EnsureSpritesLoaded();

            // 1. Generate Indoor Bedroom Plane
            roomPlane = GenerateRoomPlane(mapRoot.transform);

            // 2. Generate Outdoor Grass & Bush World
            if (generateOutsideWorld)
            {
                outsidePlane = GenerateOutsideWorldPlane(mapRoot.transform);
            }

            // Always isolate indoor bedroom by default inside the dark blue void
            SetZoneActive(false);

            // Ensure Zone Transition Manager is active
            if (FindAnyObjectByType<ZoneTransitionManager>() == null)
            {
                GameObject ztObj = new GameObject("Zone_Transition_Manager");
                ztObj.AddComponent<ZoneTransitionManager>();
            }
        }

        private GameObject GenerateRoomPlane(Transform parent)
        {
            GameObject roomGroup = new GameObject("Room_10x10_Plane");
            roomGroup.transform.SetParent(parent, false);

            // 1. Floor Plane (roomWidth x roomDepth tiles)
            for (int y = roomOrigin.y + roomDepth - 1; y >= roomOrigin.y; y--)
            {
                for (int x = roomOrigin.x + roomWidth - 1; x >= roomOrigin.x; x--)
                {
                    SpawnTile(roomGroup.transform, x, y, 0, TileType.RoomFloor, -5000);
                }
            }

            // 2. Back-Left Wall
            int blWallY = roomOrigin.y + roomDepth - 1;
            for (int x = roomOrigin.x + roomWidth - 1; x >= roomOrigin.x; x--)
            {
                for (int h = 0; h < wallHeight; h++)
                {
                    SpawnTile(roomGroup.transform, x, blWallY, h, TileType.WallLeft, 0);
                }

                if (x == doorColumn && customDoorSprite != null)
                {
                    GameObject doorObj = SpawnTile(roomGroup.transform, x, blWallY, doorElevation, TileType.WallDoor, 150);
                    if (doorObj != null)
                    {
                        var handle = doorObj.AddComponent<DoorHandleInteraction>();
                        handle.isOutdoorDoor = false; // Indoor door -> transitions outdoors
#if UNITY_EDITOR
                        handle.handleOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door handle outline.png");
                        handle.openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
#endif
                        handle.InitializeComponents();
                    }
                }
                else if (x == windowColumn && customWindowSprite != null)
                {
                    SpawnTile(roomGroup.transform, x, blWallY, windowElevation, TileType.WallWindow, 150);
                }
            }

            // 3. Back-Right Wall
            int brWallX = roomOrigin.x + roomWidth - 1;
            for (int y = roomOrigin.y + roomDepth - 1; y >= roomOrigin.y; y--)
            {
                for (int h = 0; h < wallHeight; h++)
                {
                    SpawnTile(roomGroup.transform, brWallX, y, h, TileType.WallRight, 0);
                }
            }

            // 4. Props: Computer Desk
            if (spawnDesk && customDeskSprite != null)
            {
                GameObject deskObj = SpawnTile(roomGroup.transform, deskPosition.x, deskPosition.y, 0, TileType.ComputerDesk, 10);
                if (deskObj != null)
                {
                    BoxCollider2D deskCol = deskObj.AddComponent<BoxCollider2D>();
                    deskCol.size = new Vector2(0.65f, 0.35f);
                    deskCol.offset = new Vector2(0f, 0.12f);

                    var flicker = deskObj.AddComponent<IsometricGame.Environment.ComputerScreenFlicker>();
                    flicker.defaultSprite = customDeskSprite;
#if UNITY_EDITOR
                    flicker.screenPixelGlowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/just screen glow.png");
                    flicker.ambientHaloSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/monitor_glow.png");
                    flicker.screenHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/computer scree hover outline.png")
                                                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/computer screen hover outline.png");
                    flicker.openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
#endif
                    flicker.InitializeComponents();
                }
            }

            // 4b. Props: Small Wooden Drawer (along right wall)
            if (spawnDrawer && customDrawerSprite != null)
            {
                Vector2 drawerWorldPos = IsometricCoordinates.GridToWorld(drawerPosition.x, drawerPosition.y, 0) + drawerWorldOffset;
                GameObject drawerObj = new GameObject("Prop_WoodenDrawer");
                drawerObj.transform.SetParent(roomGroup.transform, false);
                drawerObj.transform.position = new Vector3(drawerWorldPos.x, drawerWorldPos.y, -0.05f);

                SpriteRenderer drawerSr = drawerObj.AddComponent<SpriteRenderer>();
                drawerSr.sprite = customDrawerSprite;
                drawerSr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(drawerPosition.x, drawerPosition.y, 0, 50);

                BoxCollider2D drawerCol = drawerObj.AddComponent<BoxCollider2D>();
                drawerCol.size = new Vector2(0.50f, 0.35f);
                drawerCol.offset = new Vector2(0f, 0.10f);

                var drawerInteract = drawerObj.AddComponent<IsometricGame.Environment.DrawerChestInteraction>();
                drawerInteract.drawerHoverOutlineSprite = customDrawerHoverOutlineSprite;
#if UNITY_EDITOR
                if (drawerInteract.drawerHoverOutlineSprite == null)
                {
                    drawerInteract.drawerHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/sdrawer hover outline (shift 1px to the right).png");
                }
                drawerInteract.openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
#endif
                drawerInteract.InitializeComponents();
            }

            // 5. Props: Bed & Safe Zone
            if (spawnBed && customBedSprite != null)
            {
                GameObject bedObj = SpawnTile(roomGroup.transform, bedPosition.x, bedPosition.y, 0, TileType.BlueBed, 10);
                if (bedObj != null)
                {
                    if (bedWorldOffset != Vector2.zero)
                    {
                        bedObj.transform.position += new Vector3(bedWorldOffset.x, bedWorldOffset.y, 0);
                    }

                    BoxCollider2D bedCol = bedObj.AddComponent<BoxCollider2D>();
                    bedCol.size = new Vector2(0.85f, 0.45f);
                    bedCol.offset = new Vector2(0f, 0.18f);

                    var bedInteract = bedObj.AddComponent<IsometricGame.Environment.BedInteraction>();
#if UNITY_EDITOR
                    bedInteract.bedHoverOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bed hover outline.png");
                    bedInteract.sleepTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button sleep text.png");
#endif
                    bedInteract.InitializeComponents();

                    if (createSafeZone)
                    {
                        GameObject safeZoneObj = new GameObject("Bed_Safe_Zone");
                        safeZoneObj.transform.SetParent(bedObj.transform, false);
                        safeZoneObj.transform.localPosition = Vector3.zero;

                        BoxCollider2D safeTrigger = safeZoneObj.AddComponent<BoxCollider2D>();
                        safeTrigger.isTrigger = true;
                        safeTrigger.size = new Vector2(2.0f * safeZoneTileRadius, 1.2f * safeZoneTileRadius);
                        safeTrigger.offset = new Vector2(0f, 0.18f);
                    }
                }
            }

            // 6. Boundary Colliders around the room
            if (addRoomBoundaries)
            {
                CreateRoomBoundaries(roomGroup.transform);
            }

            return roomGroup;
        }

        private void CreateRoomBoundaries(Transform parent)
        {
            GameObject boundsObj = new GameObject("Room_Boundaries");
            boundsObj.transform.SetParent(parent, false);

            float halfW = IsometricCoordinates.DefaultTileWidth * 0.5f;
            float halfH = IsometricCoordinates.DefaultTileHeight * 0.5f;

            Vector2 bottom = IsometricCoordinates.GridToWorld(roomOrigin.x, roomOrigin.y) + new Vector2(0, -halfH * 0.6f);
            Vector2 right  = IsometricCoordinates.GridToWorld(roomOrigin.x + roomWidth - 1, roomOrigin.y) + new Vector2(halfW * 0.8f, 0);
            Vector2 top    = IsometricCoordinates.GridToWorld(roomOrigin.x + roomWidth - 1, roomOrigin.y + roomDepth - 1) + new Vector2(0, halfH * 0.8f);
            Vector2 left   = IsometricCoordinates.GridToWorld(roomOrigin.x, roomOrigin.y + roomDepth - 1) + new Vector2(-halfW * 0.8f, 0);

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

        private GameObject GenerateOutsideWorldPlane(Transform parent)
        {
            GameObject outsideGroup = new GameObject("Outside_World_Plane");
            outsideGroup.transform.SetParent(parent, false);

            OutdoorInfiniteTerrain terrain = outsideGroup.AddComponent<OutdoorInfiniteTerrain>();
            terrain.grassSprite = customGrassSprite;
            terrain.longGrassSprite = customLongGrassSprite;
            terrain.bushSprite = customBushSprite;
            terrain.pineTreeSprite = customPineTreeSprite;
            terrain.doorSprite = customDoorSprite;
            terrain.DoorGridPos = outsideOrigin + outsideDoorOffset;
            terrain.BushNoiseScale = bushNoiseScale;
            terrain.BushThreshold = bushThreshold;
            terrain.BushSeed = bushSeed;
            terrain.DoorClearRadius = bushClearRadius;
            terrain.RebuildAllChunks();

            return outsideGroup;
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
            if (type == TileType.LongGrass && customLongGrassSprite != null) return customLongGrassSprite;
            if (type == TileType.Bush && customBushSprite != null) return customBushSprite;
            if (type == TileType.RoomFloor && customFloorSprite != null) return customFloorSprite;
            if (type == TileType.WallLeft && customWallLeftSprite != null) return customWallLeftSprite;
            if (type == TileType.WallRight && customWallRightSprite != null) return customWallRightSprite;
            if (type == TileType.WallDoor && customDoorSprite != null) return customDoorSprite;
            if (type == TileType.WallWindow && customWindowSprite != null) return customWindowSprite;
            if (type == TileType.ComputerDesk && customDeskSprite != null) return customDeskSprite;
            if (type == TileType.BlueBed && customBedSprite != null) return customBedSprite;

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
                customFloorSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/floor wood tile 32x32 (1).png")
                                 ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden floor tile 32x32.png");
            }
            if (customWallLeftSprite == null)
            {
                customWallLeftSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/left wall tile 32x32.png");
            }
            if (customWallRightSprite == null)
            {
                customWallRightSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/right wall tile 32x32.png");
            }
            if (customDoorSprite == null || customDoorSprite.name == "wooden door")
            {
                customDoorSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden door (1).png")
                                 ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden door.png")
                                 ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden door_0002.png");
            }
            if (customWindowSprite == null)
            {
                customWindowSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden window (1).png")
                                  ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden window.png")
                                  ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden window_0002.png");
            }
            if (customDeskSprite == null || customDeskSprite.name.Contains("desk with computer") || customDeskSprite.name.StartsWith("isometric desk"))
            {
                customDeskSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk fixed (1).png")
                                ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk fixed.png")
                                ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk (1).png");
            }
            if (customDeskFlickerSprite == null || customDeskFlickerSprite.name.StartsWith("isometric desk flicker"))
            {
                customDeskFlickerSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk flicker fixed.png")
                                       ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk flicker frame (1).png")
                                       ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk flicker frame.png");
            }
            if (customDeskOffSprite == null)
            {
                customDeskOffSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/isometric desk off fixed.png");
            }
            if (customDeskGlowSprite == null)
            {
                customDeskGlowSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/just screen glow.png")
                                    ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/monitor_glow.png");
            }
            if (customBedSprite == null || customBedSprite.name == "blue bed" || customBedSprite.name.Contains("fixed bed") || customBedSprite.name.Contains("updated bed sprite"))
            {
                customBedSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/bed updated for outline.png")
                               ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/updated bed sprite.png")
                               ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/fixed bed sprite.png")
                               ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/blue bed.png");
            }
            if (customDrawerSprite == null)
            {
                customDrawerSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/small drawer for right wall.png");
            }
            if (customDrawerHoverOutlineSprite == null)
            {
                customDrawerHoverOutlineSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/sdrawer hover outline (shift 1px to the right).png");
            }
            if (customGrassSprite == null)
            {
                customGrassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/grass tile.png");
            }
            if (customLongGrassSprite == null)
            {
                customLongGrassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/long grass small tile.png");
            }
            if (customBushSprite == null)
            {
                customBushSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/bush block.png");
            }
            if (customPineTreeSprite == null)
            {
                customPineTreeSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/pine tree.png");
            }
#endif
        }

        public Vector2 GetRoomCenterWorld()
        {
            float cx = roomOrigin.x + (roomWidth - 1) * 0.5f;
            float cy = roomOrigin.y + (roomDepth - 1) * 0.5f;
            return IsometricCoordinates.GridToWorld(cx, cy);
        }

        public Vector2 GetIndoorDoorSpawnWorld()
        {
            int spX = doorColumn;
            int spY = roomOrigin.y + roomDepth - 2;
            return IsometricCoordinates.GridToWorld(spX, spY);
        }

        public Vector2 GetOutdoorDoorSpawnWorld()
        {
            int spX = outsideOrigin.x + outsideDoorOffset.x;
            int spY = outsideOrigin.y + outsideDoorOffset.y - 1;
            return IsometricCoordinates.GridToWorld(spX, spY);
        }

        public Vector2 GetOutsideCenterWorld()
        {
            return IsometricCoordinates.GridToWorld(outsideOrigin.x + outsideDoorOffset.x, outsideOrigin.y + outsideDoorOffset.y);
        }
    }
}
