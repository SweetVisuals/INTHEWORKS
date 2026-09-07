using System;
using System.Collections;
using UnityEngine;
using IsometricGame.Tilemap;
using IsometricGame.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsometricGame.Environment
{
    /// <summary>
    /// Makes a pine tree interactable:
    /// - Highlights the tree with 'Assets/Sprites/pine tree white outline overlay.png' when hovered.
    /// - Left-clicking triggers a punchy white overlay flash on the tree sprite.
    /// - Destroyed after 4 clicks and drops 4 small floating quad log blocks on the ground.
    /// </summary>
    [ExecuteAlways]
    public class HarvestableTree : MonoBehaviour
    {
        public static HarvestableTree HoveredTree { get; private set; }

        [Header("Tree Sprites and Highlight")]
        [SerializeField] private Sprite treeOutlineSprite;
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField] private bool pulseOutline = true;
        [SerializeField] private float outlinePulseSpeed = 2.5f;

        [Header("Harvesting / Health")]
        [SerializeField] private int maxHits = 4;
        [SerializeField] private int currentHits = 0;
        [SerializeField] private float hitCooldown = 0.15f;

        [Header("Visual Feedback")]
        [SerializeField] private float whiteFlashDuration = 0.12f;
        [SerializeField] private float shakeMagnitude = 0.035f;

        [Header("Grid Position")]
        public Vector2Int gridPos;

        private SpriteRenderer treeRenderer;
        private GameObject outlineObj;
        private SpriteRenderer outlineRenderer;
        private GameObject whiteOverlayObj;
        private SpriteRenderer whiteOverlayRenderer;
        private BoxCollider2D canopyTrigger;

        private bool isHovered = false;
        private float lastHitTime = -1f;
        private Coroutine flashRoutine;
        private Vector3 originalLocalPos;

        public bool IsHovered => isHovered;
        public int RemainingHits => Mathf.Max(0, maxHits - currentHits);

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            if (HoveredTree == this) HoveredTree = null;
            if (outlineObj != null) outlineObj.SetActive(false);
            if (whiteOverlayObj != null) whiteOverlayObj.SetActive(false);
        }

        public void Initialize()
        {
            treeRenderer = GetComponent<SpriteRenderer>();
            originalLocalPos = transform.localPosition;

            EnsureSpritesLoaded();
            SetupCanopyTrigger();
            SetupOutlineObject();
            SetupWhiteOverlayObject();
        }

        public void EnsureSpritesLoaded()
        {
#if UNITY_EDITOR
            if (treeOutlineSprite == null)
            {
                treeOutlineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/pine tree white outline overlay.png");
            }
#endif
            if (treeOutlineSprite == null)
            {
                Sprite[] all = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (var s in all)
                {
                    if (s.name == "pine tree white outline overlay")
                    {
                        treeOutlineSprite = s;
                        break;
                    }
                }
            }
        }

        private void SetupCanopyTrigger()
        {
            canopyTrigger = GetComponent<BoxCollider2D>();
            if (canopyTrigger == null)
            {
                canopyTrigger = gameObject.AddComponent<BoxCollider2D>();
            }
            canopyTrigger.isTrigger = true;
            // Tree sprite is 32x48 with pivot at y=10/48 (~0.208). Canopy extends up to ~1.15 units
            canopyTrigger.size = new Vector2(0.85f, 1.25f);
            canopyTrigger.offset = new Vector2(0f, 0.55f);
        }

        private void SetupOutlineObject()
        {
            EnsureSpritesLoaded();
            if (outlineObj == null)
            {
                Transform existing = transform.Find("Tree_Outline_Overlay");
                outlineObj = existing != null ? existing.gameObject : new GameObject("Tree_Outline_Overlay");
                outlineObj.transform.SetParent(transform, false);
                outlineObj.transform.localPosition = Vector3.zero;
                outlineObj.transform.localScale = Vector3.one;
            }

            outlineRenderer = outlineObj.GetComponent<SpriteRenderer>();
            if (outlineRenderer == null) outlineRenderer = outlineObj.AddComponent<SpriteRenderer>();

            outlineRenderer.sprite = treeOutlineSprite;
            outlineRenderer.color = outlineColor;

            if (treeRenderer != null)
            {
                outlineRenderer.sortingOrder = treeRenderer.sortingOrder + 2;
            }

            outlineObj.SetActive(false);
        }

        private void SetupWhiteOverlayObject()
        {
            if (whiteOverlayObj == null)
            {
                Transform existing = transform.Find("Tree_White_Overlay");
                whiteOverlayObj = existing != null ? existing.gameObject : new GameObject("Tree_White_Overlay");
                whiteOverlayObj.transform.SetParent(transform, false);
                whiteOverlayObj.transform.localPosition = Vector3.zero;
                whiteOverlayObj.transform.localScale = Vector3.one;
            }

            whiteOverlayRenderer = whiteOverlayObj.GetComponent<SpriteRenderer>();
            if (whiteOverlayRenderer == null) whiteOverlayRenderer = whiteOverlayObj.AddComponent<SpriteRenderer>();

            if (treeRenderer != null)
            {
                whiteOverlayRenderer.sprite = treeRenderer.sprite;
                whiteOverlayRenderer.sortingOrder = treeRenderer.sortingOrder + 1;
            }

            whiteOverlayRenderer.color = new Color(1f, 1f, 1f, 0.90f);
            whiteOverlayObj.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;

            UpdateHoverState();
            UpdateOutlineVisual();
            CheckClickInput();
        }

        private void UpdateHoverState()
        {
            if (IsometricGame.UI.ChestInventoryUI.IsAnyModalOpen)
            {
                SetHover(false);
                return;
            }

            Vector2 mouseWorld = IsometricInputHelper.GetMouseWorldPosition();
            bool overCanopy = (canopyTrigger != null && canopyTrigger.OverlapPoint(mouseWorld));

            if (!overCanopy)
            {
                Vector2 pos = transform.position;
                float dx = Mathf.Abs(mouseWorld.x - pos.x);
                float dy = mouseWorld.y - pos.y;
                overCanopy = (dx <= 0.42f && dy >= -0.05f && dy <= 1.20f);
            }

            SetHover(overCanopy);
        }

        private void SetHover(bool hover)
        {
            if (isHovered == hover) return;

            isHovered = hover;
            if (isHovered)
            {
                HoveredTree = this;
                if (outlineObj != null) outlineObj.SetActive(true);
            }
            else
            {
                if (HoveredTree == this) HoveredTree = null;
                if (outlineObj != null) outlineObj.SetActive(false);
            }
        }

        private void UpdateOutlineVisual()
        {
            if (!isHovered || outlineRenderer == null) return;

            if (treeRenderer != null)
            {
                outlineRenderer.sortingOrder = treeRenderer.sortingOrder + 2;
            }

            if (pulseOutline)
            {
                float t = (Mathf.Sin(Time.time * outlinePulseSpeed) + 1f) * 0.5f;
                float alpha = Mathf.SmoothStep(0.55f, 1.0f, t);
                outlineRenderer.color = new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha);
            }
            else
            {
                outlineRenderer.color = outlineColor;
            }
        }

        private void CheckClickInput()
        {
            if (!isHovered) return;

            bool leftClickDown = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                leftClickDown = Mouse.current.leftButton.wasPressedThisFrame;
            }
#else
            leftClickDown = Input.GetMouseButtonDown(0);
#endif

            if (leftClickDown && Time.time >= lastHitTime + hitCooldown)
            {
                OnTreeClicked();
            }
        }

        public void OnTreeClicked()
        {
            lastHitTime = Time.time;
            currentHits++;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashWhiteOverlayRoutine());

            if (currentHits >= maxHits)
            {
                DestroyTree();
            }
        }

        private IEnumerator FlashWhiteOverlayRoutine()
        {
            if (whiteOverlayObj != null && whiteOverlayRenderer != null && treeRenderer != null)
            {
                whiteOverlayRenderer.sprite = treeRenderer.sprite;
                whiteOverlayRenderer.sortingOrder = treeRenderer.sortingOrder + 1;
                whiteOverlayObj.SetActive(true);
            }

            float elapsed = 0f;
            Vector3 basePos = originalLocalPos;

            while (elapsed < whiteFlashDuration)
            {
                elapsed += Time.deltaTime;
                float shakeX = Mathf.Sin(elapsed * 60f) * shakeMagnitude * (1f - (elapsed / whiteFlashDuration));
                transform.localPosition = basePos + new Vector3(shakeX, 0f, 0f);
                yield return null;
            }

            transform.localPosition = basePos;
            if (whiteOverlayObj != null) whiteOverlayObj.SetActive(false);
            flashRoutine = null;
        }

        private void DestroyTree()
        {
            Vector2 baseWorldPos = (Vector2)transform.position + new Vector2(0f, 0.15f);

            if (QuarterBlockManager.Instance != null)
            {
                QuarterBlockManager.Instance.SpawnDroppedQuarterBlocks(baseWorldPos, QuarterBlockType.Log, 4);
            }

            SpawnWoodDebris(baseWorldPos);

            if (HoveredTree == this) HoveredTree = null;

            Destroy(gameObject);
        }

        private void SpawnWoodDebris(Vector2 pos)
        {
            GameObject debrisObj = new GameObject("Tree_Break_Debris");
            debrisObj.transform.position = new Vector3(pos.x, pos.y, 0f);

            ParticleSystem ps = debrisObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.48f, 0.32f, 0.18f),
                new Color(0.22f, 0.45f, 0.20f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 22) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.30f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.y = new ParticleSystem.MinMaxCurve(-0.8f, 1.2f);

            var renderer = debrisObj.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 500;

            Destroy(debrisObj, 1.2f);
        }
    }
}
