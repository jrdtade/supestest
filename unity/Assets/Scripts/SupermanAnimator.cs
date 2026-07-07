using UnityEngine;

namespace LastSon
{
    public enum MoveState { Grounded, Hover, Cruise, Boost }

    /// <summary>
    /// Procedural posing: blends the articulated rig between a grounded
    /// stance, a hover, the classic one-arm-forward cruise, and a
    /// two-fists-forward boost, with breathing/bobbing idle motion.
    /// </summary>
    public class SupermanAnimator : MonoBehaviour
    {
        private SupermanRig rig;
        private FlightController controller;
        private float blendSpeed = 7f;

        public void Init(SupermanRig r, FlightController fc)
        {
            rig = r;
            controller = fc;
        }

        private void LateUpdate()
        {
            if (rig == null || controller == null) return;
            float t = Time.time;
            MoveState st = controller.State;
            float breathe = Mathf.Sin(t * 2.2f);

            // Target local rotations per joint (degrees).
            Vector3 torso = Vector3.zero, headR = Vector3.zero;
            Vector3 shL = Vector3.zero, shR = Vector3.zero, elL = Vector3.zero, elR = Vector3.zero;
            Vector3 hipL = Vector3.zero, hipR = Vector3.zero, knL = Vector3.zero, knR = Vector3.zero;

            switch (st)
            {
                case MoveState.Grounded:
                    // Heroic stance: chest up, arms relaxed slightly out, fists.
                    torso = new Vector3(-3f + breathe * 1.2f, 0f, 0f);
                    shL = new Vector3(4f, 0f, 10f);
                    shR = new Vector3(4f, 0f, -10f);
                    elL = new Vector3(-10f, 0f, 0f);
                    elR = new Vector3(-10f, 0f, 0f);
                    hipL = new Vector3(0f, 0f, 4f);
                    hipR = new Vector3(0f, 0f, -4f);
                    break;

                case MoveState.Hover:
                    // Floating: knees softly bent, arms away from the body.
                    torso = new Vector3(-6f + breathe * 1.5f, 0f, 0f);
                    headR = new Vector3(2f, 0f, 0f);
                    shL = new Vector3(8f, 0f, 32f);
                    shR = new Vector3(8f, 0f, -32f);
                    elL = new Vector3(-18f, 0f, 6f);
                    elR = new Vector3(-18f, 0f, -6f);
                    hipL = new Vector3(14f, 0f, 4f);
                    hipR = new Vector3(10f, 0f, -4f);
                    knL = new Vector3(22f, 0f, 0f);
                    knR = new Vector3(26f, 0f, 0f);
                    break;

                case MoveState.Cruise:
                    // The classic: right fist leading, left arm swept back,
                    // legs together, toes pointed. (Body pitch comes from the
                    // controller; the head compensates to look forward.)
                    torso = new Vector3(4f, 0f, 0f);
                    headR = new Vector3(-52f, 0f, 0f);
                    shR = new Vector3(-192f, 0f, -6f);
                    elR = new Vector3(-4f, 0f, 0f);
                    shL = new Vector3(28f, 0f, 8f);
                    elL = new Vector3(-14f, 0f, 0f);
                    hipL = new Vector3(-4f, 0f, 1.5f);
                    hipR = new Vector3(-7f, 0f, -1.5f);
                    knL = new Vector3(5f, 0f, 0f);
                    knR = new Vector3(8f, 0f, 0f);
                    break;

                case MoveState.Boost:
                    // Both fists forward, streamlined.
                    torso = new Vector3(2f, 0f, 0f);
                    headR = new Vector3(-55f, 0f, 0f);
                    shL = new Vector3(-196f, 0f, 10f);
                    shR = new Vector3(-196f, 0f, -10f);
                    elL = Vector3.zero;
                    elR = Vector3.zero;
                    hipL = new Vector3(-3f, 0f, 1f);
                    hipR = new Vector3(-3f, 0f, -1f);
                    knL = new Vector3(2f, 0f, 0f);
                    knR = new Vector3(2f, 0f, 0f);
                    break;
            }

            float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            Blend(rig.torso, torso, k);
            Blend(rig.neck, headR, k);
            Blend(rig.shoulderL, shL, k);
            Blend(rig.shoulderR, shR, k);
            Blend(rig.elbowL, elL, k);
            Blend(rig.elbowR, elR, k);
            Blend(rig.hipL, hipL, k);
            Blend(rig.hipR, hipR, k);
            Blend(rig.kneeL, knL, k);
            Blend(rig.kneeR, knR, k);
        }

        private static void Blend(Transform joint, Vector3 targetEuler, float k)
        {
            joint.localRotation = Quaternion.Slerp(
                joint.localRotation, Quaternion.Euler(targetEuler), k);
        }
    }
}
