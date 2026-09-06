#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IsometricGame.Core;
using IsometricGame.Environment;

namespace IsometricGame.Editor
{
    public static class IsometricSceneSetupEditor
    {
        [MenuItem("GameObject/Isometric/Setup New Isometric Room & Camera", false, 10)]
        public static void CreateIsometricRoomAndCamera()
        {
            // 1. Create Room Generator
            GameObject roomObj = new GameObject("Isometric_Room");
            Undo.RegisterCreatedObjectUndo(roomObj, "Create Isometric Room");
            IsometricRoomBuilder roomBuilder = roomObj.AddComponent<IsometricRoomBuilder>();
            roomBuilder.RebuildRoom();

            // 2. Setup or Configure Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Isometric_Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(camObj, "Create Isometric Camera");
            }
            else
            {
                Undo.RecordObject(mainCam.gameObject, "Configure Isometric Camera");
            }

            // Set Orthographic and True Isometric Angle
            mainCam.orthographic = true;
            mainCam.orthographicSize = 6.5f;
            mainCam.nearClipPlane = -50f;
            mainCam.farClipPlane = 100f;
            mainCam.backgroundColor = new Color(0.96f, 0.933f, 0.866f); // Warm beige background matching reference
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // Room center is around (5, 1.5, 5) for 10x10 room
            Vector3 roomCenter = roomBuilder.GetRoomCenter();

            // Add Zoom & Orbit Rotation Controller
            if (!mainCam.TryGetComponent<IsometricCameraController>(out var controller))
            {
                controller = mainCam.gameObject.AddComponent<IsometricCameraController>();
            }
            controller.SetTarget(roomCenter, 8.5f);

            // 3. Setup Sun Directional Light
            GameObject sunObj = GameObject.Find("Directional Light (Sun)");
            Light sunLight = sunObj != null ? sunObj.GetComponent<Light>() : null;
            if (sunLight == null)
            {
                sunObj = new GameObject("Directional Light (Sun)");
                sunLight = sunObj.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(sunObj, "Create Sun Light");
            }

            sunLight.transform.rotation = Quaternion.Euler(45f, -45f, 0f);
            sunLight.color = new Color(1f, 0.96f, 0.88f);
            sunLight.intensity = 1.35f;
            sunLight.shadows = LightShadows.Soft;

            // 4. Setup Soft Fill Light
            GameObject fillObj = GameObject.Find("Directional Light (Soft Fill)");
            Light fillLight = fillObj != null ? fillObj.GetComponent<Light>() : null;
            if (fillLight == null)
            {
                fillObj = new GameObject("Directional Light (Soft Fill)");
                fillLight = fillObj.AddComponent<Light>();
                fillLight.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(fillObj, "Create Fill Light");
            }

            fillLight.transform.rotation = Quaternion.Euler(60f, 135f, 0f);
            fillLight.color = new Color(0.78f, 0.86f, 1.0f);
            fillLight.intensity = 0.45f;
            fillLight.shadows = LightShadows.None;

            Selection.activeGameObject = roomObj;
            Debug.Log("<color=green>[Isometric Game]</color> Isometric Room with Door, Black Void, Window, 360° Orbit Camera, and Lighting setup complete!");
        }
    }
}
#endif
