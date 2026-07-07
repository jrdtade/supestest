const assert = require('assert');
const { spawn } = require('child_process');
const net = require('net');
const path = require('path');

async function getFreePort() {
  return new Promise((resolve, reject) => {
    const probe = net.createServer();
    probe.once('error', reject);
    probe.listen(0, '127.0.0.1', () => {
      const { port } = probe.address();
      probe.close(() => resolve(port));
    });
  });
}

function wait(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function startServer(port) {
  const child = spawn(process.execPath, [path.join(__dirname, 'server.js')], {
    env: { ...process.env, HOST: '127.0.0.1', PORT: String(port) },
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  let output = '';
  child.stdout.on('data', chunk => { output += chunk.toString('utf8'); });
  child.stderr.on('data', chunk => { output += chunk.toString('utf8'); });

  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('relay did not start\n' + output)), 3000);
    child.once('exit', code => reject(new Error('relay exited early with code ' + code + '\n' + output)));
    const poll = setInterval(() => {
      if (!output.includes('LastSon relay listening')) return;
      clearTimeout(timer);
      clearInterval(poll);
      resolve(child);
    }, 25);
  });
}

function makeClient(port) {
  const socket = net.createConnection({ host: '127.0.0.1', port });
  socket.setEncoding('utf8');

  const messages = [];
  const waiters = [];
  let buffer = '';

  function resolveWaiters(msg) {
    for (let i = waiters.length - 1; i >= 0; i--) {
      const waiter = waiters[i];
      if (!waiter.predicate(msg)) continue;
      clearTimeout(waiter.timer);
      waiters.splice(i, 1);
      waiter.resolve(msg);
    }
  }

  socket.on('data', chunk => {
    buffer += chunk;
    let idx;
    while ((idx = buffer.indexOf('\n')) >= 0) {
      const line = buffer.slice(0, idx).trim();
      buffer = buffer.slice(idx + 1);
      if (!line) continue;
      const msg = JSON.parse(line);
      messages.push(msg);
      resolveWaiters(msg);
    }
  });

  function waitFor(predicate, label, timeoutMs = 3000) {
    const existing = messages.find(predicate);
    if (existing) return Promise.resolve(existing);
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        reject(new Error('timed out waiting for ' + label + '; got ' + JSON.stringify(messages)));
      }, timeoutMs);
      waiters.push({ predicate, resolve, reject, timer });
    });
  }

  return new Promise((resolve, reject) => {
    socket.once('connect', () => {
      resolve({
        socket,
        waitFor,
        send(obj) { socket.write(JSON.stringify(obj) + '\n'); },
        close() { socket.destroy(); },
      });
    });
    socket.once('error', reject);
  });
}

(async () => {
  const port = await getFreePort();
  let server;
  const clients = [];

  try {
    server = await startServer(port);

    const host = await makeClient(port);
    clients.push(host);
    const hostWelcome = await host.waitFor(m => m.t === 'welcome', 'host welcome');
    host.send({ t: 'host', name: 'Clark' });
    const hosted = await host.waitFor(m => m.t === 'hosted', 'hosted');
    assert.match(hosted.code, /^[A-HJ-NP-Z2-9]{4}$/);

    const browser = await makeClient(port);
    clients.push(browser);
    await browser.waitFor(m => m.t === 'welcome', 'browser welcome');
    browser.send({ t: 'list' });
    const rooms = await browser.waitFor(m => m.t === 'rooms', 'room list');
    assert.strictEqual(rooms.rooms.length, 1);
    assert.strictEqual(rooms.rooms[0].code, hosted.code);
    assert.strictEqual(rooms.rooms[0].players, 1);

    const joiner = await makeClient(port);
    clients.push(joiner);
    const joinerWelcome = await joiner.waitFor(m => m.t === 'welcome', 'joiner welcome');
    joiner.send({ t: 'join', code: hosted.code, name: 'Lois' });
    const joined = await joiner.waitFor(m => m.t === 'joined', 'joined');
    assert.strictEqual(joined.code, hosted.code);

    host.send({ t: 'state', x: 1, y: 2, z: 3, yaw: 4, pitch: 5, bank: 6, anim: 7 });
    joiner.send({ t: 'state', x: 8, y: 9, z: 10, yaw: 11, pitch: 12, bank: 13, anim: 14 });

    const hasBothPlayers = msg =>
      msg.t === 'snap' &&
      Array.isArray(msg.players) &&
      msg.players.some(p => p.id === hostWelcome.id && p.name === 'Clark') &&
      msg.players.some(p => p.id === joinerWelcome.id && p.name === 'Lois');

    await host.waitFor(hasBothPlayers, 'host snapshot');
    await joiner.waitFor(hasBothPlayers, 'joiner snapshot');

    joiner.send({ t: 'leave' });
    await host.waitFor(m => m.t === 'bye' && m.id === joinerWelcome.id, 'leave notification');

    console.log('relay smoke test passed');
  } finally {
    for (const client of clients) client.close();
    await wait(50);
    if (server && !server.killed) server.kill();
  }
})().catch(err => {
  console.error(err.stack || err.message);
  process.exit(1);
});
