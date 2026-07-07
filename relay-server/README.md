# LastSon Relay Server

Small zero-dependency TCP relay for the game's Online multiplayer tab.
Players connect to this server, one player hosts a room, and other players
join with the four-character room code.

## Run Locally

```sh
npm test
npm start
```

By default the server listens on `0.0.0.0:7777`. You can override that:

```sh
HOST=127.0.0.1 PORT=7777 npm start
```

In the Unity multiplayer menu, use `127.0.0.1:7777` for local Editor testing.
On Android devices, use the computer's LAN IP instead, for example
`192.168.1.20:7777`.

## Current Hosted Relay

The game client is currently prefilled with the Railway TCP proxy:

```text
hayabusa.proxy.rlwy.net:53046
```

Railway project: `lastson-relay`
Service: `lastson-relay`
Internal application port: `8080`

## Deploy

Deploy this folder to any Node 18+ host that exposes a raw TCP port to the
internet. Set `PORT` to the public port required by the host. The Unity client
expects a plain `host:port` relay address.

This relay only forwards player transform snapshots. It does not store accounts,
chat, payments, or analytics data.

## Protocol

Messages are newline-delimited JSON over TCP.

- Client receives `{ "t": "welcome", "id": "..." }` on connect.
- Host sends `{ "t": "host", "name": "Clark" }` and receives
  `{ "t": "hosted", "code": "ABCD" }`.
- Browser sends `{ "t": "list" }` and receives public rooms.
- Joiner sends `{ "t": "join", "code": "ABCD", "name": "Lois" }`.
- Players send `{ "t": "state", "x": 0, "y": 0, "z": 0, ... }`.
- The server broadcasts `{ "t": "snap", "players": [...] }` at about 15 Hz.
