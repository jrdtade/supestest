using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// On-screen touch controls for Android: a floating left-thumb joystick for
    /// flight direction, a right-side drag zone for camera look, and ASCEND /
    /// DESCEND / BOOST buttons on the lower right. Multi-touch aware (steer,
    /// look, and press a button at once). Everything is drawn with IMGUI to
    /// match the code-only architecture - no prefabs or canvases needed.
    ///
    /// FlightController and CameraRig read the static properties below and blend
    /// them with keyboard/mouse, so desktop play is unaffected. On-screen
    /// controls appear only on mobile platforms (or when <see cref="simulateWithMouse"/>
    /// is enabled for a quick Editor smoke test with a single mouse pointer).
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        public static TouchControls Instance { get; private set; }

        // --- Input state consumed elsewhere -----------------------------------
        public static bool UsingTouch { get; private set; }
        public static Vector2 MoveAxis { get; private set; }    // left stick, components in -1..1
        public static float VerticalAxis { get; private set; }  // +1 ascend, -1 descend
        public static bool Boost { get; private set; }
        public static bool AscendPressed { get; private set; }  // rising edge (take-off)
        public static Vector2 LookDelta { get; private set; }   // this-frame drag, y-up like the mouse

        [Tooltip("Drive the touch UI with the mouse as a single finger for Editor testing.")]
        public bool simulateWithMouse = false;

        private const int NONE = -9999;

        // Layout (recomputed each frame in GUI space: origin top-left, y grows down).
        private float unit;                 // scale reference = min screen dimension
        private float stickRadius;
        private Vector2 stickAnchor;        // idle hint position
        private Rect boostRect, upRect, downRect;

        // Active-finger bookkeeping.
        private int stickId = NONE;
        private Vector2 stickCenter, stickPos;
        private int lookId = NONE;
        private Vector2 lookLast;
        private int boostId = NONE, upId = NONE, downId = NONE;

        private Texture2D discTex, ringTex;
        private GUIStyle glyphStyle;

        private struct Pointer
        {
            public int id;
            public Vector2 pos;         // GUI space (y down)
            public TouchPhase phase;
        }
        private readonly List<Pointer> pointers = new List<Pointer>();

        private void Awake()
        {
            Instance = this;
            discTex = MakeDisc(128, false);
            ringTex = MakeDisc(128, true);
        }

        private void Update()
        {
            UsingTouch = Application.isMobilePlatform || simulateWithMouse;
            if (!UsingTouch)
            {
                MoveAxis = Vector2.zero; VerticalAxis = 0f; Boost = false;
                AscendPressed = false; LookDelta = Vector2.zero;
                return;
            }

            ComputeLayout();
            GatherPointers();

            LookDelta = Vector2.zero;
            AscendPressed = false;

            for (int i = 0; i < pointers.Count; i++)
            {
                Pointer p = pointers[i];
                switch (p.phase)
                {
                    case TouchPhase.Began:
                        Assign(p.id, p.pos);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        Track(p.id, p.pos);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        Release(p.id);
                        break;
                }
            }

            // Resolve outputs.
            if (stickId != NONE)
            {
                Vector2 d = stickPos - stickCenter;
                if (d.magnitude > stickRadius) d = d.normalized * stickRadius;
                stickPos = stickCenter + d;                 // clamp visual knob too
                MoveAxis = new Vector2(d.x, -d.y) / stickRadius;   // up = forward
            }
            else
            {
                MoveAxis = Vector2.zero;
            }

            VerticalAxis = (upId != NONE ? 1f : 0f) - (downId != NONE ? 1f : 0f);
            Boost = boostId != NONE;
        }

        private void Assign(int id, Vector2 pos)
        {
            if (Hit(boostRect, pos)) { boostId = id; return; }
            if (Hit(upRect, pos)) { upId = id; AscendPressed = true; return; }
            if (Hit(downRect, pos)) { downId = id; return; }

            if (pos.x < Screen.width * 0.5f && stickId == NONE)
            {
                stickId = id;
                stickCenter = pos;
                stickPos = pos;
                return;
            }

            // Anything else on the right half steers the camera.
            lookId = id;
            lookLast = pos;
        }

        private void Track(int id, Vector2 pos)
        {
            if (id == stickId)
            {
                stickPos = pos;
            }
            else if (id == lookId)
            {
                Vector2 delta = pos - lookLast;
                LookDelta += new Vector2(delta.x, -delta.y);   // y-up to mirror the mouse
                lookLast = pos;
            }
        }

        private void Release(int id)
        {
            if (id == stickId) stickId = NONE;
            else if (id == lookId) lookId = NONE;
            else if (id == boostId) boostId = NONE;
            else if (id == upId) upId = NONE;
            else if (id == downId) downId = NONE;
        }

        private void GatherPointers()
        {
            pointers.Clear();

            int tc = Input.touchCount;
            for (int i = 0; i < tc; i++)
            {
                Touch t = Input.GetTouch(i);
                pointers.Add(new Pointer
                {
                    id = t.fingerId,
                    pos = ToGui(t.position),
                    phase = t.phase
                });
            }

            if (simulateWithMouse && tc == 0)
            {
                Vector2 mp = ToGui(Input.mousePosition);
                if (Input.GetMouseButtonDown(0))
                    pointers.Add(new Pointer { id = -1, pos = mp, phase = TouchPhase.Began });
                else if (Input.GetMouseButtonUp(0))
                    pointers.Add(new Pointer { id = -1, pos = mp, phase = TouchPhase.Ended });
                else if (Input.GetMouseButton(0))
                    pointers.Add(new Pointer { id = -1, pos = mp, phase = TouchPhase.Moved });
            }
        }

        private static Vector2 ToGui(Vector2 screenPos)
        {
            return new Vector2(screenPos.x, Screen.height - screenPos.y);
        }

        private void ComputeLayout()
        {
            unit = Mathf.Min(Screen.width, Screen.height);
            stickRadius = unit * 0.16f;
            float margin = unit * 0.05f;
            stickAnchor = new Vector2(margin + stickRadius, Screen.height - margin - stickRadius);

            float br = unit * 0.11f;                 // boost radius
            float sr = unit * 0.085f;                // small-button radius
            float bx = Screen.width - margin - br;
            float by = Screen.height - margin - br;
            boostRect = CircleRect(new Vector2(bx, by), br);

            float cx = bx - br - sr - unit * 0.03f;  // column left of BOOST
            downRect = CircleRect(new Vector2(cx, by), sr);
            upRect = CircleRect(new Vector2(cx, by - sr * 2.4f), sr);
        }

        private static Rect CircleRect(Vector2 center, float radius)
        {
            return new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
        }

        private static bool Hit(Rect r, Vector2 p)
        {
            return Vector2.Distance(r.center, p) <= r.width * 0.5f;
        }

        private void OnGUI()
        {
            if (!UsingTouch || Event.current.type != EventType.Repaint) return;

            if (glyphStyle == null)
            {
                glyphStyle = new GUIStyle();
                glyphStyle.alignment = TextAnchor.MiddleCenter;
                glyphStyle.normal.textColor = new Color(0.95f, 0.97f, 1f, 0.95f);
                glyphStyle.fontStyle = FontStyle.Bold;
            }
            glyphStyle.fontSize = Mathf.RoundToInt(unit * 0.05f);

            // Joystick: faint anchor when idle, live base + knob when engaged.
            if (stickId == NONE)
            {
                DrawTex(ringTex, CircleRect(stickAnchor, stickRadius), new Color(1f, 1f, 1f, 0.14f));
                DrawTex(discTex, CircleRect(stickAnchor, stickRadius * 0.42f), new Color(1f, 1f, 1f, 0.18f));
            }
            else
            {
                DrawTex(ringTex, CircleRect(stickCenter, stickRadius), new Color(0.7f, 0.82f, 1f, 0.28f));
                DrawTex(discTex, CircleRect(stickPos, stickRadius * 0.45f), new Color(0.85f, 0.92f, 1f, 0.55f));
            }

            // Action buttons.
            Color boostCol = boostId != NONE ? new Color(1f, 0.55f, 0.35f, 0.85f) : new Color(0.9f, 0.35f, 0.28f, 0.55f);
            DrawButton(boostRect, boostCol, "BOOST");
            DrawButton(upRect, ButtonCol(upId), "▲");     // ▲
            DrawButton(downRect, ButtonCol(downId), "▼"); // ▼
        }

        private static Color ButtonCol(int id)
        {
            return id != NONE
                ? new Color(0.55f, 0.8f, 1f, 0.8f)
                : new Color(0.35f, 0.5f, 0.7f, 0.5f);
        }

        private void DrawButton(Rect r, Color c, string glyph)
        {
            DrawTex(discTex, r, c);
            GUI.Label(r, glyph, glyphStyle);
        }

        private void DrawTex(Texture2D tex, Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, tex);
            GUI.color = old;
        }

        private static Texture2D MakeDisc(int size, bool ring)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r)) / r; // 0 center .. 1 edge
                    float a;
                    if (ring)
                    {
                        // Bright band near the rim, hollow centre.
                        a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.14f);
                    }
                    else
                    {
                        a = 1f - Mathf.SmoothStep(0.88f, 1f, d);
                    }
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels32(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.Apply();
            return t;
        }
    }
}
