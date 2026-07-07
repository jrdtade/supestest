using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// A physics prop Superman can punch across the street or pick up and
    /// hurl (super strength). Cars burn and explode; crates splinter;
    /// tankers go up spectacularly. Thrown props damage whatever they hit.
    /// </summary>
    public class ThrowableProp : Damageable
    {
        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public Collider[] cols;
        private bool held;
        private float thrownT;       // >0 shortly after release: impact-damage window
        private float ignoreOwnerT;

        public bool Held { get { return held; } }

        public void PickUp(Transform carrier)
        {
            if (Dead || held) return;
            held = true;
            rb.isKinematic = true;
            foreach (var c in cols) c.enabled = false;
            transform.SetParent(carrier, true);
            // Hoist overhead.
            transform.localPosition = new Vector3(0f, 2.55f, 0.25f);
            transform.localRotation = Quaternion.identity;
        }

        public void Throw(Vector3 velocity)
        {
            if (!held) return;
            held = false;
            transform.SetParent(null, true);
            rb.isKinematic = false;
            foreach (var c in cols) c.enabled = true;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.onUnitSphere * 3f;
            thrownT = 4f;
            ignoreOwnerT = 0.4f;
        }

        public void Drop()
        {
            if (!held) return;
            held = false;
            transform.SetParent(null, true);
            rb.isKinematic = false;
            foreach (var c in cols) c.enabled = true;
            rb.linearVelocity = Vector3.zero;
            ignoreOwnerT = 0.3f;
        }

        protected override void Update()
        {
            base.Update();
            if (thrownT > 0f) thrownT -= Time.deltaTime;
            if (ignoreOwnerT > 0f) ignoreOwnerT -= Time.deltaTime;
        }

        private void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < 7f) return;
            if (ignoreOwnerT > 0f && Registry.Player != null &&
                c.transform.root == Registry.Player.root) return;

            Vector3 point = c.GetContact(0).point;
            Fx.ImpactSparks(point, c.GetContact(0).normal);

            // A hurled prop is a weapon.
            if (thrownT > 0f)
            {
                var victim = c.collider.GetComponentInParent<Damageable>();
                if (victim != null && victim != this)
                {
                    victim.ApplyDamage(speed * 2.2f, DamageKind.Impact, point,
                        -c.GetContact(0).normal, Mathf.Min(speed * 0.5f, 14f));
                }
                // The projectile takes a beating too.
                ApplyDamage(speed * 1.1f, DamageKind.Impact, point, c.GetContact(0).normal);
                thrownT = 0f;
                if (Registry.Cam != null) Registry.Cam.Kick(0.3f);
            }
            else if (speed > 16f)
            {
                ApplyDamage(speed * 0.7f, DamageKind.Impact, point, c.GetContact(0).normal);
            }
        }

        protected override void OnDamaged(float amount, DamageKind kind, Vector3 point, Vector3 dir, float impulse)
        {
            if (kind == DamageKind.Punch || kind == DamageKind.Kick)
                Fx.ImpactSparks(point, dir);
        }

        protected override void OnDeath(DamageKind kind)
        {
            if (!explodesOnDeath)
            {
                // Crates and the like just burst apart.
                Fx.Burst(transform.position + Vector3.up * 0.5f, new Color(0.6f, 0.45f, 0.3f),
                         24, 5f, 0.25f, 0.8f, 1.2f, false);
            }
            Destroy(gameObject, explodesOnDeath ? 0.02f : 0.02f);
        }
    }

    /// <summary>Builders for the interactable props scattered around the test range.</summary>
    public static class PropFactory
    {
        private static readonly Color[] CarColors =
        {
            new Color(0.75f, 0.12f, 0.12f), new Color(0.15f, 0.3f, 0.6f),
            new Color(0.85f, 0.83f, 0.80f), new Color(0.15f, 0.15f, 0.17f),
            new Color(0.7f, 0.6f, 0.2f), new Color(0.25f, 0.5f, 0.35f)
        };

        public static ThrowableProp Car(Vector3 pos, float rotY)
        {
            var go = new GameObject("Car");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Color paint = CarColors[Random.Range(0, CarColors.Length)];
            var paintMat = ProceduralTextures.Standard(paint, 0.6f, 0.75f);
            var glassMat = ProceduralTextures.Standard(new Color(0.25f, 0.35f, 0.45f), 0.2f, 0.9f);
            var tireMat = ProceduralTextures.Standard(new Color(0.08f, 0.08f, 0.09f), 0f, 0.25f);

            var body = new MeshBuilder();
            body.AddBox(new Vector3(0f, 0.62f, 0f), new Vector3(1.85f, 0.55f, 4.35f));
            MeshBuilder.Spawn("Body", go.transform, body.Build("CarBody"), paintMat, Vector3.zero);
            var cabin = new MeshBuilder();
            cabin.AddBox(new Vector3(0f, 1.12f, -0.25f), new Vector3(1.6f, 0.5f, 2.1f));
            MeshBuilder.Spawn("Cabin", go.transform, cabin.Build("CarCabin"), glassMat, Vector3.zero);
            var wheels = new MeshBuilder();
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    wheels.AddSphere(new Vector3(sx * 0.85f, 0.36f, sz * 1.4f), 0.36f, 6, 8,
                        p => { p.x *= 0.55f; return p; });
            MeshBuilder.Spawn("Wheels", go.transform, wheels.Build("CarWheels"), tireMat, Vector3.zero);

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(1.9f, 1.5f, 4.4f);

            return Finish(go, 1250f, 90f, true, true, 7f, 55f);
        }

        public static ThrowableProp Crate(Vector3 pos)
        {
            var go = new GameObject("Crate");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var mat = ProceduralTextures.Standard(new Color(0.55f, 0.42f, 0.28f), 0f, 0.3f);
            var mb = new MeshBuilder();
            mb.AddBox(new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 1.1f, 1.1f));
            MeshBuilder.Spawn("Box", go.transform, mb.Build("Crate"), mat, Vector3.zero);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.55f, 0f);
            col.size = Vector3.one * 1.1f;
            return Finish(go, 90f, 35f, true, false, 0f, 0f);
        }

        public static ThrowableProp Tanker(Vector3 pos, float rotY)
        {
            var go = new GameObject("Tanker");
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            var cabMat = ProceduralTextures.Standard(new Color(0.7f, 0.25f, 0.1f), 0.3f, 0.5f);
            var tankMat = ProceduralTextures.Standard(new Color(0.78f, 0.79f, 0.82f), 0.85f, 0.8f);

            var cab = new MeshBuilder();
            cab.AddBox(new Vector3(0f, 1.1f, 2.9f), new Vector3(2.2f, 2.0f, 1.8f));
            MeshBuilder.Spawn("Cab", go.transform, cab.Build("TankerCab"), cabMat, Vector3.zero);
            var tank = new MeshBuilder();
            tank.AddSphere(new Vector3(0f, 1.35f, -0.9f), 1.15f, 10, 14,
                p => { p.z *= 3.1f; return p; });
            MeshBuilder.Spawn("Tank", go.transform, tank.Build("TankerTank"), tankMat, Vector3.zero);

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.3f, -0.3f);
            col.size = new Vector3(2.3f, 2.5f, 8.4f);
            return Finish(go, 5200f, 60f, true, true, 13f, 95f);
        }

        private static ThrowableProp Finish(GameObject go, float mass, float hp,
            bool burns, bool explodes, float explRadius, float explDamage)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.angularDamping = 0.6f;

            var prop = go.AddComponent<ThrowableProp>();
            prop.maxHp = hp;
            prop.hp = hp;
            prop.canBurn = burns;
            prop.explodesOnDeath = explodes;
            prop.explosionRadius = explRadius;
            prop.explosionDamage = explDamage;
            prop.rb = rb;
            prop.cols = go.GetComponentsInChildren<Collider>();

            foreach (var r in go.GetComponentsInChildren<Renderer>())
                Registry.Highlights.Add(new HighlightEntry(r, new Color(1f, 0.85f, 0.3f)));
            return prop;
        }
    }
}
