using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    public enum MoveState { Grounded, Walk, Sprint, Hover, Cruise, Boost }

    /// <summary>
    /// Procedural posing: grounded stance, walk and super-sprint cycles
    /// (phase-driven leg/arm swing with body lean and bob), hover, the
    /// classic one-fist cruise, and boost - with combat attack poses from
    /// CombatSystem blended on top.
    /// </summary>
    public class SupermanAnimator : MonoBehaviour
    {
        private SupermanRig rig;
        private FlightController controller;
        private CombatSystem combat;
        private Dictionary<BodyJoint, Transform> joints;
        private readonly Dictionary<BodyJoint, Vector3> basePose = new Dictionary<BodyJoint, Vector3>();
        private readonly Dictionary<BodyJoint, Vector3> attackPose = new Dictionary<BodyJoint, Vector3>();
        private float walkPhase;
        private const float blendSpeed = 9f;

        public void Init(SupermanRig r, FlightController fc, CombatSystem cs)
        {
            rig = r;
            controller = fc;
            combat = cs;
            joints = new Dictionary<BodyJoint, Transform>
            {
                { BodyJoint.Torso, rig.torso },
                { BodyJoint.Neck, rig.neck },
                { BodyJoint.ShoulderL, rig.shoulderL },
                { BodyJoint.ShoulderR, rig.shoulderR },
                { BodyJoint.ElbowL, rig.elbowL },
                { BodyJoint.ElbowR, rig.elbowR },
                { BodyJoint.HipL, rig.hipL },
                { BodyJoint.HipR, rig.hipR },
                { BodyJoint.KneeL, rig.kneeL },
                { BodyJoint.KneeR, rig.kneeR }
            };
        }

        private void LateUpdate()
        {
            if (rig == null || controller == null) return;
            float t = Time.time;
            float breathe = Mathf.Sin(t * 2.2f);
            MoveState st = controller.State;

            foreach (BodyJoint j in System.Enum.GetValues(typeof(BodyJoint)))
                basePose[j] = Vector3.zero;

            float bobY = 0f;

            switch (st)
            {
                case MoveState.Grounded:
                    basePose[BodyJoint.Torso] = new Vector3(-3f + breathe * 1.2f, 0f, 0f);
                    basePose[BodyJoint.ShoulderL] = new Vector3(4f, 0f, 10f);
                    basePose[BodyJoint.ShoulderR] = new Vector3(4f, 0f, -10f);
                    basePose[BodyJoint.ElbowL] = new Vector3(-10f, 0f, 0f);
                    basePose[BodyJoint.ElbowR] = new Vector3(-10f, 0f, 0f);
                    basePose[BodyJoint.HipL] = new Vector3(0f, 0f, 4f);
                    basePose[BodyJoint.HipR] = new Vector3(0f, 0f, -4f);
                    walkPhase = 0f;
                    break;

                case MoveState.Walk:
                case MoveState.Sprint:
                {
                    bool sprint = st == MoveState.Sprint;
                    float speed = controller.Speed;
                    walkPhase += Mathf.Min(speed * (sprint ? 0.55f : 1.5f), 15f) * Time.deltaTime;
                    float s = Mathf.Sin(walkPhase);
                    float sOpp = Mathf.Sin(walkPhase + Mathf.PI);
                    float hipAmp = sprint ? 46f : 27f;
                    float kneeAmp = sprint ? 62f : 38f;
                    float armAmp = sprint ? 55f : 24f;

                    basePose[BodyJoint.HipL] = new Vector3(s * hipAmp - (sprint ? 8f : 0f), 0f, 2f);
                    basePose[BodyJoint.HipR] = new Vector3(sOpp * hipAmp - (sprint ? 8f : 0f), 0f, -2f);
                    basePose[BodyJoint.KneeL] = new Vector3(Mathf.Max(0f, Mathf.Sin(walkPhase - 1.1f)) * kneeAmp + 6f, 0f, 0f);
                    basePose[BodyJoint.KneeR] = new Vector3(Mathf.Max(0f, Mathf.Sin(walkPhase + Mathf.PI - 1.1f)) * kneeAmp + 6f, 0f, 0f);
                    basePose[BodyJoint.ShoulderL] = new Vector3(sOpp * armAmp * 0.8f, 0f, 8f);
                    basePose[BodyJoint.ShoulderR] = new Vector3(s * armAmp * 0.8f, 0f, -8f);
                    basePose[BodyJoint.ElbowL] = new Vector3(sprint ? -70f : -25f, 0f, 0f);
                    basePose[BodyJoint.ElbowR] = new Vector3(sprint ? -70f : -25f, 0f, 0f);
                    basePose[BodyJoint.Torso] = new Vector3(sprint ? 24f : 5f, s * (sprint ? 5f : 3f), 0f);
                    basePose[BodyJoint.Neck] = new Vector3(sprint ? -18f : -3f, 0f, 0f);
                    bobY = Mathf.Abs(Mathf.Cos(walkPhase)) * (sprint ? 0.10f : 0.045f);
                    break;
                }

                case MoveState.Hover:
                    basePose[BodyJoint.Torso] = new Vector3(-6f + breathe * 1.5f, 0f, 0f);
                    basePose[BodyJoint.Neck] = new Vector3(2f, 0f, 0f);
                    basePose[BodyJoint.ShoulderL] = new Vector3(8f, 0f, 32f);
                    basePose[BodyJoint.ShoulderR] = new Vector3(8f, 0f, -32f);
                    basePose[BodyJoint.ElbowL] = new Vector3(-18f, 0f, 6f);
                    basePose[BodyJoint.ElbowR] = new Vector3(-18f, 0f, -6f);
                    basePose[BodyJoint.HipL] = new Vector3(14f, 0f, 4f);
                    basePose[BodyJoint.HipR] = new Vector3(10f, 0f, -4f);
                    basePose[BodyJoint.KneeL] = new Vector3(22f, 0f, 0f);
                    basePose[BodyJoint.KneeR] = new Vector3(26f, 0f, 0f);
                    walkPhase = 0f;
                    break;

                case MoveState.Cruise:
                    basePose[BodyJoint.Torso] = new Vector3(4f, 0f, 0f);
                    basePose[BodyJoint.Neck] = new Vector3(-52f, 0f, 0f);
                    basePose[BodyJoint.ShoulderR] = new Vector3(-192f, 0f, -6f);
                    basePose[BodyJoint.ElbowR] = new Vector3(-4f, 0f, 0f);
                    basePose[BodyJoint.ShoulderL] = new Vector3(28f, 0f, 8f);
                    basePose[BodyJoint.ElbowL] = new Vector3(-14f, 0f, 0f);
                    basePose[BodyJoint.HipL] = new Vector3(-4f, 0f, 1.5f);
                    basePose[BodyJoint.HipR] = new Vector3(-7f, 0f, -1.5f);
                    basePose[BodyJoint.KneeL] = new Vector3(5f, 0f, 0f);
                    basePose[BodyJoint.KneeR] = new Vector3(8f, 0f, 0f);
                    break;

                case MoveState.Boost:
                    basePose[BodyJoint.Torso] = new Vector3(2f, 0f, 0f);
                    basePose[BodyJoint.Neck] = new Vector3(-55f, 0f, 0f);
                    basePose[BodyJoint.ShoulderL] = new Vector3(-196f, 0f, 10f);
                    basePose[BodyJoint.ShoulderR] = new Vector3(-196f, 0f, -10f);
                    basePose[BodyJoint.HipL] = new Vector3(-3f, 0f, 1f);
                    basePose[BodyJoint.HipR] = new Vector3(-3f, 0f, -1f);
                    basePose[BodyJoint.KneeL] = new Vector3(2f, 0f, 0f);
                    basePose[BodyJoint.KneeR] = new Vector3(2f, 0f, 0f);
                    break;
            }

            // Combat pose on top.
            attackPose.Clear();
            float w = combat != null ? combat.SamplePose(attackPose) : 0f;
            if (w > 0f)
            {
                foreach (var kv in attackPose)
                {
                    Vector3 baseVal = basePose[kv.Key];
                    basePose[kv.Key] = Vector3.Lerp(baseVal, kv.Value, w);
                }
            }

            float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            foreach (var kv in joints)
            {
                kv.Value.localRotation = Quaternion.Slerp(
                    kv.Value.localRotation, Quaternion.Euler(basePose[kv.Key]), k);
            }

            // Locomotion bob on the visual root (rotation is the controller's).
            Vector3 lp = rig.visualRoot.localPosition;
            lp.y = Mathf.Lerp(lp.y, bobY, k);
            rig.visualRoot.localPosition = lp;
        }
    }
}
