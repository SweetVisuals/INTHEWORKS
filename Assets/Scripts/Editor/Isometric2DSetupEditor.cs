#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using IsometricGame.Tilemap;
using IsometricGame.Player;
using IsometricGame.CameraControl;
using IsometricGame.UI;

namespace IsometricGame.Editor
{
    public static class Isometric2DSetupEditor
    {
        [MenuItem("GameObject/2D Isometric/Setup 4x4 Room Plane & Cylinder Player", false, 10)]
        public static void Create2DIsometricWorld()
        {
            // 1. Create or Reset World Map Generator
            GameObject mapObj = GameObject.Find("2D_Isometric_World");
            if (mapObj == null)
            {
                mapObj = new GameObject("2D_Isometric_World");
                Undo.RegisterCreatedObjectUndo(mapObj, "Create 2D World");
            }

            IsometricWorldMap worldMap = mapObj.GetComponent<IsometricWorldMap>();
            if (worldMap == null) worldMap = mapObj.AddComponent<IsometricWorldMap>();

            worldMap.customFloorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/floor wood tile 32x32 (1).png")
                                       ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden floor tile 32x32.png");
            worldMap.customWallLeftSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/left wall tile 32x32.png");
            worldMap.customWallRightSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/right wall tile 32x32.png");
            worldMap.customDoorSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden door.png")
                                      ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden door_0002.png")
                                      ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/dark void door (1).png");
            worldMap.customWindowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden window (1).png")
                                        ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden window.png")
                                        ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/wooden window_0002.png");
            worldMap.customDeskSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/desk with computer_0003.png")
                                     ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/desk with computer_0002.png");
            worldMap.RoomWidth = 6;
            worldMap.RoomDepth = 6;
            worldMap.WallHeight = 3;
            worldMap.DoorColumn = 4;
            worldMap.WindowColumn = 1;
            worldMap.GenerateOpenWorld = false;
            worldMap.GenerateWorldMap();

            // 2. Spawn Cylinder Player Pawn
            GameObject playerObj = GameObject.Find("Player_Cylinder");
            if (playerObj == null)
            {
                playerObj = new GameObject("Player_Cylinder");
                Undo.RegisterCreatedObjectUndo(playerObj, "Create Cylinder Player");
            }

            Vector2 roomCenter = worldMap.GetRoomCenterWorld();
            playerObj.transform.position = new Vector3(roomCenter.x, roomCenter.y, 0);

            // Collider & Rigidbody2D
            Rigidbody2D rb = playerObj.GetComponent<Rigidbody2D>();
            if (rb == null) rb = playerObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = playerObj.GetComponent<CircleCollider2D>();
            if (col == null) col = playerObj.AddComponent<CircleCollider2D>();
            col.radius = 0.2f;
            col.offset = Vector2.zero;

            // Visual Cylinder Hierarchy
            Transform visual = playerObj.transform.Find("Visual");
            if (visual == null)
            {
                GameObject visObj = new GameObject("Visual");
                visObj.transform.SetParent(playerObj.transform, false);
                visual = visObj.transform;

                // 3D Cylinder Mesh Pawn for the player
                GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Cylinder_Body";
                cylinder.transform.SetParent(visual, false);
                cylinder.transform.localPosition = new Vector3(0, 0.18f, 0);
                cylinder.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
                cylinder.transform.localRotation = Quaternion.Euler(30f, 45f, 0);

                // Stylized cyan / teal player material
                Material playerMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                playerMat.name = "Mat_Player_Cylinder";
                playerMat.color = new Color(0.25f, 0.85f, 0.95f);
                if (cylinder.TryGetComponent<Renderer>(out var rend)) rend.sharedMaterial = playerMat;
                if (cylinder.TryGetComponent<Collider>(out var c3d)) Object.DestroyImmediate(c3d);

                // Player Shadow
                GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shadow.name = "Player_Shadow";
                shadow.transform.SetParent(visual, false);
                shadow.transform.localPosition = new Vector3(0, -0.02f, 0);
                shadow.transform.localScale = new Vector3(0.4f, 0.015f, 0.4f);
                shadow.transform.localRotation = Quaternion.Euler(30f, 45f, 0);

                Material shadowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                shadowMat.name = "Mat_Player_Shadow";
                shadowMat.color = new Color(0, 0, 0, 0.5f);
                if (shadow.TryGetComponent<Renderer>(out var sRend)) sRend.sharedMaterial = shadowMat;
                if (shadow.TryGetComponent<Collider>(out var sc3d)) Object.DestroyImmediate(sc3d);
            }

            // Player Controller
            IsometricPlayerController playerCtrl = playerObj.GetComponent<IsometricPlayerController>();
            if (playerCtrl == null) playerCtrl = playerObj.AddComponent<IsometricPlayerController>();
            playerCtrl.ControlScheme = IsometricControlScheme.ScreenRelative;

            // 3. Setup 2D Follow Camera
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                mainCam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
                Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
            }

            mainCam.orthographic = true;
            mainCam.orthographicSize = 1.6f;
            mainCam.transform.rotation = Quaternion.identity; // Flat 2D view
            mainCam.transform.position = new Vector3(roomCenter.x, roomCenter.y, -10f);
            mainCam.backgroundColor = new Color(0.045f, 0.055f, 0.09f, 1f); // Deep Void
            mainCam.clearFlags = CameraClearFlags.SolidColor;

            // Remove 3D controller if present
            if (mainCam.TryGetComponent<Core.IsometricCameraController>(out var oldCam))
            {
                Undo.DestroyObjectImmediate(oldCam);
            }

            IsometricFollowCamera followCam = mainCam.GetComponent<IsometricFollowCamera>();
            if (followCam == null) followCam = mainCam.gameObject.AddComponent<IsometricFollowCamera>();
            followCam.Target = playerObj.transform;
            followCam.FollowPlayer = false;
            followCam.PositionOnRoomCenter();

            // 4. Setup Money UI
            MoneyUISetupEditor.SetupMoneyHUD();

            Selection.activeGameObject = playerObj;
            Debug.Log("<color=green>[2D Isometric]</color> 4x4 Floor Plane, 3-High Stacked Walls (with Door & Window), Straight 2D Locomotion, and Zoomed-In Follow Camera setup complete!");
        }
    }
}
#endif
