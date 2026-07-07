using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Procedural mesh construction: lofted bodies with superellipse cross
    /// sections (organic limbs/torsos), deformable spheres (head), boxes and
    /// quads. Everything in the prototype is built through this - there are
    /// no imported model assets.
    /// </summary>
    public class MeshBuilder
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                            Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// <summary>
        /// A cross-section ring. 'squareness' blends the superellipse exponent:
        /// 2 = pure ellipse, higher = squarer, more muscular silhouette.
        /// </summary>
        public static Vector3[] Ring(Vector3 center, float halfW, float halfD, int segments,
                                     float squareness = 2.5f, float zBias = 0f)
        {
            var ring = new Vector3[segments];
            float e = 2f / squareness;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e) * halfW;
                float z = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), e) * halfD + zBias;
                ring[i] = center + new Vector3(x, 0f, z);
            }
            return ring;
        }

        /// <summary>Stitch a list of rings (equal segment counts) into a tube, optionally capped.</summary>
        public void AddLoft(List<Vector3[]> rings, bool capStart, bool capEnd)
        {
            if (rings.Count < 2) return;
            int seg = rings[0].Length;
            int baseIndex = verts.Count;
            for (int r = 0; r < rings.Count; r++)
            {
                float v = r / (float)(rings.Count - 1);
                for (int i = 0; i <= seg; i++)  // duplicate seam vertex for clean UVs
                {
                    verts.Add(rings[r][i % seg]);
                    uvs.Add(new Vector2(i / (float)seg, v));
                }
            }
            int stride = seg + 1;
            for (int r = 0; r < rings.Count - 1; r++)
            {
                for (int i = 0; i < seg; i++)
                {
                    int a = baseIndex + r * stride + i;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            if (capStart) AddCap(rings[0], true);
            if (capEnd) AddCap(rings[rings.Count - 1], false);
        }

        private void AddCap(Vector3[] ring, bool flip)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < ring.Length; i++) center += ring[i];
            center /= ring.Length;
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 a = ring[i];
                Vector3 b = ring[(i + 1) % ring.Length];
                // Rings wind CCW viewed from +Y: (center,a,b) faces -Y (bottom cap),
                // (center,b,a) faces +Y (top cap).
                if (flip) AddTriangle(center, a, b, new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
                else AddTriangle(center, b, a, new Vector2(.5f, .5f), Vector2.zero, Vector2.one);
            }
        }

        /// <summary>
        /// UV sphere with an optional per-vertex deformation callback - used to
        /// sculpt the head (jaw, chin, brow, cheekbones) and the hair shell.
        /// skipQuad lets the caller clip regions out (e.g. hair below the hairline).
        /// </summary>
        public void AddSphere(Vector3 center, float radius, int latSeg, int lonSeg,
                              Func<Vector3, Vector3> deform = null,
                              Func<Vector3, bool> skipQuad = null)
        {
            int baseIndex = verts.Count;
            var grid = new int[latSeg + 1, lonSeg + 1];
            for (int la = 0; la <= latSeg; la++)
            {
                float theta = la / (float)latSeg * Mathf.PI;   // 0 top -> PI bottom
                for (int lo = 0; lo <= lonSeg; lo++)
                {
                    float phi = lo / (float)lonSeg * Mathf.PI * 2f;
                    var p = new Vector3(
                        Mathf.Sin(theta) * Mathf.Sin(phi),
                        Mathf.Cos(theta),
                        Mathf.Sin(theta) * Mathf.Cos(phi)) * radius;
                    if (deform != null) p = deform(p);
                    grid[la, lo] = verts.Count;
                    verts.Add(center + p);
                    uvs.Add(new Vector2(lo / (float)lonSeg, 1f - la / (float)latSeg));
                }
            }
            for (int la = 0; la < latSeg; la++)
            {
                for (int lo = 0; lo < lonSeg; lo++)
                {
                    int a = grid[la, lo], b = grid[la, lo + 1];
                    int c = grid[la + 1, lo], d = grid[la + 1, lo + 1];
                    if (skipQuad != null)
                    {
                        Vector3 qc = (verts[a] + verts[b] + verts[c] + verts[d]) / 4f - center;
                        if (skipQuad(qc)) continue;
                    }
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
        }

        public void AddBox(Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3 c = center;
            Vector3[] p =
            {
                c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z),
                c + new Vector3(h.x, -h.y, h.z),   c + new Vector3(-h.x, -h.y, h.z),
                c + new Vector3(-h.x, h.y, -h.z),  c + new Vector3(h.x, h.y, -h.z),
                c + new Vector3(h.x, h.y, h.z),    c + new Vector3(-h.x, h.y, h.z)
            };
            Vector2 u0 = Vector2.zero, u1 = Vector2.right, u2 = Vector2.one, u3 = Vector2.up;
            AddQuad(p[3], p[2], p[6], p[7], u0, u1, u2, u3); // front (+z)
            AddQuad(p[1], p[0], p[4], p[5], u0, u1, u2, u3); // back
            AddQuad(p[0], p[3], p[7], p[4], u0, u1, u2, u3); // left
            AddQuad(p[2], p[1], p[5], p[6], u0, u1, u2, u3); // right
            AddQuad(p[7], p[6], p[5], p[4], u0, u1, u2, u3); // top
            AddQuad(p[0], p[1], p[2], p[3], u0, u1, u2, u3); // bottom
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Create a child GameObject rendering the built mesh.</summary>
        public static GameObject Spawn(string name, Transform parent, Mesh mesh, Material mat,
                                       Vector3 localPos, bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
