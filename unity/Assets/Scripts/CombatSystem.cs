using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    public enum BodyJoint { Torso, Neck, ShoulderL, ShoulderR, ElbowL, ElbowR, HipL, HipR, KneeL, KneeR }

    /// <summary>One keyframe of a procedural attack: joint eulers at time t (normalized).</summary>
    public class PoseKey
    {
        public float t;
        public Dictionary<BodyJoint, Vector3> pose = new Dictionary<BodyJoint, Vector3>();
        public PoseKey(float time) { t = time; }
        public PoseKey Set(BodyJoint j, float x, float y, float z)
        {
            pose[j] = new Vector3(x, y, z);
            return this;
        }
    }

    public class AttackDef
    {
        public string name;
        public float duration;
        public float hitTime;        // normalized moment the hit lands
        public float range = 1.9f;   // reach in front
        public float radius = 0.95f; // hit sphere radius
        public float damage;
        public float impulse = 8f;
        public float upKick = 0f;    // extra upward launch
        public float lunge = 2.5f;   // forward step
        public float spin = 0f;      // degrees of body spin over the attack
        public bool aoe = false;     // 360 degrees hit
        public DamageKind kind = DamageKind.Punch;
        public PoseKey[] keys;
    }

    /// <summary>
    /// Melee combat: LMB punches, RMB kicks, chained into combos
    /// (P-P-P haymaker, P-P-K rising kick launcher, K-K 360 spin kick...).
    /// Attacks are procedural pose keyframes blended over the animator's base
    /// pose, with a hit sphere at the strike frame and physics knockback.
    /// </summary>
    public class CombatSystem : MonoBehaviour
    {
        private FlightController fc;
        private SupermanRig rig;
        private PowersController powers;

        private AttackDef current;
        private float attackT;
        private bool hitDone;
        private string chain = "";
        private float chainResetT;
        private char queued = '\0';

        public string LastAttackName { get; private set; }
        public float LastAttackTime { get; private set; }

        private static Dictionary<string, AttackDef> attacks;

        public void Init(FlightController controller, SupermanRig r, PowersController pw)
        {
            fc = controller;
            rig = r;
            powers = pw;
            BuildAttacks();
        }

        public bool Attacking { get { return current != null; } }

        /// <summary>Extra yaw applied to the body during spinning attacks.</summary>
        public float SpinOffset
        {
            get
            {
                if (current == null || current.spin == 0f) return 0f;
                float k = Mathf.Clamp01(attackT / current.duration);
                return current.spin * (k * k * (3f - 2f * k));
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Input (ignored while carrying a prop - LMB hurls it instead,
            // with a short lockout so the hurl click doesn't also punch).
            bool free = powers == null ||
                        (powers.Held == null && Time.time - powers.LastThrowTime > 0.15f);
            if (free && Cursor.lockState == CursorLockMode.Locked)
            {
                if (Input.GetMouseButtonDown(0)) queued = 'P';
                else if (Input.GetMouseButtonDown(1)) queued = 'K';
            }

            if (current == null)
            {
                if (chainResetT > 0f)
                {
                    chainResetT -= dt;
                    if (chainResetT <= 0f) chain = "";
                }
                if (queued != '\0')
                {
                    StartAttack(Next(queued));
                    queued = '\0';
                }
                return;
            }

            attackT += dt;
            if (!hitDone && attackT >= current.hitTime * current.duration)
            {
                hitDone = true;
                DoHit();
            }
            if (attackT >= current.duration)
            {
                current = null;
                chainResetT = 1.1f;
                if (queued != '\0')
                {
                    StartAttack(Next(queued));
                    queued = '\0';
                }
            }
        }

        private AttackDef Next(char input)
        {
            string key = chain + input;
            AttackDef def;
            if (attacks.TryGetValue(key, out def))
            {
                chain = key;
            }
            else
            {
                chain = input.ToString();
                def = attacks[chain];
            }
            return def;
        }

        /// <summary>Attack direction: where the player is aiming, not where the
        /// (possibly pitched-forward) body mesh points.</summary>
        private Vector3 AimForward()
        {
            if (Registry.Cam != null) return Registry.Cam.transform.forward;
            return rig.visualRoot.forward;
        }

        private void StartAttack(AttackDef def)
        {
            current = def;
            attackT = 0f;
            hitDone = false;
            LastAttackName = def.name;
            LastAttackTime = Time.time;

            // Lunge toward where we're aiming.
            Vector3 fwd = AimForward();
            fwd.y *= 0.3f;
            fwd.Normalize();
            fc.AddImpulse(fwd * def.lunge);
        }

        private void DoHit()
        {
            var def = current;
            Vector3 fwd = AimForward();
            Vector3 origin = fc.transform.position + Vector3.up * 1.15f;
            Vector3 center = def.aoe ? origin : origin + fwd * def.range * 0.75f;
            float radius = def.aoe ? def.range : def.radius;

            Collider[] hits = Physics.OverlapSphere(center, radius);
            var seen = new HashSet<Damageable>();
            bool connected = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (Registry.Player != null && hits[i].transform.root == Registry.Player.root) continue;
                var d = hits[i].GetComponentInParent<Damageable>();
                if (d == null || !seen.Add(d)) continue;

                Vector3 dir = (d.transform.position - fc.transform.position).normalized;
                if (def.aoe) dir = (d.transform.position - origin).normalized;
                dir += Vector3.up * (def.upKick > 0f ? 0.9f : 0.15f);
                dir.Normalize();

                Vector3 point = hits[i].ClosestPoint(origin + fwd * 0.6f);
                d.ApplyDamage(def.damage, def.kind, point, dir, def.impulse + def.upKick);
                connected = true;
            }

            if (connected && Registry.Cam != null) Registry.Cam.Kick(0.28f);
        }

        /// <summary>Blend weight and pose of the running attack (for the animator).</summary>
        public float SamplePose(Dictionary<BodyJoint, Vector3> into)
        {
            if (current == null) return 0f;
            float nt = Mathf.Clamp01(attackT / current.duration);

            // Envelope: quick ramp in, ease out at the tail.
            float w = Mathf.Clamp01(nt / 0.12f) * Mathf.Clamp01((1f - nt) / 0.22f);
            w = Mathf.Clamp01(w * 1.6f);

            PoseKey[] keys = current.keys;
            PoseKey a = keys[0], b = keys[keys.Length - 1];
            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (nt >= keys[i].t && nt <= keys[i + 1].t) { a = keys[i]; b = keys[i + 1]; break; }
            }
            float span = Mathf.Max(b.t - a.t, 0.0001f);
            float k = Mathf.Clamp01((nt - a.t) / span);
            k = k * k * (3f - 2f * k);

            foreach (var kv in a.pose)
            {
                Vector3 target = kv.Value;
                Vector3 bVal;
                if (b.pose.TryGetValue(kv.Key, out bVal)) target = Vector3.Lerp(kv.Value, bVal, k);
                into[kv.Key] = target;
            }
            foreach (var kv in b.pose)
            {
                if (!a.pose.ContainsKey(kv.Key)) into[kv.Key] = kv.Value;
            }
            return w;
        }

        // --------------------------------------------------------------------
        private static void BuildAttacks()
        {
            if (attacks != null) return;
            attacks = new Dictionary<string, AttackDef>();

            // P: Jab (right)
            attacks["P"] = new AttackDef
            {
                name = "JAB",
                duration = 0.30f,
                hitTime = 0.45f,
                damage = 22f,
                impulse = 7f,
                lunge = 2.5f,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.ShoulderR, -60f, 0f, -22f).Set(BodyJoint.ElbowR, -45f, 0f, 0f)
                        .Set(BodyJoint.Torso, 2f, 12f, 0f),
                    new PoseKey(0.45f).Set(BodyJoint.ShoulderR, -97f, 5f, -2f).Set(BodyJoint.ElbowR, -2f, 0f, 0f)
                        .Set(BodyJoint.Torso, 5f, -16f, 0f).Set(BodyJoint.Neck, 0f, -6f, 0f),
                    new PoseKey(1f).Set(BodyJoint.ShoulderR, -55f, 0f, -14f).Set(BodyJoint.ElbowR, -30f, 0f, 0f)
                        .Set(BodyJoint.Torso, 2f, 0f, 0f)
                }
            };

            // P-P: Cross (left)
            attacks["PP"] = new AttackDef
            {
                name = "CROSS",
                duration = 0.34f,
                hitTime = 0.45f,
                damage = 28f,
                impulse = 9f,
                lunge = 3f,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.ShoulderL, -60f, 0f, 22f).Set(BodyJoint.ElbowL, -45f, 0f, 0f)
                        .Set(BodyJoint.Torso, 2f, -12f, 0f),
                    new PoseKey(0.45f).Set(BodyJoint.ShoulderL, -98f, -5f, 2f).Set(BodyJoint.ElbowL, -2f, 0f, 0f)
                        .Set(BodyJoint.Torso, 6f, 20f, 0f).Set(BodyJoint.Neck, 0f, 6f, 0f),
                    new PoseKey(1f).Set(BodyJoint.ShoulderL, -55f, 0f, 14f).Set(BodyJoint.ElbowL, -30f, 0f, 0f)
                        .Set(BodyJoint.Torso, 2f, 0f, 0f)
                }
            };

            // P-P-P: Haymaker finisher
            attacks["PPP"] = new AttackDef
            {
                name = "HAYMAKER",
                duration = 0.52f,
                hitTime = 0.52f,
                damage = 55f,
                impulse = 16f,
                lunge = 4.5f,
                radius = 1.15f,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.ShoulderR, -40f, 0f, -85f).Set(BodyJoint.ElbowR, -55f, 0f, 0f)
                        .Set(BodyJoint.Torso, 0f, 30f, 6f).Set(BodyJoint.HipL, 4f, 0f, 2f),
                    new PoseKey(0.52f).Set(BodyJoint.ShoulderR, -105f, 0f, 8f).Set(BodyJoint.ElbowR, 0f, 0f, 0f)
                        .Set(BodyJoint.Torso, 8f, -34f, -6f).Set(BodyJoint.Neck, 0f, -10f, 0f),
                    new PoseKey(1f).Set(BodyJoint.ShoulderR, -60f, 0f, -10f).Set(BodyJoint.Torso, 3f, -6f, 0f)
                }
            };

            // K: Front kick (right leg)
            attacks["K"] = new AttackDef
            {
                name = "FRONT KICK",
                duration = 0.42f,
                hitTime = 0.48f,
                damage = 32f,
                impulse = 12f,
                lunge = 2f,
                kind = DamageKind.Kick,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.HipR, -35f, 0f, 0f).Set(BodyJoint.KneeR, 75f, 0f, 0f)
                        .Set(BodyJoint.Torso, -6f, 0f, 0f).Set(BodyJoint.KneeL, 10f, 0f, 0f),
                    new PoseKey(0.48f).Set(BodyJoint.HipR, -95f, 0f, 0f).Set(BodyJoint.KneeR, 6f, 0f, 0f)
                        .Set(BodyJoint.Torso, -12f, 0f, 0f).Set(BodyJoint.ShoulderL, 10f, 0f, 30f)
                        .Set(BodyJoint.ShoulderR, 10f, 0f, -30f),
                    new PoseKey(1f).Set(BodyJoint.HipR, -20f, 0f, 0f).Set(BodyJoint.KneeR, 30f, 0f, 0f)
                        .Set(BodyJoint.Torso, -3f, 0f, 0f)
                }
            };

            // K-K: 360 spin kick, hits everything around
            attacks["KK"] = new AttackDef
            {
                name = "SPIN KICK",
                duration = 0.60f,
                hitTime = 0.55f,
                damage = 42f,
                impulse = 13f,
                range = 2.6f,
                aoe = true,
                spin = 360f,
                lunge = 1f,
                kind = DamageKind.Kick,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.HipR, -30f, 0f, -10f).Set(BodyJoint.KneeR, 40f, 0f, 0f)
                        .Set(BodyJoint.Torso, -6f, 0f, 8f),
                    new PoseKey(0.5f).Set(BodyJoint.HipR, -85f, 0f, -20f).Set(BodyJoint.KneeR, 4f, 0f, 0f)
                        .Set(BodyJoint.Torso, -10f, 0f, 14f).Set(BodyJoint.ShoulderL, 15f, 0f, 55f)
                        .Set(BodyJoint.ShoulderR, 15f, 0f, -55f),
                    new PoseKey(1f).Set(BodyJoint.HipR, -15f, 0f, 0f).Set(BodyJoint.KneeR, 25f, 0f, 0f)
                        .Set(BodyJoint.Torso, -2f, 0f, 0f)
                }
            };

            // P-P-K: Rising kick launcher
            attacks["PPK"] = new AttackDef
            {
                name = "RISING KICK",
                duration = 0.50f,
                hitTime = 0.5f,
                damage = 48f,
                impulse = 8f,
                upKick = 12f,
                lunge = 3f,
                kind = DamageKind.Kick,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.HipR, -20f, 0f, 0f).Set(BodyJoint.KneeR, 80f, 0f, 0f)
                        .Set(BodyJoint.Torso, 6f, 0f, 0f),
                    new PoseKey(0.5f).Set(BodyJoint.HipR, -125f, 0f, 0f).Set(BodyJoint.KneeR, 15f, 0f, 0f)
                        .Set(BodyJoint.Torso, -16f, 0f, 0f).Set(BodyJoint.Neck, 8f, 0f, 0f),
                    new PoseKey(1f).Set(BodyJoint.HipR, -25f, 0f, 0f).Set(BodyJoint.KneeR, 35f, 0f, 0f)
                        .Set(BodyJoint.Torso, -4f, 0f, 0f)
                }
            };

            // K-P: Roundhouse elbow-follow (bonus variation)
            attacks["KP"] = new AttackDef
            {
                name = "BACKFIST",
                duration = 0.38f,
                hitTime = 0.5f,
                damage = 30f,
                impulse = 10f,
                lunge = 2f,
                keys = new[]
                {
                    new PoseKey(0f).Set(BodyJoint.ShoulderR, -70f, 0f, 40f).Set(BodyJoint.ElbowR, -70f, 0f, 0f)
                        .Set(BodyJoint.Torso, 0f, 24f, 0f),
                    new PoseKey(0.5f).Set(BodyJoint.ShoulderR, -95f, 0f, -30f).Set(BodyJoint.ElbowR, -5f, 0f, 0f)
                        .Set(BodyJoint.Torso, 4f, -28f, 0f),
                    new PoseKey(1f).Set(BodyJoint.ShoulderR, -55f, 0f, -10f).Set(BodyJoint.Torso, 0f, 0f, 0f)
                }
            };
        }
    }
}
