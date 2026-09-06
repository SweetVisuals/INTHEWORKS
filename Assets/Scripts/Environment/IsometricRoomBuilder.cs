using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsometricGame.Environment
{
    /// <summary>
    /// Builds a stylized isometric diorama room floating in a moody void:
    /// - Back-Left Wall (North-West, Z = depth): 6-panel Door + Doorframe + Pitch-Black Void backdrop + Fluorescent Tube Light.
    /// - Back-Right Wall (South-East, X = width): Open Framed Window + Sill + Glass + Warm Sun/Neon Radiance + Fluorescent Light.
    /// - Glowing Retro Computer Desk with phosphor CRT screen glow and keyboard.
    /// - Floating Diarama Underside Base.
    /// - Front faces (X = 0 and Z = 0) are completely open with dark framing floor rims.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class IsometricRoomBuilder : MonoBehaviour
    {
        [Header("Room Dimensions")]
        public float roomWidth = 10f;
        public float roomDepth = 10f;
        public float wallHeight = 5f;
        public float thickness = 0.35f;

        [Header("Door Settings (Back-Left Wall: Z = Depth)")]
        public bool includeDoor = true;
        public float doorPositionX = 2.0f;
        public float doorWidth = 1.8f;
        public float doorHeight = 3.6f;
        public float doorAjarAngle = 22f;

        [Header("Window Settings (Back-Right Wall: X = Width)")]
        public bool includeWindow = true;
        public float windowPositionZ = 5.0f;
        public float windowWidth = 2.8f;
        public float windowHeight = 2.2f;
        public float windowElevationY = 1.8f;

        [Header("Glow & Lighting Accents")]
        public bool includeFluorescentWallLights = true;
        public bool includeGlowingDeskTerminal = true;
        public Color wallTubeGlowColor = new Color(0.75f, 0.92f, 1.0f); // Cool cyan-white fluorescent
        public Color crtScreenGlowColor = new Color(0.3f, 1.0f, 0.6f); // Retro phosphor green
        public float glowIntensity = 2.8f;

        [Header("Colors & Styling")]
        public Color floorColor = new Color(0.82f, 0.84f, 0.86f);
        public Color wallColor = new Color(0.70f, 0.76f, 0.70f); // Pale sage green
        public Color trimColor = new Color(0.14f, 0.16f, 0.19f); // Matte dark charcoal
        public Color doorFrameColor = new Color(0.92f, 0.92f, 0.93f);
        public Color doorColor = new Color(0.96f, 0.96f, 0.97f);
        public Color windowFrameColor = new Color(0.92f, 0.92f, 0.93f);
        public Color glassColor = new Color(0.7f, 0.88f, 0.98f, 0.4f);

        [Header("Runtime References")]
        [SerializeField] private GameObject roomRoot;

        private void Awake()
        {
            RebuildRoom();
        }

        private void Start()
        {
            if (roomRoot == null || roomRoot.transform.childCount == 0)
            {
                RebuildRoom();
            }
        }

        private void OnEnable()
        {
            if (roomRoot == null || roomRoot.transform.childCount == 0)
            {
                RebuildRoom();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    RebuildRoom();
                }
            };
        }
#endif

        [ContextMenu("Build / Rebuild Room")]
        public void RebuildRoom()
        {
            if (roomRoot != null)
            {
                if (Application.isPlaying)
                    Destroy(roomRoot);
                else
                    DestroyImmediate(roomRoot);
            }

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Generated_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }

            roomRoot = new GameObject("Generated_IsometricRoom");
            roomRoot.transform.SetParent(transform, false);

            Material floorMat = CreateLitMaterial("Mat_Room_Floor", floorColor, 0.12f);
            Material wallMat = CreateLitMaterial("Mat_Room_Wall", wallColor, 0.05f);
            Material trimMat = CreateLitMaterial("Mat_Room_Trim", trimColor, 0.0f);
            Material doorFrameMat = CreateLitMaterial("Mat_Door_Frame", doorFrameColor, 0.2f);
            Material doorMat = CreateLitMaterial("Mat_Door_Slab", doorColor, 0.15f);
            Material windowFrameMat = CreateLitMaterial("Mat_Window_Frame", windowFrameColor, 0.2f);
            Material glassMat = CreateGlassMaterial("Mat_Window_Glass", glassColor);
            Material voidMat = CreateUnlitMaterial("Mat_Black_Void", Color.black);
            Material brassMat = CreateLitMaterial("Mat_Brass_Handle", new Color(0.85f, 0.7f, 0.25f), 0.8f, 0.9f);
            Material tubeGlowMat = CreateEmissiveMaterial("Mat_Tube_Glow", wallTubeGlowColor, glowIntensity);
            Material crtGlowMat = CreateEmissiveMaterial("Mat_CRT_Screen_Glow", crtScreenGlowColor, glowIntensity * 1.2f);
            Material deskWoodMat = CreateLitMaterial("Mat_Desk_Wood", new Color(0.72f, 0.48f, 0.30f), 0.3f);
            Material darkPlasticMat = CreateLitMaterial("Mat_Dark_Plastic", new Color(0.2f, 0.2f, 0.22f), 0.4f);

            // 1. Floating Diorama Base (Underside floating in void)
            BuildFloatingVoidBase(roomRoot.transform, trimMat);

            // 2. Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(roomRoot.transform, false);
            floor.transform.localPosition = new Vector3(roomWidth / 2f, -thickness / 2f, roomDepth / 2f);
            floor.transform.localScale = new Vector3(roomWidth, thickness, roomDepth);
            SetMaterial(floor, floorMat);

            // 3. Back-Left Wall (Z = roomDepth) with Doorway and Black Void
            BuildBackLeftWallWithDoor(roomRoot.transform, wallMat, doorFrameMat, doorMat, brassMat, voidMat);

            // 4. Back-Right Wall (X = roomWidth) with Window
            BuildBackRightWallWithWindow(roomRoot.transform, wallMat, windowFrameMat, glassMat);

            // 5. Diorama Framing Trims (Dark accent rims)
            BuildFramingTrims(roomRoot.transform, trimMat);

            // 6. Glowing Fluorescent Wall Light Fixtures
            if (includeFluorescentWallLights)
            {
                BuildWallGlowFixtures(roomRoot.transform, trimMat, tubeGlowMat);
            }

            // 7. Glowing Retro Computer Desk Terminal
            if (includeGlowingDeskTerminal)
            {
                BuildDeskTerminal(roomRoot.transform, deskWoodMat, darkPlasticMat, doorFrameMat, crtGlowMat);
            }
        }

        private void BuildFloatingVoidBase(Transform parent, Material baseMat)
        {
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Floating_Void_Base_Underbelly";
            baseObj.transform.SetParent(parent, false);
            baseObj.transform.localPosition = new Vector3(roomWidth / 2f, -thickness - 0.5f, roomDepth / 2f);
            baseObj.transform.localScale = new Vector3(roomWidth + thickness * 1.8f, 1.0f, roomDepth + thickness * 1.8f);
            SetMaterial(baseObj, baseMat);
        }

        private void BuildBackLeftWallWithDoor(Transform parent, Material wallMat, Material frameMat, Material doorMat, Material handleMat, Material voidMat)
        {
            GameObject wallGroup = new GameObject("Wall_Back_Left_Group");
            wallGroup.transform.SetParent(parent, false);

            float zPos = roomDepth + thickness / 2f;

            if (!includeDoor)
            {
                GameObject solidWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                solidWall.name = "Wall_Back_Left_Solid";
                solidWall.transform.SetParent(wallGroup.transform, false);
                solidWall.transform.localPosition = new Vector3(roomWidth / 2f, wallHeight / 2f, zPos);
                solidWall.transform.localScale = new Vector3(roomWidth + thickness * 2f, wallHeight, thickness);
                SetMaterial(solidWall, wallMat);
                return;
            }

            // Left Section (from X = -thickness to doorPositionX)
            float leftWidth = doorPositionX + thickness;
            GameObject leftSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftSection.name = "Wall_BL_LeftSection";
            leftSection.transform.SetParent(wallGroup.transform, false);
            leftSection.transform.localPosition = new Vector3((-thickness + doorPositionX) / 2f, wallHeight / 2f, zPos);
            leftSection.transform.localScale = new Vector3(leftWidth, wallHeight, thickness);
            SetMaterial(leftSection, wallMat);

            // Right Section (from X = doorPositionX + doorWidth to roomWidth + thickness)
            float rightStartX = doorPositionX + doorWidth;
            float rightWidth = (roomWidth + thickness) - rightStartX;
            GameObject rightSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightSection.name = "Wall_BL_RightSection";
            rightSection.transform.SetParent(wallGroup.transform, false);
            rightSection.transform.localPosition = new Vector3((rightStartX + roomWidth + thickness) / 2f, wallHeight / 2f, zPos);
            rightSection.transform.localScale = new Vector3(rightWidth, wallHeight, thickness);
            SetMaterial(rightSection, wallMat);

            // Header Section above door
            float headerHeight = wallHeight - doorHeight;
            GameObject headerSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            headerSection.name = "Wall_BL_HeaderSection";
            headerSection.transform.SetParent(wallGroup.transform, false);
            headerSection.transform.localPosition = new Vector3(doorPositionX + doorWidth / 2f, doorHeight + headerHeight / 2f, zPos);
            headerSection.transform.localScale = new Vector3(doorWidth, headerHeight, thickness);
            SetMaterial(headerSection, wallMat);

            // Black Void Backing (Unlit black box directly behind doorway)
            GameObject blackVoid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blackVoid.name = "Door_BlackVoid_Backdrop";
            blackVoid.transform.SetParent(wallGroup.transform, false);
            blackVoid.transform.localPosition = new Vector3(doorPositionX + doorWidth / 2f, doorHeight / 2f, zPos + thickness * 1.6f);
            blackVoid.transform.localScale = new Vector3(doorWidth * 1.5f, doorHeight * 1.25f, thickness * 2.2f);
            SetMaterial(blackVoid, voidMat);

            // Door Frame (Molding)
            float frameThick = 0.08f;
            float frameDepth = thickness * 1.2f;

            CreateCube(wallGroup.transform, "DoorFrame_LeftPost",
                new Vector3(doorPositionX - frameThick / 2f, doorHeight / 2f, zPos),
                new Vector3(frameThick, doorHeight, frameDepth), frameMat);

            CreateCube(wallGroup.transform, "DoorFrame_RightPost",
                new Vector3(doorPositionX + doorWidth + frameThick / 2f, doorHeight / 2f, zPos),
                new Vector3(frameThick, doorHeight, frameDepth), frameMat);

            CreateCube(wallGroup.transform, "DoorFrame_TopHeader",
                new Vector3(doorPositionX + doorWidth / 2f, doorHeight + frameThick / 2f, zPos),
                new Vector3(doorWidth + frameThick * 2f, frameThick, frameDepth), frameMat);

            // Door Slab & Panels (Open ajar so the void is visible!)
            GameObject doorPivot = new GameObject("Door_Pivot");
            doorPivot.transform.SetParent(wallGroup.transform, false);
            doorPivot.transform.localPosition = new Vector3(doorPositionX + 0.05f, 0, zPos);
            doorPivot.transform.localRotation = Quaternion.Euler(0, -doorAjarAngle, 0);

            float doorThick = 0.08f;
            float doorSlabWidth = doorWidth - 0.06f;
            GameObject doorSlab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorSlab.name = "Door_Slab";
            doorSlab.transform.SetParent(doorPivot.transform, false);
            doorSlab.transform.localPosition = new Vector3(doorSlabWidth / 2f, doorHeight / 2f, 0);
            doorSlab.transform.localScale = new Vector3(doorSlabWidth, doorHeight - 0.04f, doorThick);
            SetMaterial(doorSlab, doorMat);

            // 6 Raised Door Panels
            int rows = 3;
            int cols = 2;
            float panelWidth = (doorSlabWidth - 0.22f) / cols;
            float panelHeight = (doorHeight - 0.55f) / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float px = 0.08f + c * (panelWidth + 0.05f) + panelWidth / 2f;
                    float py = 0.14f + r * (panelHeight + 0.09f) + panelHeight / 2f;

                    GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    panel.name = $"Door_Panel_{r}_{c}";
                    panel.transform.SetParent(doorPivot.transform, false);
                    panel.transform.localPosition = new Vector3(px, py, -doorThick / 2f - 0.012f);
                    panel.transform.localScale = new Vector3(panelWidth, panelHeight, 0.02f);
                    SetMaterial(panel, frameMat);
                }
            }

            // Door Knob
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = "Door_Knob";
            handle.transform.SetParent(doorPivot.transform, false);
            handle.transform.localPosition = new Vector3(doorSlabWidth - 0.12f, doorHeight * 0.45f, -doorThick / 2f - 0.06f);
            handle.transform.localScale = new Vector3(0.1f, 0.1f, 0.12f);
            SetMaterial(handle, handleMat);
        }

        private void BuildBackRightWallWithWindow(Transform parent, Material wallMat, Material frameMat, Material glassMat)
        {
            GameObject wallGroup = new GameObject("Wall_Back_Right_Group");
            wallGroup.transform.SetParent(parent, false);

            float xPos = roomWidth + thickness / 2f;

            if (!includeWindow)
            {
                GameObject solidWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                solidWall.name = "Wall_Back_Right_Solid";
                solidWall.transform.SetParent(wallGroup.transform, false);
                solidWall.transform.localPosition = new Vector3(xPos, wallHeight / 2f, roomDepth / 2f);
                solidWall.transform.localScale = new Vector3(thickness, wallHeight, roomDepth);
                SetMaterial(solidWall, wallMat);
                return;
            }

            // Front Section (from Z = 0 to windowPositionZ - windowWidth/2)
            float winStartZ = windowPositionZ - windowWidth / 2f;
            float leftLen = winStartZ;
            GameObject frontSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontSection.name = "Wall_BR_FrontSection";
            frontSection.transform.SetParent(wallGroup.transform, false);
            frontSection.transform.localPosition = new Vector3(xPos, wallHeight / 2f, leftLen / 2f);
            frontSection.transform.localScale = new Vector3(thickness, wallHeight, leftLen);
            SetMaterial(frontSection, wallMat);

            // Back Section (from Z = windowPositionZ + windowWidth/2 to roomDepth)
            float winEndZ = windowPositionZ + windowWidth / 2f;
            float rightLen = roomDepth - winEndZ;
            GameObject backSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backSection.name = "Wall_BR_BackSection";
            backSection.transform.SetParent(wallGroup.transform, false);
            backSection.transform.localPosition = new Vector3(xPos, wallHeight / 2f, (winEndZ + roomDepth) / 2f);
            backSection.transform.localScale = new Vector3(thickness, wallHeight, rightLen);
            SetMaterial(backSection, wallMat);

            // Bottom Sill Section
            GameObject bottomSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomSection.name = "Wall_BR_BottomSillWall";
            bottomSection.transform.SetParent(wallGroup.transform, false);
            bottomSection.transform.localPosition = new Vector3(xPos, windowElevationY / 2f, windowPositionZ);
            bottomSection.transform.localScale = new Vector3(thickness, windowElevationY, windowWidth);
            SetMaterial(bottomSection, wallMat);

            // Top Header Section
            float winTopY = windowElevationY + windowHeight;
            float topHeaderHeight = wallHeight - winTopY;
            GameObject topSection = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topSection.name = "Wall_BR_TopHeaderWall";
            topSection.transform.SetParent(wallGroup.transform, false);
            topSection.transform.localPosition = new Vector3(xPos, winTopY + topHeaderHeight / 2f, windowPositionZ);
            topSection.transform.localScale = new Vector3(thickness, topHeaderHeight, windowWidth);
            SetMaterial(topSection, wallMat);

            // Window Framing & Sills
            float fThick = 0.09f;
            float fDepth = thickness * 1.3f;
            float winCenterY = windowElevationY + windowHeight / 2f;

            // Window Sill Shelf
            CreateCube(wallGroup.transform, "Window_Sill_Bottom",
                new Vector3(xPos, windowElevationY - fThick / 2f, windowPositionZ),
                new Vector3(fDepth * 1.15f, fThick * 1.2f, windowWidth + fThick * 3f), frameMat);

            // Window Top Frame
            CreateCube(wallGroup.transform, "Window_Frame_Top",
                new Vector3(xPos, winTopY + fThick / 2f, windowPositionZ),
                new Vector3(fDepth, fThick, windowWidth + fThick * 2f), frameMat);

            // Window Posts
            CreateCube(wallGroup.transform, "Window_Frame_FrontPost",
                new Vector3(xPos, winCenterY, winStartZ - fThick / 2f),
                new Vector3(fDepth, windowHeight, fThick), frameMat);

            CreateCube(wallGroup.transform, "Window_Frame_BackPost",
                new Vector3(xPos, winCenterY, winEndZ + fThick / 2f),
                new Vector3(fDepth, windowHeight, fThick), frameMat);

            // Mullions (Center Cross Grid)
            CreateCube(wallGroup.transform, "Window_Mullion_Vertical",
                new Vector3(xPos, winCenterY, windowPositionZ),
                new Vector3(thickness * 0.8f, windowHeight, 0.05f), frameMat);

            CreateCube(wallGroup.transform, "Window_Mullion_Horizontal",
                new Vector3(xPos, winCenterY, windowPositionZ),
                new Vector3(thickness * 0.8f, 0.05f, windowWidth), frameMat);

            // Glass Pane
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Window_Glass_Pane";
            glass.transform.SetParent(wallGroup.transform, false);
            glass.transform.localPosition = new Vector3(xPos, winCenterY, windowPositionZ);
            glass.transform.localScale = new Vector3(0.02f, windowHeight - 0.04f, windowWidth - 0.04f);
            SetMaterial(glass, glassMat);
        }

        private void BuildFramingTrims(Transform parent, Material trimMat)
        {
            GameObject trimGroup = new GameObject("Diorama_Framing_Trims");
            trimGroup.transform.SetParent(parent, false);

            // Top rim of Back-Left Wall
            CreateCube(trimGroup.transform, "Trim_BackLeft_Top",
                new Vector3(roomWidth / 2f, wallHeight + 0.06f, roomDepth + thickness / 2f),
                new Vector3(roomWidth + thickness * 2f + 0.04f, 0.12f, thickness * 1.05f), trimMat);

            // Top rim of Back-Right Wall
            CreateCube(trimGroup.transform, "Trim_BackRight_Top",
                new Vector3(roomWidth + thickness / 2f, wallHeight + 0.06f, roomDepth / 2f),
                new Vector3(thickness * 1.05f, 0.12f, roomDepth + 0.04f), trimMat);

            // Far Corner Post (Top far vertex)
            CreateCube(trimGroup.transform, "Trim_FarCornerPost",
                new Vector3(roomWidth + thickness / 2f, wallHeight / 2f, roomDepth + thickness / 2f),
                new Vector3(thickness * 1.08f, wallHeight + 0.12f, thickness * 1.08f), trimMat);

            // Front-Left Floor Rim (Facing camera: along X = 0)
            CreateCube(trimGroup.transform, "Trim_FrontLeft_FloorRim",
                new Vector3(-thickness / 2f, -thickness / 2f, roomDepth / 2f),
                new Vector3(thickness, thickness * 1.02f, roomDepth + thickness * 2f), trimMat);

            // Front-Right Floor Rim (Facing camera: along Z = 0)
            CreateCube(trimGroup.transform, "Trim_FrontRight_FloorRim",
                new Vector3(roomWidth / 2f, -thickness / 2f, -thickness / 2f),
                new Vector3(roomWidth, thickness * 1.02f, thickness), trimMat);

            // Left Wall End Cap Trim
            CreateCube(trimGroup.transform, "Trim_LeftWall_EndCap",
                new Vector3(-thickness / 2f, wallHeight / 2f, roomDepth + thickness / 2f),
                new Vector3(thickness * 1.05f, wallHeight + 0.12f, thickness * 1.05f), trimMat);

            // Right Wall End Cap Trim
            CreateCube(trimGroup.transform, "Trim_RightWall_EndCap",
                new Vector3(roomWidth + thickness / 2f, wallHeight / 2f, -thickness / 2f),
                new Vector3(thickness * 1.05f, wallHeight + 0.12f, thickness * 1.05f), trimMat);
        }

        private void BuildWallGlowFixtures(Transform parent, Material fixtureMat, Material glowMat)
        {
            GameObject glowGroup = new GameObject("Wall_Glow_Fixtures");
            glowGroup.transform.SetParent(parent, false);

            // Light 1: Mounted on Back-Left Wall (near top)
            Vector3 lightPos1 = new Vector3(6.5f, wallHeight - 0.6f, roomDepth - 0.08f);
            BuildTubeFixture(glowGroup.transform, "FluorescentLight_BL", lightPos1, new Vector3(2.6f, 0.1f, 0.1f), fixtureMat, glowMat, wallTubeGlowColor);

            // Light 2: Mounted on Back-Right Wall (near top)
            Vector3 lightPos2 = new Vector3(roomWidth - 0.08f, wallHeight - 0.6f, 2.5f);
            BuildTubeFixture(glowGroup.transform, "FluorescentLight_BR", lightPos2, new Vector3(0.1f, 0.1f, 2.6f), fixtureMat, glowMat, wallTubeGlowColor);
        }

        private void BuildTubeFixture(Transform parent, string name, Vector3 pos, Vector3 scale, Material fixtureMat, Material glowMat, Color lightColor)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            group.transform.localPosition = pos;

            // Fixture Base/Bracket
            GameObject bracket = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bracket.name = "Fixture_Mount";
            bracket.transform.SetParent(group.transform, false);
            bracket.transform.localPosition = Vector3.zero;
            bracket.transform.localScale = scale;
            SetMaterial(bracket, fixtureMat);

            // Glowing Fluorescent Tube
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Emissive_Tube";
            tube.transform.SetParent(group.transform, false);
            tube.transform.localPosition = new Vector3(0, -0.06f, 0);

            if (scale.x > scale.z)
            {
                tube.transform.localScale = new Vector3(0.08f, scale.x * 0.45f, 0.08f);
                tube.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            }
            else
            {
                tube.transform.localScale = new Vector3(0.08f, scale.z * 0.45f, 0.08f);
                tube.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            }
            SetMaterial(tube, glowMat);

            // Soft Point Light
            GameObject lightObj = new GameObject("Tube_PointLight");
            lightObj.transform.SetParent(group.transform, false);
            lightObj.transform.localPosition = new Vector3(0, -0.3f, 0);
            Light pLight = lightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = lightColor;
            pLight.intensity = 1.6f;
            pLight.range = 6.0f;
            pLight.shadows = LightShadows.None;
        }

        private void BuildDeskTerminal(Transform parent, Material woodMat, Material darkMat, Material chassisMat, Material crtGlowMat)
        {
            GameObject deskGroup = new GameObject("Retro_Desk_Terminal");
            deskGroup.transform.SetParent(parent, false);
            deskGroup.transform.localPosition = new Vector3(4.2f, 0, 7.5f);
            deskGroup.transform.localRotation = Quaternion.Euler(0, 45f, 0);

            // Desk Tabletop
            CreateCube(deskGroup.transform, "Desk_Top", new Vector3(0, 1.4f, 0), new Vector3(2.4f, 0.1f, 1.2f), woodMat);
            // Desk Legs
            CreateCube(deskGroup.transform, "Desk_Leg_FL", new Vector3(-1.05f, 0.7f, -0.45f), new Vector3(0.1f, 1.4f, 0.1f), darkMat);
            CreateCube(deskGroup.transform, "Desk_Leg_FR", new Vector3(1.05f, 0.7f, -0.45f), new Vector3(0.1f, 1.4f, 0.1f), darkMat);
            CreateCube(deskGroup.transform, "Desk_Leg_BL", new Vector3(-1.05f, 0.7f, 0.45f), new Vector3(0.1f, 1.4f, 0.1f), darkMat);
            CreateCube(deskGroup.transform, "Desk_Leg_BR", new Vector3(1.05f, 0.7f, 0.45f), new Vector3(0.1f, 1.4f, 0.1f), darkMat);

            // Retro Computer Chassis
            CreateCube(deskGroup.transform, "PC_Chassis", new Vector3(-0.4f, 1.8f, 0.1f), new Vector3(0.85f, 0.7f, 0.75f), chassisMat);

            // Glowing CRT Screen
            GameObject screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "CRT_Glow_Screen";
            screen.transform.SetParent(deskGroup.transform, false);
            screen.transform.localPosition = new Vector3(-0.4f, 1.82f, -0.28f);
            screen.transform.localScale = new Vector3(0.65f, 0.48f, 0.05f);
            SetMaterial(screen, crtGlowMat);

            // Screen Glow Point Light
            GameObject screenLight = new GameObject("Screen_PointLight");
            screenLight.transform.SetParent(deskGroup.transform, false);
            screenLight.transform.localPosition = new Vector3(-0.4f, 1.82f, -0.6f);
            Light sLight = screenLight.AddComponent<Light>();
            sLight.type = LightType.Point;
            sLight.color = crtScreenGlowColor;
            sLight.intensity = 1.2f;
            sLight.range = 3.5f;
            sLight.shadows = LightShadows.None;

            // Keyboard
            CreateCube(deskGroup.transform, "PC_Keyboard", new Vector3(-0.4f, 1.48f, -0.3f), new Vector3(0.75f, 0.04f, 0.3f), darkMat);

            // Stylized Desk Rug (Teal square rug underneath)
            Material rugMat = CreateLitMaterial("Mat_Teal_Rug", new Color(0.2f, 0.55f, 0.58f), 0.05f);
            CreateCube(deskGroup.transform, "Desk_Rug", new Vector3(0, 0.015f, 0), new Vector3(3.2f, 0.02f, 2.6f), rugMat);
        }

        private void CreateCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localScale = localScale;
            SetMaterial(obj, mat);
        }

        private void CreateCube(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            Material mat = CreateLitMaterial("Mat_" + name, color, 0.2f);
            CreateCube(parent, name, localPos, localScale, mat);
        }

        private void SetMaterial(GameObject obj, Material mat)
        {
            if (obj.TryGetComponent<Renderer>(out var r))
            {
                r.sharedMaterial = mat;
            }
        }

        private Material CreateLitMaterial(string matName, Color color, float smoothness, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = matName,
                color = color
            };

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            return mat;
        }

        private Material CreateEmissiveMaterial(string matName, Color color, float intensity)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = matName,
                color = color
            };

            Color hdrEmission = color * intensity;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", hdrEmission);
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.9f);

            return mat;
        }

        private Material CreateUnlitMaterial(string matName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader)
            {
                name = matName,
                color = color
            };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            return mat;
        }

        private Material CreateGlassMaterial(string matName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader)
            {
                name = matName,
                color = color
            };

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            mat.renderQueue = 3000;

            return mat;
        }

        public Vector3 GetRoomCenter()
        {
            return new Vector3(roomWidth / 2f, wallHeight * 0.35f, roomDepth / 2f);
        }
    }
}
