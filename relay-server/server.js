// LastSon relay server - lets players find and join each other's games over
// the internet, not just the same Wi-Fi. Zero dependencies (built-in `net`
// module only). Protocol: newline-delimited JSON over a plain TCP socket.
//
// Run locally:   node server.js            (listens on 7777, or $PORT)
// Deploy: push this folder to Railway / Render / Fly.io / a VPS - anywhere
// that can run `node server.js` and exposes a TCP port to the internet.

const net = require('net');

const PORT = process.env.PORT ? parseInt(process.env.PORT, 10) : 7777;
const HOST = process.env.HOST || '0.0.0.0';
const TICK_MS = 66; // ~15Hz snapshot broadcast
const CODE_CHARS = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // no 0/O/1/I - easy to read aloud

/** @type {Map<string, Room>} */
const rooms = new Map();
let nextConnId = 1;

class Player {
  constructor(id, socket) {
    this.id = id;
    this.socket = socket;
    this.name = 'Kryptonian';
    this.room = null;
    this.state = null; // last {x,y,z,yaw,pitch,bank,anim}
    this.buffer = '';
  }
}

class Room {
  constructor(code, name) {
    this.code = code;
    this.name = name;
    this.players = new Map(); // id -> Player
  }
}

function randomCode() {
  let code;
  do {
    code = '';
    for (let i = 0; i < 4; i++) code += CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)];
  } while (rooms.has(code));
  return code;
}

function send(player, obj) {
  try { player.socket.write(JSON.stringify(obj) + '\n'); } catch (e) { /* socket may be closing */ }
}

function sanitizeName(name) {
  if (typeof name !== 'string') return 'Kryptonian';
  const cleaned = name.replace(/[\r\n]/g, ' ').trim().slice(0, 20);
  return cleaned.length ? cleaned : 'Kryptonian';
}

function leaveRoom(player) {
  const room = player.room;
  if (!room) return;
  room.players.delete(player.id);
  player.room = null;
  player.state = null;
  for (const p of room.players.values()) send(p, { t: 'bye', id: player.id });
  if (room.players.size === 0) rooms.delete(room.code);
}

function handleMessage(player, msg) {
  switch (msg.t) {
    case 'host': {
      leaveRoom(player);
      player.name = sanitizeName(msg.name);
      const code = randomCode();
      const room = new Room(code, player.name + "'s game");
      rooms.set(code, room);
      room.players.set(player.id, player);
      player.room = room;
      send(player, { t: 'hosted', code });
      break;
    }
    case 'list': {
      const list = Array.from(rooms.values()).map(r => ({ code: r.code, name: r.name, players: r.players.size }));
      send(player, { t: 'rooms', rooms: list });
      break;
    }
    case 'join': {
      const code = typeof msg.code === 'string' ? msg.code.trim().toUpperCase() : '';
      const room = rooms.get(code);
      if (!room) { send(player, { t: 'error', msg: 'No game with that code' }); break; }
      leaveRoom(player);
      player.name = sanitizeName(msg.name);
      room.players.set(player.id, player);
      player.room = room;
      send(player, { t: 'joined', code: room.code, host: room.name });
      break;
    }
    case 'state': {
      if (!player.room) break;
      player.state = {
        id: player.id, name: player.name,
        x: Num(msg.x), y: Num(msg.y), z: Num(msg.z),
        yaw: Num(msg.yaw), pitch: Num(msg.pitch), bank: Num(msg.bank),
        anim: Num(msg.anim) | 0,
      };
      break;
    }
    case 'leave': {
      leaveRoom(player);
      break;
    }
  }
}

function Num(v) { const n = Number(v); return Number.isFinite(n) ? n : 0; }

const server = net.createServer(socket => {
  socket.setNoDelay(true);
  socket.setKeepAlive(true, 30000);
  const player = new Player(String(nextConnId++), socket);
  send(player, { t: 'welcome', id: player.id });

  socket.on('data', chunk => {
    player.buffer += chunk.toString('utf8');
    let idx;
    while ((idx = player.buffer.indexOf('\n')) >= 0) {
      const line = player.buffer.slice(0, idx).trim();
      player.buffer = player.buffer.slice(idx + 1);
      if (!line) continue;
      try { handleMessage(player, JSON.parse(line)); }
      catch (e) { /* ignore malformed line */ }
    }
    // Guard against a runaway client that never sends a newline.
    if (player.buffer.length > 8192) player.buffer = '';
  });

  const cleanup = () => leaveRoom(player);
  socket.on('close', cleanup);
  socket.on('error', cleanup);
});

setInterval(() => {
  for (const room of rooms.values()) {
    if (room.players.size === 0) continue;
    const players = Array.from(room.players.values()).filter(p => p.state).map(p => p.state);
    if (players.length === 0) continue;
    const msg = { t: 'snap', players };
    for (const p of room.players.values()) send(p, msg);
  }
}, TICK_MS);

server.on('error', err => {
  console.error(`LastSon relay failed: ${err.message}`);
  process.exitCode = 1;
});

server.listen(PORT, HOST, () => {
  console.log(`LastSon relay listening on ${HOST}:${PORT}`);
});
