using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Simple pedestrians: they stroll the streets, scatter screaming (well,
    /// visually) from explosions, tumble if bowled over, and pick themselves
    /// back up. Effectively indestructible - they're here to react, not die.
    /// </summary>
    public class CivilianNPC : Damageable
    {
        private Vector3 home;
        private Vector3 target;
        private float repickT;
        private float fleeT;
        private Vector3 fleeDir;
        private float downT;
        private Rigidbody rb;
        private float wanderRadius = 18f;
        public Vector3 wanderAxis = new Vector3(0.3f, 0f, 1f);

        public static CivilianNPC Spawn(Vector3 pos)
        {
            var go = new GameObject("Civilian");
            go.transform.position = pos;

            Color shirt = Color.HSVToRGB(Random.value, Random.Range(0.35f, 0.8f), Random.Range(0.45f, 0.9f));
            var bodyMat = ProceduralTextures.Standard(shirt, 0f, 0.3f);
            var pantsMat = ProceduralTextures.Standard(shirt * 0.4f, 0f, 0.3f);
            var skinMat = ProceduralTextures.Standard(new Color(0.85f, 0.66f, 0.52f), 0f, 0.3f);

            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0f, 1.15f, 0f), new Vector3(0.38f, 0.55f, 0.22f)); // torso
            MeshBuilder.Spawn("Torso", go.transform, mb.Build("CivTorso"), bodyMat, Vector3.zero);

            var lb = new MeshBuilder();
            lb.AddBox(new Vector3(-0.09f, 0.44f, 0f), new Vector3(0.15f, 0.88f, 0.17f));
            lb.AddBox(new Vector3(0.09f, 0.44f, 0f), new Vector3(0.15f, 0.88f, 0.17f));
            MeshBuilder.Spawn("Legs", go.transform, lb.Build("CivLegs"), pantsMat, Vector3.zero);

            var hb = new MeshBuilder();
            hb.AddSphere(new Vector3(0f, 1.58f, 0f), 0.115f, 8, 10);
            MeshBuilder.Spawn("Head", go.transform, hb.Build("CivHead"), skinMat, Vector3.zero);

            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.7f;
            col.radius = 0.28f;
            col.center = new Vector3(0f, 0.85f, 0f);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 75f;
            rb.isKinematic = true;

            var npc = go.AddComponent<CivilianNPC>();
            npc.maxHp = 999999f;   // sandbox dummies: unkillable, just reactive
            npc.hp = npc.maxHp;
            npc.rb = rb;
            npc.home = pos;

            foreach (var r in go.GetComponentsInChildren<Renderer>())
                Registry.Highlights.Add(new HighlightEntry(r, new Color(0.3f, 1f, 0.5f)));
            Registry.Civilians.Add(npc);
            return npc;
        }

        public void Flee(Vector3 from, float radius)
        {
            if (Vector3.Distance(transform.position, from) > radius) return;
            fleeT = 4.5f;
            Vector3 away = transform.position - from;
            away.y = 0f;
            fleeDir = away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere;
            fleeDir.y = 0f;
        }

        protected override void Update()
        {
            base.Update();
            if (Frozen) return;

            if (downT > 0f)
            {
                downT -= Time.deltaTime;
                if (downT <= 0f)
                {
                    rb.isKinematic = true;
                    Vector3 p = transform.position;
                    transform.position = new Vector3(p.x, 0f, p.z);
                    transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    Flee(transform.position - transform.forward, 999f); // run from whatever hit us
                }
                return;
            }

            float speed;
            Vector3 to;
            if (fleeT > 0f)
            {
                fleeT -= Time.deltaTime;
                to = fleeDir;
                speed = 6.5f;
            }
            else
            {
                repickT -= Time.deltaTime;
                if (repickT <= 0f || Vector3.Distance(transform.position, target) < 0.8f)
                {
                    repickT = Random.Range(4f, 9f);
                    Vector2 r = Random.insideUnitCircle * wanderRadius;
                    target = home + Vector3.Scale(new Vector3(r.x, 0f, r.y), wanderAxis);
                }
                to = target - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude < 0.5f) return;
                to.Normalize();
                speed = 1.7f;
            }

            Quaternion face = Quaternion.LookRotation(to, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 5f * Time.deltaTime);
            transform.position += transform.forward * speed * Time.deltaTime;
            float bob = Mathf.Abs(Mathf.Sin(Time.time * (fleeT > 0f ? 11f : 5f))) * 0.04f;
            transform.position = new Vector3(transform.position.x, bob, transform.position.z);
        }

        protected override void OnDamaged(float amount, DamageKind kind, Vector3 point, Vector3 dir, float impulse)
        {
            hp = maxHp; // never dies
            if (impulse > 2f)
            {
                downT = 3.5f;
                Registry.ScareAt(transform.position, 18f);
            }
        }
    }
}
