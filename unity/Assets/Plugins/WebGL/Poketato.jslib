mergeInto(LibraryManager.library, {
  // Current size of the wasm heap in bytes (grows on demand).
  PoketatoHeapSize: function () {
    return (typeof HEAP8 !== 'undefined' && HEAP8) ? HEAP8.length : 0;
  },

  // Benchmark results are pushed to window.__poketato.reports so tools/webgl_perf.mjs can read them.
  PoketatoReport: function (ptr) {
    var s = UTF8ToString(ptr);
    window.__poketato = window.__poketato || {};
    window.__poketato.reports = window.__poketato.reports || [];
    try { window.__poketato.reports.push(JSON.parse(s)); } catch (e) { window.__poketato.reports.push({ raw: s }); }
  },

  PoketatoMark: function (ptr) {
    var k = UTF8ToString(ptr);
    window.__poketato = window.__poketato || {};
    window.__poketato.marks = window.__poketato.marks || {};
    if (window.__poketato.marks[k] === undefined) window.__poketato.marks[k] = performance.now();
  },

  PoketatoQuery: function () {
    var q = (typeof window !== 'undefined' && window.location) ? window.location.search : '';
    var size = lengthBytesUTF8(q) + 1;
    var buf = _malloc(size);
    stringToUTF8(q, buf, size);
    return buf;
  },
});
