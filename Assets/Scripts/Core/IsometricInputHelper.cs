using UnityEngine;

namespace IsometricGame.Core
{
    /// <summary>
    /// Robust helper for input handling and mouse coordinate unprojection.
    /// Works across both New Input System and Legacy Input Manager seamlessly.
    /// </summary>
    public static class IsometricInputHelper
    {
        public static Vector2 GetMouseScreenPosition()
        {
            Vector2 screenPos = Vector2.zero;
            bool found = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
            {
                screenPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                found = true;
            }
#endif

            if (!found || screenPos == Vector2.zero)
            {
                try
                {
                    screenPos = Input.mousePosition;
                }
                catch { }
            }

            return screenPos;
        }

        public static Vector2 GetMouseWorldPosition(Camera targetCam = null)
        {
            Camera cam = targetCam != null ? targetCam : Camera.main;
            if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
            if (cam == null) return Vector2.zero;

            Vector2 screenPos = GetMouseScreenPosition();
            float depth = -cam.transform.position.z;
            if (depth <= 0.001f) depth = 10f;

            Vector3 screen3D = new Vector3(screenPos.x, screenPos.y, depth);
            Vector3 world3D = cam.ScreenToWorldPoint(screen3D);
            return new Vector2(world3D.x, world3D.y);
        }

        public static bool IsLeftMouseButtonDown()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                return true;
            }
#endif
            try
            {
                return Input.GetMouseButtonDown(0);
            }
            catch
            {
                return false;
            }
        }
    }
}
