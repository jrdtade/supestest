using UnityEngine;

namespace LastSon
{
    /// <summary>Minimal IMGUI overlay: speed, altitude, state, and a controls card.</summary>
    public class FlightHUD : MonoBehaviour
    {
        public FlightController controller;
        private bool showHelp = true;
        private Texture2D panelTex;
        private GUIStyle label, big, help;

        private void EnsureStyles()
        {
            if (panelTex != null) return;
            panelTex = new Texture2D(1, 1);
            panelTex.SetPixel(0, 0, new Color(0f, 0f, 0.03f, 0.55f));
            panelTex.Apply();

            label = new GUIStyle();
            label.normal.textColor = new Color(0.92f, 0.95f, 1f);
            label.fontSize = 16;

            big = new GUIStyle(label);
            big.fontSize = 26;
            big.fontStyle = FontStyle.Bold;

            help = new GUIStyle(label);
            help.fontSize = 14;
            help.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H)) showHelp = !showHelp;
        }

        private void OnGUI()
        {
            if (controller == null) return;
            EnsureStyles();

            float kmh = controller.Speed * 3.6f;
            float alt = controller.transform.position.y;

            GUI.DrawTexture(new Rect(14, 14, 210, 84), panelTex);
            GUI.Label(new Rect(26, 20, 200, 32), string.Format("{0:0} km/h", kmh), big);
            GUI.Label(new Rect(26, 52, 200, 22), string.Format("ALT  {0:0} m", alt), label);
            GUI.Label(new Rect(26, 72, 200, 22), controller.State.ToString().ToUpperInvariant(), label);

            if (controller.Boosting)
            {
                GUI.Label(new Rect(Screen.width / 2f - 60, Screen.height * 0.82f, 200, 30),
                    "MACH SPEED", big);
            }

            // Crosshair dot.
            GUI.DrawTexture(new Rect(Screen.width / 2f - 2, Screen.height / 2f - 2, 4, 4), panelTex);

            if (showHelp)
            {
                if (TouchControls.UsingTouch)
                {
                    // Top-left so the card clears the joystick and buttons.
                    GUI.DrawTexture(new Rect(14, 108, 340, 134), panelTex);
                    float y = 116;
                    GUI.Label(new Rect(26, y, 340, 20), "LEFT STICK - fly (up follows your aim)", help); y += 20;
                    GUI.Label(new Rect(26, y, 340, 20), "DRAG RIGHT - look / steer", help); y += 20;
                    GUI.Label(new Rect(26, y, 340, 20), "▲ / ▼ - ascend / descend", help); y += 20;
                    GUI.Label(new Rect(26, y, 340, 20), "BOOST - super-speed", help); y += 20;
                    GUI.Label(new Rect(26, y, 340, 20), "▲ near ground - take off", help); y += 20;
                    GUI.Label(new Rect(26, y, 340, 20), "Fan prototype - personal use only", help);
                }
                else
                {
                    GUI.DrawTexture(new Rect(14, Screen.height - 168, 320, 154), panelTex);
                    float y = Screen.height - 160;
                    GUI.Label(new Rect(26, y, 320, 20), "MOUSE - look / steer", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "W A S D - fly (W follows your aim)", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "SPACE / CTRL - ascend / descend", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "HOLD SHIFT - super-speed", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "SPACE near ground - take off / land", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "H - toggle help   ESC - release mouse", help); y += 20;
                    GUI.Label(new Rect(26, y, 320, 20), "Fan prototype - personal use only", help);
                }
            }
        }
    }
}
