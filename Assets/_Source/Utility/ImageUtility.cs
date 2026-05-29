using System;
using UnityEngine;

public class ImageUtility {
    public Sprite StringToImage(string str) {
        byte[] imageBytes = Convert.FromBase64String(str);
        Texture2D tex = new Texture2D(2,2);
        tex.LoadImage(imageBytes);
        Sprite sprite = Sprite.Create(tex,new Rect(0.0f,0.0f,tex.width,tex.height),new Vector2(0.5f,0.5f),100.0f);
        return sprite;
    }

    private byte[] CompressTexture(Texture2D texture,int quality) {
        // Create a new texture with the same dimensions as the original
        Texture2D compressedTexture = new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);

        // Copy the pixels from the original texture to the new texture
        compressedTexture.SetPixels(texture.GetPixels());

        // Compress the new texture with the desired quality
        byte[] bytes = compressedTexture.EncodeToJPG(quality);

        return bytes;
    }

    public string GetBase64ImageData(Texture2D screenshot) {
        // Encode the screenshot to a PNG byte array
        byte[] pngBytes = screenshot.EncodeToPNG();

        // Convert the byte array to a Base64 string
        string base64String = Convert.ToBase64String(pngBytes);

        return base64String;
    }

    public string GetBase64ImageData(Sprite sprite) {
        // Get the sprite's texture data as a PNG byte array
        //byte[] pngData = sprite.texture.EncodeToPNG();
        byte[] pngData = GetUnCompressedTexture(sprite.texture).EncodeToPNG();

        // Convert the byte array to a base64 string
        string base64String = Convert.ToBase64String(pngData);

        return base64String;
    }

    private Texture2D GetUnCompressedTexture(Texture2D compressedTexture) {
        // Create a new uncompressed texture with the same dimensions
        Texture2D uncompressedTexture = new Texture2D(compressedTexture.width,compressedTexture.height,TextureFormat.RGBA32,false);

        // Set the pixels of the uncompressed texture to the pixels of the compressed texture
        uncompressedTexture.SetPixels(compressedTexture.GetPixels());

        // Apply changes to the uncompressed texture
        uncompressedTexture.Apply();

        // Encode the uncompressed texture to PNG
        return uncompressedTexture;
    }
}//ImageUtility class end.