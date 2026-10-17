using UnityEngine;
using UnityEngine.Tilemaps;

namespace Poketato
{
    /// <summary>Runtime tile with optional frame animation (water, lava, flowers, tall grass).</summary>
    public class PixelTile : TileBase
    {
        public Sprite[] frames;
        public float fps = 2f;

        public static PixelTile Make(params Sprite[] frames)
        {
            var t = CreateInstance<PixelTile>();
            t.frames = frames;
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = frames[0];
            tileData.color = Color.white;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = Tile.ColliderType.None;
        }

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
        {
            if (frames.Length < 2) return false;
            data.animatedSprites = frames;
            data.animationSpeed = fps;
            // Desynchronise neighbouring tiles a little, like the GBA's staggered flower/water animation.
            data.animationStartTime = ((position.x * 7 + position.y * 13) & 3) * 0.25f;
            return true;
        }
    }
}
