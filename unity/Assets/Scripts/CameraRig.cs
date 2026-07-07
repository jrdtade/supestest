using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// Third-person orbit camera: mouse look, distance and FOV that stretch
    /// with speed, spherecast pull-in so buildings never occlude, and a small
    /// impact kick. Click to capture the cursor, Esc to release.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public float Yaw { get { return yaw; } }

        private Transform target;            // superman root
        private FlightController controller;
        private Camera cam;
        private float yaw = 15f;
        private float pitch = 12f;
        private float kick;

        private const float BASE_DIST = 4.4f;
        private const float BOOST_DIST = 7.2f;
        private const float BASE_FOV = 62f;
        private const float BOOST_FOV = 86f;
        private const float SENS = 2.6f;

        public void Init(Transform followTarget, FlightController fc)
        {
            target = followTarget;
            controller = fc;
            cam = GetComponent<Camera>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Kick(float amount)
        {
            kick = Mathf.Max(kick, amount);
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            // Cursor capture toggling.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += Input.GetAxis("Mouse X") * SENS;
                pitch -= Input.GetAxis("Mouse Y") * SENS;
                pitch = Mathf.Clamp(pitch, -65f, 78f);
            }

            float speedT = Mathf.Clamp01(controller.Speed / 120f);
            float dist = Mathf.Lerp(BASE_DIST, BOOST_DIST, speedT);
            Vector3 focus = target.position + Vector3.up * 1.35f;

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = focus - rot * Vector3.forward * dist;

            // Pull in if something solid is between focus and camera.
            RaycastHit hit;
            Vector3 dir = desired - focus;
            if (Physics.SphereCast(focus, 0.3f, dir.normalized, out hit, dir.magnitude))
            {
                desired = focus + dir.normalized * Mathf.Max(hit.distance - 0.1f, 0.6f);
            }

            // Impact kick decay.
            if (kick > 0.001f)
            {
                desired += Random.insideUnitSphere * kick * 0.4f;
                kick = Mathf.Lerp(kick, 0f, 8f * dt);
            }

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-14f * dt));
            transform.rotation = rot;

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView,
                Mathf.Lerp(BASE_FOV, BOOST_FOV, speedT), 1f - Mathf.Exp(-5f * dt));
        }
    }
}
