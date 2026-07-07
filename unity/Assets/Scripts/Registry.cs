using System.Collections.Generic;
using UnityEngine;

namespace LastSon
{
    /// <summary>One entry in the X-ray highlight list.</summary>
    public class HighlightEntry
    {
        public Renderer renderer;
        public Color color;
        public HighlightEntry(Renderer r, Color c) { renderer = r; color = c; }
    }

    /// <summary>
    /// Scene-wide registries: what X-ray vision should fade (buildings) and
    /// what it should highlight (NPCs, props), plus global references and the
    /// panic broadcast that sends civilians running from explosions.
    /// </summary>
    public static class Registry
    {
        public static readonly List<Renderer> XRayOccluders = new List<Renderer>();
        public static readonly List<HighlightEntry> Highlights = new List<HighlightEntry>();
        public static readonly List<CivilianNPC> Civilians = new List<CivilianNPC>();

        public static CameraRig Cam;
        public static Transform Player;

        public static void Clear()
        {
            XRayOccluders.Clear();
            Highlights.Clear();
            Civilians.Clear();
            Cam = null;
            Player = null;
        }

        public static void ScareAt(Vector3 pos, float radius)
        {
            for (int i = 0; i < Civilians.Count; i++)
            {
                if (Civilians[i] != null) Civilians[i].Flee(pos, radius);
            }
        }
    }
}
