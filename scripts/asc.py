"""Small App Store Connect API helper. Reads the key from secrets/ and IDs from .env.

Never prints the key, the issuer ID or the team ID.

  python -I scripts/asc.py bundle-ids        register both bundle IDs (skips existing ones)
  python -I scripts/asc.py apps              list app records
  python -I scripts/asc.py builds            list recent builds and their processing state
"""
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

import jwt

ROOT = Path(__file__).resolve().parent.parent
KEY_ID = "DJGLH7KV6G"
BUNDLES = {"dev.zazag.bakeoffgodot": "Bakeoff Godot", "dev.zazag.bakeoffunity": "Bakeoff Unity"}


def env():
    values = {}
    for line in (ROOT / ".env").read_text().splitlines():
        if "=" in line and not line.startswith("#"):
            k, v = line.split("=", 1)
            values[k.strip()] = v.strip().strip('"')
    return values


def token():
    key = (ROOT / "secrets" / f"AuthKey_{KEY_ID}.p8").read_text()
    # This PC's clock runs a few minutes fast, and Apple refuses a token issued in the future.
    iat = int(time.time()) - 600
    payload = {"iss": env()["ISSUER_ID"], "iat": iat, "exp": iat + 1200, "aud": "appstoreconnect-v1"}
    return jwt.encode(payload, key, algorithm="ES256", headers={"kid": KEY_ID, "typ": "JWT"})


def call(method, path, body=None):
    req = urllib.request.Request(
        "https://api.appstoreconnect.apple.com" + path,
        method=method,
        data=json.dumps(body).encode() if body else None,
        headers={"Authorization": "Bearer " + token(), "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req) as r:
            return json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        detail = json.loads(e.read() or b"{}").get("errors", [{}])[0]
        sys.exit(f"{method} {path.split('?')[0]} failed: {e.code} {detail.get('title')}: {detail.get('detail')}")


def bundle_ids():
    existing = {b["attributes"]["identifier"] for b in call("GET", "/v1/bundleIds?limit=200")["data"]}
    for ident, name in BUNDLES.items():
        if ident in existing:
            print(f"{ident}: already registered")
            continue
        body = {"data": {"type": "bundleIds", "attributes": {"identifier": ident, "name": name, "platform": "IOS"}}}
        call("POST", "/v1/bundleIds", body)
        print(f"{ident}: registered")


def apps():
    for a in call("GET", "/v1/apps?limit=50")["data"]:
        print(a["attributes"]["bundleId"], "|", a["attributes"]["name"], "|", a["id"])


def builds():
    data = call("GET", "/v1/builds?limit=10&sort=-uploadedDate&include=app")
    names = {i["id"]: i["attributes"]["bundleId"] for i in data.get("included", [])}
    for b in data["data"]:
        at = b["attributes"]
        app = names.get(b["relationships"]["app"]["data"]["id"], "?")
        print(app, at["version"], at["processingState"], at["uploadedDate"])


if __name__ == "__main__":
    {"bundle-ids": bundle_ids, "apps": apps, "builds": builds}[sys.argv[1]]()
