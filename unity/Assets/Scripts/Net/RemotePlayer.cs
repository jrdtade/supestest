using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// A networked peer's Superman, rendered locally. Reuses the same
    /// procedural <see cref="SupermanModel"/> as the local player (cape and
    /// all), but carries no physics or controllers - it is purely a visual
    /// puppet that eases toward the transform snapshots relayed by the host.
    /// A floating name tag identifies who it is.
    /// </summary>
    public class RemotePlayer : MonoBehaviour
    {
        public string PlayerName = "Kryptonian";

        private Transform visual;
        private Vector3 targetPos, smoothPos;
        private float targetYaw, targetPitch, targetBank;
        private bool primed;

        private static GUIStyle tagStyle;

        public static RemotePlayer Create(string id, string name)
        {
            var go = new GameObject("Remote:" + name);
            SupermanRig rig = SupermanModel.Build(go.transform);
            var rp = go.AddComponent<RemotePlayer>();
            rp.PlayerName = name;
            rp.visual = rig.visualRoot;
            return rp;
        }

        public void SetTarget(Vector3 pos, float yaw, float pitch, float bank)
        {
            targetPos = pos;
            targetYaw = yaw;
            targetPitch = pitch;
            targetBank = bank;

            if (!primed)
            {
                smoothPos = pos;
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                primed = true;
            }
        }

        private void Update()
        {
            // Frame-rate independent smoothing toward the latest snapshot.
            float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
            smoothPos = Vector3.Lerp(smoothPos, targetPos, k);
            transform.position = smoothPos;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, targetYaw, 0f), k);
            if (visual != null)
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(targetPitch, 0f, targetBank), k);
        }

        private void OnGUI()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.25f);
            if (sp.z <= 0f) return;

            if (tagStyle == null)
            {
                tagStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                tagStyle.normal.textColor = new Color(0.75f, 0.9f, 1f);
            }

            var r = new Rect(sp.x - 80f, Screen.height - sp.y - 26f, 160f, 20f);
            var shadow = r; shadow.x += 1f; shadow.y += 1f;
            Color prev = tagStyle.normal.textColor;
            tagStyle.normal.textColor = new Color(0f, 0f, 0.05f, 0.8f);
            GUI.Label(shadow, PlayerName, tagStyle);
            tagStyle.normal.textColor = prev;
            GUI.Label(r, PlayerName, tagStyle);
        }

        public void Remove()
        {
            if (this != null) Destroy(gameObject);
        }
    }
}
