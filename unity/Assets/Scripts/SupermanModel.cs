using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Joint references for the procedurally built Superman. The body is an
    /// articulated hierarchy (action-figure style) posed by SupermanAnimator.
    /// </summary>
    public class SupermanRig
    {
        public Transform visualRoot;    // rotated by flight controller (pitch/bank)
        public Transform pelvis, torso, neck, head;
        public Transform shoulderL, shoulderR, elbowL, elbowR;
        public Transform hipL, hipR, kneeL, kneeR;
        public Transform handL, handR, bootL, bootR;
        public Transform eyeL, eyeR;      // heat vision beam origins
        public CapeSimulation cape;
    }

    /// <summary>
    /// Builds the New 52-styled Superman entirely from procedural meshes:
    /// armored blue suit with micro-weave and panel piping, chest emblem,
    /// red cape with the glyph on the back, red boots, red belt with gold
    /// accents, dark full head of hair over a squared jaw. No trunks.
    /// </summary>
    public static class SupermanModel
    {
        // Shared materials (created once).
        private static Material suitMat, capeMat, bootMat, beltMat, goldMat,
                                skinMat, hairMat, emblemMat, eyeMat, browMat;

        private static void EnsureMaterials()
        {
            if (suitMat != null) return;
            suitMat = ProceduralTextures.Standard(Color.white, 0.18f, 0.48f, ProceduralTextures.SuitTexture());
            capeMat = ProceduralTextures.Standard(Color.white, 0.02f, 0.28f, ProceduralTextures.CapeTexture());
            bootMat = ProceduralTextures.Standard(ProceduralTextures.BootRed, 0.10f, 0.55f);
            beltMat = ProceduralTextures.Standard(ProceduralTextures.BootRed, 0.12f, 0.50f);
            goldMat = ProceduralTextures.Standard(ProceduralTextures.Gold, 0.85f, 0.72f);
            skinMat = ProceduralTextures.Standard(ProceduralTextures.Skin, 0.0f, 0.32f);
            hairMat = ProceduralTextures.Standard(ProceduralTextures.Hair, 0.05f, 0.62f);
            emblemMat = ProceduralTextures.Cutout(ProceduralTextures.EmblemTexture(), 0.5f);
            eyeMat = ProceduralTextures.Standard(Color.white, 0f, 0.85f, ProceduralTextures.EyeTexture());
            browMat = ProceduralTextures.Standard(ProceduralTextures.Hair, 0f, 0.3f);
        }

        private static float S(float a, float b, float x)
        {
            float t = Mathf.Clamp01(Mathf.InverseLerp(a, b, x));
            return t * t * (3f - 2f * t);
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public static SupermanRig Build(Transform parent)
        {
            EnsureMaterials();
            var rig = new SupermanRig();
            const int SEG = 24;

            rig.visualRoot = Joint("Visual", parent, Vector3.zero);
            var root = rig.visualRoot;

            // ----------------------------------------------------------------
            // Pelvis & belt (New 52: no trunks; red belt, gold buckle/studs)
            // ----------------------------------------------------------------
            rig.pelvis = Joint("Pelvis", root, new Vector3(0f, 1.06f, 0f));
            {
                var mb = new MeshBuilder();
                var rings = new List<Vector3[]>
                {
                    MeshBuilder.Ring(new Vector3(0f, -0.20f, 0f), 0.150f, 0.115f, SEG, 2.4f),
                    MeshBuilder.Ring(new Vector3(0f, -0.13f, 0f), 0.185f, 0.130f, SEG, 2.6f),
                    MeshBuilder.Ring(new Vector3(0f, -0.05f, 0f), 0.175f, 0.122f, SEG, 2.5f),
                    MeshBuilder.Ring(new Vector3(0f, 0.02f, 0f), 0.168f, 0.118f, SEG, 2.4f)
                };
                mb.AddLoft(rings, true, true);
                MeshBuilder.Spawn("PelvisMesh", rig.pelvis, mb.Build("Pelvis"), suitMat, Vector3.zero);

                // Belt band
                var belt = new MeshBuilder();
                belt.AddLoft(new List<Vector3[]>
                {
                    MeshBuilder.Ring(new Vector3(0f, -0.045f, 0f), 0.187f, 0.135f, SEG, 2.6f),
                    MeshBuilder.Ring(new Vector3(0f, 0.005f, 0f), 0.183f, 0.132f, SEG, 2.6f)
                }, true, true);
                MeshBuilder.Spawn("Belt", rig.pelvis, belt.Build("Belt"), beltMat, Vector3.zero);

                // Gold buckle + studs around the front of the band
                var buckle = new MeshBuilder();
                buckle.AddBox(new Vector3(0f, -0.02f, 0.138f), new Vector3(0.072f, 0.048f, 0.022f));
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int k = 1; k <= 2; k++)
                    {
                        float a = side * k * 0.55f;
                        buckle.AddBox(new Vector3(Mathf.Sin(a) * 0.175f, -0.02f, Mathf.Cos(a) * 0.128f),
                                      new Vector3(0.028f, 0.03f, 0.02f));
                    }
                }
                MeshBuilder.Spawn("Buckle", rig.pelvis, buckle.Build("Buckle"), goldMat, Vector3.zero);
            }

            // ----------------------------------------------------------------
            // Torso: V-taper, chest projection, trap taper into the neck
            // ----------------------------------------------------------------
            rig.torso = Joint("Torso", root, new Vector3(0f, 1.08f, 0f));
            {
                var mb = new MeshBuilder();
                var rings = new List<Vector3[]>
                {
                    MeshBuilder.Ring(new Vector3(0f, 0.00f, 0f), 0.165f, 0.115f, SEG, 2.4f),
                    MeshBuilder.Ring(new Vector3(0f, 0.10f, 0.004f), 0.185f, 0.125f, SEG, 2.6f),
                    MeshBuilder.Ring(new Vector3(0f, 0.24f, 0.014f), 0.225f, 0.140f, SEG, 2.7f),
                    MeshBuilder.Ring(new Vector3(0f, 0.34f, 0.020f), 0.255f, 0.150f, SEG, 2.8f),
                    MeshBuilder.Ring(new Vector3(0f, 0.44f, 0.006f), 0.263f, 0.145f, SEG, 2.8f),
                    MeshBuilder.Ring(new Vector3(0f, 0.50f, 0f), 0.20f, 0.105f, SEG, 2.4f),
                    MeshBuilder.Ring(new Vector3(0f, 0.535f, 0f), 0.115f, 0.082f, SEG, 2.2f)
                };
                mb.AddLoft(rings, true, true);
                MeshBuilder.Spawn("TorsoMesh", rig.torso, mb.Build("Torso"), suitMat, Vector3.zero);

                // Low armored collar ring (New 52 detail)
                var collar = new MeshBuilder();
                collar.AddLoft(new List<Vector3[]>
                {
                    MeshBuilder.Ring(new Vector3(0f, 0.525f, 0f), 0.085f, 0.072f, SEG, 2.2f),
                    MeshBuilder.Ring(new Vector3(0f, 0.555f, 0f), 0.078f, 0.068f, SEG, 2.2f)
                }, false, true);
                MeshBuilder.Spawn("Collar", rig.torso, collar.Build("Collar"), suitMat, Vector3.zero);

                // Chest emblem: a curved decal strip conforming to the chest.
                var em = new MeshBuilder();
                const int emSeg = 10;
                float emW = 0.335f, emH = 0.245f, yBot = 0.20f;
                for (int i = 0; i < emSeg; i++)
                {
                    float t0 = i / (float)emSeg, t1 = (i + 1) / (float)emSeg;
                    float a0 = (t0 - 0.5f) * 1.35f, a1 = (t1 - 0.5f) * 1.35f;
                    // Wrap around the chest and pull slightly off the surface;
                    // the top edge tilts back to follow the upper-chest taper.
                    Vector3 b0 = new Vector3(Mathf.Sin(a0) * emW * 0.62f, yBot, Mathf.Cos(a0) * 0.168f + 0.020f);
                    Vector3 b1 = new Vector3(Mathf.Sin(a1) * emW * 0.62f, yBot, Mathf.Cos(a1) * 0.168f + 0.020f);
                    Vector3 up = new Vector3(0f, emH, -0.012f);
                    em.AddQuad(b0, b1, b1 + up, b0 + up,
                        new Vector2(t0, 0f), new Vector2(t1, 0f), new Vector2(t1, 1f), new Vector2(t0, 1f));
                }
                MeshBuilder.Spawn("Emblem", rig.torso, em.Build("Emblem"), emblemMat, Vector3.zero, false);
            }

            // ----------------------------------------------------------------
            // Neck & head (squared jaw, chin, dark hair, face details)
            // ----------------------------------------------------------------
            rig.neck = Joint("Neck", rig.torso, new Vector3(0f, 0.52f, 0f));
            {
                var mb = new MeshBuilder();
                mb.AddLoft(new List<Vector3[]>
                {
                    MeshBuilder.Ring(new Vector3(0f, 0.0f, 0.01f), 0.056f, 0.060f, 16, 2.1f),
                    MeshBuilder.Ring(new Vector3(0f, 0.09f, 0.012f), 0.052f, 0.056f, 16, 2.1f)
                }, false, false);
                MeshBuilder.Spawn("NeckMesh", rig.neck, mb.Build("Neck"), skinMat, Vector3.zero);
            }
            rig.head = Joint("Head", rig.neck, new Vector3(0f, 0.06f, 0f));
            BuildHead(rig.head);
            rig.eyeL = Joint("EyeL", rig.head, new Vector3(-0.040f, 0.177f, 0.112f));
            rig.eyeR = Joint("EyeR", rig.head, new Vector3(0.040f, 0.177f, 0.112f));

            // ----------------------------------------------------------------
            // Arms (bare hands; deltoid caps; bicep/forearm shaping)
            // ----------------------------------------------------------------
            rig.shoulderL = BuildArm(rig.torso, -1, out rig.elbowL, out rig.handL);
            rig.shoulderR = BuildArm(rig.torso, +1, out rig.elbowR, out rig.handR);

            // ----------------------------------------------------------------
            // Legs & boots
            // ----------------------------------------------------------------
            rig.hipL = BuildLeg(rig.pelvis, -1, out rig.kneeL, out rig.bootL);
            rig.hipR = BuildLeg(rig.pelvis, +1, out rig.kneeR, out rig.bootR);

            // ----------------------------------------------------------------
            // Cape: cloth-simulated, pinned across the shoulder blades
            // ----------------------------------------------------------------
            var anchorL = Joint("CapeAnchorL", rig.torso, new Vector3(-0.13f, 0.475f, -0.105f));
            var anchorR = Joint("CapeAnchorR", rig.torso, new Vector3(0.13f, 0.475f, -0.105f));
            var capeGO = new GameObject("Cape");
            capeGO.transform.SetParent(parent, false);
            rig.cape = capeGO.AddComponent<CapeSimulation>();
            rig.cape.Init(anchorL, anchorR, rig.visualRoot, capeMat);

            return rig;
        }

        // --------------------------------------------------------------------
        private static void BuildHead(Transform headJoint)
        {
            float r = 0.118f;
            Vector3 c = new Vector3(0f, 0.155f, 0.012f);

            System.Func<Vector3, Vector3> faceDeform = p =>
            {
                float ny = p.y / r, nz = p.z / r, nax = Mathf.Abs(p.x) / r;
                p.x *= 0.90f;
                p.y *= 1.07f;
                // Squared jaw: widen the lower front half.
                float jaw = S(-0.05f, -0.62f, ny) * S(-0.15f, 0.45f, nz);
                p.x *= 1f + 0.17f * jaw;
                // Strong chin: bottom-front-center pushed forward and down.
                float chin = S(-0.45f, -0.95f, ny) * S(0.25f, 0.85f, nz) * S(0.45f, 0.08f, nax);
                p.z += 0.030f * chin;
                p.y -= 0.010f * chin;
                // Flatter face plane, fuller back of skull.
                if (nz > 0.55f && ny > -0.1f) p.z -= 0.012f * S(0.55f, 1f, nz);
                if (nz < -0.3f) p.z -= 0.008f * S(-0.3f, -1f, nz);
                return p;
            };

            var mb = new MeshBuilder();
            mb.AddSphere(c, r, 20, 24, faceDeform);
            MeshBuilder.Spawn("Skull", headJoint, mb.Build("Head"), skinMat, Vector3.zero);

            // Full head of dark hair: a slightly inflated shell clipped below
            // the hairline (short at the sides/back, volume on top, subtle
            // widow's peak at the front).
            var hair = new MeshBuilder();
            hair.AddSphere(c, r * 1.055f, 20, 24,
                p =>
                {
                    p = faceDeform(p);
                    // A touch of volume on top/back.
                    if (p.y > 0f) p.y *= 1.06f;
                    if (p.z < 0f) p.z *= 1.05f;
                    return p;
                },
                qc =>
                {
                    float ny = qc.y / r, nz = qc.z / r, nax = Mathf.Abs(qc.x) / r;
                    // Hairline height by direction: low at the back, high at the face.
                    float front = S(-0.2f, 0.8f, nz);
                    float line = Mathf.Lerp(-0.25f, 0.48f, front);
                    // Widow's peak: dip the front-center hairline.
                    if (nz > 0.4f && nax < 0.22f) line -= 0.14f;
                    // Sideburns.
                    if (nax > 0.72f && nz > 0.1f && ny > -0.25f && ny < 0.2f) return false;
                    return qc.y / r < line; // true = skip (no hair)
                });
            MeshBuilder.Spawn("Hair", headJoint, hair.Build("Hair"), hairMat, Vector3.zero);

            // Eyes, brows, nose, mouth - small geo details that read at close range.
            var eyes = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 e = c + new Vector3(side * 0.040f, 0.022f, 0.100f);
                Vector3 rt = new Vector3(0.019f, 0f, side * -0.004f);
                Vector3 up = new Vector3(0f, 0.013f, 0f);
                eyes.AddQuad(e - rt - up, e + rt - up, e + rt + up, e - rt + up,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up);
            }
            MeshBuilder.Spawn("Eyes", headJoint, eyes.Build("Eyes"), eyeMat, Vector3.zero, false);

            var brows = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                brows.AddBox(c + new Vector3(side * 0.042f, 0.047f, 0.103f),
                             new Vector3(0.046f, 0.009f, 0.012f));
            }
            MeshBuilder.Spawn("Brows", headJoint, brows.Build("Brows"), browMat, Vector3.zero, false);

            var nose = new MeshBuilder();
            nose.AddBox(c + new Vector3(0f, -0.005f, 0.115f), new Vector3(0.022f, 0.042f, 0.026f));
            MeshBuilder.Spawn("Nose", headJoint, nose.Build("Nose"), skinMat, Vector3.zero, false);

            var mouth = new MeshBuilder();
            mouth.AddBox(c + new Vector3(0f, -0.052f, 0.104f), new Vector3(0.034f, 0.006f, 0.008f));
            var mouthMat = ProceduralTextures.Standard(new Color(0.55f, 0.32f, 0.28f), 0f, 0.3f);
            MeshBuilder.Spawn("Mouth", headJoint, mouth.Build("Mouth"), mouthMat, Vector3.zero, false);
        }

        // --------------------------------------------------------------------
        private static Transform BuildArm(Transform torso, int side, out Transform elbow, out Transform hand)
        {
            var shoulder = Joint(side < 0 ? "ShoulderL" : "ShoulderR", torso,
                new Vector3(side * 0.235f, 0.455f, 0f));

            // Deltoid cap
            var delt = new MeshBuilder();
            delt.AddSphere(new Vector3(side * 0.012f, -0.01f, 0f), 0.085f, 10, 14,
                p => { p.x *= 1.12f; return p; });
            MeshBuilder.Spawn("Deltoid", shoulder, delt.Build("Deltoid"), suitMat, Vector3.zero);

            // Upper arm with bicep swell
            var ua = new MeshBuilder();
            ua.AddLoft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0f, -0.03f, 0f), 0.072f, 0.072f, 16, 2.3f),
                MeshBuilder.Ring(new Vector3(0f, -0.12f, 0.008f), 0.080f, 0.082f, 16, 2.5f),
                MeshBuilder.Ring(new Vector3(0f, -0.24f, 0f), 0.062f, 0.064f, 16, 2.3f),
                MeshBuilder.Ring(new Vector3(0f, -0.31f, 0f), 0.055f, 0.056f, 16, 2.2f)
            }, false, true);
            MeshBuilder.Spawn("UpperArm", shoulder, ua.Build("UpperArm"), suitMat, Vector3.zero);

            elbow = Joint(side < 0 ? "ElbowL" : "ElbowR", shoulder, new Vector3(0f, -0.31f, 0f));
            var fa = new MeshBuilder();
            fa.AddLoft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0f, 0.0f, 0f), 0.054f, 0.055f, 16, 2.2f),
                MeshBuilder.Ring(new Vector3(0f, -0.09f, 0.004f), 0.061f, 0.063f, 16, 2.4f),
                MeshBuilder.Ring(new Vector3(0f, -0.28f, 0f), 0.040f, 0.042f, 16, 2.1f)
            }, true, true);
            MeshBuilder.Spawn("Forearm", elbow, fa.Build("Forearm"), suitMat, Vector3.zero);

            // Bare-handed fist (New 52 - no gloves)
            hand = Joint(side < 0 ? "HandL" : "HandR", elbow, new Vector3(0f, -0.30f, 0f));
            var fist = new MeshBuilder();
            fist.AddSphere(new Vector3(0f, -0.045f, 0.005f), 0.052f, 8, 10,
                p => { p.y *= 1.25f; p.z *= 0.95f; return p; });
            MeshBuilder.Spawn("Fist", hand, fist.Build("Fist"), skinMat, Vector3.zero);

            return shoulder;
        }

        // --------------------------------------------------------------------
        private static Transform BuildLeg(Transform pelvis, int side, out Transform knee, out Transform boot)
        {
            var hip = Joint(side < 0 ? "HipL" : "HipR", pelvis, new Vector3(side * 0.095f, -0.06f, 0f));

            var thigh = new MeshBuilder();
            thigh.AddLoft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0f, 0.0f, 0f), 0.096f, 0.106f, 18, 2.5f),
                MeshBuilder.Ring(new Vector3(0f, -0.18f, 0.006f), 0.088f, 0.098f, 18, 2.5f),
                MeshBuilder.Ring(new Vector3(0f, -0.38f, 0f), 0.064f, 0.068f, 18, 2.3f),
                MeshBuilder.Ring(new Vector3(0f, -0.46f, 0f), 0.057f, 0.060f, 18, 2.2f)
            }, true, true);
            MeshBuilder.Spawn("Thigh", hip, thigh.Build("Thigh"), suitMat, Vector3.zero);

            knee = Joint(side < 0 ? "KneeL" : "KneeR", hip, new Vector3(0f, -0.46f, 0f));

            // Upper shin in suit blue...
            var shin = new MeshBuilder();
            shin.AddLoft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0f, 0.0f, 0f), 0.056f, 0.059f, 18, 2.2f),
                MeshBuilder.Ring(new Vector3(0f, -0.10f, -0.006f), 0.060f, 0.066f, 18, 2.4f),
                MeshBuilder.Ring(new Vector3(0f, -0.16f, 0f), 0.052f, 0.056f, 18, 2.2f)
            }, true, true);
            MeshBuilder.Spawn("Shin", knee, shin.Build("Shin"), suitMat, Vector3.zero);

            // ...red boot from mid-shin down, with the foot.
            boot = Joint(side < 0 ? "BootL" : "BootR", knee, Vector3.zero);
            var bt = new MeshBuilder();
            bt.AddLoft(new List<Vector3[]>
            {
                MeshBuilder.Ring(new Vector3(0f, -0.14f, 0f), 0.060f, 0.064f, 18, 2.4f),
                MeshBuilder.Ring(new Vector3(0f, -0.15f, 0f), 0.062f, 0.066f, 18, 2.4f), // boot lip
                MeshBuilder.Ring(new Vector3(0f, -0.26f, -0.004f), 0.052f, 0.058f, 18, 2.4f),
                MeshBuilder.Ring(new Vector3(0f, -0.38f, 0f), 0.045f, 0.050f, 18, 2.3f),
                MeshBuilder.Ring(new Vector3(0f, -0.44f, 0f), 0.044f, 0.048f, 18, 2.3f)
            }, true, true);
            // Foot: top flush with the ankle loft, sole at ground level.
            bt.AddBox(new Vector3(0f, -0.49f, 0.055f), new Vector3(0.085f, 0.10f, 0.25f));
            MeshBuilder.Spawn("Boot", boot, bt.Build("Boot"), bootMat, Vector3.zero);

            return hip;
        }
    }
}
