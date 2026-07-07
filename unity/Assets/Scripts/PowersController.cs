using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Heat vision (hold Q): twin eye beams to the crosshair - damages,
    /// heats and ignites. Freeze breath (hold F): a frost cone - douses
    /// fires and encases things in ice. X-ray vision (toggle X): buildings
    /// fade to a translucent shell while NPCs and props glow through walls.
    /// Super strength (E): grab the nearest prop, walk/fly with it overhead,
    /// LMB to hurl it.
    /// </summary>
    public class PowersController : MonoBehaviour
    {
        private FlightController fc;
        private SupermanRig rig;
        private Camera cam;

        // Heat vision
        private LineRenderer beamL, beamR;
        private Light beamLight;
        private float heatTickFx;
        public bool HeatActive { get; private set; }

        // Freeze breath
        private ParticleSystem breathPS;
        public bool FreezeActive { get; private set; }

        // X-ray
        public bool XRayOn { get; private set; }
        private Material xrayBuildingMat;
        private readonly List<Material> savedOccluderMats = new List<Material>();
        private readonly List<Material> savedHighlightMats = new List<Material>();
        private readonly Dictionary<Color, Material> glowMats = new Dictionary<Color, Material>();

        // Super strength carry
        private ThrowableProp held;
        public ThrowableProp Held { get { return held; } }
        public float LastThrowTime { get; private set; }

        private const float HEAT_RANGE = 320f;
        private const float HEAT_DPS = 55f;
        private const float HEAT_BUILD = 85f;      // heat accumulation per second
        private const float FREEZE_RANGE = 14f;
        private const float FREEZE_ANGLE = 32f;
        private const float FREEZE_BUILD = 110f;
        private const float GRAB_RANGE = 4.5f;

        public void Init(FlightController controller, SupermanRig r, Camera camera)
        {
            fc = controller;
            rig = r;
            cam = camera;
            BuildBeams();
            BuildBreath();
        }

        private void BuildBeams()
        {
            beamL = MakeBeam("HeatBeamL");
            beamR = MakeBeam("HeatBeamR");
            var go = new GameObject("HeatImpactLight");
            beamLight = go.AddComponent<Light>();
            beamLight.type = LightType.Point;
            beamLight.color = new Color(1f, 0.45f, 0.1f);
            beamLight.range = 9f;
            beamLight.intensity = 0f;
        }

        private LineRenderer MakeBeam(string name)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = 0.055f;
            lr.endWidth = 0.10f;
            lr.numCapVertices = 3;
            Shader s = Shader.Find("Legacy Shaders/Particles/Additive");
            if (s == null) s = Shader.Find("Sprites/Default");
            lr.material = new Material(s);
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.85f, 0.55f), 0f),
                    new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 1f) });
            lr.colorGradient = grad;
            lr.enabled = false;
            return lr;
        }

        private void BuildBreath()
        {
            var go = new GameObject("FreezeBreath");
            go.transform.SetParent(rig.head, false);
            go.transform.localPosition = new Vector3(0f, 0.10f, 0.14f);
            breathPS = go.AddComponent<ParticleSystem>();
            breathPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = breathPS.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.96f, 1f, 0.8f), new Color(0.55f, 0.8f, 1f, 0.6f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 16f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.maxParticles = 900;

            var em = breathPS.emission;
            em.rateOverTime = 420f;
            em.enabled = false;

            var shape = breathPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.06f;
            // Cone emits along +Z of its transform; head +Z is forward. Good.

            Shader s = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (s == null) s = Shader.Find("Sprites/Default");
            breathPS.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(s);
        }

        private void Update()
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            UpdateHeat(locked);
            UpdateFreeze(locked);
            UpdateXRay(locked);
            UpdateGrab(locked);
        }

        // --- Heat vision ------------------------------------------------------
        private void UpdateHeat(bool locked)
        {
            HeatActive = locked && Input.GetKey(KeyCode.Q) && held == null;
            beamL.enabled = HeatActive;
            beamR.enabled = HeatActive;
            if (!HeatActive)
            {
                beamLight.intensity = 0f;
                return;
            }

            // Aim from the camera crosshair.
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Vector3 end = ray.origin + ray.direction * HEAT_RANGE;
            RaycastHit hit;
            bool hitSomething = false;
            var playerRoot = Registry.Player != null ? Registry.Player.root : null;
            RaycastHit[] hits = Physics.RaycastAll(ray, HEAT_RANGE);
            float best = float.MaxValue;
            RaycastHit bestHit = new RaycastHit();
            for (int i = 0; i < hits.Length; i++)
            {
                if (playerRoot != null && hits[i].transform.root == playerRoot) continue;
                if (hits[i].distance < best) { best = hits[i].distance; bestHit = hits[i]; hitSomething = true; }
            }
            if (hitSomething)
            {
                hit = bestHit;
                end = hit.point;
                beamLight.transform.position = hit.point + hit.normal * 0.4f;
                beamLight.intensity = 3.5f + Mathf.PingPong(Time.time * 14f, 1.5f);

                var d = hit.collider.GetComponentInParent<Damageable>();
                if (d != null)
                {
                    d.AddHeat(HEAT_BUILD * Time.deltaTime);
                    d.ApplyDamage(HEAT_DPS * Time.deltaTime, DamageKind.Heat, hit.point, ray.direction);
                }

                heatTickFx -= Time.deltaTime;
                if (heatTickFx <= 0f)
                {
                    heatTickFx = 0.08f;
                    Fx.Burst(hit.point, new Color(1f, 0.55f, 0.15f), 3, 2.5f, 0.12f, 0.3f);
                }
            }
            else
            {
                beamLight.intensity = 0f;
            }

            beamL.SetPosition(0, rig.eyeL.position);
            beamL.SetPosition(1, end);
            beamR.SetPosition(0, rig.eyeR.position);
            beamR.SetPosition(1, end);
        }

        // --- Freeze breath ----------------------------------------------------
        private void UpdateFreeze(bool locked)
        {
            bool want = locked && Input.GetKey(KeyCode.F) && held == null;
            if (want != FreezeActive)
            {
                FreezeActive = want;
                var em = breathPS.emission;
                em.enabled = want;
                if (want) breathPS.Play();
                else breathPS.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            if (!FreezeActive) return;

            // Aim the emitter along the camera's view.
            breathPS.transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);

            Vector3 mouth = breathPS.transform.position;
            Vector3 dir = cam.transform.forward;
            Collider[] near = Physics.OverlapSphere(mouth + dir * FREEZE_RANGE * 0.5f, FREEZE_RANGE * 0.62f);
            var seen = new HashSet<Damageable>();
            var playerRoot = Registry.Player != null ? Registry.Player.root : null;
            for (int i = 0; i < near.Length; i++)
            {
                if (playerRoot != null && near[i].transform.root == playerRoot) continue;
                var d = near[i].GetComponentInParent<Damageable>();
                if (d == null || !seen.Add(d)) continue;
                Vector3 to = d.transform.position + Vector3.up * 0.8f - mouth;
                if (to.magnitude > FREEZE_RANGE) continue;
                if (Vector3.Angle(dir, to) > FREEZE_ANGLE) continue;
                d.AddCold(FREEZE_BUILD * Time.deltaTime);
            }
        }

        // --- X-ray vision -----------------------------------------------------
        private void UpdateXRay(bool locked)
        {
            if (locked && Input.GetKeyDown(KeyCode.X)) SetXRay(!XRayOn);
        }

        private Material GlowMat(Color c)
        {
            Material m;
            if (glowMats.TryGetValue(c, out m)) return m;
            Shader s = Shader.Find("LastSon/XRayGlow");
            if (s == null) s = Shader.Find("Sprites/Default");
            m = new Material(s);
            m.SetColor("_Color", c);
            glowMats[c] = m;
            return m;
        }

        private void SetXRay(bool on)
        {
            XRayOn = on;
            if (on)
            {
                if (xrayBuildingMat == null)
                {
                    Shader s = Shader.Find("LastSon/XRayBuilding");
                    if (s == null) s = Shader.Find("Sprites/Default");
                    xrayBuildingMat = new Material(s);
                    xrayBuildingMat.SetColor("_Color", new Color(0.35f, 0.6f, 1f, 0.10f));
                }
                savedOccluderMats.Clear();
                for (int i = 0; i < Registry.XRayOccluders.Count; i++)
                {
                    var r = Registry.XRayOccluders[i];
                    if (r == null) { savedOccluderMats.Add(null); continue; }
                    savedOccluderMats.Add(r.sharedMaterial);
                    r.sharedMaterial = xrayBuildingMat;
                }
                savedHighlightMats.Clear();
                for (int i = 0; i < Registry.Highlights.Count; i++)
                {
                    var h = Registry.Highlights[i];
                    if (h.renderer == null) { savedHighlightMats.Add(null); continue; }
                    savedHighlightMats.Add(h.renderer.sharedMaterial);
                    h.renderer.sharedMaterial = GlowMat(h.color);
                }
                RenderSettings.fogDensity = 0.0002f;
            }
            else
            {
                for (int i = 0; i < Registry.XRayOccluders.Count && i < savedOccluderMats.Count; i++)
                {
                    var r = Registry.XRayOccluders[i];
                    if (r != null && savedOccluderMats[i] != null) r.sharedMaterial = savedOccluderMats[i];
                }
                for (int i = 0; i < Registry.Highlights.Count && i < savedHighlightMats.Count; i++)
                {
                    var h = Registry.Highlights[i];
                    if (h.renderer != null && savedHighlightMats[i] != null) h.renderer.sharedMaterial = savedHighlightMats[i];
                }
                RenderSettings.fogDensity = 0.00085f;
            }
        }

        // --- Super strength: grab & throw ---------------------------------------
        private void UpdateGrab(bool locked)
        {
            if (!locked) return;

            if (held != null)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    // HURL along the camera aim.
                    var prop = held;
                    held = null;
                    LastThrowTime = Time.time;
                    prop.Throw(cam.transform.forward * 42f + Vector3.up * 4f + fc.Velocity * 0.5f);
                    if (Registry.Cam != null) Registry.Cam.Kick(0.2f);
                }
                else if (Input.GetKeyDown(KeyCode.E))
                {
                    var prop = held;
                    held = null;
                    prop.Drop();
                }
                else if (held.Dead)
                {
                    held = null;
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                // Find the nearest prop in reach.
                Collider[] near = Physics.OverlapSphere(transform.position + Vector3.up, GRAB_RANGE);
                ThrowableProp best = null;
                float bestDist = float.MaxValue;
                for (int i = 0; i < near.Length; i++)
                {
                    var p = near[i].GetComponentInParent<ThrowableProp>();
                    if (p == null || p.Dead || p.Held || p.Frozen) continue;
                    float dd = Vector3.Distance(transform.position, p.transform.position);
                    if (dd < bestDist) { bestDist = dd; best = p; }
                }
                if (best != null)
                {
                    held = best;
                    best.PickUp(rig.visualRoot);
                }
            }
        }
    }
}
