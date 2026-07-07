using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Training robot: wanders its home patch, staggers and tumbles under
    /// hits, glows and sparks under heat vision, freezes solid (then shatters
    /// beautifully under a punch), and finally collapses in smoke.
    /// </summary>
    public class RobotNPC : Damageable
    {
        private Vector3 home;
        private Vector3 target;
        private float repickT;
        private float staggerT;
        private Rigidbody rb;
        private Renderer[] bodyRenderers;
        private Material bodyMat;
        private float flashT;
        private float wanderRadius = 12f;
        // Wander offsets are scaled by this so street NPCs stay on their road
        // (default: free along z, tight along x).
        public Vector3 wanderAxis = new Vector3(0.35f, 0f, 1f);

        public static RobotNPC Spawn(Vector3 pos)
        {
            var go = new GameObject("Robot");
            go.transform.position = pos;

            // Boxy mech body, built procedurally like everything else.
            var bodyMat = ProceduralTextures.Standard(new Color(0.38f, 0.40f, 0.46f), 0.55f, 0.55f);
            var jointMat = ProceduralTextures.Standard(new Color(0.22f, 0.23f, 0.27f), 0.4f, 0.4f);
            var eyeMat = ProceduralTextures.Standard(new Color(0.9f, 0.15f, 0.1f), 0f, 0.6f);
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.SetColor("_EmissionColor", new Color(0.9f, 0.1f, 0.05f));

            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0f, 1.25f, 0f), new Vector3(0.62f, 0.62f, 0.36f));  // torso
            mb.AddBox(new Vector3(0f, 0.85f, 0f), new Vector3(0.40f, 0.22f, 0.28f));  // waist
            mb.AddBox(new Vector3(-0.44f, 1.28f, 0f), new Vector3(0.20f, 0.52f, 0.24f)); // arm L
            mb.AddBox(new Vector3(0.44f, 1.28f, 0f), new Vector3(0.20f, 0.52f, 0.24f));  // arm R
            MeshBuilder.Spawn("Body", go.transform, mb.Build("RobotBody"), bodyMat, Vector3.zero);

            var jb = new MeshBuilder();
            jb.AddBox(new Vector3(-0.15f, 0.38f, 0f), new Vector3(0.20f, 0.78f, 0.24f)); // leg L
            jb.AddBox(new Vector3(0.15f, 0.38f, 0f), new Vector3(0.20f, 0.78f, 0.24f));  // leg R
            jb.AddBox(new Vector3(0f, 1.72f, 0f), new Vector3(0.30f, 0.26f, 0.26f));     // head
            MeshBuilder.Spawn("Joints", go.transform, jb.Build("RobotJoints"), jointMat, Vector3.zero);

            var eb = new MeshBuilder();
            eb.AddBox(new Vector3(0f, 1.74f, 0.13f), new Vector3(0.20f, 0.05f, 0.03f)); // eye strip
            MeshBuilder.Spawn("Eye", go.transform, eb.Build("RobotEye"), eyeMat, Vector3.zero, false);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.9f;
            col.radius = 0.38f;
            col.center = new Vector3(0f, 0.95f, 0f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 260f;
            rb.isKinematic = true;

            var npc = go.AddComponent<RobotNPC>();
            npc.maxHp = 120f;
            npc.hp = 120f;
            npc.rb = rb;
            npc.home = pos;
            npc.bodyRenderers = go.GetComponentsInChildren<Renderer>();
            npc.bodyMat = bodyMat;

            foreach (var r in npc.bodyRenderers)
                Registry.Highlights.Add(new HighlightEntry(r, new Color(1f, 0.25f, 0.2f)));
            return npc;
        }

        protected override void Update()
        {
            base.Update();
            if (Dead || Frozen) return;

            if (flashT > 0f)
            {
                flashT -= Time.deltaTime;
                bodyMat.color = Color.Lerp(new Color(0.38f, 0.40f, 0.46f), Color.white, flashT * 4f);
            }

            if (staggerT > 0f)
            {
                staggerT -= Time.deltaTime;
                if (staggerT <= 0f) Recover();
                return;
            }

            // Wander.
            repickT -= Time.deltaTime;
            if (repickT <= 0f || Vector3.Distance(transform.position, target) < 0.7f)
            {
                repickT = Random.Range(3f, 7f);
                Vector2 r = Random.insideUnitCircle * wanderRadius;
                target = home + Vector3.Scale(new Vector3(r.x, 0f, r.y), wanderAxis);
            }
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.4f)
            {
                Quaternion face = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, face, 3f * Time.deltaTime);
                transform.position += transform.forward * 2.2f * Time.deltaTime;
                // Walk bob.
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 6f)) * 0.05f;
                transform.position = new Vector3(transform.position.x, bob, transform.position.z);
            }
        }

        protected override void OnDamaged(float amount, DamageKind kind, Vector3 point, Vector3 dir, float impulse)
        {
            flashT = 0.25f;
            Fx.ImpactSparks(point, dir);
            if (impulse > 3f && !Dead)
            {
                staggerT = 1.6f;   // knocked into physics; recover after
            }
        }

        protected override void OnHeated(float amount)
        {
            // Heat vision cooks robots directly.
            ApplyDamage(amount * 0.5f, DamageKind.Heat, transform.position + Vector3.up * 1.2f, Vector3.up);
            if (Random.value < 0.15f)
                Fx.Burst(transform.position + Vector3.up * 1.3f, new Color(1f, 0.6f, 0.2f), 4, 2f, 0.12f, 0.3f);
        }

        private void Recover()
        {
            if (Dead || Frozen) return;
            rb.isKinematic = true;
            // Snap upright at ground level.
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, 0f, p.z);
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        protected override void OnDeath(DamageKind kind)
        {
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.AddTorque(Random.onUnitSphere * 60f, ForceMode.VelocityChange);
            }
            bodyMat.color = new Color(0.14f, 0.14f, 0.16f);
            Fx.Burst(transform.position + Vector3.up * 1.2f, new Color(1f, 0.7f, 0.3f), 26, 5f, 0.2f, 0.6f);
            Fx.Burst(transform.position + Vector3.up * 1.4f, new Color(0.2f, 0.2f, 0.2f), 16, 1.6f, 0.5f, 1.6f, -0.2f, false);
        }
    }
}
