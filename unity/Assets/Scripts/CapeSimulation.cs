using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Verlet cloth cape: a point grid pinned across the shoulder blades,
    /// blown by airflow opposite to flight velocity, with flutter turbulence
    /// and a capsule collision keeping it off the back. The mesh is built
    /// double-sided so it needs no special shader.
    /// </summary>
    public class CapeSimulation : MonoBehaviour
    {
        private const int W = 13;         // points across
        private const int H = 18;         // points down
        private const float LENGTH = 1.22f;
        private const float TOP_WIDTH = 0.36f;
        private const float BOTTOM_WIDTH = 1.00f;

        private Transform anchorL, anchorR, body;
        private Vector3[] pos, prev;
        private float[] restV;            // vertical rest length per row gap
        private float restHBase;
        private Mesh mesh;
        private Vector3[] meshVerts;

        /// <summary>World-space velocity of the wearer; set by the flight controller.</summary>
        public Vector3 BodyVelocity { get; set; }

        public void Init(Transform anchorLeft, Transform anchorRight, Transform bodyRoot, Material mat)
        {
            anchorL = anchorLeft;
            anchorR = anchorRight;
            body = bodyRoot;

            pos = new Vector3[W * H];
            prev = new Vector3[W * H];
            restV = new float[H];
            for (int r = 0; r < H; r++) restV[r] = LENGTH / (H - 1);
            restHBase = 1f / (W - 1);

            // Start hanging straight down from the anchors.
            for (int r = 0; r < H; r++)
            {
                float t = r / (float)(H - 1);
                float width = Mathf.Lerp(TOP_WIDTH, BOTTOM_WIDTH, Mathf.Pow(t, 0.85f));
                for (int c = 0; c < W; c++)
                {
                    float u = c / (float)(W - 1) - 0.5f;
                    Vector3 top = Vector3.Lerp(anchorL.position, anchorR.position, c / (float)(W - 1));
                    pos[r * W + c] = top + Vector3.down * (t * LENGTH) + body.right * (u * (width - TOP_WIDTH));
                    prev[r * W + c] = pos[r * W + c];
                }
            }

            // Double-sided mesh: front sheet + back sheet with flipped winding.
            mesh = new Mesh { name = "Cape" };
            meshVerts = new Vector3[W * H * 2];
            var uvs = new Vector2[W * H * 2];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                {
                    var uv = new Vector2(c / (float)(W - 1), r / (float)(H - 1));
                    uvs[r * W + c] = uv;
                    uvs[W * H + r * W + c] = uv;
                }
            int quadCount = (W - 1) * (H - 1);
            var tris = new int[quadCount * 12];
            int ti = 0;
            for (int r = 0; r < H - 1; r++)
            {
                for (int c = 0; c < W - 1; c++)
                {
                    int a = r * W + c, b = a + 1, d = a + W, e = d + 1;
                    tris[ti++] = a; tris[ti++] = d; tris[ti++] = b;
                    tris[ti++] = b; tris[ti++] = d; tris[ti++] = e;
                    int o = W * H;
                    tris[ti++] = o + a; tris[ti++] = o + b; tris[ti++] = o + d;
                    tris[ti++] = o + b; tris[ti++] = o + e; tris[ti++] = o + d;
                }
            }
            mesh.vertices = meshVerts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.MarkDynamic();

            var mf = GetComponent<MeshFilter>();
            if (mf == null)
                mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = GetComponent<MeshRenderer>();
            if (mr == null)
                mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private void LateUpdate()
        {
            if (anchorL == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            if (dt <= 0f) return;

            Simulate(dt);
            UpdateMesh();
        }

        private void Simulate(float dt)
        {
            // Relative airflow: still air minus our velocity, plus flutter.
            Vector3 airflow = -BodyVelocity;
            float speed = BodyVelocity.magnitude;
            float flutterAmp = 0.8f + speed * 0.10f;

            Vector3 gravity = new Vector3(0f, -11f, 0f);
            float t = Time.time;

            for (int r = 1; r < H; r++)   // row 0 is pinned
            {
                for (int c = 0; c < W; c++)
                {
                    int i = r * W + c;
                    float rowT = r / (float)(H - 1);
                    Vector3 flutter = new Vector3(
                        Mathf.Sin(t * 9f + c * 1.1f + r * 0.6f),
                        Mathf.Sin(t * 7.3f + r * 0.9f),
                        Mathf.Cos(t * 8.1f + c * 0.7f)) * (flutterAmp * rowT);
                    Vector3 accel = gravity + airflow * (2.6f * rowT) + flutter;

                    Vector3 p = pos[i];
                    Vector3 vel = (p - prev[i]) * 0.985f;
                    prev[i] = p;
                    pos[i] = p + vel + accel * (dt * dt);
                }
            }

            // Pin the top row across the shoulders.
            for (int c = 0; c < W; c++)
            {
                pos[c] = Vector3.Lerp(anchorL.position, anchorR.position, c / (float)(W - 1));
                prev[c] = pos[c];
            }

            // Distance constraints (structural), a few relaxation passes.
            for (int pass = 0; pass < 4; pass++)
            {
                for (int r = 0; r < H; r++)
                {
                    float rowT = r / (float)(H - 1);
                    float width = Mathf.Lerp(TOP_WIDTH, BOTTOM_WIDTH, Mathf.Pow(rowT, 0.85f));
                    float restH = width * restHBase;
                    for (int c = 0; c < W - 1; c++)
                        Constrain(r * W + c, r * W + c + 1, restH, r == 0);
                }
                for (int r = 0; r < H - 1; r++)
                    for (int c = 0; c < W; c++)
                        Constrain(r * W + c, (r + 1) * W + c, restV[r], r == 0);
            }

            // Keep the cloth off the torso: push out of a capsule along the spine.
            Vector3 spineA = body.TransformPoint(new Vector3(0f, 1.05f, -0.02f));
            Vector3 spineB = body.TransformPoint(new Vector3(0f, 1.55f, -0.02f));
            const float bodyR = 0.24f;
            for (int i = W; i < pos.Length; i++)
            {
                Vector3 cp = ClosestOnSeg(pos[i], spineA, spineB);
                Vector3 d = pos[i] - cp;
                float m = d.magnitude;
                if (m < bodyR && m > 1e-5f) pos[i] = cp + d * (bodyR / m);
            }
        }

        private void Constrain(int i, int j, float rest, bool iPinned)
        {
            Vector3 d = pos[j] - pos[i];
            float m = d.magnitude;
            if (m < 1e-6f) return;
            float diff = (m - rest) / m;
            if (iPinned)
            {
                pos[j] -= d * diff;
            }
            else
            {
                pos[i] += d * (diff * 0.5f);
                pos[j] -= d * (diff * 0.5f);
            }
        }

        private static Vector3 ClosestOnSeg(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        private void UpdateMesh()
        {
            // The cape object stays at the world origin; vertices are world-space.
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            int n = W * H;
            for (int i = 0; i < n; i++)
            {
                meshVerts[i] = pos[i];
                meshVerts[n + i] = pos[i];
            }
            mesh.vertices = meshVerts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
