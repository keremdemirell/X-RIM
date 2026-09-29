using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using XRim.Simulation.Unity2D;

namespace XRim.Editor
{
    /// <summary>
    /// Creates the primitive placeholder sprites as PNG assets (once) and loads them: a white square, a white circle,
    /// and the yellow-and-black crash-test calibration marker that shows the hit zones (GDD §2, Decided). Each sprite
    /// is one world unit across. Existing files are reused, so art can replace them in place.
    /// </summary>
    internal static class PlaceholderSprites
    {
        private const int SquareSizePixels = 4;
        private const int RoundSizePixels = 64;
        private const float EdgeSoftnessPixels = 1f;

        private static readonly Color MarkerYellow = new Color(1f, 0.82f, 0.05f);
        private static readonly Color MarkerBlack = new Color(0.08f, 0.08f, 0.08f);
        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        private static string SquarePath => EditorPaths.PlaceholderArtFolder + "/Square.png";
        private static string CirclePath => EditorPaths.PlaceholderArtFolder + "/Circle.png";
        private static string MarkerPath => EditorPaths.PlaceholderArtFolder + "/CalibrationMarker.png";

        public static RagdollSprites LoadOrCreate()
        {
            EditorPaths.EnsureFolder(EditorPaths.PlaceholderArtFolder);
            return new RagdollSprites(
                LoadOrCreate(SquarePath, SquareSizePixels, (x, y) => Color.white),
                LoadOrCreate(CirclePath, RoundSizePixels, (x, y) => Disc(x, y, Color.white)),
                LoadOrCreate(MarkerPath, RoundSizePixels, Marker));
        }

        private static Sprite LoadOrCreate(string path, int sizePixels, Func<float, float, Color> pixel)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            var texture = new Texture2D(sizePixels, sizePixels, TextureFormat.RGBA32, false);
            for (int y = 0; y < sizePixels; y++)
            {
                for (int x = 0; x < sizePixels; x++)
                {
                    // Pixel centres in [-1, 1].
                    float u = (x + 0.5f) / sizePixels * 2f - 1f;
                    float v = (y + 0.5f) / sizePixels * 2f - 1f;
                    texture.SetPixel(x, y, pixel(u, v));
                }
            }

            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = sizePixels;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>A filled circle with a soft one-pixel edge.</summary>
        private static Color Disc(float u, float v, Color color)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float edge = EdgeSoftnessPixels * 2f / RoundSizePixels;
            float alpha = Mathf.Clamp01((1f - radius) / edge);
            return alpha <= 0f ? Clear : new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>The classic calibration marker: a disc in four quadrants, yellow and black in turn.</summary>
        private static Color Marker(float u, float v) => Disc(u, v, (u >= 0f) == (v >= 0f) ? MarkerYellow : MarkerBlack);
    }
}
