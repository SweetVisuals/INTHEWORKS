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
        [Tooltip("Opacity when player walks through (1.0 = no opacity reduction).")]
        [Range(0f, 1f)]
        [SerializeField] private float bushWalkThroughOpacity = 1.0f;

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

        [Header("Procedural Long Grass Overlay (Rarer Clusters ~0.9%)")]
        [Tooltip("Scale of Perlin noise for long grass patches (smaller = larger patch radius).")]
        [SerializeField] private float longGrassNoiseScale = 0.18f;
        [Tooltip("Threshold above which a long grass cluster patch forms (higher = sparser patches).")]
        [SerializeField] private float longGrassClusterThreshold = 0.72f;
        [Tooltip("Spawn probability within cluster patches (~0.18 yields ~0.9% overall spawn rate).")]
        [Range(0f, 1f)]
        [SerializeField] private float longGrassSpawnProbability = 0.18f;
        [SerializeField] private int longGrassSeed = 9999;

        [Header("Procedural Short Grass Overlay (Natural Clusters ~3.2%)")]
        [Tooltip("Scale of Perlin noise for short grass patches.")]
        [SerializeField] private float shortGrassNoiseScale = 0.16f;
        [Tooltip("Threshold above which a short grass cluster patch forms.")]
        [SerializeField] private float shortGrassClusterThreshold = 0.65f;
        [Tooltip("Spawn probability within short grass cluster patches.")]
        [Range(0f, 1f)]
        [SerializeField] private float shortGrassSpawnProbability = 0.30f;
        [SerializeField] private int shortGrassSeed = 7777;

        [Header("Procedural Tiny Grass Foliage (Subtle ~0.3% Chance Each)")]
        [Range(0f, 1f)]
        [SerializeField] private float tinyFoliage1SpawnRate = 0.003f;
        [Range(0f, 1f)]
        [SerializeField] private float tinyFoliage2SpawnRate = 0.003f;
        [Range(0f, 1f)]
        [SerializeField] private float tinyFoliage3SpawnRate = 0.003f;
        [SerializeField] private int tinyFoliageSeed = 5555;

        [Header("Procedural Stone Quad Plates Path (Batches of Max 3)")]
        [Tooltip("Scale of regional Perlin noise controlling where stone path batches appear.")]
        [SerializeField] private float stonePathNoiseScale = 0.08f;
        [Tooltip("Noise threshold above which a region can spawn stone path batches.")]
        [SerializeField] private float stonePathClusterThreshold = 0.58f;
        [Tooltip("Seed for stone path noise and batch placement.")]
        [SerializeField] private int stonePathSeed = 8888;
        [Tooltip("Grid spacing (cell size) between possible batch seed locations.")]
        [SerializeField] private int stonePathCellSize = 6;

        [Header("Procedural Small Rocks Overlay (Rare Clusters ~0.6%)")]
        [Tooltip("Scale of Perlin noise for small rock patches.")]
        [SerializeField] private float smallRocksNoiseScale = 0.16f;
        [Tooltip("Threshold above which a small rock patch forms (higher = sparser patches).")]
        [SerializeField] private float smallRocksClusterThreshold = 0.75f;
        [Tooltip("Spawn probability within rock cluster patches (~0.12 yields ~0.6% overall spawn rate).")]
        [Range(0f, 1f)]
        [SerializeField] private float smallRocksSpawnProbability = 0.12f;
        [SerializeField] private int smallRocksSeed = 4444;

        [Header("Sprites")]
        public Sprite grassSprite;
        public Sprite stoneBlockSprite;
        public Sprite longGrassSprite;
        public Sprite shortGrassSprite;
        public Sprite tinyFoliage1Sprite;
        public Sprite tinyFoliage2Sprite;
        public Sprite tinyFoliage3Sprite;
        public Sprite stonePathSprite;
        public Sprite smallRock1Sprite;
        public Sprite smallRock2Sprite;
        public Sprite smallRock3Sprite;
        public Sprite smallRocks12Sprite;
        public Sprite smallRocks13Sprite;
        public Sprite smallRocks23Sprite;
        public Sprite smallRocksClusterSprite;
        public Sprite bushSprite;
        public Sprite pineTreeSprite;
        public Sprite doorSprite;

        [Header("Subterranean Stone Layers")]
        [Tooltip("Vertical step height per subterranean layer in world units (10px = 0.3125f)")]
        [SerializeField] private float subterraneanStepHeight = 0.3125f;
        [Tooltip("Surface decoration vertical offset to sit on top of elevated grass block (10px = 0.3125f)")]
        [SerializeField] private float grassTileSurfaceOffset = 0.3125f;
        [Tooltip("Number of subterranean stone layers rendered beneath grass")]
        [SerializeField] private int subterraneanLayers = 1;

        public float SubterraneanStepHeight => subterraneanStepHeight;
        public float GrassTileSurfaceOffset => grassTileSurfaceOffset;
        public int SubterraneanLayers => subterraneanLayers;

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
        public float ShortGrassNoiseScale { get => shortGrassNoiseScale; set => shortGrassNoiseScale = value; }
        public float ShortGrassClusterThreshold { get => shortGrassClusterThreshold; set => shortGrassClusterThreshold = value; }
        public float ShortGrassSpawnProbability { get => shortGrassSpawnProbability; set => shortGrassSpawnProbability = value; }
        public int ShortGrassSeed { get => shortGrassSeed; set => shortGrassSeed = value; }
        public float TinyFoliage1SpawnRate { get => tinyFoliage1SpawnRate; set => tinyFoliage1SpawnRate = value; }
        public float TinyFoliage2SpawnRate { get => tinyFoliage2SpawnRate; set => tinyFoliage2SpawnRate = value; }
        public float TinyFoliage3SpawnRate { get => tinyFoliage3SpawnRate; set => tinyFoliage3SpawnRate = value; }
        public int TinyFoliageSeed { get => tinyFoliageSeed; set => tinyFoliageSeed = value; }
        public float StonePathNoiseScale { get => stonePathNoiseScale; set => stonePathNoiseScale = value; }
        public float StonePathClusterThreshold { get => stonePathClusterThreshold; set => stonePathClusterThreshold = value; }
        public int StonePathSeed { get => stonePathSeed; set => stonePathSeed = value; }
        public int StonePathCellSize { get => stonePathCellSize; set => stonePathCellSize = value; }
        public float SmallRocksNoiseScale { get => smallRocksNoiseScale; set => smallRocksNoiseScale = value; }
        public float SmallRocksClusterThreshold { get => smallRocksClusterThreshold; set => smallRocksClusterThreshold = value; }
        public float SmallRocksSpawnProbability { get => smallRocksSpawnProbability; set => smallRocksSpawnProbability = value; }
        public int SmallRocksSeed { get => smallRocksSeed; set => smallRocksSeed = value; }
        public float DoorClearRadius { get => doorClearRadius; set => doorClearRadius = value; }

        public static OutdoorInfiniteTerrain Instance { get; private set; }

        private readonly HashSet<Vector2Int> brokenTiles = new HashSet<Vector2Int>();

        public bool IsTileBroken(int x, int y) => brokenTiles.Contains(new Vector2Int(x, y));

        public bool BreakTile(int gridX, int gridY)
        {
            Vector2Int pos = new Vector2Int(gridX, gridY);
            brokenTiles.Add(pos);

            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)gridX / chunkSize),
                Mathf.FloorToInt((float)gridY / chunkSize)
            );

            if (loadedChunks.TryGetValue(chunkCoord, out GameObject chunkObj) && chunkObj != null)
            {
                bool removed = false;
                string[] targets = new string[]
                {
                    $"Bush_{gridX}_{gridY}",
                    $"SmallRocks_{gridX}_{gridY}",
                    $"TinyFoliage_{gridX}_{gridY}",
                    $"LongGrass_{gridX}_{gridY}",
                    $"ShortGrass_{gridX}_{gridY}",
                    $"StonePath_{gridX}_{gridY}",
                    $"Grass_{gridX}_{gridY}"
                };

                foreach (string name in targets)
                {
                    Transform child = chunkObj.transform.Find(name);
                    if (child != null)
                    {
                        if (Application.isPlaying) Destroy(child.gameObject);
                        else DestroyImmediate(child.gameObject);
                        removed = true;
                    }
                }
                return removed;
            }
            return false;
        }

        private void Awake()
        {
            Instance = this;
            EnsureSpritesLoaded();
        }

        private void OnEnable()
        {
            Instance = this;
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
            if (grassSprite == null || grassSprite.name == "grass tile" || grassSprite.name == "grass tile_0")
            {
                grassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/grass tile block.png")
                           ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/grass tile.png");
            }
            if (stoneBlockSprite == null)
            {
                stoneBlockSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/stone_block.png");
            }
            if (longGrassSprite == null)
            {
                longGrassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/long grass small tile.png");
            }
            if (shortGrassSprite == null)
            {
                shortGrassSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/short grass small tile.png");
            }
            if (tinyFoliage1Sprite == null)
            {
                tinyFoliage1Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/tiny grass foliage 1.png");
            }
            if (tinyFoliage2Sprite == null)
            {
                tinyFoliage2Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/tiny grass foliage 2.png");
            }
            if (tinyFoliage3Sprite == null)
            {
                tinyFoliage3Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/tiny grass foliage 3.png");
            }
            if (stonePathSprite == null)
            {
                stonePathSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/stone quad plates path.png");
            }
            if (smallRock1Sprite == null)
            {
                smallRock1Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rock 1.png");
            }
            if (smallRock2Sprite == null)
            {
                smallRock2Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rock 2.png");
            }
            if (smallRock3Sprite == null)
            {
                smallRock3Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rock 3.png");
            }
            if (smallRocks12Sprite == null)
            {
                smallRocks12Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rocks 1 and 2.png");
            }
            if (smallRocks13Sprite == null)
            {
                smallRocks13Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rocks 1 and 3.png");
            }
            if (smallRocks23Sprite == null)
            {
                smallRocks23Sprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rocks 2 and 3.png");
            }
            if (smallRocksClusterSprite == null)
            {
                smallRocksClusterSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/small rocks overlay (3) use each individually and together.png");
            }
            if (bushSprite == null)
            {
                bushSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/bush block.png");
            }
            if (pineTreeSprite == null || pineTreeSprite.name == "pine tree")
            {
                pineTreeSprite = IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/pine tree test 1.png")
                              ?? IsometricGame.UI.UISpriteUtility.LoadSprite("Assets/Sprites/Map/pine tree.png");
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

                    // 1. Always spawn Subterranean Stone Layer beneath (elevation -1, -2...)
                    for (int layer = 1; layer <= subterraneanLayers; layer++)
                    {
                        SpawnStoneTile(chunkObj.transform, x, y, -layer);
                    }

                    // 2. Skip surface grass & decorations if broken
                    if (IsTileBroken(x, y)) continue;

                    // 3. Spawn Grass Tile Block
                    SpawnGrassTile(chunkObj.transform, x, y);

                    float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
                    bool isStonePath = false;
                    bool hasVegetation = false;

                    if (distToDoor > doorClearRadius)
                    {
                        // 4. Procedural Stone Quad Plates Path (batches of max 3 tiles with noise)
                        isStonePath = ShouldSpawnStonePath(x, y);
                        if (isStonePath)
                        {
                            SpawnStonePathTile(chunkObj.transform, x, y);
                        }
                        else
                        {
                            // 5. Procedural Vegetation (Pine Trees & Bushes) - evaluated first so foliage doesn't spawn under them
                            float tnx = (x + treeSeed * 73f) * treeNoiseScale;
                            float tny = (y + treeSeed * 73f) * treeNoiseScale;
                            float treeGroveNoise = Mathf.PerlinNoise(tnx, tny);

                            // High frequency deterministic pseudo-random roll within the grove
                            float treeSubRoll = Mathf.PerlinNoise((x * 17.13f + treeSeed), (y * 31.87f + treeSeed));

                            bool spawnTree = (treeGroveNoise > treeThreshold) && (treeSubRoll < treeSpawnProbability);

                            if (spawnTree)
                            {
                                hasVegetation = true;
                                SpawnTreeTile(chunkObj.transform, x, y);
                            }
                            else
                            {
                                float nx = (x + bushSeed * 100f) * bushNoiseScale;
                                float ny = (y + bushSeed * 100f) * bushNoiseScale;
                                float noise = Mathf.PerlinNoise(nx, ny);

                                if (noise > bushThreshold)
                                {
                                    hasVegetation = true;
                                    SpawnBushTile(chunkObj.transform, x, y);
                                }
                            }

                            // 6. Procedural Grass Overlays: Rare Long Grass, Natural Short Grass, Small Rocks, & Tiny Foliage
                            // Only spawn on open, unobstructed grass tiles (never under bushes or trees)
                            if (!hasVegetation)
                            {
                                if (ShouldSpawnSmallRocks(x, y))
                                {
                                    SpawnSmallRocksTile(chunkObj.transform, x, y);
                                }
                                else
                                {
                                    if (ShouldSpawnLongGrass(x, y))
                                    {
                                        SpawnLongGrassTile(chunkObj.transform, x, y);
                                    }
                                    else if (ShouldSpawnShortGrass(x, y))
                                    {
                                        SpawnShortGrassTile(chunkObj.transform, x, y);
                                    }

                                    SpawnTinyFoliageIfRolled(chunkObj.transform, x, y);
                                }
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
            sr.sortingLayerName = "Default";
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000);
        }

        private void SpawnStoneTile(Transform parent, int gridX, int gridY, int layer)
        {
            if (stoneBlockSprite == null) return;

            Vector2 baseWorld = IsometricCoordinates.GridToWorld(gridX, gridY, 0);
            Vector2 worldPos = baseWorld + new Vector2(0f, layer * subterraneanStepHeight);
            string tileName = layer == -1 ? $"Stone_{gridX}_{gridY}" : $"Stone_{gridX}_{gridY}_L{layer}";
            GameObject tileObj = new GameObject(tileName);
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = stoneBlockSprite;
            sr.sortingLayerName = "Subterranean";
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -12000 + (layer * 10));
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

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + new Vector2(0f, grassTileSurfaceOffset);
            GameObject tileObj = new GameObject($"LongGrass_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = longGrassSprite;
            // 50% random horizontal flip for natural organic variation
            sr.flipX = DeterministicRoll(gridX, gridY, longGrassSeed + 456) < 0.5f;
            // Sitting at layerOffset = -8000 + 1 renders directly on top of the base grass tile (-8000)
            // but far below characters/props (>= 0) and behind tiles closer to the camera.
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 1;
        }

        public bool ShouldSpawnShortGrass(int x, int y)
        {
            if (shortGrassSprite == null) return false;

            float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
            if (distToDoor <= doorClearRadius) return false;

            float cnx = (x + shortGrassSeed * 37f) * shortGrassNoiseScale;
            float cny = (y + shortGrassSeed * 37f) * shortGrassNoiseScale;
            float clusterNoise = Mathf.PerlinNoise(cnx, cny);

            if (clusterNoise <= shortGrassClusterThreshold) return false;

            // Deterministic pseudo-random roll within the short grass cluster patch (~30% roll * ~10.5% zone = ~3.2% spawn rate)
            float roll = DeterministicRoll(x, y, shortGrassSeed);
            return roll < shortGrassSpawnProbability;
        }

        private void SpawnShortGrassTile(Transform parent, int gridX, int gridY)
        {
            if (shortGrassSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + new Vector2(0f, grassTileSurfaceOffset);
            GameObject tileObj = new GameObject($"ShortGrass_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = shortGrassSprite;
            // 50% random horizontal flip for natural organic variation
            sr.flipX = DeterministicRoll(gridX, gridY, shortGrassSeed + 789) < 0.5f;
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 1;
        }

        private void SpawnTinyFoliageIfRolled(Transform parent, int gridX, int gridY)
        {
            // Variant 1 (1% independent spawn chance)
            if (tinyFoliage1Sprite != null && DeterministicRoll(gridX, gridY, tinyFoliageSeed + 101) < tinyFoliage1SpawnRate)
            {
                SpawnTinyFoliageTile(parent, gridX, gridY, tinyFoliage1Sprite, 1, -0.12f, 0.04f);
            }

            // Variant 2 (1% independent spawn chance)
            if (tinyFoliage2Sprite != null && DeterministicRoll(gridX, gridY, tinyFoliageSeed + 202) < tinyFoliage2SpawnRate)
            {
                SpawnTinyFoliageTile(parent, gridX, gridY, tinyFoliage2Sprite, 2, 0.10f, -0.03f);
            }

            // Variant 3 (1% independent spawn chance)
            if (tinyFoliage3Sprite != null && DeterministicRoll(gridX, gridY, tinyFoliageSeed + 303) < tinyFoliage3SpawnRate)
            {
                SpawnTinyFoliageTile(parent, gridX, gridY, tinyFoliage3Sprite, 3, -0.02f, -0.06f);
            }
        }

        private void SpawnTinyFoliageTile(Transform parent, int gridX, int gridY, Sprite sprite, int variant, float offsetX, float offsetY)
        {
            if (sprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + new Vector2(0f, grassTileSurfaceOffset);
            GameObject foliageObj = new GameObject($"TinyFoliage_{variant}_{gridX}_{gridY}");
            foliageObj.transform.SetParent(parent, false);
            foliageObj.transform.position = new Vector3(worldPos.x + offsetX, worldPos.y + offsetY, 0f);

            SpriteRenderer sr = foliageObj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            // 50% random horizontal flip for natural organic variation
            sr.flipX = DeterministicRoll(gridX, gridY, tinyFoliageSeed + variant * 77) < 0.5f;
            // Sitting at layerOffset = -8000 + 2 places tiny foliage directly on top of base grass and tile overlays
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 2;
        }

        public bool ShouldSpawnStonePath(int x, int y)
        {
            if (stonePathSprite == null) return false;

            float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
            if (distToDoor <= doorClearRadius) return false;

            int cx = Mathf.FloorToInt((float)x / stonePathCellSize);
            int cy = Mathf.FloorToInt((float)y / stonePathCellSize);

            // Regional noise check: does this region have stone path stepping stones?
            float rnx = (cx * stonePathCellSize + stonePathSeed * 61f) * stonePathNoiseScale;
            float rny = (cy * stonePathCellSize + stonePathSeed * 61f) * stonePathNoiseScale;
            float noise = Mathf.PerlinNoise(rnx, rny);
            if (noise <= stonePathClusterThreshold) return false;

            int h = HashCell(cx, cy, stonePathSeed);
            int batchSize = (h % 3) + 1; // Strictly 1, 2, or 3 in a batch
            int dir = (h >> 3) % 4;

            int dx = 0, dy = 0;
            switch (dir)
            {
                case 0: dx = 1; dy = 0; break;
                case 1: dx = 0; dy = 1; break;
                case 2: dx = 1; dy = 1; break;
                default: dx = 1; dy = -1; break;
            }

            int margin = stonePathCellSize - 4;
            if (margin < 1) margin = 1;

            int ax = cx * stonePathCellSize + 1 + ((h >> 6) % margin);
            int ay = cy * stonePathCellSize + (dy == -1 ? 3 : 1) + ((h >> 9) % margin);

            if (x == ax && y == ay) return true;
            if (batchSize >= 2 && x == ax + dx && y == ay + dy) return true;
            if (batchSize == 3 && x == ax + 2 * dx && y == ay + 2 * dy) return true;

            return false;
        }

        private int HashCell(int cx, int cy, int seed)
        {
            unchecked
            {
                int h = seed;
                h = (h * 31 + cx) & 0x7FFFFFFF;
                h = (h * 31 + cy) & 0x7FFFFFFF;
                h = ((h ^ (h >> 16)) * 0x45d9f3b) & 0x7FFFFFFF;
                h = ((h ^ (h >> 16)) * 0x45d9f3b) & 0x7FFFFFFF;
                h = (h ^ (h >> 16)) & 0x7FFFFFFF;
                return h;
            }
        }

        private void SpawnStonePathTile(Transform parent, int gridX, int gridY)
        {
            if (stonePathSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + new Vector2(0f, grassTileSurfaceOffset);
            GameObject tileObj = new GameObject($"StonePath_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = stonePathSprite;
            // 50% random horizontal flip for natural variety
            sr.flipX = DeterministicRoll(gridX, gridY, stonePathSeed + 111) < 0.5f;
            // Sitting at layerOffset = -8000 + 1 renders directly on top of base grass (-8000)
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 1;
        }

        public bool ShouldSpawnSmallRocks(int x, int y)
        {
            if (smallRocksClusterSprite == null && smallRock1Sprite == null) return false;

            float distToDoor = Vector2.Distance(new Vector2(x, y), doorGridPos);
            if (distToDoor <= doorClearRadius) return false;

            float cnx = (x + smallRocksSeed * 53f) * smallRocksNoiseScale;
            float cny = (y + smallRocksSeed * 53f) * smallRocksNoiseScale;
            float clusterNoise = Mathf.PerlinNoise(cnx, cny);

            if (clusterNoise <= smallRocksClusterThreshold) return false;

            float roll = DeterministicRoll(x, y, smallRocksSeed);
            return roll < smallRocksSpawnProbability;
        }

        private void SpawnSmallRocksTile(Transform parent, int gridX, int gridY)
        {
            float roll = DeterministicRoll(gridX, gridY, smallRocksSeed + 333);
            Sprite chosenSprite = null;
            string label = "Rocks";

            if (roll < 0.65f)
            {
                // 1 Rock (65% of rock spawns - single rock for lighter visual presence)
                int subRoll = (int)(DeterministicRoll(gridX, gridY, smallRocksSeed + 444) * 3f);
                if (subRoll == 0) { chosenSprite = smallRock1Sprite ?? smallRocksClusterSprite; label = "Rock1"; }
                else if (subRoll == 1) { chosenSprite = smallRock2Sprite ?? smallRocksClusterSprite; label = "Rock2"; }
                else { chosenSprite = smallRock3Sprite ?? smallRocksClusterSprite; label = "Rock3"; }
            }
            else if (roll < 0.90f)
            {
                // 2 Rocks (25% of rock spawns)
                int subRoll = (int)(DeterministicRoll(gridX, gridY, smallRocksSeed + 555) * 3f);
                if (subRoll == 0) { chosenSprite = smallRocks12Sprite ?? smallRocksClusterSprite; label = "Rocks12"; }
                else if (subRoll == 1) { chosenSprite = smallRocks13Sprite ?? smallRocksClusterSprite; label = "Rocks13"; }
                else { chosenSprite = smallRocks23Sprite ?? smallRocksClusterSprite; label = "Rocks23"; }
            }
            else
            {
                // All 3 Rocks together (10% of rock spawns - rare cluster)
                chosenSprite = smallRocksClusterSprite ?? smallRock1Sprite;
                label = "RocksCluster";
            }

            if (chosenSprite == null) return;

            Vector2 worldPos = IsometricCoordinates.GridToWorld(gridX, gridY, 0) + new Vector2(0f, grassTileSurfaceOffset);
            GameObject tileObj = new GameObject($"Small{label}_{gridX}_{gridY}");
            tileObj.transform.SetParent(parent, false);
            tileObj.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            SpriteRenderer sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite = chosenSprite;
            // 50% random horizontal flip for natural organic variation
            sr.flipX = DeterministicRoll(gridX, gridY, smallRocksSeed + 777) < 0.5f;
            // Sitting at layerOffset = -8000 + 2 renders above base grass and overlays
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, -8000) + 2;
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
            sr.sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridX, gridY, 0, 45);

            // Walk-through trigger collider: player passes through with smooth 25% opacity reduction
            CircleCollider2D col = bushObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = bushTriggerRadius;
            col.offset = bushTriggerOffset;

            var trigger = bushObj.AddComponent<IsometricGame.Environment.BushTransparencyTrigger>();
            trigger.SetTargetOpacity(bushWalkThroughOpacity);
        }

        [Header("Tree Alignment Offset")]
        [SerializeField] private Vector2 treeWorldOffset = new Vector2(0f, 0.078125f); // +2.5px / 32

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

            var harvestable = treeObj.AddComponent<IsometricGame.Environment.HarvestableTree>();
            harvestable.gridPos = new Vector2Int(gridX, gridY);
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
