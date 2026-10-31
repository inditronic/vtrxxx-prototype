using UnityEngine;
using UnityEditor;
using System.IO;

namespace Ghostclub.Editor
{
    public static class GhostclubPBRTextureGenerator
    {
        public static void GeneratePBRTextures()
        {
            string texDir = "Assets/GhostclubWorld/Textures";
            if (!Directory.Exists(texDir)) Directory.CreateDirectory(texDir);

            // 1. Generate Concrete Normal Map (512x512)
            string normalPath = $"{texDir}/Concrete_Normal.png";
            if (!File.Exists(normalPath))
            {
                int size = 512;
                Texture2D normalTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float[,] heightMap = new float[size, size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float nx = (float)x / size;
                        float ny = (float)y / size;

                        float n1 = Mathf.PerlinNoise(nx * 12f, ny * 12f);
                        float n2 = Mathf.PerlinNoise(nx * 48f, ny * 48f) * 0.4f;
                        float n3 = Mathf.PerlinNoise(nx * 96f, ny * 96f) * 0.2f;

                        float boardLine = Mathf.Sin(ny * Mathf.PI * 32f);
                        float boardSeam = (boardLine > 0.95f) ? 0.3f : 0f;

                        heightMap[x, y] = n1 + n2 + n3 + boardSeam;
                    }
                }

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int xL = (x - 1 + size) % size;
                        int xR = (x + 1) % size;
                        int yD = (y - 1 + size) % size;
                        int yU = (y + 1) % size;

                        float dX = (heightMap[xR, y] - heightMap[xL, y]) * 4.5f;
                        float dY = (heightMap[x, yU] - heightMap[x, yD]) * 4.5f;

                        Vector3 normal = new Vector3(-dX, -dY, 1.0f).normalized;
                        Color normalColor = new Color(
                            normal.x * 0.5f + 0.5f,
                            normal.y * 0.5f + 0.5f,
                            normal.z * 0.5f + 0.5f,
                            1.0f
                        );
                        normalTex.SetPixel(x, y, normalColor);
                    }
                }
                normalTex.Apply();

                File.WriteAllBytes(normalPath, normalTex.EncodeToPNG());
                AssetDatabase.ImportAsset(normalPath, ImportAssetOptions.ForceUpdate);
            }

            ConfigureNormalMap(normalPath);
            ConfigureNormalMap($"{texDir}/FunktionOne_Grille_Normal.png");
            ConfigureTexture($"{texDir}/CDJ_Screen_Waveform.png", 2048);
            ConfigureTexture($"{texDir}/DJM_Mixer_Faceplate.png", 2048);

            // Configure 4K Pioneer UDJ-1000 Textures
            ConfigureTexture($"{texDir}/UDJ-1000_MainColor.png", 4096);
            ConfigureTexture($"{texDir}/UDJ-1000_Emission.png", 4096);
            ConfigureTexture($"{texDir}/UDJ-1000_Metallic.png", 4096, false);
            ConfigureTexture($"{texDir}/UDJ-1000_AlphaMask.png", 2048, false);

            // Configure 6 Underground Party Poster Textures
            for (int i = 1; i <= 6; i++)
            {
                ConfigureTexture($"{texDir}/Poster_Party_0{i}.png", 2048);
            }

            AssetDatabase.Refresh();
            Debug.Log("[Ghostclub] PBR, 4K Pioneer DJ, and Party Poster textures verified.");
        }

        static void ConfigureNormalMap(string path)
        {
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureTexture(string path, int maxSize = 2048, bool sRGB = true)
        {
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; changed = true; }
                if (importer.sRGBTexture != sRGB) { importer.sRGBTexture = sRGB; changed = true; }
                if (importer.maxTextureSize != maxSize) { importer.maxTextureSize = maxSize; changed = true; }
                if (importer.wrapMode != TextureWrapMode.Clamp) { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
        }
    }
}
