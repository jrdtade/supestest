using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// IMGUI overlay: speed/altitude/state, active-power indicators, combo
    /// name popups, carrying prompt, and the controls card.
    /// </summary>
    public class FlightHUD : MonoBehaviour
    {
        public FlightController controller;
        public PowersController powers;
        public CombatSystem combat;

        private bool showHelp = true;
        private Texture2D panelTex;
        private GUIStyle label, big, help, combo;

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

            combo = new GUIStyle(label);
            combo.fontSize = 30;
            combo.fontStyle = FontStyle.Bold;
            combo.alignment = TextAnchor.MiddleCenter;
            combo.normal.textColor = new Color(1f, 0.85f, 0.35f);
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

            // Active power indicators.
            if (powers != null)
            {
                float py = 106;
                if (powers.HeatActive)
                {
                    GUI.DrawTexture(new Rect(14, py, 130, 26), panelTex);
                    GUI.Label(new Rect(26, py + 4, 130, 20), "HEAT VISION", label);
                    py += 30;
                }
                if (powers.FreezeActive)
                {
                    GUI.DrawTexture(new Rect(14, py, 130, 26), panelTex);
                    GUI.Label(new Rect(26, py + 4, 130, 20), "FREEZE BREATH", label);
                    py += 30;
                }
                if (powers.XRayOn)
                {
                    GUI.DrawTexture(new Rect(14, py, 130, 26), panelTex);
                    GUI.Label(new Rect(26, py + 4, 130, 20), "X-RAY VISION", label);
                }
                if (powers.Held != null)
                {
                    GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height * 0.74f, 300, 30),
                        "LMB  HURL      E  SET DOWN", combo);
                }
            }

            if (controller.Boosting || controller.State == MoveState.Sprint)
            {
                GUI.Label(new Rect(Screen.width / 2f - 100, Screen.height * 0.82f, 200, 30),
                    controller.State == MoveState.Sprint ? "SUPER SPRINT" : "MACH SPEED", big);
            }

            // Combo name popup.
            if (combat != null && Time.time - combat.LastAttackTime < 0.8f &&
                !string.IsNullOrEmpty(combat.LastAttackName))
            {
                float a = 1f - (Time.time - combat.LastAttackTime) / 0.8f;
                Color c = combo.normal.textColor;
                c.a = a;
                combo.normal.textColor = c;
                GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height * 0.68f, 300, 36),
                    combat.LastAttackName, combo);
                c.a = 1f;
                combo.normal.textColor = c;
            }

            // Crosshair dot.
            GUI.DrawTexture(new Rect(Screen.width / 2f - 2, Screen.height / 2f - 2, 4, 4), panelTex);

            if (showHelp)
            {
                GUI.DrawTexture(new Rect(14, Screen.height - 248, 360, 234), panelTex);
                float y = Screen.height - 240;
                GUI.Label(new Rect(26, y, 360, 20), "MOUSE - look / steer", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "W A S D - move (airborne: W follows your aim)", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "SPACE / CTRL - up / down · SPACE - take off", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "SHIFT - boost (air) / super sprint (ground)", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "LMB - punch    RMB - kick   (chain combos!)", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "   P-P-P haymaker · P-P-K launcher · K-K spin", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "Q (hold) - heat vision at the crosshair", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "F (hold) - freeze breath · punch ice to shatter", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "X - x-ray vision    E - grab / set down props", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "H - toggle help · ESC - release mouse", help); y += 20;
                GUI.Label(new Rect(26, y, 360, 20), "Fan prototype - personal use only", help);
            }
        }
    }
}
