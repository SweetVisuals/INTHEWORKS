using UnityEngine;

namespace IsometricGame.Tilemap
{
    /// <summary>
    /// Utility for 2D Isometric projection math, coordinate transforms, and sprite depth sorting.
    /// Standard 2:1 isometric diamond projection for 32x32 pixel art (16x8 step per grid cell).
    /// </summary>
    public static class IsometricCoordinates
    {
        // 32x32 Pixel Art metrics: 32 PPU, 16px horizontal half-step, 8px vertical half-step, 8px vertical wall rise
        public const float PixelsPerUnit = 32.0f;
        public const float StepPixelsX = 16.0f;
        public const float StepPixelsY = 8.0f;
        public const float WallStepPixelsY = 8.0f;

        public const float DefaultTileWidth = (StepPixelsX * 2f) / PixelsPerUnit;       // 1.0f  (32px / 32 PPU)
        public const float DefaultTileHeight = (StepPixelsY * 2f) / PixelsPerUnit;      // 0.5f  (16px / 32 PPU)
        public const float DefaultWallStepHeight = WallStepPixelsY / PixelsPerUnit;     // 0.25f (8px / 32 PPU)

        /// <summary>
        /// Converts integer Grid coordinates (gridX, gridY) and optional elevation (stack height) to 2D World coordinates.
        /// </summary>
        public static Vector2 GridToWorld(int gridX, int gridY, int elevation = 0, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight, float wallStepHeight = DefaultWallStepHeight)
        {
            float worldX = (gridX - gridY) * (tileWidth * 0.5f);
            float worldY = (gridX + gridY) * (tileHeight * 0.5f) + (elevation * wallStepHeight);
            return new Vector2(worldX, worldY);
        }

        public static Vector2 GridToWorld(Vector2Int gridPos, int elevation = 0, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight, float wallStepHeight = DefaultWallStepHeight)
        {
            return GridToWorld(gridPos.x, gridPos.y, elevation, tileWidth, tileHeight, wallStepHeight);
        }

        /// <summary>
        /// Converts 2D World coordinates to the nearest integer Grid coordinate.
        /// </summary>
        public static Vector2Int WorldToGrid(Vector2 worldPos, float tileWidth = DefaultTileWidth, float tileHeight = DefaultTileHeight)
        {
            float halfW = tileWidth * 0.5f;
            float halfH = tileHeight * 0.5f;

            float x = (worldPos.x / halfW + worldPos.y / halfH) * 0.5f;
            float y = (worldPos.y / halfH - worldPos.x / halfW) * 0.5f;

            return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
        }

        /// <summary>
        /// Calculates depth Sorting Order for 2D sprites so objects closer to the camera render in front.
        /// As (gridX + gridY) decreases, the tile is closer to the screen bottom / camera.
        /// As elevation increases, the tile stacks higher visually.
        /// </summary>
        public static int CalculateSortingOrder(int gridX, int gridY, int elevation = 0, int layerOffset = 0)
        {
            return -(gridX + gridY) * 100 + (elevation * 10) + layerOffset;
        }
    }
}
