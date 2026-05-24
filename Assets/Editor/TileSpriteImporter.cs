using UnityEngine;
using UnityEditor;

public class TileSpriteImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.EndsWith("Assets/Sprites/tile.png"))
            ConfigureTile((TextureImporter)assetImporter);
        else if (assetPath.EndsWith("Assets/Sprites/arena_bg.png"))
            ConfigureBg((TextureImporter)assetImporter);
    }

    static void ConfigureTile(TextureImporter i)
    {
        i.textureType         = TextureImporterType.Sprite;
        i.spriteImportMode    = SpriteImportMode.Single;
        i.spritePivot         = new Vector2(0.5f, 0.5f);
        i.spritePixelsPerUnit = 100f;
        i.filterMode          = FilterMode.Bilinear;
        i.textureCompression  = TextureImporterCompression.Uncompressed;
        i.alphaIsTransparency = true;
        i.mipmapEnabled       = false;
        i.maxTextureSize      = 512;
    }

    static void ConfigureBg(TextureImporter i)
    {
        i.textureType         = TextureImporterType.Sprite;
        i.spriteImportMode    = SpriteImportMode.Single;
        i.spritePivot         = new Vector2(0.5f, 0.5f);
        i.spritePixelsPerUnit = 100f;
        i.filterMode          = FilterMode.Bilinear;
        i.textureCompression  = TextureImporterCompression.CompressedHQ;
        i.alphaIsTransparency = false;
        i.mipmapEnabled       = false;
        i.maxTextureSize      = 2048;
    }
}
