using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LastSon
{
    public enum NetRole { Offline, Host, Client }

    /// <summary>LAN = same-Wi-Fi UDP; Online = internet via the relay server.</summary>
    public enum NetTransport { Lan, Online }

    /// <summary>One discovered host, as advertised over the LAN beacon.</summary>
    public class LobbyInfo
    {
        public string hostId;
        public string name;
        public IPAddress address;
        public int gamePort;
        public int players;
        public float lastSeen;
    }

    /// <summary>
    /// Basic multiplayer manager. LAN mode uses raw UDP on the same Wi-Fi.
    /// Online mode uses the sibling Node relay server over newline-delimited
    /// TCP JSON so players can host/join by room code over the internet.
    /// Remote players are rendered as lightweight <see cref="RemotePlayer"/>
    /// ghosts.
    /// </summary>
    public class LanMultiplayer : MonoBehaviour
    {
        public const int DiscoveryPort = 47777;
        public const int GamePort = 47778;
        public const string Version = "LS1";

        private const float SendInterval = 1f / 15f;
        private const float BeaconInterval = 1f;
        private const float LobbyTimeout = 5f;
        private const float PlayerTimeout = 6f;

        public const int DefaultRelayPort = 7777;
        public const string DefaultRelayAddress = "hayabusa.proxy.rlwy.net:53046";

        public NetRole Role { get; private set; } = NetRole.Offline;
        public NetTransport Transport { get; private set; } = NetTransport.Lan;
        public string LocalName = "Kryptonian";
        public string StatusText { get; private set; } = "";
        public IReadOnlyList<LobbyInfo> Lobbies => lobbyList;

        // Online (relay) surface for the menu.
        public string RelayAddress = DefaultRelayAddress;
        public bool RelayConnected { get; private set; }
        public string RoomCode { get; private set; } = "";
        public IReadOnlyList<LobbyInfo> OnlineRooms => onlineRooms;

        public int PeerCount
        {
            get
            {
                if (Role == NetRole.Offline) return 0;
                if (Transport == NetTransport.Online) return ghosts.Count + 1;
                return Role == NetRole.Host ? clients.Count + 1 : ghosts.Count + 1;
            }
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private string localId;
        private readonly List<LobbyInfo> lobbyList = new List<LobbyInfo>();
        private readonly Dictionary<string, LobbyInfo> lobbies = new Dictionary<string, LobbyInfo>();

        // Sockets / threads.
        private UdpClient discoverySock;   // always listening for beacons (lobby browsing)
        private Thread discoveryThread;
        private UdpClient beaconSock;      // host: sends beacons
        private UdpClient gameSock;        // host: bound to GamePort; client: ephemeral -> host
        private Thread gameThread;
        private volatile bool running;
        private readonly ConcurrentQueue<Packet> inbox = new ConcurrentQueue<Packet>();

        // Host bookkeeping.
        private class ClientRec { public IPEndPoint ep; public float lastSeen; public PlayerSnapshot snap; }
        private readonly Dictionary<string, ClientRec> clients = new Dictionary<string, ClientRec>();

        // Client bookkeeping.
        private IPEndPoint hostEp;
        private float hostLastSeen;
        private string hostId = "";
        private string hostName = "";

        // Online relay (TCP to the relay server).
        private TcpClient relay;
        private NetworkStream relayStream;
        private Thread relayThread;
        private volatile bool relayWantConnect;
        private readonly ConcurrentQueue<string> relayInbox = new ConcurrentQueue<string>();
        private readonly List<LobbyInfo> onlineRooms = new List<LobbyInfo>();
        private string relayHost;
        private int relayPort = DefaultRelayPort;
        private string myRelayId = "";
        private float listTimer;

        // Rendered remote peers.
        private readonly Dictionary<string, RemotePlayer> ghosts = new Dictionary<string, RemotePlayer>();
        private readonly List<string> scratchRemove = new List<string>();
        private readonly List<PlayerSnapshot> scratchSnaps = new List<PlayerSnapshot>();

        private float sendTimer, beaconTimer;
        private Transform localVisual;

        // JSON shapes exchanged with the relay (JsonUtility fills matching
        // fields and ignores the rest, so one class covers every message type).
        [Serializable] private class RelayMsg { public string t, code, host, msg, id; public RelayRoom[] rooms; public RelayState[] players; }
        [Serializable] private class RelayRoom { public string code, name; public int players; }
        [Serializable] private class RelayState { public string id, name; public float x, y, z, yaw, pitch, bank; public int anim; }

        private struct Packet { public string data; public IPEndPoint from; }

        private struct PlayerSnapshot
        {
            public string id, name;
            public Vector3 pos;
            public float yaw, pitch, bank;
            public byte anim;
        }

        // ------------------------------------------------------------------ //
        // Lifecycle
        // ------------------------------------------------------------------ //
        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(RelayAddress))
                RelayAddress = DefaultRelayAddress;
            localId = Guid.NewGuid().ToString("N").Substring(0, 8);
            running = true;
            AcquireMulticastLock();
            StartDiscoveryListener();
        }

        private void Update()
        {
            float now = Time.time;
            float dt = Time.deltaTime;

            // Drain everything the network threads received since last frame.
            while (inbox.TryDequeue(out Packet p)) Handle(p);
            while (relayInbox.TryDequeue(out string line)) HandleRelay(line);

            ExpireLobbies(now);

            if (Transport == NetTransport.Online)
            {
                UpdateOnline(dt);
                return;
            }

            if (Role == NetRole.Host)
            {
                beaconTimer += dt;
                if (beaconTimer >= BeaconInterval) { beaconTimer = 0f; SendBeacon(); }

                sendTimer += dt;
                if (sendTimer >= SendInterval) { sendTimer = 0f; HostTick(now); }
            }
            else if (Role == NetRole.Client)
            {
                sendTimer += dt;
                if (sendTimer >= SendInterval) { sendTimer = 0f; Send(gameSock, State(Local()), hostEp); }

                if (hostLastSeen > 0f && now - hostLastSeen > PlayerTimeout)
                {
                    StatusText = "Host connection lost";
                    Leave();
                }
            }
        }

        private void UpdateOnline(float dt)
        {
            if (!RelayConnected) return;

            // In a room: stream our transform to the relay ~15x/s.
            if (Role != NetRole.Offline)
            {
                sendTimer += dt;
                if (sendTimer >= SendInterval) { sendTimer = 0f; SendRelayState(); }
            }
            else
            {
                // Browsing: poll the public room list every couple of seconds.
                listTimer += dt;
                if (listTimer >= 2f) { listTimer = 0f; SendRelay("{\"t\":\"list\"}"); }
            }
        }

        private void OnDestroy() { Shutdown(); }
        private void OnApplicationQuit() { Shutdown(); }

        private void Shutdown()
        {
            if (!running && discoverySock == null) return;
            try { if (Transport == NetTransport.Lan && Role == NetRole.Host) BroadcastToClients(Msg("BYE", localId)); } catch { }
            try { if (Transport == NetTransport.Lan && Role == NetRole.Client && hostEp != null) Send(gameSock, Msg("BYE", localId), hostEp); } catch { }
            DisconnectRelay();
            running = false;
            CloseSocket(ref gameSock);
            CloseSocket(ref beaconSock);
            CloseSocket(ref discoverySock);
            ReleaseMulticastLock();
            DestroyAllGhosts();
        }

        // ------------------------------------------------------------------ //
        // Public API (driven by NetworkMenu)
        // ------------------------------------------------------------------ //
        public void StartHost()
        {
            SetTransport(NetTransport.Lan);
            try
            {
                gameSock = new UdpClient();
                gameSock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                gameSock.Client.Bind(new IPEndPoint(IPAddress.Any, GamePort));
                beaconSock = new UdpClient { EnableBroadcast = true };
            }
            catch (Exception e)
            {
                StatusText = "Could not host: " + e.Message;
                Debug.LogWarning("[MP] host failed: " + e);
                CloseSocket(ref gameSock);
                CloseSocket(ref beaconSock);
                return;
            }

            Role = NetRole.Host;
            clients.Clear();
            StartGameThread(gameSock);
            SendBeacon();
            StatusText = "Hosting \"" + LocalName + "\"";
        }

        public void JoinLobby(LobbyInfo l)
        {
            if (l == null) return;
            ConnectTo(l.hostId, l.name, new IPEndPoint(l.address, l.gamePort));
        }

        public bool JoinByIp(string ip)
        {
            if (!IPAddress.TryParse(ip.Trim(), out IPAddress addr))
            {
                StatusText = "Invalid IP address";
                return false;
            }
            ConnectTo("", ip.Trim(), new IPEndPoint(addr, GamePort));
            return true;
        }

        private void ConnectTo(string id, string name, IPEndPoint ep)
        {
            SetTransport(NetTransport.Lan);
            try
            {
                gameSock = new UdpClient(); // OS-assigned local port
            }
            catch (Exception e)
            {
                StatusText = "Could not join: " + e.Message;
                return;
            }

            Role = NetRole.Client;
            hostId = id;
            hostName = name;
            hostEp = ep;
            hostLastSeen = Time.time;
            StartGameThread(gameSock);
            Send(gameSock, Msg("JOIN", localId, San(LocalName)), hostEp);
            StatusText = "Joining \"" + name + "\"...";
        }

        /// <summary>Leave the current room/session (keeps the relay connection
        /// alive when online so the player lands back on the lobby list).</summary>
        public void Leave()
        {
            if (Transport == NetTransport.Online)
            {
                if (Role != NetRole.Offline) SendRelay("{\"t\":\"leave\"}");
                Role = NetRole.Offline;
                RoomCode = "";
                DestroyAllGhosts();
                return;
            }

            if (Role == NetRole.Host) { try { BroadcastToClients(Msg("BYE", localId)); } catch { } }
            else if (Role == NetRole.Client && hostEp != null) { try { Send(gameSock, Msg("BYE", localId), hostEp); } catch { } }

            Role = NetRole.Offline;
            // Close the sockets first so the blocking Receive unblocks, then the
            // join returns immediately instead of waiting out the timeout.
            CloseSocket(ref gameSock);
            CloseSocket(ref beaconSock);
            StopGameThread();
            clients.Clear();
            hostEp = null;
            hostId = "";
            hostLastSeen = 0f;
            DestroyAllGhosts();
        }

        /// <summary>Switch between LAN and Online, tearing down whatever the
        /// previous transport had open.</summary>
        public void SetTransport(NetTransport t)
        {
            Leave();
            if (Transport == NetTransport.Online && t != NetTransport.Online)
                DisconnectRelay();
            Transport = t;
            onlineRooms.Clear();
            StatusText = "";
        }

        // ------------------------------------------------------------------ //
        // Online transport (relay server over TCP)
        // ------------------------------------------------------------------ //
        public void ConnectRelay(string address)
        {
            SetTransport(NetTransport.Online);
            if (!ParseAddress(address, out relayHost, out relayPort))
            {
                StatusText = "Enter a relay address, e.g. myhost.com:7777";
                return;
            }
            RelayAddress = address.Trim();
            StartRelayThread();
            StatusText = "Connecting to " + relayHost + "...";
        }

        public void HostOnline()
        {
            if (!RelayConnected) { StatusText = "Connect to a relay first"; return; }
            Leave();
            Role = NetRole.Host;
            SendRelay("{\"t\":\"host\",\"name\":\"" + JsonEsc(San(LocalName)) + "\"}");
            StatusText = "Creating online game...";
        }

        public void JoinOnlineCode(string code)
        {
            if (!RelayConnected) { StatusText = "Connect to a relay first"; return; }
            if (string.IsNullOrWhiteSpace(code)) { StatusText = "Enter a room code"; return; }
            Leave();
            Role = NetRole.Client;
            SendRelay("{\"t\":\"join\",\"code\":\"" + JsonEsc(code.Trim().ToUpperInvariant()) + "\",\"name\":\"" + JsonEsc(San(LocalName)) + "\"}");
            StatusText = "Joining " + code.Trim().ToUpperInvariant() + "...";
        }

        public void JoinOnline(LobbyInfo room) { if (room != null) JoinOnlineCode(room.hostId); }

        private void HandleRelay(string line)
        {
            RelayMsg m;
            try { m = JsonUtility.FromJson<RelayMsg>(line); }
            catch { return; }
            if (m == null || string.IsNullOrEmpty(m.t)) return;

            switch (m.t)
            {
                case "_connected":
                    RelayConnected = true;
                    StatusText = "Connected to relay";
                    SendRelay("{\"t\":\"list\"}");
                    break;
                case "_disconnected":
                    RelayConnected = false;
                    if (Transport == NetTransport.Online)
                    {
                        Role = NetRole.Offline;
                        RoomCode = "";
                        onlineRooms.Clear();
                        DestroyAllGhosts();
                        StatusText = "Disconnected from relay";
                    }
                    break;
                case "_error":
                    RelayConnected = false;
                    onlineRooms.Clear();
                    StatusText = "Relay error: " + m.msg;
                    break;
                case "welcome":
                    myRelayId = m.id;
                    break;
                case "hosted":
                    RoomCode = m.code;
                    StatusText = "Online game live - code " + m.code;
                    break;
                case "joined":
                    RoomCode = m.code;
                    StatusText = "Joined " + (string.IsNullOrEmpty(m.host) ? m.code : m.host);
                    break;
                case "error":
                    StatusText = m.msg;
                    if (Role == NetRole.Client && RoomCode == "") Role = NetRole.Offline;
                    break;
                case "rooms":
                    RebuildOnlineRooms(m.rooms);
                    break;
                case "snap":
                    HandleRelaySnap(m.players);
                    break;
                case "bye":
                    RemoveGhost(m.id);
                    break;
            }
        }

        private void RebuildOnlineRooms(RelayRoom[] rooms)
        {
            onlineRooms.Clear();
            if (rooms == null) return;
            foreach (var r in rooms)
                onlineRooms.Add(new LobbyInfo { hostId = r.code, name = r.name, players = r.players });
        }

        private void HandleRelaySnap(RelayState[] players)
        {
            scratchSnaps.Clear();
            if (players != null)
            {
                foreach (var s in players)
                {
                    if (s.id == myRelayId) continue; // don't ghost ourselves
                    scratchSnaps.Add(new PlayerSnapshot
                    {
                        id = s.id,
                        name = s.name,
                        pos = new Vector3(s.x, s.y, s.z),
                        yaw = s.yaw,
                        pitch = s.pitch,
                        bank = s.bank,
                        anim = (byte)Mathf.Clamp(s.anim, 0, 255)
                    });
                }
            }
            SyncGhosts(scratchSnaps);
        }

        private void SendRelayState()
        {
            PlayerSnapshot p = Local();
            var sb = new StringBuilder("{\"t\":\"state\"");
            sb.Append(",\"x\":").Append(F(p.pos.x));
            sb.Append(",\"y\":").Append(F(p.pos.y));
            sb.Append(",\"z\":").Append(F(p.pos.z));
            sb.Append(",\"yaw\":").Append(F(p.yaw));
            sb.Append(",\"pitch\":").Append(F(p.pitch));
            sb.Append(",\"bank\":").Append(F(p.bank));
            sb.Append(",\"anim\":").Append(((int)p.anim).ToString(Inv));
            sb.Append('}');
            SendRelay(sb.ToString());
        }

        private void SendRelay(string json)
        {
            NetworkStream s = relayStream;
            if (s == null) return;
            try { byte[] b = Encoding.UTF8.GetBytes(json + "\n"); s.Write(b, 0, b.Length); }
            catch (Exception) { /* connection dropping; read thread will report */ }
        }

        private void StartRelayThread()
        {
            DisconnectRelay();
            relayWantConnect = true;
            relayThread = new Thread(RelayLoop) { IsBackground = true, Name = "MP-Relay" };
            relayThread.Start();
        }

        private void RelayLoop()
        {
            bool reportedError = false;
            try
            {
                var c = new TcpClient();
                c.NoDelay = true;
                c.Connect(relayHost, relayPort); // blocking
                relay = c;
                relayStream = c.GetStream();
                relayInbox.Enqueue("{\"t\":\"_connected\"}");

                var buf = new byte[4096];
                string acc = "";
                while (relayWantConnect)
                {
                    int n = relayStream.Read(buf, 0, buf.Length);
                    if (n <= 0) break;
                    acc += Encoding.UTF8.GetString(buf, 0, n);
                    int idx;
                    while ((idx = acc.IndexOf('\n')) >= 0)
                    {
                        string msg = acc.Substring(0, idx).Trim();
                        acc = acc.Substring(idx + 1);
                        if (msg.Length > 0) relayInbox.Enqueue(msg);
                    }
                    if (acc.Length > 16384) acc = ""; // runaway guard
                }
            }
            catch (Exception e)
            {
                if (relayWantConnect)
                {
                    reportedError = true;
                    relayInbox.Enqueue("{\"t\":\"_error\",\"msg\":\"" + JsonEsc(e.Message) + "\"}");
                }
            }
            if (!reportedError)
                relayInbox.Enqueue("{\"t\":\"_disconnected\"}");
        }

        private void DisconnectRelay()
        {
            relayWantConnect = false;
            RelayConnected = false;
            myRelayId = "";
            RoomCode = "";
            try { relayStream?.Close(); } catch { }
            try { relay?.Close(); } catch { }
            relayStream = null;
            relay = null;
            Thread t = relayThread;
            relayThread = null;
            if (t != null && t.IsAlive) t.Join(200);
        }

        private static bool ParseAddress(string address, out string host, out int port)
        {
            host = null;
            port = DefaultRelayPort;
            if (string.IsNullOrWhiteSpace(address)) return false;
            string a = address.Trim();
            // Tolerate a pasted URL like tcp://host:port or host:port.
            int scheme = a.IndexOf("://", StringComparison.Ordinal);
            if (scheme >= 0) a = a.Substring(scheme + 3);
            a = a.TrimEnd('/');
            int colon = a.LastIndexOf(':');
            if (colon > 0 && colon < a.Length - 1 && int.TryParse(a.Substring(colon + 1), NumberStyles.Integer, Inv, out int p))
            {
                host = a.Substring(0, colon);
                port = p;
            }
            else
            {
                host = a;
            }
            return host.Length > 0;
        }

        private static string JsonEsc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        // ------------------------------------------------------------------ //
        // Host relay (LAN)
        // ------------------------------------------------------------------ //
        private void HostTick(float now)
        {
            // Drop clients that stopped reporting.
            scratchRemove.Clear();
            foreach (var kv in clients)
                if (now - kv.Value.lastSeen > PlayerTimeout) scratchRemove.Add(kv.Key);
            foreach (var id in scratchRemove) clients.Remove(id);

            // Build the combined snapshot: this host plus every live client.
            scratchSnaps.Clear();
            PlayerSnapshot mine = Local();
            scratchSnaps.Add(mine);
            foreach (var kv in clients) scratchSnaps.Add(kv.Value.snap);

            string snap = Snapshot(scratchSnaps);
            foreach (var kv in clients) Send(gameSock, snap, kv.Value.ep);

            // Show every client as a ghost in the host's own world.
            scratchSnaps.Clear();
            foreach (var kv in clients) scratchSnaps.Add(kv.Value.snap);
            SyncGhosts(scratchSnaps);
        }

        private void SendBeacon()
        {
            if (beaconSock == null) return;
            int players = clients.Count + 1;
            string beacon = Msg("LOBBY", Version, San(LocalName), GamePort.ToString(Inv), players.ToString(Inv), localId);
            try { byte[] b = Encoding.UTF8.GetBytes(beacon); beaconSock.Send(b, b.Length, new IPEndPoint(IPAddress.Broadcast, DiscoveryPort)); }
            catch (Exception e) { Debug.LogWarning("[MP] beacon send failed: " + e.Message); }
        }

        private void BroadcastToClients(string msg)
        {
            foreach (var kv in clients) Send(gameSock, msg, kv.Value.ep);
        }

        // ------------------------------------------------------------------ //
        // Message handling (main thread)
        // ------------------------------------------------------------------ //
        private void Handle(Packet p)
        {
            string[] f = p.data.Split('|');
            if (f.Length < 2 || f[1] != Version) return;

            switch (f[0])
            {
                case "LOBBY":
                    if (Role != NetRole.Host) HandleBeacon(f, p.from);
                    break;
                case "JOIN":
                    if (Role == NetRole.Host && f.Length >= 3) TouchClient(f[2], p.from);
                    break;
                case "STATE":
                    if (Role == NetRole.Host && f.Length >= 3) HandleState(f[2], p.from);
                    break;
                case "SNAP":
                    if (Role == NetRole.Client) HandleSnapshot(f);
                    break;
                case "BYE":
                    HandleBye(f, p.from);
                    break;
            }
        }

        private void HandleBeacon(string[] f, IPEndPoint from)
        {
            // LOBBY | ver | name | gamePort | players | hostId
            if (f.Length < 6) return;
            string id = f[5];
            if (id == localId) return; // our own broadcast

            if (!lobbies.TryGetValue(id, out LobbyInfo l))
            {
                l = new LobbyInfo { hostId = id };
                lobbies[id] = l;
                lobbyList.Add(l);
            }
            l.name = f[2];
            int.TryParse(f[3], NumberStyles.Integer, Inv, out l.gamePort);
            int.TryParse(f[4], NumberStyles.Integer, Inv, out l.players);
            l.address = from.Address;
            l.lastSeen = Time.time;
        }

        private void TouchClient(string id, IPEndPoint from)
        {
            if (!clients.TryGetValue(id, out ClientRec rec))
            {
                rec = new ClientRec { snap = new PlayerSnapshot { id = id, name = "Kryptonian" } };
                clients[id] = rec;
                StatusText = "Hosting - " + (clients.Count + 1) + " players";
            }
            rec.ep = from;
            rec.lastSeen = Time.time;
        }

        private void HandleState(string entry, IPEndPoint from)
        {
            if (!TryParseEntry(entry, out PlayerSnapshot s)) return;
            TouchClient(s.id, from);
            clients[s.id].snap = s;
        }

        private void HandleSnapshot(string[] f)
        {
            hostLastSeen = Time.time;
            if (StatusText.StartsWith("Joining")) StatusText = "Connected to \"" + hostName + "\"";

            scratchSnaps.Clear();
            if (f.Length >= 3 && f[2].Length > 0)
            {
                string[] entries = f[2].Split(';');
                for (int i = 0; i < entries.Length; i++)
                    if (TryParseEntry(entries[i], out PlayerSnapshot s) && s.id != localId)
                        scratchSnaps.Add(s);
            }
            SyncGhosts(scratchSnaps);
        }

        private void HandleBye(string[] f, IPEndPoint from)
        {
            if (f.Length < 3) return;
            string id = f[2];
            if (Role == NetRole.Host)
            {
                clients.Remove(id);
            }
            else if (Role == NetRole.Client && id == hostId)
            {
                StatusText = "Host ended the game";
                Leave();
            }
        }

        // ------------------------------------------------------------------ //
        // Ghost management
        // ------------------------------------------------------------------ //
        private void SyncGhosts(List<PlayerSnapshot> snaps)
        {
            var present = new HashSet<string>();
            for (int i = 0; i < snaps.Count; i++)
            {
                PlayerSnapshot s = snaps[i];
                if (s.id == localId) continue;
                present.Add(s.id);
                if (!ghosts.TryGetValue(s.id, out RemotePlayer g) || g == null)
                {
                    g = RemotePlayer.Create(s.id, s.name);
                    ghosts[s.id] = g;
                }
                g.SetTarget(s.pos, s.yaw, s.pitch, s.bank);
            }

            scratchRemove.Clear();
            foreach (var kv in ghosts)
                if (!present.Contains(kv.Key)) scratchRemove.Add(kv.Key);
            foreach (var id in scratchRemove)
            {
                if (ghosts[id] != null) ghosts[id].Remove();
                ghosts.Remove(id);
            }
        }

        private void RemoveGhost(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!ghosts.TryGetValue(id, out RemotePlayer g)) return;
            if (g != null) g.Remove();
            ghosts.Remove(id);
        }

        private void DestroyAllGhosts()
        {
            foreach (var kv in ghosts)
                if (kv.Value != null) kv.Value.Remove();
            ghosts.Clear();
        }

        private void ExpireLobbies(float now)
        {
            for (int i = lobbyList.Count - 1; i >= 0; i--)
            {
                if (now - lobbyList[i].lastSeen > LobbyTimeout)
                {
                    lobbies.Remove(lobbyList[i].hostId);
                    lobbyList.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ //
        // Local player snapshot
        // ------------------------------------------------------------------ //
        private PlayerSnapshot Local()
        {
            var s = new PlayerSnapshot { id = localId, name = San(LocalName) };
            Transform t = Registry.Player;
            if (t != null)
            {
                s.pos = t.position;
                s.yaw = t.eulerAngles.y;
                if (localVisual == null) localVisual = FindDeep(t, "Visual");
                if (localVisual != null)
                {
                    Vector3 e = localVisual.localEulerAngles;
                    s.pitch = e.x;
                    s.bank = e.z;
                }
                var fc = t.GetComponent<FlightController>();
                if (fc != null) s.anim = (byte)(int)fc.State;
            }
            return s;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------ //
        // Wire format
        // ------------------------------------------------------------------ //
        private static string Msg(params string[] parts) => string.Join("|", parts);

        private static string F(float v) => v.ToString("0.###", Inv);

        private static string Entry(PlayerSnapshot s) => string.Join(",",
            s.id, San(s.name), F(s.pos.x), F(s.pos.y), F(s.pos.z),
            F(s.yaw), F(s.pitch), F(s.bank), ((int)s.anim).ToString(Inv));

        private static string State(PlayerSnapshot s) => "STATE|" + Version + "|" + Entry(s);

        private static string Snapshot(List<PlayerSnapshot> snaps)
        {
            var sb = new StringBuilder("SNAP|").Append(Version).Append('|');
            for (int i = 0; i < snaps.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Entry(snaps[i]));
            }
            return sb.ToString();
        }

        private static bool TryParseEntry(string e, out PlayerSnapshot s)
        {
            s = default;
            string[] a = e.Split(',');
            if (a.Length < 9) return false;
            s.id = a[0];
            s.name = a[1];
            s.pos = new Vector3(Pf(a[2]), Pf(a[3]), Pf(a[4]));
            s.yaw = Pf(a[5]);
            s.pitch = Pf(a[6]);
            s.bank = Pf(a[7]);
            int.TryParse(a[8], NumberStyles.Integer, Inv, out int anim);
            s.anim = (byte)Mathf.Clamp(anim, 0, 255);
            return true;
        }

        private static float Pf(string s)
        {
            float.TryParse(s, NumberStyles.Float, Inv, out float v);
            return v;
        }

        /// <summary>Strip the field/record delimiters so free text can't corrupt a packet.</summary>
        private static string San(string s)
        {
            if (string.IsNullOrEmpty(s)) return "Kryptonian";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
                sb.Append(c == '|' || c == ',' || c == ';' || c == '\n' || c == '\r' ? ' ' : c);
            string r = sb.ToString().Trim();
            return r.Length == 0 ? "Kryptonian" : (r.Length > 20 ? r.Substring(0, 20) : r);
        }

        // ------------------------------------------------------------------ //
        // Sockets / threads
        // ------------------------------------------------------------------ //
        private void StartDiscoveryListener()
        {
            try
            {
                discoverySock = new UdpClient();
                discoverySock.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                discoverySock.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
                discoverySock.EnableBroadcast = true;
                discoveryThread = new Thread(() => ReceiveLoop(discoverySock)) { IsBackground = true, Name = "MP-Discovery" };
                discoveryThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MP] discovery listener failed (lobby browsing disabled): " + e.Message);
            }
        }

        private void StartGameThread(UdpClient sock)
        {
            gameThread = new Thread(() => ReceiveLoop(sock)) { IsBackground = true, Name = "MP-Game" };
            gameThread.Start();
        }

        private void StopGameThread()
        {
            // Closing the socket makes the blocking Receive throw, ending the loop.
            Thread t = gameThread;
            gameThread = null;
            if (t != null && t.IsAlive) t.Join(200);
        }

        private void ReceiveLoop(UdpClient sock)
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = sock.Receive(ref any);
                    inbox.Enqueue(new Packet { data = Encoding.UTF8.GetString(data), from = new IPEndPoint(any.Address, any.Port) });
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (!running) break; }
                catch (Exception) { /* ignore malformed traffic */ }
            }
        }

        private static void Send(UdpClient sock, string msg, IPEndPoint ep)
        {
            if (sock == null || ep == null) return;
            try { byte[] b = Encoding.UTF8.GetBytes(msg); sock.Send(b, b.Length, ep); }
            catch (Exception) { /* peer may have vanished */ }
        }

        private static void CloseSocket(ref UdpClient sock)
        {
            if (sock == null) return;
            try { sock.Close(); } catch { }
            sock = null;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject multicastLock;
#endif

        /// <summary>
        /// Android drops broadcast/multicast UDP by default; grabbing a Wi-Fi
        /// MulticastLock lets the discovery socket actually receive lobby
        /// beacons. No-op on other platforms (and harmless if it fails - the
        /// menu's "join by IP" path still works).
        /// </summary>
        private void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity"))
                using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "LastSonLobby");
                    multicastLock.Call("setReferenceCounted", true);
                    multicastLock.Call("acquire");
                }
            }
            catch (Exception e) { Debug.LogWarning("[MP] multicast lock unavailable: " + e.Message); }
#endif
        }

        private void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { if (multicastLock != null) { multicastLock.Call("release"); multicastLock = null; } }
            catch { }
#endif
        }
    }
}
