using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Superman locomotion: walking and super-sprinting on the ground,
    /// full 3D flight in the air (camera-aim steering, Shift boost). Uses a
    /// CharacterController so buildings and ground are solid. The visual body
    /// pitches into travel, banks through turns, and spins during spin
    /// attacks; speed trails ignite at boost/sprint.
    /// </summary>
    public class FlightController : MonoBehaviour
    {
        public MoveState State { get; private set; } = MoveState.Hover;
        public Vector3 Velocity { get { return velocity; } }
        public float Speed { get { return velocity.magnitude; } }
        public bool Boosting { get; private set; }
        [HideInInspector] public CombatSystem combat;

        private const float WALK_SPEED = 7f;
        private const float SPRINT_SPEED = 45f;
        private const float HOVER_SPEED = 14f;
        private const float CRUISE_SPEED = 42f;
        private const float BOOST_SPEED = 140f;
        private const float ACCEL = 3.0f;
        private const float GRAVITY = 22f;

        private CharacterController cc;
        private SupermanRig rig;
        private CameraRig cam;
        private Vector3 velocity;
        private float yVel;
        private bool grounded;
        private float bankAngle;
        private float lastYaw;
        private TrailRenderer[] trails;

        public void Init(SupermanRig r, CameraRig cameraRig)
        {
            rig = r;
            cam = cameraRig;
            cc = GetComponent<CharacterController>();
            lastYaw = transform.eulerAngles.y;

            trails = new TrailRenderer[4];
            trails[0] = MakeTrail(rig.handL, -0.09f);   // at the fists
            trails[1] = MakeTrail(rig.handR, -0.09f);
            trails[2] = MakeTrail(rig.bootL, -0.46f);   // at the boot soles
            trails[3] = MakeTrail(rig.bootR, -0.46f);
        }

        /// <summary>External velocity change (attack lunges, knockback).</summary>
        public void AddImpulse(Vector3 v)
        {
            velocity += v;
        }

        private TrailRenderer MakeTrail(Transform parent, float yOffset)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.35f;
            tr.startWidth = 0.16f;
            tr.endWidth = 0.0f;
            tr.numCapVertices = 4;
            tr.material = new Material(Shader.Find("Sprites/Default"));
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.45f, 0.25f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.65f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            tr.colorGradient = grad;
            tr.emitting = false;
            return tr;
        }

        private void Update()
        {
            if (cam == null) return;
            float dt = Time.deltaTime;

            // --- Input --------------------------------------------------------
            float ix = Input.GetAxisRaw("Horizontal");
            float iz = Input.GetAxisRaw("Vertical");
            float iy = 0f;
            if (Input.GetKey(KeyCode.Space)) iy += 1f;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) iy -= 1f;
            bool shift = Input.GetKey(KeyCode.LeftShift);
            bool anyMove = Mathf.Abs(ix) + Mathf.Abs(iz) + Mathf.Abs(iy) > 0.1f;
            Boosting = shift && anyMove && !grounded;

            // Camera-relative wish direction. While airborne, W follows the
            // camera's full 3D aim so you dive and climb by looking.
            Vector3 camF = cam.transform.forward;
            Vector3 camR = cam.transform.right;
            Vector3 wish;
            if (grounded)
            {
                camF.y = 0f; camF.Normalize();
                camR.y = 0f; camR.Normalize();
                wish = camF * iz + camR * ix;
            }
            else
            {
                wish = camF * iz + camR * ix + Vector3.up * iy;
            }
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            // --- State & target speed ----------------------------------------
            float targetSpeed;
            if (grounded && iy <= 0f)
            {
                bool moving = wish.sqrMagnitude > 0.01f;
                bool sprint = moving && shift;
                State = !moving ? MoveState.Grounded : (sprint ? MoveState.Sprint : MoveState.Walk);
                targetSpeed = !moving ? 0f : (sprint ? SPRINT_SPEED : WALK_SPEED);
            }
            else if (Boosting)
            {
                State = MoveState.Boost;
                targetSpeed = BOOST_SPEED;
            }
            else if (wish.sqrMagnitude > 0.01f)
            {
                State = Speed > HOVER_SPEED * 1.4f || iz > 0.5f ? MoveState.Cruise : MoveState.Hover;
                targetSpeed = State == MoveState.Cruise ? CRUISE_SPEED : HOVER_SPEED;
            }
            else
            {
                State = MoveState.Hover;
                targetSpeed = 0f;
            }

            // --- Integrate ----------------------------------------------------
            float k = 1f - Mathf.Exp(-ACCEL * dt);
            velocity = Vector3.Lerp(velocity, wish * targetSpeed, k);

            // Idle hover just above the ground settles into a landing.
            if (!grounded && State == MoveState.Hover && wish.sqrMagnitude < 0.01f)
            {
                RaycastHit gh;
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out gh, 2.2f))
                {
                    velocity.y = Mathf.Max(velocity.y - 8f * dt, -2.5f);
                }
            }

            if (grounded)
            {
                yVel -= GRAVITY * dt;
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    yVel = 0f;
                    grounded = false;
                    velocity.y = 7f;   // hop into the air
                }
                else velocity.y = yVel;
            }

            CollisionFlags flags = cc.Move(velocity * dt);
            if ((flags & CollisionFlags.Below) != 0)
            {
                yVel = 0f;
                if (velocity.y < 0f && Speed < 12f) grounded = true;
                if (velocity.y < 0f) velocity.y = 0f;
            }
            else if (grounded)
            {
                if (yVel < -8f) grounded = false;   // walked off an edge
            }
            if ((flags & CollisionFlags.Sides) != 0 && Speed > 30f)
            {
                velocity *= 0.35f;
                if (cam != null) cam.Kick(0.5f);
            }

            // --- Visual body orientation --------------------------------------
            UpdateBodyPose(dt);

            // Feed the cape.
            if (rig.cape != null) rig.cape.BodyVelocity = velocity;

            // Trails at boost / super-sprint.
            bool trailsOn = (State == MoveState.Boost && Speed > CRUISE_SPEED) ||
                            (State == MoveState.Sprint && Speed > WALK_SPEED * 2f);
            for (int i = 0; i < trails.Length; i++) trails[i].emitting = trailsOn;
        }

        private void UpdateBodyPose(float dt)
        {
            Transform vis = rig.visualRoot;
            float speed = Speed;

            // Yaw: face travel direction (or camera yaw when idle).
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float yaw = flat.sqrMagnitude > 4f
                ? Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg
                : cam.Yaw;

            // While attacking on the spot, square up to the camera aim.
            if (combat != null && combat.Attacking && flat.sqrMagnitude <= 4f)
                yaw = cam.Yaw;

            // Bank into turns (airborne only).
            float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(dt, 1e-4f);
            lastYaw = yaw;
            float bankScale = grounded ? 0.1f : 1f;
            float targetBank = Mathf.Clamp(-yawRate * 0.25f, -55f, 55f)
                             * Mathf.Clamp01(speed / CRUISE_SPEED) * bankScale;
            bankAngle = Mathf.Lerp(bankAngle, targetBank, 1f - Mathf.Exp(-4f * dt));

            // Pitch into the velocity vector as speed builds.
            float pitch = 0f;
            if (State == MoveState.Cruise || State == MoveState.Boost)
            {
                float horiz = flat.magnitude;
                float velPitch = -Mathf.Atan2(velocity.y, Mathf.Max(horiz, 0.1f)) * Mathf.Rad2Deg;
                float lean = Mathf.Lerp(0f, 80f, Mathf.Clamp01(speed / CRUISE_SPEED));
                pitch = Mathf.Clamp(velPitch + lean, -80f, 105f);
            }
            else if (State == MoveState.Hover)
            {
                pitch = Mathf.Clamp(velocity.y * -0.6f, -14f, 14f);
            }

            // Spin attacks add yaw.
            float spin = combat != null ? combat.SpinOffset : 0f;

            Quaternion target = Quaternion.Euler(0f, yaw + spin, 0f)
                              * Quaternion.Euler(pitch, 0f, bankAngle);
            float rotK = combat != null && combat.Attacking ? 14f : 6f;
            vis.rotation = Quaternion.Slerp(vis.rotation, target, 1f - Mathf.Exp(-rotK * dt));
        }
    }
}
