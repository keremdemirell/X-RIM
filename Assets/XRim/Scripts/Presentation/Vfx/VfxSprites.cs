using System;
using UnityEngine;

namespace XRim.Presentation.Vfx
{
    /// <summary>
    /// Placeholder effect shapes drawn in code, white with soft edges and tinted by the effect's colour (art may replace them in Session
    /// 17): a hex-headed bolt, a nut, a coil spring, a gear, a spark streak, a drop of fluid, a soft puff and a ring. Each sprite is one
    /// world unit wide, so its scale is its size. Owns its textures: <see cref="Dispose"/> frees them.
    /// </summary>
    public sealed class VfxSprites : IDisposable
    {
        internal const int BoltWidth = 12, BoltHeight = 28;
        internal const int NutSize = 24;
        internal const int SpringWidth = 12, SpringHeight = 28;
        internal const int GearSize = 32;
        internal const int SparkWidth = 32, SparkHeight = 8;
        internal const int DropWidth = 16, DropHeight = 22;
        internal const int PuffSize = 32;
        internal const int RingSize = 64;

        /// <summary>How many pixels an edge fades over (antialiasing).</summary>
        private const float EdgePixels = 1.2f;

        private readonly Sprite[] _sprites = new Sprite[Enum.GetValues(typeof(VfxParticleKind)).Length];
        private readonly Texture2D[] _textures = new Texture2D[Enum.GetValues(typeof(VfxParticleKind)).Length];

        public Sprite For(VfxParticleKind kind)
        {
            int index = (int)kind;
            if (_sprites[index] != null) return _sprites[index];
            switch (kind)
            {
                case VfxParticleKind.Bolt: return Make(kind, BoltWidth, BoltHeight, Bolt);
                case VfxParticleKind.Nut: return Make(kind, NutSize, NutSize, Nut);
                case VfxParticleKind.Spring: return Make(kind, SpringWidth, SpringHeight, Spring);
                case VfxParticleKind.Gear: return Make(kind, GearSize, GearSize, Gear);
                case VfxParticleKind.Spark: return Make(kind, SparkWidth, SparkHeight, Spark);
                case VfxParticleKind.Drop: return Make(kind, DropWidth, DropHeight, Drop);
                case VfxParticleKind.Ring: return Make(kind, RingSize, RingSize, Ring);
                default: return Make(kind, PuffSize, PuffSize, Puff); // a puff and a flash share the soft disc
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                if (_sprites[i] != null) UnityEngine.Object.Destroy(_sprites[i]);
                if (_textures[i] != null) UnityEngine.Object.Destroy(_textures[i]);
                _sprites[i] = null;
                _textures[i] = null;
            }
        }

        private Sprite Make(VfxParticleKind kind, int width, int height, Func<float, float, float, float, float> coverage)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Pixel centres in [-1, 1] across each axis; the shape function also gets the size of a pixel for its soft edge.
                    float u = (x + 0.5f) / width * 2f - 1f;
                    float v = (y + 0.5f) / height * 2f - 1f;
                    float alpha = Mathf.Clamp01(coverage(u, v, 2f / width, 2f / height));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            _textures[(int)kind] = texture;
            _sprites[(int)kind] = sprite;
            return sprite;
        }

        /// <summary>1 inside, 0 outside, fading over the edge: inside when the distance is below 0.</summary>
        private static float Edge(float signedDistance, float pixel) => Mathf.Clamp01(0.5f - signedDistance / (pixel * EdgePixels));

        private static float Box(float u, float v, float halfWidth, float halfHeight, float pu, float pv) =>
            Mathf.Min(Edge(Mathf.Abs(u) - halfWidth, pu), Edge(Mathf.Abs(v) - halfHeight, pv));

        /// <summary>A hexagon (flat top and bottom) of this circumradius.</summary>
        private static float Hexagon(float u, float v, float radius, float pixel)
        {
            float x = Mathf.Abs(u), y = Mathf.Abs(v);
            float distance = Mathf.Max(x * 0.866025f + y * 0.5f, y) - radius * 0.866025f;
            return Edge(distance, pixel);
        }

        private static float Bolt(float u, float v, float pu, float pv)
        {
            float head = v > 0.55f ? Hexagon(u, (v - 0.775f) * 2.4f, 0.95f, pu) : 0f;
            float shaft = v <= 0.6f ? Box(u, v + 0.2f, 0.42f, 0.8f, pu, pv) : 0f;
            bool thread = v < 0.45f && Mathf.Repeat((v + 1f) * 9f, 1f) < 0.35f;
            return Mathf.Max(head, shaft * (thread ? 0.55f : 1f));
        }

        private static float Nut(float u, float v, float pu, float pv)
        {
            float hole = Edge(0.38f - Mathf.Sqrt(u * u + v * v), pu);
            return Mathf.Min(Hexagon(u, v, 0.95f, pu), hole);
        }

        private static float Spring(float u, float v, float pu, float pv)
        {
            // A zigzag coil: the wire's x follows a triangle wave down the spring's length.
            float wave = Mathf.Abs(Mathf.Repeat(v * 3f, 2f) - 1f) * 1.6f - 0.8f;
            float wire = Edge(Mathf.Abs(u - wave) - 0.22f, pu);
            return Mathf.Abs(v) < 0.95f ? wire : 0f;
        }

        private static float Gear(float u, float v, float pu, float pv)
        {
            float radius = Mathf.Sqrt(u * u + v * v);
            float angle = Mathf.Atan2(v, u);
            float rim = Mathf.Cos(angle * 8f) > 0.2f ? 0.95f : 0.72f;
            return Mathf.Min(Edge(radius - rim, pu), Edge(0.25f - radius, pu));
        }

        private static float Spark(float u, float v, float pu, float pv)
        {
            float along = 1f - u * u;
            float across = 1f - Mathf.Abs(v);
            return Mathf.Clamp01(along) * Mathf.Clamp01(across * 1.5f);
        }

        private static float Drop(float u, float v, float pu, float pv)
        {
            // A round bottom tapering to a point at the top.
            float bulb = Edge(Mathf.Sqrt(u * u + (v + 0.35f) * (v + 0.35f)) - 0.62f, pu);
            float taper = v > -0.35f ? Edge(Mathf.Abs(u) - 0.62f * (1f - (v + 0.35f) / 1.35f), pu) : 0f;
            return Mathf.Max(bulb, taper);
        }

        private static float Puff(float u, float v, float pu, float pv)
        {
            float fade = 1f - Mathf.Sqrt(u * u + v * v);
            return fade > 0f ? fade * fade : 0f;
        }

        private static float Ring(float u, float v, float pu, float pv)
        {
            float band = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.82f);
            return Mathf.Clamp01(1f - band / 0.12f);
        }
    }
}
