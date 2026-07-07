using UnityEngine;

namespace LastSon
{
    /// <summary>
    /// IMGUI lobby screen for <see cref="LanMultiplayer"/>: name your hero,
    /// host a game, browse the live LAN lobby list and join with one tap, or
    /// type a host IP directly. While hosting or connected it shows the peer
    /// count and a leave button. Opens on start; toggle with M or the on-screen
    /// MP button (so it also works on touch devices).
    /// </summary>
    public class NetworkMenu : MonoBehaviour
    {
        public LanMultiplayer net;

        private bool open = true;
        private string ipField = "";
        private string codeField = "";
        private Vector2 lobbyScroll;

        private Texture2D panelTex, rowTex, btnTex, tabOnTex, tabOffTex;
        private GUIStyle title, label, small, button, row, tabOn, tabOff, code;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) open = !open;
        }

        private void EnsureStyles()
        {
            if (panelTex != null) return;

            panelTex = Solid(new Color(0.02f, 0.03f, 0.08f, 0.92f));
            rowTex = Solid(new Color(0.10f, 0.14f, 0.22f, 0.9f));
            btnTex = Solid(new Color(0.14f, 0.30f, 0.55f, 0.95f));
            tabOnTex = Solid(new Color(0.14f, 0.30f, 0.55f, 0.95f));
            tabOffTex = Solid(new Color(0.08f, 0.10f, 0.16f, 0.9f));

            label = new GUIStyle { fontSize = 15 };
            label.normal.textColor = new Color(0.9f, 0.94f, 1f);

            title = new GUIStyle(label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color(0.75f, 0.88f, 1f);

            small = new GUIStyle(label) { fontSize = 13 };
            small.normal.textColor = new Color(0.7f, 0.78f, 0.9f);

            button = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            button.normal.textColor = Color.white;
            button.normal.background = btnTex;
            button.hover.background = btnTex;
            button.active.background = Solid(new Color(0.2f, 0.42f, 0.72f, 1f));
            button.padding = new RectOffset(10, 10, 8, 8);

            row = new GUIStyle(label) { fontSize = 15 };

            tabOn = new GUIStyle(button) { fontSize = 15 };
            tabOn.normal.background = tabOnTex; tabOn.hover.background = tabOnTex;
            tabOff = new GUIStyle(button) { fontSize = 15 };
            tabOff.normal.background = tabOffTex; tabOff.hover.background = tabOffTex;
            tabOff.normal.textColor = new Color(0.65f, 0.72f, 0.85f);

            code = new GUIStyle(label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            code.normal.textColor = new Color(1f, 0.85f, 0.35f);
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (net == null) return;
            EnsureStyles();

            // Toggle button, top-right (clears the HUD panels on the left).
            float bw = 84f;
            if (GUI.Button(new Rect(Screen.width - bw - 14f, 14f, bw, 34f),
                open ? "CLOSE" : (net.Role == NetRole.Offline ? "MULTIPLAYER" : "MP: " + net.PeerCount), button))
            {
                open = !open;
            }

            if (!open) return;

            float w = Mathf.Min(440f, Screen.width - 40f);
            float h = Mathf.Min(520f, Screen.height - 60f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.DrawTexture(panel, panelTex);

            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, panel.width - 40f, panel.height - 32f));
            GUILayout.Label("MULTIPLAYER", title);
            GUILayout.Space(6f);

            // LAN / Online tabs (locked while in a session).
            GUI.enabled = net.Role == NetRole.Offline;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SAME WI-FI", net.Transport == NetTransport.Lan ? tabOn : tabOff, GUILayout.Height(32f)))
                net.SetTransport(NetTransport.Lan);
            if (GUILayout.Button("ONLINE", net.Transport == NetTransport.Online ? tabOn : tabOff, GUILayout.Height(32f)))
                net.SetTransport(NetTransport.Online);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            GUILayout.Space(8f);

            if (net.Role != NetRole.Offline) DrawSession();
            else if (net.Transport == NetTransport.Online) DrawOnlineBrowser();
            else DrawLobbyBrowser();

            if (!string.IsNullOrEmpty(net.StatusText))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(net.StatusText, small);
            }
            GUILayout.EndArea();
        }

        private void DrawLobbyBrowser()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", label, GUILayout.Width(50f));
            net.LocalName = GUILayout.TextField(net.LocalName, 20, GUILayout.Height(26f));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            if (GUILayout.Button("HOST GAME", button, GUILayout.Height(40f)))
                net.StartHost();

            GUILayout.Space(10f);
            GUILayout.Label("LOBBIES ON YOUR NETWORK", small);

            lobbyScroll = GUILayout.BeginScrollView(lobbyScroll, GUILayout.ExpandHeight(true));
            var lobbies = net.Lobbies;
            if (lobbies.Count == 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Searching for games...", small);
            }
            for (int i = 0; i < lobbies.Count; i++)
            {
                LobbyInfo l = lobbies[i];
                Rect r = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(r, rowTex);
                GUI.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 100f, 22f),
                    l.name + "   (" + l.players + " here)", row);
                if (GUI.Button(new Rect(r.xMax - 84f, r.y + 4f, 80f, 26f), "JOIN", button))
                    net.JoinLobby(l);
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("OR JOIN BY IP", small);
            GUILayout.BeginHorizontal();
            ipField = GUILayout.TextField(ipField, 24, GUILayout.Height(26f));
            if (GUILayout.Button("GO", button, GUILayout.Width(60f), GUILayout.Height(26f)))
            {
                if (net.JoinByIp(ipField)) { /* status set by net */ }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawOnlineBrowser()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", label, GUILayout.Width(50f));
            net.LocalName = GUILayout.TextField(net.LocalName, 20, GUILayout.Height(26f));
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("RELAY SERVER ADDRESS", small);
            GUILayout.BeginHorizontal();
            net.RelayAddress = GUILayout.TextField(net.RelayAddress, 64, GUILayout.Height(26f));
            if (GUILayout.Button(net.RelayConnected ? "RECONNECT" : "CONNECT", button, GUILayout.Width(112f), GUILayout.Height(26f)))
                net.ConnectRelay(net.RelayAddress);
            GUILayout.EndHorizontal();
            GUILayout.Label(net.RelayConnected ? "connected" : "not connected", small);

            if (!net.RelayConnected)
            {
                GUILayout.Space(8f);
                GUILayout.Label("Enter a relay address as host:port.", small);
                return;
            }

            GUILayout.Space(8f);
            if (GUILayout.Button("HOST ONLINE GAME", button, GUILayout.Height(40f)))
                net.HostOnline();

            GUILayout.Space(10f);
            GUILayout.Label("PUBLIC GAMES", small);
            lobbyScroll = GUILayout.BeginScrollView(lobbyScroll, GUILayout.ExpandHeight(true));
            var rooms = net.OnlineRooms;
            if (rooms.Count == 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("No open games right now.", small);
            }
            for (int i = 0; i < rooms.Count; i++)
            {
                LobbyInfo l = rooms[i];
                Rect r = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(r, rowTex);
                GUI.Label(new Rect(r.x + 8f, r.y + 6f, r.width - 100f, 22f),
                    l.hostId + "  " + l.name + "  (" + l.players + ")", row);
                if (GUI.Button(new Rect(r.xMax - 84f, r.y + 4f, 80f, 26f), "JOIN", button))
                    net.JoinOnline(l);
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();

            GUILayout.Space(6f);
            GUILayout.Label("OR JOIN BY CODE", small);
            GUILayout.BeginHorizontal();
            codeField = GUILayout.TextField(codeField, 8, GUILayout.Height(26f));
            if (GUILayout.Button("GO", button, GUILayout.Width(60f), GUILayout.Height(26f)))
                net.JoinOnlineCode(codeField);
            GUILayout.EndHorizontal();
        }

        private void DrawSession()
        {
            bool online = net.Transport == NetTransport.Online;
            string headline = net.Role == NetRole.Host ? "HOSTING" : "CONNECTED";
            GUILayout.Label(headline + (online ? "  (online)" : "  (same Wi-Fi)"), label);

            if (online && net.Role == NetRole.Host && !string.IsNullOrEmpty(net.RoomCode))
            {
                GUILayout.Space(6f);
                GUILayout.Label("SHARE THIS CODE", small);
                GUILayout.Label(net.RoomCode, code);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Players in game:  " + net.PeerCount, label);

            GUILayout.Space(10f);
            if (net.Role == NetRole.Host)
                GUILayout.Label(online
                    ? "Friends can join from anywhere with your code.\nFly and fight together in the city."
                    : "Others on your Wi-Fi can find and join.\nFly and fight together in the city.", small);
            else
                GUILayout.Label("You're in the shared city.\nOther heroes appear around you in real time.", small);

            GUILayout.Space(14f);
            if (GUILayout.Button("BACK TO GAME", button, GUILayout.Height(38f)))
                open = false;

            GUILayout.Space(6f);
            if (GUILayout.Button(net.Role == NetRole.Host ? "STOP HOSTING" : "LEAVE GAME", button, GUILayout.Height(34f)))
                net.Leave();
        }
    }
}
