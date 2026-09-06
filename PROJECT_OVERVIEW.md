# 2D Isometric RPG / Life-Sim — Project Technical Overview

> **Target Engine:** Unity 6 (`6000.6.0f1`)  
> **Render Pipeline:** Universal Render Pipeline (URP 2D)  
> **Projection:** 2:1 Dimetric / Isometric (`Tile Width : Height = 2 : 1`)  
> **Visual Standard:** Crisp Pixel Art (`32 Pixels Per Unit`, Point / No Filter, Uncompressed)  
> **Canvas Scaler:** Scale With Screen Size (`1920 × 1080`, Match `0.5`)  
> **Active Scene:** `Assets/Scenes/SampleScene.unity`

---

## 1. Executive Summary & Core Concept

This project is a high-performance, pixel-perfect 2D Isometric RPG / Life-Sim built in Unity 6 using URP. It features:
- A custom 2:1 dimetric coordinate system with continuous Y-depth sorting.
- Procedural indoor room generation with walls, doorways, windows, and perimeter collision boundaries.
- Infinite procedural outdoor terrain generation with Perlin-noise clutter (bushes, pine trees) and doorway safe zones.
- An 8-directional physics-based player controller with directional walk/idle animations and dynamic ground shadow projection.
- A smooth follow camera with lookahead, room-center framing, and orthographic zoom.
- A comprehensive retro pixel-art HUD suite (Clock & Calendar, XP Bar, Energy/Stamina Bar, Money Card, 4-Slot Hotbar, Contextual World Interaction Popups, Animated Sleep Transition, Chest Inventory, and Jobs Board).

---

## 2. Directory Structure & Key Files

```
Assets/
├── Scenes/
│   └── SampleScene.unity                    # Main gameplay scene
├── Scripts/
│   ├── Camera/
│   │   └── IsometricFollowCamera.cs        # Follow camera with lookahead, zoom, room framing
│   ├── Core/
│   │   └── IsometricInputHelper.cs         # Input abstractions for mouse/gamepad/keys
│   ├── Editor/
│   │   ├── FixPixelArtImporter.cs          # Batch importer ensuring point filtering & 32 PPU
│   │   ├── Isometric2DSetupEditor.cs       # Scene setup and world generation menu commands
│   │   └── MoneyUISetupEditor.cs           # Auto-ensures & bakes full GUI HUD in active scene
│   ├── Environment/
│   │   ├── BedInteraction.cs               # Bed interaction -> trigger sleep transition
│   │   ├── ComputerScreenFlicker.cs        # CRT flicker & ambient screen glow animation
│   │   ├── DoorHandleInteraction.cs        # Door opening & room exit trigger
│   │   ├── DrawerChestInteraction.cs       # Dresser chest interaction -> open inventory
│   │   └── ZoneTransitionManager.cs        # Fade-based indoor <-> outdoor zone warping
│   ├── Player/
│   │   └── IsometricPlayerController.cs    # 8-dir movement, physics, animations, shadow
│   ├── Tilemap/
│   │   ├── IsometricCoordinates.cs         # 2:1 dimetric projection math & depth sorting
│   │   ├── IsometricWorldMap.cs            # Procedural room, furniture, and boundary builder
│   │   └── OutdoorInfiniteTerrain.cs       # Chunked terrain generator with noise distribution
│   └── UI/
│       ├── ChestInventoryUI.cs             # Modal container inventory grid UI
│       ├── ClockUI.cs                      # Top-Right Clock HUD (AM/PM, Days, dynamic digits)
│       ├── CustomGameCursor.cs             # Pixel cursor hardware/software manager
│       ├── EnergyBarUI.cs                  # Top-Right Energy HUD (61x7 @ 4x, drain & regen)
│       ├── EnsureCanvasAndMoneyUI.cs       # Master auto-initialiser for all GUI elements
│       ├── HotbarUI.cs                     # Bottom-Center 4-slot inventory hotbar
│       ├── JobsBoardUI.cs                  # Modal job board & task list interface
│       ├── MoneyUI.cs                      # Top-Center Money HUD panel
│       ├── PixelNumberDisplay.cs           # Sliced pixel digit font renderer (0-9)
│       ├── SleepTransitionUI.cs            # Sleep overlay with animated 3-frame text
│       ├── UISpriteUtility.cs              # Multi-tier sprite loader & slicer utility
│       └── XpBarUI.cs                      # Top-Right XP Bar HUD (61x7 @ 4x, level progression)
└── Sprites/
    ├── Character/                          # 8-direction idle & walk character frame strips
    ├── GUI/                                # UI cards, frames, bars, icons, fonts
    │   └── Clock/                          # clock am/pm, clock mon..sun, clock hud example
    └── Map/                                # Floors, walls, doors, windows, flora, furniture
```

---

## 3. Mathematical Foundations & Projection System

### 2:1 Dimetric Projection Math (`IsometricCoordinates.cs`)
Standard 2D coordinates `(gridX, gridY)` are projected to isometric world coordinates `(worldX, worldY)` using a 2:1 aspect ratio:

$$\text{worldX} = (\text{gridX} - \text{gridY}) \times \frac{\text{TileWidth}}{2}$$

$$\text{worldY} = (\text{gridX} + \text{gridY}) \times \frac{\text{TileHeight}}{2}$$

Where `TileWidth = 1.0f` and `TileHeight = 0.5f` (for standard 32×16 px isometric tiles at 32 PPU).

Inverse projection from world space back to grid coordinates:

$$\text{gridX} = \frac{\text{worldX}}{\text{TileWidth}} + \frac{\text{worldY}}{\text{TileHeight}}$$

$$\text{gridY} = \frac{\text{worldY}}{\text{TileHeight}} - \frac{\text{worldX}}{\text{TileWidth}}$$

### Continuous Depth Sorting
To eliminate z-fighting and render characters correctly behind/in front of walls and objects:
$$\text{sortingOrder} = -\text{RoundToInt}(\text{worldY} \times 100) + \text{elevationOffset}$$
- Characters and movable objects dynamically update `sortingOrder` each frame.
- Static wall tops, roofs, and elevated structures use an elevation offset to remain in front of base tiles.

---

## 4. Player & Camera Subsystems

### Player Controller (`IsometricPlayerController.cs`)
- **Physics:** `Rigidbody2D` set to `Dynamic` with `gravityScale = 0` and `constraints = FreezeRotation`. Collision capsule/circle positioned at the feet.
- **Directional Movement:** 8 directions:
  - North (`+X, +Y` in grid space $\rightarrow$ `+Y` screen)
  - South (`-X, -Y` in grid space $\rightarrow$ `-Y` screen)
  - East (`+X, -Y` in grid space $\rightarrow$ `+X` screen)
  - West (`-X, +Y` in grid space $\rightarrow$ `-X` screen)
  - Diagonals: NE, NW, SE, SW.
- **Locomotion:** Smooth acceleration (`acceleration = 24`), deceleration (`deceleration = 28`), walk speed (`1.15`), run speed (`1.8`).
- **Animations:** Directional sprite frames with frame-rate scaling based on normalized velocity.
- **Shadow:** Ground contact shadow automatically rendered beneath feet with configurable opacity and scale.

### Camera System (`IsometricFollowCamera.cs`)
- Targets the player with smooth lerp damping (`followSpeed = 6`).
- **Velocity Lookahead:** Shifts view in movement direction (`lookAheadFactor = 0.4`).
- **Room Framing:** When player is indoors, smooth transition to `roomCenterOffset` to frame the room without jitter.
- **Zoom Controls:** Orthographic size range `0.8` to `4.0` (default `1.6`) with smooth scroll-wheel damping.

---

## 5. UI Architecture & HUD Systems

All UI systems use `[ExecuteAlways]` and are instantiated and maintained automatically via `EnsureCanvasAndMoneyUI.cs`.

```
Canvas (ScreenSpaceOverlay, 1920x1080 Reference, Match 0.5)
├── Money_HUD_Panel          (Top Center: x=0, y=-22)
├── Hotbar_Panel             (Bottom Center: x=0, y=20)
├── Clock_HUD                (Top Right: x=-24, y=-22 | 244x56 px)
├── XP_Bar_HUD               (Top Right: x=-24, y=-82 | 244x28 px)
├── Energy_Bar_HUD           (Top Right: x=-24, y=-114 | 244x28 px)
├── Interaction_Popup        (World-space follow card)
├── Sleep_Transition_UI      (Fullscreen overlay)
├── Chest_Inventory_UI       (Modal window)
└── Jobs_Board_UI            (Modal window)
```

### Top-Right Stack Specification
The Top-Right HUD elements are stacked with uniform 4px vertical gaps:

| HUD Element | Script | Anchored Pos (`Top-Right`) | Native Px | 4× Scale Px | Top Y | Bottom Y | Description |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Clock / Time** | `ClockUI.cs` | `(-24, -22)` | 61 × 14 | 244 × 56 | `-22f` | `-78f` | AM/PM base, Day overlay, dynamic Date & Time digits |
| *Spacing* | — | — | — | — | `-78f` | `-82f` | *4 px gap* |
| **XP Bar** | `XpBarUI.cs` | `(-24, -82)` | 61 × 7 | 244 × 28 | `-82f` | `-110f` | Green fill bar, level tracking, smooth lerp |
| *Spacing* | — | — | — | — | `-110f` | `-114f` | *4 px gap* |
| **Energy Bar** | `EnergyBarUI.cs` | `(-24, -114)` | 61 × 7 | 244 × 28 | `-114f` | `-142f` | Yellow fill bar, stamina star icon, drain & regen |

#### Clock HUD Breakdown (`ClockUI.cs`)
- **Base Layer:** Swaps `clock am.png` and `clock pm.png` (contains clock icon at left, colon at X=28, AM/PM tag at X=41..49).
- **Day Overlay:** Swaps `clock mon.png` through `clock sun.png` (displays day abbreviation at X=17..34, Y=1..5).
- **Dynamic Digits:** Slices 3×5 pixel glyphs (at 4× scale = 12×20 px) from `numbers 1 - 9.png`, with an in-memory generated hollow `'0'` glyph:
  - **Date Tens:** `X = 37`, `Y = 1`
  - **Date Ones:** `X = 42`, `Y = 1`
  - **Hour Tens:** `X = 18`, `Y = 8`
  - **Hour Ones:** `X = 23`, `Y = 8`
  - **Colon:** Fixed at `X = 28`
  - **Min Tens:** `X = 30`, `Y = 8`
  - **Min Ones:** `X = 35`, `Y = 8`
- **Time Speed:** `timeScale = 60f` (1 real second = 1 in-game minute).

#### XP Bar (`XpBarUI.cs`)
- **Textures:** `xp bar empty new.png` (empty frame + icon) & `xp bar green fill.png` (green horizontal fill).
- **Fill Mapping:** Pixels 13 to 57 out of 61 total width (44 px fill track):
  $$\text{fillAmount} = \frac{13 + (\text{normalizedXp} \times 44)}{61}$$
- **Progression:** Dynamic level-up scaling (`maxXp = Round(maxXp * 1.25)`).

#### Energy Bar (`EnergyBarUI.cs`)
- **Textures:** `energy bar empty.png` (empty frame + star icon) & `energy bar yellow fill.png` (yellow horizontal fill).
- **Fill Mapping:** Exact same 44 px track (X: 13..57) for visual consistency with the XP Bar.
- **Restoration:** Integrated with `SleepTransitionUI.cs` to fully restore energy upon waking.

---

## 6. Environment & Interaction Mechanics

1. **Bed (`BedInteraction.cs`)**:
   - Triggers `SleepTransitionUI` when player interacts.
   - Screen fades to black, advances animated "Sleeping..." frames (1 $\rightarrow$ 2 $\rightarrow$ 3), advances in-game clock to next morning 6:00 AM, and calls `EnergyBarUI.Instance.RestoreFullEnergy()`.
2. **Computer Desk (`ComputerScreenFlicker.cs`)**:
   - Renders animated CRT monitor flicker using alternating off/on/flicker sprites.
   - Casts a soft radial screen glow light onto adjacent tiles.
3. **Dresser / Chest (`DrawerChestInteraction.cs`)**:
   - Displays interaction popup on hover and toggles `ChestInventoryUI` modal.
4. **Door & Transitions (`DoorHandleInteraction.cs`, `ZoneTransitionManager.cs`)**:
   - Smoothly teleports player between indoor room door and outdoor campsite door with fade transitions.

---

## 7. Critical Coding Guidelines & Conventions

### 1. Disambiguating `Object`
When writing C# scripts that import both `System` and `UnityEngine`, C# will error with `CS0104: 'Object' is an ambiguous reference`.
**Rule:** Always add the alias at the top of the file:
```csharp
using System;
using UnityEngine;
using Object = UnityEngine.Object;
```
Or qualify explicitly: `UnityEngine.Object.FindAnyObjectByType<T>()`.

### 2. Sprite Import Configuration
All 2D pixel art textures must have matching `.meta` settings:
```yaml
TextureImporter:
  textureType: 8          # Sprite (2D and UI)
  spriteMode: 1           # Single
  filterMode: 0           # Point (no filter)
  alphaIsTransparency: 1  # Preserve alpha
  isReadable: 1           # Allow CPU pixel read
  spritePixelsToUnits: 32 # Consistent 32 PPU
```
To fix any blurry textures across the project, run menu:  
**`GameObject > 2D Isometric > Force Crisp Point Filter On All Sprites`**.

### 3. Edit-Mode UI Execution
Every UI component must include:
```csharp
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class MyUIComponent : MonoBehaviour
```
To rebuild or repair the entire UI in Edit Mode or Play Mode at any time, run:  
**`GameObject > UI > Setup Full GUI HUD (Clock, Money, Hotbar, Energy & XP)`**.

---

*Document compiled for pair-programming and multi-agent workflow continuity.*
