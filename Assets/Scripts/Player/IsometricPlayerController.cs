using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using IsometricGame.Tilemap;

namespace IsometricGame.Player
{
    public enum CharacterFacing
    {
        South = 0,
        SouthEast = 1,
        East = 2,
        NorthEast = 3,
        North = 4,
        NorthWest = 5,
        West = 6,
        SouthWest = 7
    }

    public enum IsometricControlScheme
    {
        ScreenRelative = 0,       // W=Straight Up, S=Straight Down, D=Straight Right, A=Straight Left
        IsometricDirectional = 1, // W=Normal Up (North), S=Normal Down (South), D=Down-Right (SouthEast), A=Up-Left (NorthWest)
        IsometricGrid = 2         // Classic 4-way diamond grid mapping
    }

    /// <summary>
    /// Smooth 2D Isometric Character Controller with 8-directional idle and 6-directional walk animations.
    /// Features:
    /// - Normal Up/Down vertical movement with directional sideways isometric stepping.
    /// - Cozy, perfectly paced walk speed with smooth sub-pixel acceleration and deceleration.
    /// - Dynamic stride-synced walk cycle playback (no foot-sliding).
    /// - Contact drop shadow and dynamic isometric depth sorting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class IsometricPlayerController : MonoBehaviour
    {
        [Header("Movement Dynamics")]
        [Tooltip("IsometricDirectional provides normal straight Up/Down with directional sideways movement.")]
        [SerializeField] private IsometricControlScheme controlScheme = IsometricControlScheme.IsometricDirectional;
        [Tooltip("Smooth walking speed in units per second.")]
        [SerializeField] private float walkSpeed = 1.15f;
        [Tooltip("Optional sprint speed when holding Left Shift.")]
        [SerializeField] private float runSpeed = 1.80f;
        [Tooltip("Acceleration responsiveness (higher = snappier).")]
        [SerializeField] private float acceleration = 24f;
        [Tooltip("Deceleration braking force (higher = crisper stop).")]
        [SerializeField] private float deceleration = 28f;

        [Header("Grid Alignment & Lane Locking")]
        [Tooltip("When enabled, gently locks the player to the isometric tile lane.")]
        [SerializeField] private bool snapToGridLanes = false;
        [SerializeField] private float laneSnapStrength = 3.5f;

        [Header("Animation Playback")]
        [Tooltip("Base walk animation frames per second.")]
        [SerializeField] private float walkFps = 8f;
        [Tooltip("Scales animation playback speed with movement velocity to prevent foot sliding.")]
        [SerializeField] private bool scaleAnimWithVelocity = true;

        [Header("8-Directional Idle Sprites")]
        public Sprite idleSouth;
        public Sprite idleSouthEast;
        public Sprite idleEast;
        public Sprite idleNorthEast;
        public Sprite idleNorth;
        public Sprite idleNorthWest;
        public Sprite idleWest;
        public Sprite idleSouthWest;

        [Header("6-Directional Walk Cycles (8 Frames Each)")]
        public Sprite[] walkSouth = new Sprite[8];
        public Sprite[] walkNorth = new Sprite[8];
        public Sprite[] walkSouthEast = new Sprite[8];
        public Sprite[] walkSouthWest = new Sprite[8];
        public Sprite[] walkNorthEast = new Sprite[8];
        public Sprite[] walkNorthWest = new Sprite[8];

        [Header("Visual & Shadow")]
        [SerializeField] private Vector2 characterScale = new Vector2(0.42f, 0.42f);
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private SpriteRenderer shadowRenderer;
        [SerializeField] private Vector2 shadowScale = new Vector2(0.35f, 0.14f);
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.40f);

        [Header("Footstep Particles & Trail")]
        [SerializeField] private bool enableFootstepParticles = true;
        [SerializeField] private Color dustColor = new Color(0.92f, 0.88f, 0.80f, 0.85f);
        [SerializeField] private float dustRate = 14.0f;
        [SerializeField] private Vector2 dustEmitterOffset = new Vector2(0f, -0.025f);

        [Header("Energy & Stamina")]
        [Tooltip("Energy consumed per second while moving slowly over time.")]
        [SerializeField] private float walkEnergyDrainRate = 1.0f;
        [Tooltip("Speed multiplier when energy is completely depleted (0.5 = 50% slower).")]
        [SerializeField] private float exhaustedSpeedMultiplier = 0.5f;

        [Header("Bush Slowdown Penalty")]
        [Tooltip("Speed multiplier when walking or running through bushes (0.5 = 50% slower).")]
        [SerializeField] private float bushSlowMultiplier = 0.5f;

        private int overlappingBushesCount = 0;

        private Rigidbody2D rb;
        private CircleCollider2D col;
        private ParticleSystem footstepParticles;
        private Vector2 currentVelocity;
        private Vector2 rawInput;
        private Vector2 moveDir;
        private CharacterFacing currentFacing = CharacterFacing.South;
        private CharacterFacing lastHorizontalFacing = CharacterFacing.SouthEast;
        private float animTimer = 0f;
        private static Sprite cachedShadowSprite;
        private static Texture2D cachedDustTexture;

        public static IsometricPlayerController Instance { get; private set; }
        private bool inputEnabled = true;

        public IsometricControlScheme ControlScheme { get => controlScheme; set => controlScheme = value; }
        public Vector2 MoveInput => rawInput;
        public Vector2 Velocity => rb != null ? rb.linearVelocity : currentVelocity;
        public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;
        public CharacterFacing Facing => currentFacing;
        public bool InputEnabled => inputEnabled;
        public SpriteRenderer CharacterRenderer => characterRenderer;
        public float WalkEnergyDrainRate { get => walkEnergyDrainRate; set => walkEnergyDrainRate = value; }
        public float ExhaustedSpeedMultiplier { get => exhaustedSpeedMultiplier; set => exhaustedSpeedMultiplier = value; }
        public float BushSlowMultiplier { get => bushSlowMultiplier; set => bushSlowMultiplier = value; }
        public bool IsInBush => overlappingBushesCount > 0;
        public bool IsExhausted => IsometricGame.UI.EnergyBarUI.Instance != null && IsometricGame.UI.EnergyBarUI.Instance.CurrentEnergy <= 0.001f;

        public void EnterBush()
        {
            overlappingBushesCount++;
        }

        public void ExitBush()
        {
            overlappingBushesCount = Mathf.Max(0, overlappingBushesCount - 1);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                rawInput = Vector2.zero;
                moveDir = Vector2.zero;
                currentVelocity = Vector2.zero;
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        public void TeleportTo(Vector2 targetPos)
        {
            transform.position = new Vector3(targetPos.x, targetPos.y, 0f);
            if (rb != null)
            {
                rb.position = targetPos;
                rb.linearVelocity = Vector2.zero;
            }
            currentVelocity = Vector2.zero;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            SetupPhysics();
            EnsureSpritesLoaded();
            SetupVisualHierarchy();
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSpritesLoaded();
            if (!Application.isPlaying)
            {
                SetupVisualHierarchy();
            }
        }

        [ContextMenu("Auto-Load Character Sprites")]
        public void AutoLoadSprites()
        {
            EnsureSpritesLoaded(true);
        }
#endif

        private void SetupPhysics()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                col.radius = 0.075f;
                col.offset = new Vector2(0f, 0.0225f);
            }
        }

        private void SetupVisualHierarchy()
        {
            // Clean up old 3D cylinder primitives if present
            Transform oldVisual = transform.Find("Visual");
            if (oldVisual != null)
            {
                if (Application.isPlaying) Destroy(oldVisual.gameObject);
                else DestroyImmediate(oldVisual.gameObject);
            }

            // 1. Character Sprite Visual
            Transform charTrans = transform.Find("Character_Visual");
            GameObject charObj = charTrans != null ? charTrans.gameObject : new GameObject("Character_Visual");
            charObj.transform.SetParent(transform, false);
            charObj.transform.localPosition = Vector3.zero;
            charObj.transform.localScale = new Vector3(characterScale.x, characterScale.y, 1f);

            characterRenderer = charObj.GetComponent<SpriteRenderer>();
            if (characterRenderer == null) characterRenderer = charObj.AddComponent<SpriteRenderer>();

            Material spriteMat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
            spriteMat.name = "Mat_Character_Sprite";
            characterRenderer.material = spriteMat;

            // 2. Ground Contact Shadow
            Transform shadowTrans = transform.Find("Player_Ground_Shadow");
            GameObject shadowObj = shadowTrans != null ? shadowTrans.gameObject : new GameObject("Player_Ground_Shadow");
            shadowObj.transform.SetParent(transform, false);
            shadowObj.transform.localPosition = new Vector3(0f, -0.015f, 0f);
            shadowObj.transform.localScale = new Vector3(shadowScale.x, shadowScale.y, 1f);

            shadowRenderer = shadowObj.GetComponent<SpriteRenderer>();
            if (shadowRenderer == null) shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();

            shadowRenderer.sprite = GetOrCreateShadowSprite();
            shadowRenderer.color = shadowColor;
            shadowRenderer.material = spriteMat;

            // 3. Footstep Dust Particles
            SetupFootstepParticles();

            UpdateAnimationVisual(0f);
        }

        private void SetupFootstepParticles()
        {
            if (!enableFootstepParticles)
            {
                Transform existing = transform.Find("Player_Footstep_Dust");
                if (existing != null)
                {
                    if (Application.isPlaying) Destroy(existing.gameObject);
                    else DestroyImmediate(existing.gameObject);
                }
                return;
            }

            Transform existingTrans = transform.Find("Player_Footstep_Dust");
            GameObject dustObj = existingTrans != null ? existingTrans.gameObject : new GameObject("Player_Footstep_Dust");
            dustObj.transform.SetParent(transform, false);
            dustObj.transform.localPosition = new Vector3(dustEmitterOffset.x, dustEmitterOffset.y, 0f);
            dustObj.transform.localScale = Vector3.one;

            footstepParticles = dustObj.GetComponent<ParticleSystem>();
            if (footstepParticles == null) footstepParticles = dustObj.AddComponent<ParticleSystem>();

            var main = footstepParticles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.11f); // 2-4 pixels in isometric space
            main.startColor = dustColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 32;

            var emission = footstepParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = footstepParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.045f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var col = footstepParticles.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(dustColor, 0.0f),
                    new GradientColorKey(Color.white, 0.4f),
                    new GradientColorKey(new Color(0.80f, 0.78f, 0.72f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(dustColor.a, 0.25f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeOverLifetime = footstepParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0.0f, 0.4f);
            sizeCurve.AddKey(0.25f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var psr = dustObj.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = shadowRenderer != null ? shadowRenderer.sortingOrder + 1 : 15;
                Shader spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                                   ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                                   ?? Shader.Find("Sprites/Default");
                if (spriteShader != null)
                {
                    Material pMat = new Material(spriteShader);
                    pMat.name = "Mat_Footstep_Dust";
                    Texture2D dustTex = GetOrCreateDustTexture();
                    pMat.mainTexture = dustTex;
                    if (pMat.HasProperty("_BaseMap")) pMat.SetTexture("_BaseMap", dustTex);
                    psr.sharedMaterial = pMat;
                }
            }
        }

        private static Texture2D GetOrCreateDustTexture()
        {
            if (cachedDustTexture != null) return cachedDustTexture;

            int size = 8;
            cachedDustTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            cachedDustTexture.filterMode = FilterMode.Point;
            cachedDustTexture.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(3.5f, 3.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = dist <= 3.2f ? 1.0f : 0.0f;
                    cachedDustTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            cachedDustTexture.Apply();
            return cachedDustTexture;
        }

        private static Sprite GetOrCreateShadowSprite()
        {
            if (cachedShadowSprite != null) return cachedShadowSprite;

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
            cachedShadowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
            return cachedShadowSprite;
        }

        private void EnsureSpritesLoaded(bool force = false)
        {
#if UNITY_EDITOR
            bool needsLoad = force || idleSouth == null ||
                             walkNorth == null || walkNorth.Length == 0 || walkNorth[0] == null ||
                             walkSouth == null || walkSouth.Length == 0 || walkSouth[0] == null ||
                             walkSouthEast == null || walkSouthEast.Length == 0 || walkSouthEast[0] == null;

            if (needsLoad)
            {
                if (walkSouth == null || walkSouth.Length != 8) walkSouth = new Sprite[8];
                if (walkNorth == null || walkNorth.Length != 8) walkNorth = new Sprite[8];
                if (walkSouthEast == null || walkSouthEast.Length != 8) walkSouthEast = new Sprite[8];
                if (walkSouthWest == null || walkSouthWest.Length != 8) walkSouthWest = new Sprite[8];
                if (walkNorthEast == null || walkNorthEast.Length != 8) walkNorthEast = new Sprite[8];
                if (walkNorthWest == null || walkNorthWest.Length != 8) walkNorthWest = new Sprite[8];

                idleSouth = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/south.png");
                idleSouthEast = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/south-east.png");
                idleEast = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/east.png");
                idleNorthEast = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/north-east.png");
                idleNorth = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/north.png");
                idleNorthWest = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/north-west.png");
                idleWest = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/west.png");
                idleSouthWest = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Character/idle/rotations/south-west.png");

                for (int i = 0; i < 8; i++)
                {
                    walkSouth[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/south/frame_00{i}.png");
                    walkNorth[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/north/frame_00{i}.png");
                    walkSouthEast[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/south-east/frame_00{i}.png");
                    walkSouthWest[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/south-west/frame_00{i}.png");
                    walkNorthEast[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/north-east/frame_00{i}.png");
                    walkNorthWest[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Character/walking/north-west/frame_00{i}.png");
                }
            }
#endif
        }

        private void Update()
        {
            ReadInput();
            UpdateEnergyConsumption();
            UpdateAnimation();
            UpdateDepthSorting();
            UpdateFootstepParticles();
        }

        private void UpdateEnergyConsumption()
        {
            if (Application.isPlaying && IsMoving && rawInput.sqrMagnitude > 0.01f)
            {
                var energyUI = IsometricGame.UI.EnergyBarUI.Instance;
                if (energyUI != null && energyUI.CurrentEnergy > 0f)
                {
                    energyUI.UseEnergy(walkEnergyDrainRate * Time.deltaTime);
                }
            }
        }

        private void UpdateFootstepParticles()
        {
            if (footstepParticles == null) return;

            var emission = footstepParticles.emission;
            if (IsMoving)
            {
                if (!footstepParticles.isPlaying) footstepParticles.Play();
                emission.rateOverTime = dustRate * Mathf.Clamp01(currentVelocity.magnitude / walkSpeed);
            }
            else
            {
                emission.rateOverTime = 0f;
            }
        }

        private void FixedUpdate()
        {
            ApplyMovement();
        }

        private void ReadInput()
        {
            if (!inputEnabled)
            {
                rawInput = Vector2.zero;
                moveDir = Vector2.zero;
                return;
            }

            Vector2 input = Vector2.zero;
            bool sprint = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
                sprint = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            }
#endif
#if !ENABLE_INPUT_SYSTEM
            try
            {
                if (input.sqrMagnitude < 0.001f)
                {
                    input.x = Input.GetAxisRaw("Horizontal");
                    input.y = Input.GetAxisRaw("Vertical");
                    sprint = Input.GetKey(KeyCode.LeftShift);
                }
            }
            catch { }
#endif

            rawInput = input.sqrMagnitude > 1f ? input.normalized : input;

            if (rawInput.sqrMagnitude > 0.01f)
            {
                if (controlScheme == IsometricControlScheme.ScreenRelative)
                {
                    // Screen-relative: W=Straight Up, S=Straight Down, D=Straight Right, A=Straight Left
                    moveDir = rawInput.normalized;
                }
                else if (controlScheme == IsometricControlScheme.IsometricDirectional)
                {
                    // Normal Up/Down vertical + directional sideways isometric:
                    // W/S = Straight Up/Down on screen (0, y)
                    // D/A = Sideways along isometric diamond axis (2, -1) / (-2, 1)
                    Vector2 vert = new Vector2(0f, rawInput.y);
                    Vector2 horiz = rawInput.x * new Vector2(2f, -1f).normalized;
                    Vector2 combined = vert + horiz;
                    moveDir = combined.sqrMagnitude > 0.001f ? combined.normalized : Vector2.zero;
                }
                else
                {
                    // Isometric Grid-relative (classic diamond axes):
                    Vector2 gridDirX = new Vector2(2f, 1f).normalized;  // Up-Right (+X)
                    Vector2 gridDirY = new Vector2(-2f, 1f).normalized; // Up-Left (+Y)
                    Vector2 combined = rawInput.y * gridDirX + rawInput.x * (-gridDirY);
                    moveDir = combined.sqrMagnitude > 0.001f ? combined.normalized : Vector2.zero;
                }

                // Explicit override: pressing W (or Up) always triggers North anim, pressing S (or Down) always triggers South anim
                if (Mathf.Abs(rawInput.y) > 0.1f && Mathf.Abs(rawInput.x) < 0.35f)
                {
                    currentFacing = rawInput.y > 0 ? CharacterFacing.North : CharacterFacing.South;
                }
                else if (Mathf.Abs(rawInput.x) > 0.1f && Mathf.Abs(rawInput.y) < 0.35f)
                {
                    currentFacing = rawInput.x > 0 ? CharacterFacing.SouthEast : CharacterFacing.SouthWest;
                }
                else if (rawInput.y > 0 && rawInput.x > 0)
                {
                    currentFacing = CharacterFacing.NorthEast;
                }
                else if (rawInput.y > 0 && rawInput.x < 0)
                {
                    currentFacing = CharacterFacing.NorthWest;
                }
                else if (rawInput.y < 0 && rawInput.x > 0)
                {
                    currentFacing = CharacterFacing.SouthEast;
                }
                else if (rawInput.y < 0 && rawInput.x < 0)
                {
                    currentFacing = CharacterFacing.SouthWest;
                }
                else
                {
                    currentFacing = ResolveFacingDirection(moveDir);
                }

                if (currentFacing == CharacterFacing.East || currentFacing == CharacterFacing.SouthEast || currentFacing == CharacterFacing.NorthEast)
                {
                    lastHorizontalFacing = CharacterFacing.SouthEast;
                }
                else if (currentFacing == CharacterFacing.West || currentFacing == CharacterFacing.SouthWest || currentFacing == CharacterFacing.NorthWest)
                {
                    lastHorizontalFacing = CharacterFacing.SouthWest;
                }
            }
            else
            {
                moveDir = Vector2.zero;
            }
        }

        private CharacterFacing ResolveFacingDirection(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // -180 to 180

            if (angle >= -22.5f && angle < 22.5f) return CharacterFacing.East;
            if (angle >= 22.5f && angle < 67.5f) return CharacterFacing.NorthEast;
            if (angle >= 67.5f && angle < 112.5f) return CharacterFacing.North;
            if (angle >= 112.5f && angle < 157.5f) return CharacterFacing.NorthWest;
            if (angle >= 157.5f || angle < -157.5f) return CharacterFacing.West;
            if (angle >= -157.5f && angle < -112.5f) return CharacterFacing.SouthWest;
            if (angle >= -112.5f && angle < -67.5f) return CharacterFacing.South;
            return CharacterFacing.SouthEast;
        }

        private void ApplyMovement()
        {
            bool isSprinting = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) isSprinting = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
#endif
            bool isExhausted = IsExhausted;
            if (isExhausted)
            {
                isSprinting = false; // Cannot sprint when depleted
            }

            float targetSpeed = isSprinting ? runSpeed : walkSpeed;
            if (isExhausted)
            {
                targetSpeed *= exhaustedSpeedMultiplier; // 50% slower walk
            }
            if (overlappingBushesCount > 0)
            {
                targetSpeed *= bushSlowMultiplier; // 50% slower when walking/running through bushes
            }

            Vector2 targetVelocity = moveDir * targetSpeed;

            if (snapToGridLanes && controlScheme == IsometricControlScheme.IsometricGrid && rawInput.sqrMagnitude > 0.01f)
            {
                // If moving predominantly along one isometric axis (single-key walk)
                bool isXAxis = Mathf.Abs(rawInput.y) > 0.5f && Mathf.Abs(rawInput.x) < 0.3f; // W or S
                bool isYAxis = Mathf.Abs(rawInput.x) > 0.5f && Mathf.Abs(rawInput.y) < 0.3f; // A or D

                if (isXAxis || isYAxis)
                {
                    Vector2 currentPos = rb.position;
                    // Grid coordinates: gx = x + 2y, gy = 2y - x
                    float gx = currentPos.x + 2f * currentPos.y;
                    float gy = 2f * currentPos.y - currentPos.x;

                    Vector2 gridDirX = new Vector2(2f, 1f).normalized;
                    Vector2 gridDirY = new Vector2(-2f, 1f).normalized;

                    if (isXAxis)
                    {
                        // Moving along gx axis (W/S), lock gy to nearest tile lane
                        float targetGy = Mathf.Round(gy);
                        float errorGy = targetGy - gy;
                        targetVelocity += gridDirY * Mathf.Clamp(errorGy * laneSnapStrength, -targetSpeed * 0.4f, targetSpeed * 0.4f);
                    }
                    else if (isYAxis)
                    {
                        // Moving along gy axis (A/D), lock gx to nearest tile lane
                        float targetGx = Mathf.Round(gx);
                        float errorGx = targetGx - gx;
                        targetVelocity += gridDirX * Mathf.Clamp(errorGx * laneSnapStrength, -targetSpeed * 0.4f, targetSpeed * 0.4f);
                    }
                }
            }

            float rate = moveDir.sqrMagnitude > 0.001f ? acceleration : deceleration;
            currentVelocity = Vector2.MoveTowards(rb.linearVelocity, targetVelocity, rate * Time.fixedDeltaTime);
            rb.linearVelocity = currentVelocity;
        }

        private void UpdateAnimation()
        {
            if (characterRenderer == null) return;

            if (IsMoving)
            {
                float speed = currentVelocity.magnitude;
                float currentFps = scaleAnimWithVelocity ? (walkFps * (speed / walkSpeed)) : walkFps;
                animTimer += Time.deltaTime * Mathf.Max(currentFps, 2f);
                UpdateAnimationVisual(animTimer);
            }
            else
            {
                animTimer = 0f;
                characterRenderer.sprite = GetIdleSpriteForFacing(currentFacing);
            }
        }

        private void UpdateAnimationVisual(float timer)
        {
            if (characterRenderer == null) return;

            if (IsMoving)
            {
                Sprite[] activeCycle = GetWalkCycleForFacing(currentFacing);
                if (activeCycle != null && activeCycle.Length > 0 && activeCycle[0] != null)
                {
                    int frameIndex = Mathf.FloorToInt(timer) % activeCycle.Length;
                    characterRenderer.sprite = activeCycle[frameIndex];
                }
            }
            else
            {
                characterRenderer.sprite = GetIdleSpriteForFacing(currentFacing);
            }
        }

        private Sprite GetIdleSpriteForFacing(CharacterFacing facing)
        {
            switch (facing)
            {
                case CharacterFacing.South: return idleSouth;
                case CharacterFacing.SouthEast: return idleSouthEast;
                case CharacterFacing.East: return idleEast;
                case CharacterFacing.NorthEast: return idleNorthEast;
                case CharacterFacing.North: return idleNorth;
                case CharacterFacing.NorthWest: return idleNorthWest;
                case CharacterFacing.West: return idleWest;
                case CharacterFacing.SouthWest: return idleSouthWest;
                default: return idleSouth;
            }
        }

        private Sprite[] GetWalkCycleForFacing(CharacterFacing facing)
        {
            switch (facing)
            {
                case CharacterFacing.North:
                    if (walkNorth != null && walkNorth.Length > 0 && walkNorth[0] != null) return walkNorth;
                    return lastHorizontalFacing == CharacterFacing.SouthWest ? walkNorthWest : walkNorthEast;
                case CharacterFacing.South:
                    if (walkSouth != null && walkSouth.Length > 0 && walkSouth[0] != null) return walkSouth;
                    return lastHorizontalFacing == CharacterFacing.SouthWest ? walkSouthWest : walkSouthEast;
                case CharacterFacing.NorthEast:
                    return walkNorthEast;
                case CharacterFacing.NorthWest:
                    return walkNorthWest;
                case CharacterFacing.SouthEast:
                    return walkSouthEast;
                case CharacterFacing.SouthWest:
                    return walkSouthWest;
                case CharacterFacing.East:
                    return walkSouthEast;
                case CharacterFacing.West:
                    return walkSouthWest;
                default:
                    return walkSouth;
            }
        }

        private void UpdateDepthSorting()
        {
            Vector2Int gridPos = IsometricCoordinates.WorldToGrid(transform.position);
            int sortingOrder = IsometricCoordinates.CalculateSortingOrder(gridPos.x, gridPos.y, 0, 40);

            if (characterRenderer != null)
            {
                characterRenderer.sortingOrder = sortingOrder;
            }
            if (shadowRenderer != null)
            {
                shadowRenderer.sortingOrder = sortingOrder - 1;
            }
            if (footstepParticles != null)
            {
                var psr = footstepParticles.GetComponent<ParticleSystemRenderer>();
                if (psr != null) psr.sortingOrder = sortingOrder - 1;
            }
        }
    }
}
