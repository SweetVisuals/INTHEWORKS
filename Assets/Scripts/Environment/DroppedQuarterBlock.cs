using System;
using UnityEngine;
using IsometricGame.Tilemap;
using IsometricGame.Player;

namespace IsometricGame.Environment
{
    /// <summary>
    /// Represents a small floating quarter block collectible item dropped on the floor after breaking.
    /// Pops out in a parabolic isometric arc, hovers slightly above the floor with a soft shadow and idle bob,
    /// and smoothly magnetizes toward the player when in proximity to be picked up.
    /// </summary>
    [ExecuteAlways]
    public class DroppedQuarterBlock : MonoBehaviour
    {
        [Header("Item Data")]
        [SerializeField] private QuarterBlockType blockType = QuarterBlockType.Grass;
        [SerializeField] private Sprite itemSprite;

        [Header("Visual Size & Floating")]
        [Tooltip("Scale of the small floating item")]
        [SerializeField] private Vector3 itemScale = new Vector3(0.55f, 0.55f, 1f);
        [Tooltip("Base height the item hovers above its floor contact shadow")]
        [SerializeField] private float hoverHeight = 0.08f;
        [Tooltip("Amplitude of the idle bobbing animation")]
        [SerializeField] private float bobAmplitude = 0.02f;
        [Tooltip("Speed of the idle bobbing hover")]
        [SerializeField] private float bobSpeed = 3.5f;

        [Header("Arc / Drop Physics")]
        [SerializeField] private float arcDuration = 0.40f;
        [SerializeField] private float arcHeight = 0.24f;

        [Header("Pickup Settings")]
        [SerializeField] private float pickupDelay = 0.35f;
        [SerializeField] private float pickupRadius = 1.35f;
        [SerializeField] private float initialMagnetSpeed = 3.5f;
        [SerializeField] private float magnetAcceleration = 14.0f;

        private GameObject itemSpriteObj;
        private SpriteRenderer itemRenderer;
        private GameObject shadowObj;
        private SpriteRenderer shadowRenderer;

        private Vector2 startPos;
        private Vector2 targetFloorPos;
        private float spawnTime;
        private bool hasLanded = false;
        private bool isMagnetizing = false;
        private float currentMagnetSpeed;
        private float randomBobPhase;

        public QuarterBlockType BlockType => blockType;

        public static DroppedQuarterBlock Spawn(Vector2 origin, Vector2 floorTarget, QuarterBlockType type, Sprite sprite)
        {
            GameObject obj = new GameObject($"Dropped_{type}_{UnityEngine.Random.Range(1000, 9999)}");
            DroppedQuarterBlock drop = obj.AddComponent<DroppedQuarterBlock>();
            drop.blockType = type;
            drop.itemSprite = sprite;
            drop.startPos = origin;
            drop.targetFloorPos = floorTarget;
            drop.transform.position = origin;
            drop.InitializeVisuals();
            return drop;
        }

        private void Awake()
        {
            spawnTime = Time.time;
            randomBobPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            currentMagnetSpeed = initialMagnetSpeed;
        }

        private void Start()
        {
            InitializeVisuals();
        }

        private void InitializeVisuals()
        {
            if (itemSpriteObj == null)
            {
                itemSpriteObj = new GameObject("Visual");
                itemSpriteObj.transform.SetParent(transform, false);
                itemRenderer = itemSpriteObj.AddComponent<SpriteRenderer>();
            }

            if (itemSprite != null && itemRenderer != null)
            {
                itemRenderer.sprite = itemSprite;
            }

            itemSpriteObj.transform.localScale = itemScale;

            // Soft ground shadow
            if (shadowObj == null)
            {
                shadowObj = new GameObject("Shadow");
                shadowObj.transform.SetParent(transform, false);
                shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
                shadowRenderer.sprite = GetOrCreateDropShadowSprite();
                shadowRenderer.color = new Color(0f, 0f, 0f, 0.35f);
            }

            shadowObj.transform.localScale = new Vector3(0.24f, 0.10f, 1f);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            float elapsed = Time.time - spawnTime;

            // 1. Initial Parabolic Arc Landing
            if (!hasLanded)
            {
                if (elapsed < arcDuration)
                {
                    float t = elapsed / arcDuration;
                    Vector2 currentFloor = Vector2.Lerp(startPos, targetFloorPos, t);
                    float arcY = Mathf.Sin(t * Mathf.PI) * arcHeight;

                    transform.position = currentFloor;
                    if (itemSpriteObj != null)
                    {
                        itemSpriteObj.transform.localPosition = new Vector3(0f, hoverHeight + arcY, 0f);
                    }
                    if (shadowObj != null)
                    {
                        shadowObj.transform.localPosition = Vector3.zero;
                        float shadowScaleFactor = Mathf.Lerp(0.7f, 1.0f, 1f - Mathf.Sin(t * Mathf.PI) * 0.45f);
                        shadowObj.transform.localScale = new Vector3(0.24f * shadowScaleFactor, 0.10f * shadowScaleFactor, 1f);
                    }

                    UpdateSortingOrder(currentFloor.y);
                    return;
                }
                else
                {
                    hasLanded = true;
                    transform.position = targetFloorPos;
                    if (shadowObj != null)
                    {
                        shadowObj.transform.localPosition = Vector3.zero;
                        shadowObj.transform.localScale = new Vector3(0.24f, 0.10f, 1f);
                    }
                }
            }

            // 2. Small Floating Item Idle Bob
            if (!isMagnetizing)
            {
                float bobY = Mathf.Sin(Time.time * bobSpeed + randomBobPhase) * bobAmplitude;
                if (itemSpriteObj != null)
                {
                    itemSpriteObj.transform.localPosition = new Vector3(0f, hoverHeight + bobY, 0f);
                }
            }

            // 3. Magnetize towards Player when nearby
            if (elapsed >= pickupDelay)
            {
                Transform playerTrans = GetPlayerTransform();
                if (playerTrans != null)
                {
                    Vector2 playerPos = playerTrans.position;
                    Vector2 targetPos = playerPos + new Vector2(0f, 0.15f);
                    float dist = Vector2.Distance(transform.position, targetPos);

                    if (dist <= pickupRadius || isMagnetizing)
                    {
                        isMagnetizing = true;
                        currentMagnetSpeed += magnetAcceleration * Time.deltaTime;
                        transform.position = Vector2.MoveTowards(transform.position, targetPos, currentMagnetSpeed * Time.deltaTime);

                        // Also scale down slightly as it is absorbed
                        if (itemSpriteObj != null)
                        {
                            float absorbScale = Mathf.Clamp01(dist / 0.8f);
                            itemSpriteObj.transform.localScale = itemScale * Mathf.Lerp(0.4f, 1.0f, absorbScale);
                        }

                        if (dist < 0.22f)
                        {
                            CollectItem();
                            return;
                        }
                    }
                }
            }

            UpdateSortingOrder(transform.position.y);
        }

        private void UpdateSortingOrder(float worldY)
        {
            int order = -Mathf.RoundToInt(worldY * 100f) + 10;
            if (itemRenderer != null) itemRenderer.sortingOrder = order;
            if (shadowRenderer != null) shadowRenderer.sortingOrder = order - 1;
        }

        private void CollectItem()
        {
            if (QuarterBlockManager.Instance != null)
            {
                QuarterBlockManager.Instance.AddToInventory(blockType, 1);
            }

            if (IsometricGame.UI.HotbarUI.Instance != null)
            {
                IsometricGame.UI.HotbarUI.Instance.SyncWithInventory();
            }

            Destroy(gameObject);
        }

        private static Transform GetPlayerTransform()
        {
            if (IsometricPlayerController.Instance != null)
            {
                return IsometricPlayerController.Instance.transform;
            }
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) return player.transform;
            return null;
        }

        private static Sprite cachedDropShadow;
        private static Sprite GetOrCreateDropShadowSprite()
        {
            if (cachedDropShadow != null) return cachedDropShadow;

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - dist * dist);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            cachedDropShadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedDropShadow;
        }
    }
}
