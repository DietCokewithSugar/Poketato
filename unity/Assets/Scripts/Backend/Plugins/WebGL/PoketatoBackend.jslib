var PoketatoBackendState = { sockets: {}, inbox: {}, next: 1 };

mergeInto(LibraryManager.library, {
  PoketatoBackendConfig: function (buf, len) {
    var cfg = (typeof window !== "undefined" && window.POKETATO_BACKEND) ? window.POKETATO_BACKEND : {};
    var s = JSON.stringify({
      url: cfg.url || "",
      anonKey: cfg.anonKey || "",
      gameServer: cfg.gameServer || "",
      wsUrl: cfg.wsUrl || "",
      apiBase: cfg.apiBase || ""
    });
    var need = lengthBytesUTF8(s) + 1;
    if (need > len) return -1;
    stringToUTF8(s, buf, len);
    return need - 1;
  },

  PoketatoWsOpen: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var id = PoketatoBackendState.next++;
    var inbox = [];
    PoketatoBackendState.inbox[id] = inbox;
    var ws = new WebSocket(url);
    PoketatoBackendState.sockets[id] = ws;
    ws.onmessage = function (ev) {
      if (typeof ev.data !== "string") return;
      if (inbox.length > 100) inbox.shift();
      inbox.push(ev.data);
    };
    return id;
  },

  PoketatoWsSend: function (id, msgPtr) {
    var ws = PoketatoBackendState.sockets[id];
    if (!ws || ws.readyState !== 1) return 0;
    ws.send(UTF8ToString(msgPtr));
    return 1;
  },

  PoketatoWsClose: function (id) {
    var ws = PoketatoBackendState.sockets[id];
    if (ws) {
      ws.onmessage = null;
      ws.close();
    }
    delete PoketatoBackendState.sockets[id];
    delete PoketatoBackendState.inbox[id];
  },

  PoketatoWsState: function (id) {
    var ws = PoketatoBackendState.sockets[id];
    if (!ws) return 3;
    return ws.readyState;
  },

  PoketatoWsPop: function (id, buf, len) {
    var q = PoketatoBackendState.inbox[id];
    if (!q || !q.length) return 0;
    var s = q[0];
    var need = lengthBytesUTF8(s) + 1;
    if (need > len) return -1;
    stringToUTF8(s, buf, len);
    q.shift();
    return need - 1;
  }
});
