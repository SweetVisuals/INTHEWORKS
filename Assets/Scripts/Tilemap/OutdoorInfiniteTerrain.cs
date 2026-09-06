using System.Collections.Generic;
using UnityEngine;
using IsometricGame.Environment;
using IsometricGame.Player;

namespace IsometricGame.Tilemap
{
    /// <summary>
    /// Dynamically streams infinite 2D Isometric Grass & Bush terrain chunks around the player/camera.
    /// Features:
    /// - Dynamic chunk loading and unloading as the player explores infinitely in all directions.
    /// - Deterministic Perlin-noise organic bush placement with wide, obstacle-free walking clearings.
    /// - Tight, smooth circle foot colliders on bushes so the player never gets obstructed.
    /// - Safe clearing around the outdoor return door.
    /// - Full Edit-Mode & Play-Mode support with zero visible void borders.
    /// </summary>
    [ExecuteAlways]
    public class OutdoorInfiniteTerrain : MonoBehaviour
    {
        [Header("Chunk Grid Configuration")]
        [Tooltip("Number of isometric tiles along each axis of a chunk (e.g. 8x8 = 64 tiles).")]
        [SerializeField] private int chunkSize = 8;
        [Tooltip("Radius of chunks kept active around the player.")]
        [SerializeField] private int viewRadiusInChunks = 3;
        [Tooltip("Radius beyond which chunks are destroyed/unloaded.")]
        [SerializeField] private int unloadRadiusInChunks = 4;

        [Header("Door & Spawn Plaza")]
        [SerializeField] private Vector2Int doorGridPos = new Vector2Int(24, 4);
        [SerializeField] private float doorClearRadius = 3.5f;

        [Header("Procedural Bush Clusters")]
        [Tooltip("Scale of Perlin noise (smaller = broader, more natural clusters).")]
        [SerializeField] private float bushNoiseScale = 0.18f;
        [Tooltip("Threshold above which bushes spawn (higher = sparser, more open walkable space).")]
        [SerializeField] private float bushThreshold = 0.70f;
        [SerializeField] private int bushSeed = 42;
        [Tooltip("Bush walk-through trigger radius for opacity fading.")]
        [SerializeField] private float bushTriggerRadius = 0.32f;
        [SerializeField] private Vector2 bushTriggerOffset = new Vector2(0f, 0.12f);
        [Tooltip("Opacity when player walks through (0.75 = 25% reduction).")]
        [Range(0f, 1f)]
        [SerializeField] private float bushWalkThroughOpacity = 0.75f;

        [Header("Procedural Pine Trees (Groves & Bunches)")]
        [Tooltip("Scale of Perlin noise for tree groves (smaller = broader bunches).")]
        [SerializeField] private float treeNoiseScale = 0.14f;
        [Tooltip("Threshold above which pine tree groves form (higher = sparser, more open groves).")]
        [SerializeField] private float treeThreshold = 0.68f;
        [Tooltip("Spawn probability within grove clusters (lower = sparser, less dense trees).")]
        [Range(0f, 1f)]
        [SerializeField] private float treeSpawnProbability = 0.28f;
        [SerializeField] private int treeSeed = 1337;
        [Tooltip("Tree trunk foot collision radius.")]
        [SerializeField] private float treeColliderRadius = 0.14f;
        [SerializeField] private Vector2 treeColliderOffset = new Vector2(0f, 0.08f);

        [Header("Procedural Long Grass Overlay (Rare Clusters ~2.5%)")]
        [Tooltip("Scale of Perlin noise for long grass patches (smaller = larger patch radius).")]
        [SerializeField] private float longGrassNoiseScale = 0.16f;
        [Tooltip("Threshold above which a long grass cluster patch forms.")]
        [SerializeField] private float longGrassClusterThreshold = 0.65f;
        [Tooltip("Spawn probability within cluster patches (~0.24 yields ~2.5% overall spawn rate).")]
        [Range(0f, 1f)]
        [SerializeField] private float longGrassSpawnProbability = 0.24f;
        [SerializeField] private int longGrassSeed = 9999;

        [Header("Sprites")]
        public Sprite grassSprite;
        public Sprite longGrassSprite;
        public Sprite bushSprite;
        public Sprite pineTreeSprite;
        public Sprite doorSprite;

        private Dictionary<Vector2Int, GameObject> loadedChunks = new Dictionary<Vector2Int, GameObject>();
        private Vector2Int lastChunkCoord = new Vector2Int(int.MinValue, int.MinValue);
        private GameObject doorObject;

        public Vector2Int DoorGridPos { get => doorGridPos; set => doorGridPos = value; }
        public int ChunkSize { get => chunkSize; set => chunkSize = value; }
        public int ViewRadiusInChunks { get => viewRadiusInChunks; set => viewRadiusInChunks = value; }
        public float BushNoiseScale { get => bushNoiseScale; set => bushNoiseScale = value; }
        public float BushThreshold { get => bushThreshold; set => bushThreshold = value; }
        public int BushSeed { get => bushSeed; set => bushSeed = value; }
        public float TreeNoiseScale { get => treeNoiseScale; set => treeNoiseScale = value; }
        public float TreeThreshold { get => treeThreshold; set => treeThreshold = value; }
        public float TreeSpawnProbability { get => treeSpawnProbability; set => treeSpawnProbability = value; }
        public int TreeSeed { get => treeSeed; set => treeSeed = value; }
        public float LongGrassNoiseScale { get => longGrassNoiseScale; set => longGrassNoiseScale = value; }
        public float LongGrassClusterThreshold { get => longGrassClusterThreshold; set => longGrassClusterThreshold = value; }
        public float LongGrassSpawnProbability { get => longGrassSpawnProbability; set => longGrassSpawnProbability = value; }
        public int LongGrassSeed { get => longGrassSeed; set => longGrassSeed = value; }
        public float DoorClearRadius { get => doorClearRadius; set => doorClearRadius = value; }

        private void Awake()
        {
            EnsureSpritesLoaded();
        }

        private void OnEnable()
        {
            EnsureSpritesLoaded();
            RebuildAllChunks();
        }

        private void OnDisable()
        {
            ClearAllChunks();
        }

        private void Start()
        {
            EnsureSpritesLoaded();
            if (gameObject.activeInHierarchy)
            {
                RebuildAllChunks();
            }
        }

        private void Update()
        {
            Vector2 targetWorldPos = GetTargetPosition();
            Vector2Int currentChunk = WorldToChunkCoord(targetWorldPos);

            if (currentChunk != lastChunkCoord || loadedChunks.Count == 0)
            {
                lastChunkCoord = currentChunk;
                UpdateChunksAround(currentChunk);
            }
        }

        public void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (grassSprite == null)
            {
                grassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/grass tile.png");
            }
            if (longGrassSprite == null)
            {
                longGrassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/long grass small tile.png");
            }
            if (bushSprite == null)
            {
                bushSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/bush block.png");
            }
            if (pineTreeSprite == null)
            {
                pineTreeSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/pine tree.png");
            }
            if (doorSprite == null)
            {
                doorSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden door (1).png")
                          ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/wooden door.png");
            }
            if (treeThreshold < 0.65f) treeThreshold = 0.68f;
            if (treeSpawnProbability > 0.35f) treeSpawnProbability = 0.28f;
#endif
        }

        private Vector2 GetTargetPosition()
        {
            if (Application.isPlaying)
            {
                if (IsometricPlayerController.Instance != null)
                {
                    Vector2 pPos = IsometricPlayerController.Instance.transform.position;
                    // If player is in outdoor region (X > 4.0), track player
                    if (pPos.x > 4.0f)
                    {
                        return pPos;
                    }
                }
            }

            // Default center around outdoor door spawn
            return IsometricCoordinates.GridToWorld(doorGridPos.x, doorGridPos.y);
        }

        private Vector2Int WorldToChunkCoord(Vector2 worldPos)
        {
            Vector2Int gridPos = IsometricCoordinates.WorldToGrid(worldPos);
            int cx = Mathf.FloorToInt((float)gridPos.x / chunkSize);
            int cy = Mathf.FloorToInt((float)gridPos.y / chunkSize);
            return new Vector2Int(cx, cy);
        }

        public void RebuildAllChunks()
        {
            ClearAllChunks();
            EnsureSpritesLoaded();
            EnsureDoorObject();

            Vector2 targetPos = GetTargetPosition();
            Vector2Int centerChunk = WorldToChunkCoord(targetPos);
            lastChunkCoord = centerChunk;
            UpdateChunksAround(centerChunk);
        }

        public void ClearAllChunks()
        {
            foreach (var kvp in loadedChunks)
            {
                if (kvp.Value != null)
                {
                    if (Application.isPlaying) Destroy(kvp.Value);
                    else DestroyImmediate(kvp.Value);
                }
            }
            loadedChunks.Clear();
            lastChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

            // Clean any stray chunk children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Chunk_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private void UpdateChunksAround(Vector2Int centerChunk)
        {
            // 1. Spawn missing chunks in view radius
            for (int cy = centerChunk.y - viewRadiusInChunks; cy <= centerChunk.y + viewRadiusInChunks; cy++)
            {
                for (int cx = centerChunk.x - viewRadiusInChunks; cx <= centerChunk.x + viewRadiusInChunks; cx++)
                {
                    Vector2Int chunkCoord = new Vector2Int(cx, cy);
                    if (!loadedChunks.ContainsKey(chunkCoord))
                    {
                        GameObject chunkObj = GenerateChunk(chunkCoord);
                        if (chunkObj != null)
                        {
                            loadedChunks[chunkCoord] = chunkObj;
                        }
                    }
                }
            }

            // 2. Unload far chunks outside unload radius
            List<Vector2Int> toRemove = new List<Vector2Int>();
            foreach (var kvp in loadedChunks)
            {
                Vector2Int coord = kvp.Key;
                if (Mathf.Abs(coord.x - centerChunk.x) > unloadRadiusInChunks ||
                    Mathf.Abs(coord.y - centerChunk.y) > unloadRadiusInChunks)
                {
                    toRemove.Add(coord);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                Vector2Int coord = toRemove[i];
                if (loadedChunks.TryGetValue(coord, out GameObject chunkObj))
                {
                    if (chunkObj != null)
                    {
                        if (Application.isPlaying) Destroy(chunkObj);
                        else DestroyImmediate(chunkObj);
                    }
                    loadedChunks.Remove(coord);
                }
            }
        }

        private GameObject GenerateChunk(Vector2Int chunkCoord)
        {
            GameObject chunkObj = new GameObject($"Chunk_{chunkCoord.x}_{chunkCoord.y}");
            chunkObj.transform.SetParent(transform, false);

            int startX = chunkCoord.x * chunkSize;
            int startY = chunkCoord.y * chunkSize;

            for (int y = startY + chunkSize - 1; y >= startY; y--)
            {
                for (int x = startX + chunkSize - 1; x >= startX; x--)
                {
                    // Exclude indoor bedroom coordinate bounds [-2, 8] x [-2, 8]
                    if (IsInsideRoomBounds(x, y)) continue;

                    // 1. Spawn Grass Tile
                    SpawnGrassTile(chunkObj.transform, x, y);

                    // 2. Procedural Long Grass Overlay (rare clusters ~2.5% spawn rate)
                    if (ShouldSpawnLongGrass(x, y))
                    {
                        SpawnLongGrassTile(chunkObj.transform, x, y);
                    }

                    // 3. Procedural Vegetation (Pine Trees & Bushes)
                    float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
                    if (distToDoor > doorClearRadius)
                    {
                        // A. Procedural Pine Trees in organic bunches/groves
                        float tnx = (x + treeSeed * 73f) * treeNoiseScale;
                        float tny = (y + treeSeed * 73f) * treeNoiseScale;
                        float treeGroveNoise = Mathf.PerlinNoise(tnx, tny);

                        // High frequency deterministic pseudo-random roll within the grove
                        float treeSubRoll = Mathf.PerlinNoise((x * 17.13f + treeSeed), (y * 31.87f + treeSeed));

                        bool spawnTree = (treeGroveNoise > treeThreshold) && (treeSubRoll < treeSpawnProbability);

                        if (spawnTree)
                        {
                            SpawnTreeTile(chunkObj.transform, x, y);
                        }
                        else
                        {
                            // B. Procedural Bush Check
                            float nx = (x + bushSeed * 100f) * bushNoiseScale;
                            float ny = (y + bushSeed * 100f) * bushNoiseScale;
                            float noise = Mathf.PerlinNoise(nx, ny);

                            if (noise > bushThreshold)
                            {
                                SpawnBushTile(chunkObj.transform, x, y);
                            }
                        }
                    }
                }
            }

            if (chunkObj.transform.childCount == 0)
            {
                if (Application.isPlaying) Destroy(chunkObj);
                else DestroyImmediate(chunkObj);
                return null;
            }

            return chunkObj;
        }

        private bool IsInsideRoomBounds(int gx, int gy)
        {
            // The indoor room occupies grid [0..5] x [0..5]
            // Keep a generous sector barrier so outdoor terrain only spawns in the outdoor realm (gx >= 16)
            return gx < 16;
        }

        private void SpawnGrassTile(Transform parent, int gridX, int gridY)
        {
            if (grassSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0);
            GameObject tileObj = new GameObject($"Grass_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = grassSprite;
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000);
        }

        public bool ShouldSpawnLongGrass(int x, int y)
        {
            if (longGrassSprite == null) return false;

            float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
            if (distToDoor <= doorClearRadius) return false;

            float cnx = (x + longGrassSeed * 47f) * longGrassNoiseScale;
            float cny = (y + longGrassSeed * 47f) * longGrassNoiseScale;
            float clusterNoise = Mathf.PerlinNoise(cnx, cny);

            if (clusterNoise <= longGrassClusterThreshold) return false;

            // Deterministic pseudo-random roll within the cluster patch (~24% roll * ~10.5% zone = ~2.5% spawn rate)
            float roll = DeterministicRoll(x, y, longGrassSeed);
            return roll < longGrassSpawnProbability;
        }

        private float DeterministicRoll(int x, int y, int seed)
        {
            unchecked
            {
                int h = seed;
                h = h * 31 + x;
                h = h * 31 + y;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                h = (h ^ (h >> 16)) * 0x45d9f3b;
                h = h ^ (h >> 16);
                return (float)((h & 0x7FFFFFFF) % 10000) / 10000f;
            }
        }

        private void SpawnLongGrassTile(Transform parent, int gridX, int gridY)
        {
            if (longGrassSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0);
            GameObject tileObj = new GameObject($"LongGrass_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = longGrassSprite;
            // Sitting at layerOffset = -8000 + 1 renders directly on top of the base grass tile (-8000)
            // but far below characters/props (>= 0) and behind tiles closer to the camera.
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 1;
        }

        private void SpawnBushTile(Transform parent, int gridX, int gridY)
        {
            if (bushSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0);
            GameObject bushObj = new GameObject($"Bush_{gridX}_{gridY}");
            bushObj.transform.SetParent(parent, false);
            bushObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = bushObj.AddComponent<SpriteRenderer>();
            sr.sprite = bushSprite;
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, 10);

            // Walk-through trigger collider: player passes through with smooth 25% opacity reduction
            CircleCollider2D col = bushObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = bushTriggerRadius;
            col.offset = bushTriggerOffset;

            var trigger = bushObj.AddComponent<IsometricGame.Environment.BushTransparencyTrigger>();
            trigger.SetTargetOpacity(bushWalkThroughOpacity);
        }

        [Header("Tree Alignment Offset")]
        [SerializeField] private Vector2 treeWorldOffset = new Vector2(0f, -0.234375f); // -7.5px / 32

        private void SpawnTreeTile(Transform parent, int gridX, int gridY)
        {
            if (pineTreeSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + treeWorldOffset;
            GameObject treeObj = new GameObject($"PineTree_{gridX}_{gridY}");
            treeObj.transform.SetParent(parent, false);
            treeObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = treeObj.AddComponent<SpriteRenderer>();
            sr.sprite = pineTreeSprite;
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, 20);

            // Compact trunk base collider for solid wood navigation
            CircleCollider2D col = treeObj.AddComponent<CircleCollider2D>();
            col.radius = treeColliderRadius;
            col.offset = treeColliderOffset;
        }

        public void EnsureDoorObject()
        {
            Transform existing = transform.Find("Outside_Door_To_Room");
            if (existing != null)
            {
                doorObject = existing.gameObject;
            }

            if (doorObject == null)
            {
                if (doorSprite == null) EnsureSpritesLoaded();
                if (doorSprite == null) return;

                Vector2 doorWorldPos = IsometricCoordinates.GridToWorld(doorGridPos.x, doorGridPos.y, 0);
                doorObject = new GameObject("Outside_Door_To_Room");
                doorObject.transform.SetParent(transform, false);
                doorObject.transform.position = new Vector3(doorWorldPos.x, doorWorldPos.y, 0f);

                SpriteRenderer sr = doorObject.AddComponent<SpriteRenderer>();
                sr.sprite = doorSprite;
                sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(doorGridPos.x, doorGridPos.y, 0, 150);

                var handle = doorObject.AddComponent<DoorHandleInteraction>();
                handle.isOutdoorDoor = true; // Returns to indoor room
#if UNITY_EDITOR
                handle.handleOutlineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/door handle outline.png");
                handle.openTextSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/GUI/gui card button open text.png");
#endif
                handle.InitializeComponents();
            }
        }
    }
}
