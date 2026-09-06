using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IsometricGame.Core
{
    /// <summary>
    /// Smooth Isometric / Orthographic Camera Controller.
    /// Features:
    /// - Mouse Scroll Wheel to Zoom In / Out.
    /// - Click and Hold (Left Click or Right Click) to smoothly rotate 360° around the room.
    /// - Q and E keys to rotate via keyboard.
    /// - WASD / Middle Mouse to pan.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraController : MonoBehaviour
    {
        [Header("Room Target")]
        [Tooltip("Center point of the room to orbit around.")]
        public Vector3 focalPoint = new Vector3(5f, 1.5f, 5f);
        public float distanceToPivot = 25f;

        [Header("Zoom Settings")]
        public float initialZoom = 8.5f;
        public float minZoom = 3.0f;
        public float maxZoom = 18.0f;
        public float zoomSensitivity = 1.5f;
        public float zoomDampening = 12f;

        [Header("Orbit / Rotation Settings")]
        public bool enableRotation = true;
        [Range(0.1f, 2.0f)]
        public float mouseRotationSensitivity = 0.4f;
        public float pitchAngle = 30.0f;
        public float startingYaw = 45.0f;
        public float keyboardRotateSpeed = 90f;
        public float rotationDampening = 12f;

        [Header("Pan Settings (Optional)")]
        public bool enablePanning = true;
        public float panSpeed = 10f;

        private Camera cam;
        private float targetZoom = 8.5f;
        private float currentYaw = 45f;
        private float targetYaw = 45f;
        private Vector3 targetFocalPoint;

        private Vector2 previousMousePos;
        private bool isDraggingRotation;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (!cam.orthographic)
            {
                cam.orthographic = true;
            }

            targetZoom = initialZoom;
            cam.orthographicSize = initialZoom;
            targetFocalPoint = focalPoint;
            currentYaw = startingYaw;
            targetYaw = startingYaw;

            UpdateCameraTransform(true);
        }

        private void Start()
        {
            targetZoom = initialZoom;
            currentYaw = startingYaw;
            targetYaw = startingYaw;
            UpdateCameraTransform(true);
        }

        private void Update()
        {
            HandleZoom();
            HandleRotation();
            HandlePanning();
            UpdateCameraTransform(false);
        }

        private void HandleZoom()
        {
            float scroll = GetScrollDelta();
            if (Mathf.Abs(scroll) > 0.001f)
            {
                targetZoom -= scroll * zoomSensitivity;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }

        private void HandleRotation()
        {
            if (!enableRotation) return;

            // Mouse Drag Rotation: Hold Right Click (button 1) OR Left Click (button 0)
            bool isMouseDown = IsMouseButtonDown(1) || IsMouseButtonDown(0);
            bool isMouseHeld = IsMouseButtonHeld(1) || IsMouseButtonHeld(0);
            bool isMouseUp = IsMouseButtonUp(1) && IsMouseButtonUp(0);

            Vector2 currentMousePos = GetCurrentMousePos();

            if (isMouseDown)
            {
                isDraggingRotation = true;
                previousMousePos = currentMousePos;
            }
            else if (isMouseUp)
            {
                isDraggingRotation = false;
            }

            if (isDraggingRotation && isMouseHeld)
            {
                Vector2 delta = currentMousePos - previousMousePos;
                previousMousePos = currentMousePos;

                if (Mathf.Abs(delta.x) > 0.001f)
                {
                    targetYaw += delta.x * mouseRotationSensitivity;
                }
            }

            // Keyboard Rotation (Q / E keys)
            float keyTurn = 0f;
            if (IsKeyPressed(KeyCode.Q)) keyTurn -= 1f;
            if (IsKeyPressed(KeyCode.E)) keyTurn += 1f;

            if (Mathf.Abs(keyTurn) > 0.01f)
            {
                targetYaw += keyTurn * keyboardRotateSpeed * Time.deltaTime;
            }
        }

        private void HandlePanning()
        {
            if (!enablePanning) return;

            Vector3 panMove = Vector3.zero;
            Vector2 dir = GetMovementVector();

            if (dir.sqrMagnitude > 0.001f)
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
                panMove += (right * dir.x + forward * dir.y) * panSpeed * Time.deltaTime;
            }

            if (panMove.sqrMagnitude > 0f)
            {
                targetFocalPoint += panMove;
                targetFocalPoint.x = Mathf.Clamp(targetFocalPoint.x, -5f, 15f);
                targetFocalPoint.z = Mathf.Clamp(targetFocalPoint.z, -5f, 15f);
            }
        }

        private void UpdateCameraTransform(bool instant)
        {
            if (cam == null) cam = GetComponent<Camera>();

            if (instant)
            {
                cam.orthographicSize = targetZoom;
                currentYaw = targetYaw;
                focalPoint = targetFocalPoint;
            }
            else
            {
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * zoomDampening);
                currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, Time.deltaTime * rotationDampening);
                focalPoint = Vector3.Lerp(focalPoint, targetFocalPoint, Time.deltaTime * zoomDampening);
            }

            Quaternion rot = Quaternion.Euler(pitchAngle, currentYaw, 0f);
            Vector3 pos = focalPoint - (rot * Vector3.forward * distanceToPivot);

            transform.rotation = rot;
            transform.position = pos;
        }

        #region Input Helpers (Supports New & Legacy Input Systems)

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

        private bool IsMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.wasPressedThisFrame;
                if (button == 1) return Mouse.current.rightButton.wasPressedThisFrame;
                if (button == 2) return Mouse.current.middleButton.wasPressedThisFrame;
            }
            return false;
#else
            try { return Input.GetMouseButtonDown(button); } catch { return false; }
#endif
        }

        private bool IsMouseButtonHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.isPressed;
                if (button == 1) return Mouse.current.rightButton.isPressed;
                if (button == 2) return Mouse.current.middleButton.isPressed;
            }
            return false;
#else
            try { return Input.GetMouseButton(button); } catch { return false; }
#endif
        }

        private bool IsMouseButtonUp(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (button == 0) return Mouse.current.leftButton.wasReleasedThisFrame;
                if (button == 1) return Mouse.current.rightButton.wasReleasedThisFrame;
                if (button == 2) return Mouse.current.middleButton.wasReleasedThisFrame;
            }
            return false;
#else
            try { return Input.GetMouseButtonUp(button); } catch { return false; }
#endif
        }

        private Vector2 GetCurrentMousePos()
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            return Vector2.zero;
#else
            try { return Input.mousePosition; } catch { return Vector2.zero; }
#endif
        }

        private bool IsKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (key == KeyCode.Q) return Keyboard.current.qKey.isPressed;
                if (key == KeyCode.E) return Keyboard.current.eKey.isPressed;
            }
            return false;
#else
            try { return Input.GetKey(key); } catch { return false; }
#endif
        }

        private Vector2 GetMovementVector()
        {
            Vector2 move = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
                return move.normalized;
            }
            return Vector2.zero;
#else
            try
            {
                move.x = Input.GetAxisRaw("Horizontal");
                move.y = Input.GetAxisRaw("Vertical");
                return move.normalized;
            }
            catch { return Vector2.zero; }
#endif
        }

        #endregion

        /// <summary>
        /// Sets the focal target and zoom level of the camera.
        /// </summary>
        public void SetTarget(Vector3 target, float zoom = 8.5f)
        {
            focalPoint = target;
            targetFocalPoint = target;
            initialZoom = zoom;
            targetZoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            UpdateCameraTransform(true);
        }
    }
}
