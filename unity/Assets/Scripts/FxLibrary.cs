using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// One-shot visual effects, all created from code: particle bursts,
    /// explosions with physics push and damage, impact sparks, ice shards.
    /// </summary>
    public static class Fx
    {
        private static Material additiveMat, alphaMat;

        private static Material ParticleMat(bool additive)
        {
            if (additiveMat == null)
            {
                Shader add = Shader.Find("Legacy Shaders/Particles/Additive");
                if (add == null) add = Shader.Find("Sprites/Default");
                additiveMat = new Material(add);
                Shader alpha = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                if (alpha == null) alpha = Shader.Find("Sprites/Default");
                alphaMat = new Material(alpha);
            }
            return additive ? additiveMat : alphaMat;
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed,
                                 float size, float life = 0.8f, float gravity = 0f,
                                 bool additive = true)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.gravityModifier = gravity;
            main.maxParticles = count + 8;

            var em = ps.emission;
            em.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat(additive);

            ps.Emit(count);
            Object.Destroy(go, life + 0.6f);
        }

        public static void ImpactSparks(Vector3 pos, Vector3 dir)
        {
            Burst(pos + dir * 0.1f, new Color(1f, 0.85f, 0.4f), 14, 7f, 0.14f, 0.35f);
            Burst(pos, Color.white, 6, 3f, 0.10f, 0.2f);
        }

        public static void IceShards(Vector3 pos, float scale)
        {
            Burst(pos, new Color(0.75f, 0.92f, 1f), 30, 6f * scale, 0.22f * scale, 0.7f, 1.2f, false);
            Burst(pos, Color.white, 12, 3f, 0.12f, 0.4f);
        }

        /// <summary>
        /// Explosion: fireball + smoke + light flash + shockwave sphere, then
        /// radial damage, physics push, camera kick, and civilian panic.
        /// </summary>
        public static void Explosion(Vector3 pos, float radius, float damage)
        {
            Burst(pos, new Color(1f, 0.55f, 0.15f), 44, radius * 1.9f, radius * 0.30f, 0.6f);
            Burst(pos, new Color(1f, 0.9f, 0.6f), 16, radius * 1.1f, radius * 0.18f, 0.35f);
            Burst(pos, new Color(0.25f, 0.24f, 0.23f), 22, radius * 0.9f, radius * 0.45f, 1.6f, -0.25f, false);

            // Light flash
            var flashGO = new GameObject("FxFlash");
            flashGO.transform.position = pos + Vector3.up * 1f;
            var light = flashGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = radius * 4f;
            light.intensity = 6f;
            light.color = new Color(1f, 0.7f, 0.35f);
            var fade = flashGO.AddComponent<FxFader>();
            fade.lightFade = true;
            fade.life = 0.35f;

            // Shockwave sphere
            var wave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(wave.GetComponent<Collider>());
            wave.transform.position = pos;
            wave.transform.localScale = Vector3.one * 0.5f;
            var wmat = new Material(Shader.Find("Sprites/Default"));
            wmat.color = new Color(1f, 0.8f, 0.5f, 0.5f);
            wave.GetComponent<MeshRenderer>().sharedMaterial = wmat;
            var wfade = wave.AddComponent<FxFader>();
            wfade.growTo = radius * 2.2f;
            wfade.life = 0.4f;

            // Damage + physics push (dedupe multi-collider objects).
            Collider[] hits = Physics.OverlapSphere(pos, radius);
            var seen = new System.Collections.Generic.HashSet<Damageable>();
            for (int i = 0; i < hits.Length; i++)
            {
                var d = hits[i].GetComponentInParent<Damageable>();
                if (d != null && seen.Add(d))
                {
                    float dist = Vector3.Distance(pos, d.transform.position);
                    float t = 1f - Mathf.Clamp01(dist / radius);
                    Vector3 dir = (d.transform.position - pos).normalized + Vector3.up * 0.5f;
                    d.ApplyDamage(damage * (0.3f + 0.7f * t), DamageKind.Explosion,
                                  d.transform.position, dir.normalized, 6f + 10f * t);
                }
                var rb = hits[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    rb.AddExplosionForce(damage * 40f, pos, radius, 1.0f, ForceMode.Impulse);
                }
            }

            Registry.ScareAt(pos, radius * 5f);
            if (Registry.Cam != null)
            {
                float camDist = Vector3.Distance(Registry.Cam.transform.position, pos);
                Registry.Cam.Kick(Mathf.Clamp01(1f - camDist / 90f) * 1.2f);
            }
        }

        /// <summary>Looping flame effect attached to a burning object.</summary>
        public static GameObject AttachFire(Transform target, float scale)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(target, false);
            go.transform.localPosition = Vector3.up * 0.4f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.6f, 0.15f), new Color(1f, 0.3f, 0.05f));
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 2.6f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.8f * scale);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            var em = ps.emission;
            em.rateOverTime = 45f * scale;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f * scale;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = ParticleMat(true);
            return go;
        }
    }

    /// <summary>Tiny helper that fades/grows one-shot effect objects, then destroys them.</summary>
    public class FxFader : MonoBehaviour
    {
        public float life = 0.4f;
        public float growTo = 0f;
        public bool lightFade = false;
        private float t;
        private float startIntensity = -1f;
        private Vector3 startScale;
        private Material mat;

        private void Start()
        {
            startScale = transform.localScale;
            var mr = GetComponent<MeshRenderer>();
            if (mr != null) mat = mr.sharedMaterial;
            var l = GetComponent<Light>();
            if (l != null) startIntensity = l.intensity;
        }

        private void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            if (growTo > 0f)
            {
                transform.localScale = Vector3.Lerp(startScale, Vector3.one * growTo, 1f - (1f - k) * (1f - k));
            }
            if (mat != null)
            {
                Color c = mat.color;
                c.a = (1f - k) * 0.5f;
                mat.color = c;
            }
            if (lightFade && startIntensity > 0f)
            {
                var l = GetComponent<Light>();
                if (l != null) l.intensity = startIntensity * (1f - k);
            }
            if (t >= life) Destroy(gameObject);
        }
    }
}
