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
        [Header("Target Follow")]
        [Tooltip("The player transform to follow.")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0, 0, -10f);
        [SerializeField] private float followSpeed = 6.0f;
        [SerializeField] private float lookAheadFactor = 0.5f;

        [Header("Zoom Settings")]
        [SerializeField] private float initialZoom = 2.5f;
        [SerializeField] private float minZoom = 1.2f;
        [SerializeField] private float maxZoom = 5.0f;
        [SerializeField] private float zoomSensitivity = 0.5f;
        [SerializeField] private float zoomDampening = 10f;

        private Camera cam;
        private float targetZoom;
        private Vector3 currentVelocity;

        public Transform Target { get => target; set => target = value; }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = initialZoom;
            targetZoom = initialZoom;
        }

        private void Start()
        {
            if (target == null)
            {
                var player = FindAnyObjectByType<IsometricGame.Player.IsometricPlayerController>();
                if (player != null) target = player.transform;
            }

            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

        private void Update()
        {
            HandleZoom();
        }

        private void LateUpdate()
        {
            FollowTarget();
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

        private void FollowTarget()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;

            // Optional velocity lead-ahead
            if (target.TryGetComponent<Rigidbody2D>(out var rb))
            {
                targetPos += (Vector3)(rb.linearVelocity * lookAheadFactor);
            }

            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, 1f / followSpeed);
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
