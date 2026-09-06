using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IsometricGame.Tilemap;

namespace IsometricGame.Player
{
    public enum IsometricControlScheme
    {
        ScreenRelative = 0,     // W=Straight Up, S=Straight Down, D=Straight Right, A=Straight Left (Natural / Standard)
        IsometricGrid = 1       // W=Up-Right (Grid+Y), S=Down-Left (Grid-Y), D=Down-Right (Grid+X), A=Up-Left (Grid-X)
    }

    /// <summary>
    /// Smooth 3D Isometric Pawn Controller for the Cylinder Player.
    /// Features:
    /// - Clean Screen-Relative (W=Up, S=Down, D=Right, A=Left) or Grid-Relative locomotion.
    /// - 3D Upright Pawn with isometric pitch and smooth 3D Y-axis yaw turning.
    /// - Bouncy 3D walk bobbing and lean.
    /// - Precise depth sorting in isometric space.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class IsometricPlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("ScreenRelative makes W/S/A/D move directly Up/Down/Left/Right on screen. IsometricGrid aligns W/S/A/D to diamond grid axes.")]
        [SerializeField] private IsometricControlScheme controlScheme = IsometricControlScheme.ScreenRelative;
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float acceleration = 30f;
        [SerializeField] private float deceleration = 35f;

        [Header("3D Visual Pawn")]
        [SerializeField] private Transform visualTransform;
        [SerializeField] private Transform cylinderBody;
        [SerializeField] private Transform shadowTransform;
        [SerializeField] private float turnSpeed = 14f;
        [SerializeField] private float pitchAngle = 30f;

        [Header("Locomotion Animation (3D Bobbing)")]
        [SerializeField] private float bobFrequency = 14f;
        [SerializeField] private float bobHeight = 0.04f;
        [SerializeField] private float leanAmount = 4f;

        [Header("Depth Sorting")]
        [SerializeField] private SpriteRenderer[] spriteRenderers;
        [SerializeField] private MeshRenderer[] meshRenderers;

        private Rigidbody2D rb;
        private Vector2 currentVelocity;
        private Vector2 rawInput;
        private Vector2 isoMoveDir;
        private float currentYaw = 0f;
        private float targetYaw = 0f;
        private float bobTimer = 0f;
        private Vector3 initialBodyLocalPos = new Vector3(0, 0.18f, 0);

        public IsometricControlScheme ControlScheme { get => controlScheme; set => controlScheme = value; }
        public Vector2 MoveInput => rawInput;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
        public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (visualTransform == null && transform.childCount > 0)
            {
                visualTransform = transform.Find("Visual");
                if (visualTransform == null) visualTransform = transform.GetChild(0);
            }

            if (visualTransform != null)
            {
                if (cylinderBody == null) cylinderBody = visualTransform.Find("Cylinder_Body");
                if (shadowTransform == null) shadowTransform = visualTransform.Find("Player_Shadow");
                if (cylinderBody != null) initialBodyLocalPos = cylinderBody.localPosition;
                else initialBodyLocalPos = visualTransform.localPosition;
            }

            spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
            meshRenderers = GetComponentsInChildren<MeshRenderer>();

            // Ensure player mesh is never magenta/pink by assigning a clean stylized shader
            if (meshRenderers != null && meshRenderers.Length > 0)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Sprites/Default")
                             ?? Shader.Find("Unlit/Color");

                if (shader != null)
                {
                    Material playerMat = new Material(shader);
                    playerMat.name = "Mat_Player_Cyan";
                    playerMat.color = new Color(0.2f, 0.85f, 0.95f);

                    Material shadowMat = new Material(shader);
                    shadowMat.name = "Mat_Player_Shadow";
                    shadowMat.color = new Color(0f, 0f, 0f, 0.45f);

                    foreach (var mr in meshRenderers)
                    {
                        if (mr != null)
                        {
                            if (mr.gameObject.name == "Player_Shadow")
                                mr.sharedMaterial = shadowMat;
                            else
                                mr.sharedMaterial = playerMat;
                        }
                    }
                }
            }
        }

        private void Start()
        {
            var worldMap = FindAnyObjectByType<IsometricWorldMap>();
            if (worldMap != null)
            {
                Vector2 center = worldMap.GetRoomCenterWorld();
                transform.position = new Vector3(center.x, center.y, 0);
                if (rb != null) rb.position = center;
            }
        }

        private void Update()
        {
            ReadInput();
            UpdateVisualPawn();
            UpdateDepthSorting();
        }

        private void FixedUpdate()
        {
            ApplyMovement();
        }

        private void ReadInput()
        {
            Vector2 input = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
            }
#endif
#if !ENABLE_INPUT_SYSTEM
            try
            {
                if (input.sqrMagnitude < 0.001f)
                {
                    input.x = Input.GetAxisRaw("Horizontal");
                    input.y = Input.GetAxisRaw("Vertical");
                }
            }
            catch { }
#endif

            rawInput = input.normalized;

            if (rawInput.sqrMagnitude > 0.01f)
            {
                if (controlScheme == IsometricControlScheme.ScreenRelative)
                {
                    // Screen-relative: W=Straight Up, S=Straight Down, D=Straight Right, A=Straight Left
                    isoMoveDir = rawInput;
                    targetYaw = Mathf.Atan2(rawInput.x, rawInput.y) * Mathf.Rad2Deg;
                }
                else
                {
                    // Isometric Grid-relative: W=Grid +Y (Up-Right), S=Grid -Y (Down-Left), D=Grid +X (Down-Right), A=Grid -X (Up-Left)
                    float stepX = IsometricCoordinates.DefaultTileWidth * 0.5f;
                    float stepY = IsometricCoordinates.DefaultTileHeight * 0.5f;

                    Vector2 gridDirX = new Vector2(stepX, -stepY).normalized;
                    Vector2 gridDirY = new Vector2(stepX, stepY).normalized;

                    isoMoveDir = (rawInput.x * gridDirX + rawInput.y * gridDirY).normalized;
                    targetYaw = Mathf.Atan2(rawInput.x, rawInput.y) * Mathf.Rad2Deg + 45f;
                }
            }
            else
            {
                isoMoveDir = Vector2.zero;
            }
        }

        private void ApplyMovement()
        {
            Vector2 targetVelocity = isoMoveDir * moveSpeed;
            float rate = isoMoveDir.sqrMagnitude > 0.001f ? acceleration : deceleration;

            currentVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
            rb.linearVelocity = currentVelocity;
        }

        private void UpdateVisualPawn()
        {
            if (visualTransform == null) return;

            // 1. Smooth 3D Yaw rotation (keeping 30° isometric pitch, rotating on Y-axis)
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * turnSpeed);
            visualTransform.localRotation = Quaternion.Euler(pitchAngle, currentYaw, 0f);

            // 2. 3D Locomotion Bouncing & Leaning
            if (cylinderBody != null && cylinderBody != visualTransform)
            {
                if (IsMoving)
                {
                    bobTimer += Time.deltaTime * bobFrequency;
                    float bobOffset = Mathf.Sin(bobTimer) * bobHeight;
                    cylinderBody.localPosition = initialBodyLocalPos + new Vector3(0, Mathf.Abs(bobOffset), 0);
                    cylinderBody.localRotation = Quaternion.Euler(Mathf.Sin(bobTimer * 0.5f) * leanAmount, 0f, 0f);
                }
                else
                {
                    bobTimer = 0f;
                    cylinderBody.localPosition = Vector3.Lerp(cylinderBody.localPosition, initialBodyLocalPos, Time.deltaTime * 10f);
                    cylinderBody.localRotation = Quaternion.Lerp(cylinderBody.localRotation, Quaternion.identity, Time.deltaTime * 10f);
                }
            }
            else
            {
                float bobOffset = IsMoving ? Mathf.Abs(Mathf.Sin(Time.time * bobFrequency)) * bobHeight : 0f;
                visualTransform.localPosition = initialBodyLocalPos + new Vector3(0, bobOffset, 0);
            }
        }

        private void UpdateDepthSorting()
        {
            Vector2Int gridPos = IsometricCoordinates.WorldToGrid(transform.position);
            int sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridPos.x, gridPos.y, 0, 40);

            if (spriteRenderers != null)
            {
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    if (spriteRenderers[i] != null) spriteRenderers[i].sortingOrder = sortingOrder;
                }
            }

            if (meshRenderers != null)
            {
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    if (meshRenderers[i] != null) meshRenderers[i].sortingOrder = sortingOrder;
                }
            }
        }
    }
}
