using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// A procedural Metropolis: a street grid of textured tower blocks with
    /// rooftop clutter, taller spires downtown, and a Daily Planet-style
    /// globe tower at the center. Everything is colliding geometry.
    /// </summary>
    public static class CityGenerator
    {
        private const float BLOCK = 90f;      // block pitch (m)
        private const int HALF_BLOCKS = 11;   // city extends +/- this many blocks

        public static void Build(Transform parent)
        {
            var rng = new System.Random(2026);

            // Ground: one big plane, textured so each block tile shows roads.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            float citySize = (HALF_BLOCKS * 2 + 2) * BLOCK;
            ground.transform.localScale = new Vector3(citySize / 10f, 1f, citySize / 10f);
            var gmat = ProceduralTextures.Standard(Color.white, 0f, 0.12f, ProceduralTextures.GroundTexture());
            gmat.mainTextureScale = new Vector2(citySize / BLOCK, citySize / BLOCK);
            // Half-tile shift so the roads run BETWEEN blocks, not through
            // the building centers.
            gmat.mainTextureOffset = new Vector2(0.5f, 0.5f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gmat;

            // Facade material variants.
            var mats = new Material[]
            {
                ProceduralTextures.Standard(Color.white, 0.05f, 0.35f, ProceduralTextures.BuildingTexture(1, new Color(0.42f, 0.44f, 0.50f))),
                ProceduralTextures.Standard(Color.white, 0.05f, 0.30f, ProceduralTextures.BuildingTexture(2, new Color(0.55f, 0.50f, 0.46f))),
                ProceduralTextures.Standard(Color.white, 0.10f, 0.55f, ProceduralTextures.BuildingTexture(3, new Color(0.34f, 0.42f, 0.52f))),
                ProceduralTextures.Standard(Color.white, 0.05f, 0.30f, ProceduralTextures.BuildingTexture(4, new Color(0.48f, 0.42f, 0.40f))),
                ProceduralTextures.Standard(Color.white, 0.15f, 0.62f, ProceduralTextures.BuildingTexture(5, new Color(0.30f, 0.36f, 0.48f)))
            };
            var roofMat = ProceduralTextures.Standard(new Color(0.22f, 0.22f, 0.24f), 0f, 0.2f);

            for (int gx = -HALF_BLOCKS; gx <= HALF_BLOCKS; gx++)
            {
                for (int gz = -HALF_BLOCKS; gz <= HALF_BLOCKS; gz++)
                {
                    if (gx == 0 && gz == 0) continue; // reserved for the landmark
                    float cx = gx * BLOCK, cz = gz * BLOCK;

                    // Taller downtown, falling off toward the edges.
                    float centerT = 1f - Mathf.Clamp01(
                        Mathf.Sqrt(gx * gx + gz * gz) / (HALF_BLOCKS * 1.05f));
                    int count = 1 + rng.Next(3);
                    for (int i = 0; i < count; i++)
                    {
                        float w = 18f + (float)rng.NextDouble() * 26f;
                        float d = 18f + (float)rng.NextDouble() * 26f;
                        float h = (18f + (float)rng.NextDouble() * 70f)
                                  + centerT * centerT * (float)rng.NextDouble() * 160f;
                        float ox = ((float)rng.NextDouble() - 0.5f) * (BLOCK - w - 24f);
                        float oz = ((float)rng.NextDouble() - 0.5f) * (BLOCK - d - 24f);

                        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        b.name = "Bldg";
                        b.transform.SetParent(parent, false);
                        b.transform.position = new Vector3(cx + ox, h / 2f, cz + oz);
                        b.transform.localScale = new Vector3(w, h, d);
                        var mr = b.GetComponent<MeshRenderer>();
                        mr.sharedMaterial = mats[rng.Next(mats.Length)];
                        mr.material.mainTextureScale = new Vector2(Mathf.Max(1f, w / 22f), Mathf.Max(1f, h / 34f));
                        Registry.XRayOccluders.Add(mr);

                        // Rooftop clutter on larger towers.
                        if (h > 60f)
                        {
                            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            box.transform.SetParent(parent, false);
                            box.transform.position = new Vector3(cx + ox + w * 0.15f, h + 2.2f, cz + oz);
                            box.transform.localScale = new Vector3(w * 0.3f, 4.4f, d * 0.3f);
                            box.GetComponent<MeshRenderer>().sharedMaterial = roofMat;
                            Registry.XRayOccluders.Add(box.GetComponent<MeshRenderer>());
                        }
                        if (h > 120f)
                        {
                            var ant = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            ant.transform.SetParent(parent, false);
                            ant.transform.position = new Vector3(cx + ox, h + 9f, cz + oz);
                            ant.transform.localScale = new Vector3(1.2f, 18f, 1.2f);
                            ant.GetComponent<MeshRenderer>().sharedMaterial = roofMat;
                            Registry.XRayOccluders.Add(ant.GetComponent<MeshRenderer>());
                        }
                    }
                }
            }

            BuildLandmark(parent, mats[2]);
        }

        /// <summary>The globe tower downtown - an unmissable navigation landmark.</summary>
        private static void BuildLandmark(Transform parent, Material facade)
        {
            float h = 250f;
            var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tower.name = "PlanetTower";
            tower.transform.SetParent(parent, false);
            tower.transform.position = new Vector3(0f, h / 2f, 0f);
            tower.transform.localScale = new Vector3(46f, h, 46f);
            var mr = tower.GetComponent<MeshRenderer>();
            mr.sharedMaterial = facade;
            mr.material.mainTextureScale = new Vector2(2f, 8f);
            Registry.XRayOccluders.Add(mr);

            var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crown.transform.SetParent(parent, false);
            crown.transform.position = new Vector3(0f, h + 4f, 0f);
            crown.transform.localScale = new Vector3(30f, 8f, 30f);
            crown.GetComponent<MeshRenderer>().sharedMaterial =
                ProceduralTextures.Standard(new Color(0.25f, 0.25f, 0.28f), 0.2f, 0.4f);

            var globe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            globe.name = "Globe";
            globe.transform.SetParent(parent, false);
            globe.transform.position = new Vector3(0f, h + 22f, 0f);
            globe.transform.localScale = Vector3.one * 26f;
            var globeMat = ProceduralTextures.Standard(ProceduralTextures.Gold, 0.9f, 0.75f);
            globeMat.EnableKeyword("_EMISSION");
            globeMat.SetColor("_EmissionColor", new Color(0.35f, 0.26f, 0.05f));
            globe.GetComponent<MeshRenderer>().sharedMaterial = globeMat;
        }
    }
}
