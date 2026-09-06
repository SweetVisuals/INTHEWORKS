using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IsometricGame.CameraControl
{
    /// <summary>
    /// Smooth 2D Isometric Follow Camera.
    /// Tracks the cylinder player in real-time, provides scroll-wheel zooming, and smooth damping.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricFollowCamera : MonoBehaviour
    {
        [Header("Room Framing / Follow Mode")]
        [Tooltip("If true, actively tracks the player (outdoor mode). If false (indoor room mode), camera remains static locked to the room framing.")]
        [SerializeField] private bool followPlayer = false;
        [Tooltip("Camera offset relative to the room center when followPlayer is false.")]
        [SerializeField] private Vector3 roomCenterOffset = new Vector3(0f, 0.45f, -10f);

        [Header("Target Follow")]
        [Tooltip("The player transform to follow (used when followPlayer is true).")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 0.75f, -10f);
        [SerializeField] private float followSpeed = 6.0f;
        [SerializeField] private float lookAheadFactor = 0.5f;

        [Header("Zoom Settings")]
        [SerializeField] private float initialZoom = 2.2f;
        [SerializeField] private float minZoom = 1.0f;
        [SerializeField] private float maxZoom = 4.5f;
        [SerializeField] private float zoomSensitivity = 0.5f;
        [SerializeField] private float zoomDampening = 10f;

        [Header("Void Background")]
        [SerializeField] private Color voidBackgroundColor = new Color(0.045f, 0.055f, 0.09f, 1f); // Deep Void Dark Blue (#0B0E17)

        private Camera cam;
        private float targetZoom;
        private Vector3 currentVelocity;

        public Transform Target { get => target; set => target = value; }
        public bool FollowPlayer { get => followPlayer; set => SetFollowPlayer(value); }
        public Vector3 RoomCenterOffset { get => roomCenterOffset; set => roomCenterOffset = value; }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = voidBackgroundColor;
            cam.orthographicSize = initialZoom;
            targetZoom = initialZoom;

            if (GetComponent<UnityEngine.EventSystems.Physics2DRaycaster>() == null)
            {
                gameObject.AddComponent<UnityEngine.EventSystems.Physics2DRaycaster>();
            }
        }

        private void Start()
        {
            FindTargetIfNeeded();

            if (!followPlayer)
            {
                PositionOnRoomCenter();
            }
            else if (target != null)
            {
                SnapToTarget();
            }
        }

        private void FindTargetIfNeeded()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<IsometricGame.Player.IsometricPlayerController>();
                if (player != null)
                {
                    target = player.transform;
                }
                else
                {
                    var playerObj = GameObject.FindGameObjectWithTag("Player");
                    if (playerObj != null) target = playerObj.transform;
                }
            }
        }

        private void Update()
        {
            HandleZoom();
        }

        private void LateUpdate()
        {
            if (followPlayer)
            {
                FollowTarget();
            }
        }

        /// <summary>
        /// Switches between static indoor room framing (false) and dynamic outdoor player following (true).
        /// </summary>
        public void SetFollowPlayer(bool enableFollow, bool snap = true)
        {
            followPlayer = enableFollow;
            currentVelocity = Vector3.zero;

            FindTargetIfNeeded();

            if (!followPlayer)
            {
                PositionOnRoomCenter();
            }
            else if (snap)
            {
                SnapToTarget();
            }
        }

        public void SetIndoorStaticMode()
        {
            SetFollowPlayer(false);
        }

        public void SetOutdoorFollowMode(Vector2? outdoorPos = null)
        {
            followPlayer = true;
            currentVelocity = Vector3.zero;
            FindTargetIfNeeded();
            SnapToTarget(outdoorPos);
        }

        public void SnapToTarget(Vector2? customWorldPos = null)
        {
            FindTargetIfNeeded();
            Vector3 pos = customWorldPos.HasValue ? (Vector3)customWorldPos.Value : (target != null ? target.position : transform.position);
            transform.position = new Vector3(pos.x + offset.x, pos.y + offset.y, offset.z);
            currentVelocity = Vector3.zero;
        }

        public void PositionOnRoomCenter()
        {
            var worldMap = FindAnyObjectByType<IsometricGame.Tilemap.IsometricWorldMap>();
            if (worldMap != null)
            {
                Vector2 roomCenter = worldMap.GetRoomCenterWorld();
                transform.position = new Vector3(roomCenter.x + roomCenterOffset.x, roomCenter.y + roomCenterOffset.y, roomCenterOffset.z);
            }
            else
            {
                transform.position = roomCenterOffset;
            }
            currentVelocity = Vector3.zero;
        }

        private void HandleZoom()
        {
            float scroll = GetScrollDelta();
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetZoom -= scroll * zoomSensitivity;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomDampening);
        }

        [Header("Smoothing")]
        [Tooltip("Smooth time in seconds for camera damping (smaller = snappier, larger = smoother).")]
        [SerializeField] private float smoothTime = 0.16f;
        [SerializeField] private bool enableVelocityLead = false;

        private Vector2 smoothedLeadVelocity;

        private void FollowTarget()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;

            if (enableVelocityLead && target.TryGetComponent<Rigidbody2D>(out var rb))
            {
                smoothedLeadVelocity = Vector2.Lerp(smoothedLeadVelocity, rb.linearVelocity, Time.deltaTime * 4f);
                targetPos += (Vector3)(smoothedLeadVelocity * lookAheadFactor);
            }

            // Smooth damping for perfectly fluid camera tracking without discrete pixel stair-stepping jumps
            float dampTime = smoothTime > 0.001f ? smoothTime : (1f / Mathf.Max(1f, followSpeed));
            Vector3 smoothed = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, dampTime, Mathf.Infinity, Time.deltaTime);
            smoothed.z = offset.z;
            transform.position = smoothed;
        }

        private float GetScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                float val = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(val) > 0.01f) return Mathf.Sign(val);
            }
            return 0f;
#else
            try { return Input.mouseScrollDelta.y; } catch { return 0f; }
#endif
        }

        public void SetTarget(Transform newTarget, float zoom = 7.0f)
        {
            target = newTarget;
            initialZoom = zoom;
            targetZoom = zoom;
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }
    }
}
