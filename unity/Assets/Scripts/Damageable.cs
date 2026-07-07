using UnityEngine;

namespace LastSon
{
    public enum DamageKind { Punch, Kick, Heat, Explosion, Impact, Shatter }

    /// <summary>
    /// Anything Superman can affect. Tracks health, accumulated heat (heat
    /// vision -> ignition for burnables) and accumulated cold (freeze breath
    /// -> encased in an ice block; frozen things shatter under a punch for
    /// bonus damage). Subclasses (robots, civilians, props) override the
    /// reaction hooks.
    /// </summary>
    public class Damageable : MonoBehaviour
    {
        public float maxHp = 100f;
        public bool canBurn = false;
        public bool explodesOnDeath = false;
        public float explosionRadius = 6f;
        public float explosionDamage = 45f;
        public float fireFuse = 3.5f;          // seconds burning before death/explosion

        [HideInInspector] public float hp;
        private float heat;
        private float cold;
        private bool onFire;
        private float fireT;
        private GameObject fireFx;
        private IceBlock ice;
        private bool dead;

        public bool Dead { get { return dead; } }
        public bool Frozen { get { return ice != null; } }
        public bool Burning { get { return onFire; } }

        protected virtual void Awake()
        {
            hp = maxHp;
        }

        protected virtual void Update()
        {
            if (dead) return;
            if (heat > 0f && !onFire) heat = Mathf.Max(0f, heat - 14f * Time.deltaTime);
            if (cold > 0f && !Frozen) cold = Mathf.Max(0f, cold - 20f * Time.deltaTime);

            if (onFire)
            {
                fireT += Time.deltaTime;
                if (fireT >= fireFuse) Die(DamageKind.Heat);
            }
        }

        public void AddHeat(float amount)
        {
            if (dead) return;
            if (Frozen) { ice.Melt(amount * 0.03f); return; }
            heat += amount;
            OnHeated(amount);
            if (canBurn && !onFire && heat >= 100f) Ignite();
        }

        public void Ignite()
        {
            if (onFire || dead) return;
            onFire = true;
            fireT = 0f;
            fireFx = Fx.AttachFire(transform, Mathf.Max(1f, transform.localScale.magnitude * 0.4f));
        }

        public void Extinguish()
        {
            onFire = false;
            heat = 0f;
            if (fireFx != null) Destroy(fireFx);
        }

        public void AddCold(float amount)
        {
            if (dead || Frozen) return;
            if (onFire) { Extinguish(); return; }
            cold += amount;
            if (cold >= 100f)
            {
                cold = 0f;
                ice = IceBlock.Encase(this);
                OnFrozen();
            }
        }

        /// <summary>Called by IceBlock when it melts away or is shattered.</summary>
        public void NotifyThawed()
        {
            ice = null;
            OnThawed();
        }

        public void ApplyDamage(float amount, DamageKind kind, Vector3 point, Vector3 dir, float impulse = 0f)
        {
            if (dead) return;

            bool physical = kind == DamageKind.Punch || kind == DamageKind.Kick ||
                            kind == DamageKind.Explosion || kind == DamageKind.Impact;

            if (Frozen && physical)
            {
                amount *= 2.5f;
                Vector3 icePos = ice.transform.position;
                ice.Shatter();
                Fx.IceShards(icePos, 1.2f);
                kind = DamageKind.Shatter;
            }

            hp -= amount;

            // Physics response.
            if (physical || kind == DamageKind.Shatter)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null && impulse > 0f)
                {
                    if (rb.isKinematic) rb.isKinematic = false;
                    rb.AddForceAtPosition(dir * impulse, point, ForceMode.VelocityChange);
                }
            }

            OnDamaged(amount, kind, point, dir, impulse);
            if (hp <= 0f) Die(kind);
        }

        protected void Die(DamageKind kind)
        {
            if (dead) return;
            dead = true;
            if (fireFx != null) Destroy(fireFx);
            if (ice != null) ice.Shatter();
            if (explodesOnDeath)
            {
                Fx.Explosion(transform.position + Vector3.up * 0.6f, explosionRadius, explosionDamage);
            }
            OnDeath(kind);
        }

        // Reaction hooks ------------------------------------------------------
        protected virtual void OnDamaged(float amount, DamageKind kind, Vector3 point, Vector3 dir, float impulse) { }
        protected virtual void OnHeated(float amount) { }
        protected virtual void OnFrozen() { }
        protected virtual void OnThawed() { }
        protected virtual void OnDeath(DamageKind kind) { }
    }

    /// <summary>
    /// The translucent ice prison spawned when something is fully frozen.
    /// Melts on its own (faster if heated); shatters instantly from a solid hit.
    /// </summary>
    public class IceBlock : MonoBehaviour
    {
        private Damageable victim;
        private float meltRemaining = 8f;
        private Vector3 fullScale;
        private Rigidbody victimRb;
        private bool wasKinematic;

        public static IceBlock Encase(Damageable target)
        {
            // Fit the block to the target's render bounds.
            var renderers = target.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(target.transform.position + Vector3.up, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;
                if (i == 0) b = renderers[i].bounds;
                else b.Encapsulate(renderers[i].bounds);
            }
            b.Expand(0.25f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "IceBlock";
            go.transform.position = b.center;
            go.transform.localScale = b.size;
            go.transform.SetParent(target.transform, true);

            var mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3f); // transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
            mat.color = new Color(0.62f, 0.85f, 1f, 0.45f);
            mat.SetFloat("_Glossiness", 0.92f);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var block = go.AddComponent<IceBlock>();
            block.victim = target;
            block.fullScale = b.size;

            // Halt the victim's physics while frozen.
            block.victimRb = target.GetComponent<Rigidbody>();
            if (block.victimRb != null)
            {
                block.wasKinematic = block.victimRb.isKinematic;
                block.victimRb.isKinematic = true;
            }

            Fx.Burst(b.center, new Color(0.8f, 0.95f, 1f), 20, 2.5f, 0.25f, 0.5f, 0f, false);
            return block;
        }

        public void Melt(float seconds)
        {
            meltRemaining -= seconds;
        }

        private void Update()
        {
            meltRemaining -= Time.deltaTime;
            if (meltRemaining < 1.2f)
            {
                float k = Mathf.Clamp01(meltRemaining / 1.2f);
                transform.localScale = fullScale * Mathf.Max(k, 0.01f);
            }
            if (meltRemaining <= 0f) Thaw();
        }

        public void Shatter()
        {
            Thaw();
        }

        private void Thaw()
        {
            if (victim != null)
            {
                if (victimRb != null) victimRb.isKinematic = wasKinematic;
                victim.NotifyThawed();
            }
            Destroy(gameObject);
        }
    }
}
