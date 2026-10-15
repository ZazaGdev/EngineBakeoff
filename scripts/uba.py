"""Unity Build Automation helper. Reads the service account key from .env; never prints it.

  python -I scripts/uba.py projects          list projects the key can see (org and project IDs)
  python -I scripts/uba.py get PATH          GET any API path, print JSON
"""
import base64
import json
import sys
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
API = "https://build-automation.services.api.unity.com/v2"


def env():
    values = {}
    for line in (ROOT / ".env").read_text().splitlines():
        if "=" in line and not line.startswith("#"):
            k, v = line.split("=", 1)
            values[k.strip()] = v.strip().strip('"')
    return values


def auth():
    e = env()
    return "Basic " + base64.b64encode(f"{e['UNITY_KEY_ID']}:{e['UNITY_SECRET']}".encode()).decode()


def call(method, path, body=None, raw=None, content_type="application/json"):
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(API + path, method=method, data=data,
                                 headers={"Authorization": auth(), "Content-Type": content_type})
    try:
        with urllib.request.urlopen(req) as r:
            text = r.read()
            return json.loads(text) if text else {}
    except urllib.error.HTTPError as e:
        sys.exit(f"{method} {path} failed: {e.code} {e.read()[:500].decode(errors='replace')}")


def projects():
    for p in call("GET", "/projects"):
        print(p.get("name"), "| org", p.get("orgid"), "| project", p.get("projectid"), "| guid", p.get("guid"))


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "projects":
        projects()
    elif cmd == "get":
        print(json.dumps(call("GET", sys.argv[2]), indent=1)[:4000])
