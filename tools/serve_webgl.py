#!/usr/bin/env python3
"""Static server for the Unity WebGL build with the headers Unity's pre-compressed files need.

Serves build/webgl by default. Brotli (.br) only decodes over HTTPS in browsers, so --https generates a
throw-away self-signed certificate (same situation as Render, which terminates TLS for you).

Usage: python3 tools/serve_webgl.py [--dir build/webgl] [--port 8443] [--https]
"""
import argparse
import http.server
import os
import ssl
import subprocess
import tempfile
from pathlib import Path

TYPES = {".wasm": "application/wasm", ".js": "application/javascript", ".data": "application/octet-stream", ".json": "application/json"}


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        path = self.translate_path(self.path.split("?")[0])
        for enc, ext in (("br", ".br"), ("gzip", ".gz")):
            if path.endswith(ext):
                self.send_header("Content-Encoding", enc)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        super().end_headers()

    def guess_type(self, path):
        base = path[:-3] if path.endswith(".br") or path.endswith(".gz") else path
        return TYPES.get(Path(base).suffix, super().guess_type(base))

    def log_message(self, fmt, *args):
        pass


def self_signed(dirpath: str):
    key, crt = os.path.join(dirpath, "key.pem"), os.path.join(dirpath, "cert.pem")
    subprocess.run(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", key, "-out", crt,
                    "-days", "2", "-subj", "/CN=localhost"], check=True, capture_output=True)
    return crt, key


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", default=str(Path(__file__).resolve().parent.parent / "build" / "webgl"))
    ap.add_argument("--port", type=int, default=8443)
    ap.add_argument("--https", action="store_true")
    args = ap.parse_args()
    os.chdir(args.dir)
    httpd = http.server.ThreadingHTTPServer(("0.0.0.0", args.port), Handler)
    scheme = "http"
    if args.https:
        tmp = tempfile.mkdtemp()
        crt, key = self_signed(tmp)
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ctx.load_cert_chain(crt, key)
        httpd.socket = ctx.wrap_socket(httpd.socket, server_side=True)
        scheme = "https"
    print(f"serving {args.dir} at {scheme}://localhost:{args.port}/", flush=True)
    httpd.serve_forever()


if __name__ == "__main__":
    main()
