using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// All textures are generated at startup: the suit's kryptonian
    /// micro-weave and panel piping, the chest emblem (rasterized as thick
    /// strokes so the glyph stays clean at any resolution), the cape cloth,
    /// building windows, and the street grid.
    /// </summary>
    public static class ProceduralTextures
    {
        // --- New 52 palette -------------------------------------------------
        public static readonly Color SuitBlue = new Color(0.115f, 0.185f, 0.50f);
        public static readonly Color SuitBlueDeep = new Color(0.07f, 0.115f, 0.34f);
        public static readonly Color CapeRed = new Color(0.62f, 0.07f, 0.09f);
        public static readonly Color BootRed = new Color(0.66f, 0.08f, 0.10f);
        public static readonly Color EmblemRed = new Color(0.78f, 0.09f, 0.10f);
        public static readonly Color Gold = new Color(0.86f, 0.66f, 0.16f);
        public static readonly Color Skin = new Color(0.86f, 0.64f, 0.50f);
        public static readonly Color Hair = new Color(0.045f, 0.05f, 0.075f);

        // ---------------------------------------------------------------------
        // Suit body texture: base blue + micro-weave noise + darker panel piping
        // (the angled New 52 seam lines). Tiled cylindrically around the torso
        // and limbs.
        // ---------------------------------------------------------------------
        public static Texture2D SuitTexture(int size = 512)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Kryptonian micro-weave: interference of two fine diagonal waves.
                    float weave =
                        Mathf.Sin((u + v) * size * 0.55f) * 0.5f +
                        Mathf.Sin((u - v) * size * 0.55f) * 0.5f;
                    float shade = 1f + weave * 0.045f;
                    Color c = SuitBlue * shade;

                    // Panel piping: chevron seams sweeping down toward the belt.
                    float chev = Mathf.Abs(Mathf.Repeat(v * 3f + Mathf.Abs(u - 0.5f) * 1.6f, 1f) - 0.5f);
                    if (chev < 0.022f) c = Color.Lerp(c, SuitBlueDeep, 0.85f);
                    // Vertical side seams.
                    float seam = Mathf.Min(Mathf.Abs(u - 0.06f), Mathf.Abs(u - 0.56f));
                    if (seam < 0.008f) c = Color.Lerp(c, SuitBlueDeep, 0.7f);

                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return MakeTex(px, size, "SuitTex");
        }

        // ---------------------------------------------------------------------
        // Emblem: diamond shield + angular S, drawn as thick round-capped
        // strokes (robust and clean, in the spirit of the angular New 52 glyph;
        // tweak the segment list to taste). Returns RGBA with transparent
        // background for use as a cutout decal.
        // ---------------------------------------------------------------------

        // Shield outline in emblem space: x in [-0.5,0.5], y in [-0.5,0.5], y up.
        private static readonly Vector2[] ShieldPoly =
        {
            new Vector2(-0.46f, 0.30f), new Vector2(0.46f, 0.30f),
            new Vector2(0.30f, -0.10f), new Vector2(0f, -0.48f),
            new Vector2(-0.30f, -0.10f)
        };

        // The S as thick strokes (from, to) in shield space.
        private static readonly Vector2[,] SStrokes =
        {
            { new Vector2(0.155f, 0.10f), new Vector2(0.13f, 0.185f) },   // top-right downturn
            { new Vector2(0.13f, 0.185f), new Vector2(-0.075f, 0.215f) }, // top bar
            { new Vector2(-0.075f, 0.215f), new Vector2(-0.115f, 0.09f) },// upper-left descender
            { new Vector2(-0.115f, 0.09f), new Vector2(0.085f, -0.01f) }, // middle diagonal
            { new Vector2(0.085f, -0.01f), new Vector2(0.115f, -0.125f) },// right descender
            { new Vector2(0.115f, -0.125f), new Vector2(-0.09f, -0.185f)},// bottom bar
            { new Vector2(-0.09f, -0.185f), new Vector2(-0.135f, -0.10f)} // bottom-left upturn
        };
        private const float SStrokeWidth = 0.062f;

        private static bool InPoly(Vector2 p, Vector2[] poly, float scale)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i] * scale, b = poly[j] * scale;
                if ((a.y > p.y) != (b.y > p.y) &&
                    p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool OnS(Vector2 p)
        {
            for (int i = 0; i < SStrokes.GetLength(0); i++)
                if (DistToSeg(p, SStrokes[i, 0], SStrokes[i, 1]) < SStrokeWidth) return true;
            return false;
        }

        /// <summary>Sample the emblem at a point in emblem space; returns color with alpha.</summary>
        public static Color SampleEmblem(Vector2 p, bool onCape)
        {
            if (!InPoly(p, ShieldPoly, 1f)) return Color.clear;
            Color border = onCape ? new Color(0.42f, 0.045f, 0.06f) : EmblemRed;
            Color field = onCape ? CapeRed * 0.92f : SuitBlueDeep * 0.9f;
            Color glyph = onCape ? new Color(0.40f, 0.04f, 0.055f) : EmblemRed;
            if (!InPoly(p, ShieldPoly, 0.86f)) return border;
            if (OnS(p)) return glyph;
            return field;
        }

        public static Texture2D EmblemTexture(int size = 512)
        {
            var px = new Color32[size * size];
            int ss = 3; // supersampling for smooth edges
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            var p = new Vector2(
                                (x + (sx + 0.5f) / ss) / size - 0.5f,
                                (y + (sy + 0.5f) / ss) / size - 0.5f);
                            acc += SampleEmblem(p, false);
                        }
                    acc /= ss * ss;
                    px[y * size + x] = acc;
                }
            }
            return MakeTex(px, size, "EmblemTex");
        }

        // ---------------------------------------------------------------------
        // Cape: red weave with a darker hem and the shield glyph across the
        // upper back. UV space: u across the cape, v = 0 at the shoulders.
        // ---------------------------------------------------------------------
        public static Texture2D CapeTexture(int size = 512)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size; // v: 0 top
                    float weave = Mathf.Sin(u * 140f) * Mathf.Sin(v * 170f);
                    Color c = CapeRed * (1f + weave * 0.035f);
                    // Darker hem at the bottom and edges.
                    float edge = Mathf.Min(Mathf.Min(u, 1f - u) * 6f, (1f - v) * 5f);
                    c = Color.Lerp(CapeRed * 0.55f, c, Mathf.Clamp01(edge + 0.55f));
                    // Shield glyph on the upper back, subtle tone-on-tone.
                    var ep = new Vector2((u - 0.5f) / 0.62f, (0.26f - v) / 0.62f);
                    Color em = SampleEmblem(ep, true);
                    if (em.a > 0.01f) c = Color.Lerp(c, em, 0.8f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return MakeTex(px, size, "CapeTex");
        }

        // ---------------------------------------------------------------------
        // Buildings & ground
        // ---------------------------------------------------------------------
        public static Texture2D BuildingTexture(int seed, Color facade, int size = 256)
        {
            var rng = new System.Random(seed);
            var px = new Color32[size * size];
            Color dark = facade * 0.55f;
            int cols = 10, rows = 14;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    px[y * size + x] = facade * (0.92f + 0.08f * ((x * 7 + y * 13) % 17) / 17f);
                }
            }
            for (int wy = 0; wy < rows; wy++)
            {
                for (int wx = 0; wx < cols; wx++)
                {
                    bool lit = rng.NextDouble() < 0.28;
                    Color win = lit ? new Color(1f, 0.87f, 0.55f) : new Color(0.25f, 0.32f, 0.42f) * (float)(0.7 + rng.NextDouble() * 0.5);
                    int x0 = (int)((wx + 0.22f) / cols * size), x1 = (int)((wx + 0.82f) / cols * size);
                    int y0 = (int)((wy + 0.25f) / rows * size), y1 = (int)((wy + 0.78f) / rows * size);
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                            px[y * size + x] = win;
                }
            }
            // floor slabs
            for (int wy = 0; wy <= rows; wy++)
            {
                int y = Mathf.Min(size - 1, (int)(wy / (float)rows * size));
                for (int x = 0; x < size; x++) px[y * size + x] = dark;
            }
            return MakeTex(px, size, "BuildingTex" + seed);
        }

        public static Texture2D GroundTexture(int size = 512)
        {
            var px = new Color32[size * size];
            Color asphalt = new Color(0.16f, 0.165f, 0.18f);
            Color sidewalk = new Color(0.34f, 0.34f, 0.35f);
            Color line = new Color(0.75f, 0.72f, 0.55f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // One tile = one city block: road margins on all sides.
                    float du = Mathf.Min(u, 1f - u), dv = Mathf.Min(v, 1f - v);
                    Color c;
                    if (du < 0.11f || dv < 0.11f)
                    {
                        c = asphalt * (0.95f + 0.05f * ((x * 3 + y * 5) % 13) / 13f);
                        // center dashes
                        if ((du < 0.005f && ((int)(v * 24) % 2 == 0)) ||
                            (dv < 0.005f && ((int)(u * 24) % 2 == 0)))
                            c = line;
                    }
                    else if (du < 0.145f || dv < 0.145f) c = sidewalk;
                    else c = sidewalk * 0.82f; // block interior / plaza
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return MakeTex(px, size, "GroundTex");
        }

        public static Texture2D EyeTexture(int size = 64)
        {
            var px = new Color32[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / (size / 2f);
                    Color c;
                    if (d > 0.95f) c = new Color(0.75f, 0.62f, 0.55f);   // lid edge blends to skin
                    else if (d > 0.55f) c = new Color(0.93f, 0.92f, 0.90f); // sclera
                    else if (d > 0.22f) c = new Color(0.18f, 0.35f, 0.62f); // blue iris
                    else c = new Color(0.03f, 0.03f, 0.04f);              // pupil
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return MakeTex(px, size, "EyeTex");
        }

        private static Texture2D MakeTex(Color32[] px, int size, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name };
            tex.SetPixels32(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.Apply(true);
            return tex;
        }

        // Shared material helpers ---------------------------------------------
        public static Material Standard(Color color, float metallic, float smoothness, Texture2D tex = null)
        {
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smoothness);
            if (tex != null) m.mainTexture = tex;
            return m;
        }

        public static Material Cutout(Texture2D tex, float smoothness)
        {
            var m = new Material(Shader.Find("Standard"));
            m.SetFloat("_Mode", 1f); // cutout
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
            m.SetFloat("_Cutoff", 0.4f);
            m.SetFloat("_Glossiness", smoothness);
            m.mainTexture = tex;
            return m;
        }
    }
}
