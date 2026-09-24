#!/usr/bin/env python3
"""Loopback-only static CDN for the existing Stellar Android verification harness."""

from __future__ import annotations

import argparse
import json
import logging
import mimetypes
import os
import re
import threading
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit


_SINGLE_RANGE = re.compile(r"^bytes=(\d+)-(\d*)$")


class VerificationCdnServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = True

    def __init__(self, address: tuple[str, int], root: Path, access_log: Path) -> None:
        self.root = root.resolve(strict=True)
        self.access_log = access_log
        self._log_lock = threading.Lock()
        super().__init__(address, VerificationCdnHandler)

    def record_request(self, payload: dict[str, object]) -> None:
        payload["timestampUtc"] = datetime.now(timezone.utc).isoformat()
        with self._log_lock:
            self.access_log.parent.mkdir(parents=True, exist_ok=True)
            with self.access_log.open("a", encoding="utf-8") as stream:
                stream.write(json.dumps(payload, separators=(",", ":")) + "\n")


class VerificationCdnHandler(BaseHTTPRequestHandler):
    server: VerificationCdnServer
    protocol_version = "HTTP/1.1"

    def do_HEAD(self) -> None:  # noqa: N802 - stdlib handler contract
        self._serve_file(send_body=False)

    def do_GET(self) -> None:  # noqa: N802 - stdlib handler contract
        if urlsplit(self.path).path == "/health":
            body = json.dumps(
                {"status": "ok", "root": str(self.server.root)},
                separators=(",", ":"),
            ).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(body)
            self.server.record_request({"method": "GET", "path": "/health", "status": 200, "bytes": len(body)})
            return

        self._serve_file(send_body=True)

    def _serve_file(self, send_body: bool) -> None:
        request_path = unquote(urlsplit(self.path).path).lstrip("/")
        candidate = (self.server.root / request_path).resolve(strict=False)
        if candidate != self.server.root and self.server.root not in candidate.parents:
            self._send_error(403, "Forbidden", request_path)
            return
        if candidate == self.server.root or not candidate.is_file():
            self._send_error(404, "Not Found", request_path)
            return

        file_size = candidate.stat().st_size
        range_header = self.headers.get("Range")
        start = 0
        end = file_size - 1
        status = 200
        if range_header:
            match = _SINGLE_RANGE.fullmatch(range_header.strip())
            if not match or file_size == 0:
                self._send_range_error(request_path, file_size)
                return
            start = int(match.group(1))
            requested_end = match.group(2)
            end = min(int(requested_end), file_size - 1) if requested_end else file_size - 1
            if start >= file_size or end < start:
                self._send_range_error(request_path, file_size)
                return
            status = 206

        content_length = max(0, end - start + 1)
        self.send_response(status)
        self.send_header("Content-Type", mimetypes.guess_type(candidate.name)[0] or "application/octet-stream")
        self.send_header("Content-Length", str(content_length))
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Connection", "close")
        if status == 206:
            self.send_header("Content-Range", f"bytes {start}-{end}/{file_size}")
        self.end_headers()

        written = 0
        if send_body and content_length:
            with candidate.open("rb") as stream:
                stream.seek(start)
                remaining = content_length
                while remaining:
                    chunk = stream.read(min(64 * 1024, remaining))
                    if not chunk:
                        break
                    self.wfile.write(chunk)
                    written += len(chunk)
                    remaining -= len(chunk)

        self.server.record_request(
            {
                "method": "GET" if send_body else "HEAD",
                "path": request_path,
                "range": range_header,
                "status": status,
                "fileSize": file_size,
                "bytes": written if send_body else content_length,
            }
        )

    def _send_range_error(self, request_path: str, file_size: int) -> None:
        self.send_response(416)
        self.send_header("Content-Range", f"bytes */{file_size}")
        self.send_header("Content-Length", "0")
        self.send_header("Connection", "close")
        self.end_headers()
        self.server.record_request(
            {"method": "GET", "path": request_path, "range": self.headers.get("Range"), "status": 416, "bytes": 0}
        )

    def _send_error(self, status: int, message: str, request_path: str) -> None:
        body = (message + "\n").encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "text/plain; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Connection", "close")
        self.end_headers()
        self.wfile.write(body)
        self.server.record_request(
            {"method": self.command, "path": request_path, "status": status, "bytes": len(body)}
        )

    def log_message(self, format_string: str, *args: object) -> None:
        logging.info("%s - %s", self.address_string(), format_string % args)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", required=True, type=Path, help="Exact YooAsset package output directory")
    parser.add_argument("--host", default="127.0.0.1", help="Bind address; the verification harness uses loopback")
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--access-log", required=True, type=Path, help="JSONL request evidence file")
    args = parser.parse_args()

    if args.host != "127.0.0.1":
        parser.error("The verification CDN must bind to 127.0.0.1 only.")
    if not 1 <= args.port <= 65535:
        parser.error("--port must be in 1..65535.")
    if not args.root.is_dir():
        parser.error(f"YooAsset package directory does not exist: {args.root}")

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
    server = VerificationCdnServer((args.host, args.port), args.root, args.access_log)
    logging.info("Verification CDN listening on http://%s:%d root=%s", args.host, args.port, server.root)
    try:
        server.serve_forever(poll_interval=0.25)
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        logging.info("Verification CDN stopped")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
