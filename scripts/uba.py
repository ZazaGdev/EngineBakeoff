"""Unity Build Automation helper. Reads the service account key from .env; never prints it.

Needs UNITY_KEY_ID, UNITY_SECRET, UNITY_ORG_ID and UNITY_PROJECT_ID in .env.

  python -I scripts/uba.py setup VERSION     repo, signing files and the iOS build target
  python -I scripts/uba.py build             start an iOS build
  python -I scripts/uba.py status            last builds and their state
  python -I scripts/uba.py download FILE     save the latest build's IPA
  python -I scripts/uba.py get PATH          GET any API path ({base} = this org and project)
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
        shown = path.replace(env().get("UNITY_ORG_ID", "?"), "{org}").replace(env().get("UNITY_PROJECT_ID", "?"), "{project}")
        sys.exit(f"{method} {shown} failed: {e.code} {e.read()[:500].decode(errors='replace')}")


REPO = "https://github.com/ZazaGdev/EngineBakeoff.git"
TARGET = "ios"


def base():
    e = env()
    return f"/orgs/{e['UNITY_ORG_ID']}/projects/{e['UNITY_PROJECT_ID']}"


def multipart(fields, files):
    boundary = "----bakeoff" + base64.b16encode(Path(__file__).name.encode()).decode()
    parts = []
    for k, v in fields.items():
        parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
    for k, path in files.items():
        head = (f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"; filename="{path.name}"\r\n'
                "Content-Type: application/octet-stream\r\n\r\n").encode()
        parts.append(head + path.read_bytes() + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def setup(unity_version):
    """Points the project at the public repo, uploads the iOS signing files and makes the iOS target."""
    b = base()
    if not call("GET", b).get("settings", {}).get("scm", {}).get("url"):
        # A service account gets a server error here; the repo is then set in the dashboard.
        call("PUT", b, {"settings": {"scm": {"type": "git", "url": REPO}}})
    print("repo set")
    sec = ROOT / "secrets"
    creds = call("GET", b + "/credentials/signing/ios")
    cred = next((c for c in creds if c.get("label") == "EngineBakeoff"), None)
    if cred is None:
        body, ctype = multipart(
            {"label": "EngineBakeoff", "certificatePass": (sec / "unity_distribution_p12_password.txt").read_text().strip()},
            {"fileCertificate": sec / "unity_distribution.p12", "fileProvisioningProfile": sec / "unity_appstore.mobileprovision"})
        cred = call("POST", b + "/credentials/signing/ios", raw=body, content_type=ctype)
    print("signing credentials:", cred.get("credentialid"))
    targets = call("GET", b + "/buildtargets")
    if any(t.get("buildtargetid") == TARGET for t in targets):
        print("build target exists")
        return
    body = {"name": TARGET, "platform": "ios", "enabled": True,
            "settings": {"autoBuild": False, "unityVersion": unity_version, "executablename": "BakeoffUnity",
                         "operatingSystemSelected": "mac",
                         # "oauth" is the GitHub connection the owner made in the dashboard.
                         "scm": {"type": "oauth", "branch": "main", "subdirectory": "unity"},
                         "platform": {"bundleId": "dev.zazag.bakeoffunity", "xcodeVersion": "xcode26_5_0"}},
            "credentials": {"signing": {"credentialid": cred["credentialid"]}}}
    t = call("POST", b + "/buildtargets", body)
    print("build target created:", t.get("buildtargetid"))


def build():
    r = call("POST", f"{base()}/buildtargets/{TARGET}/builds", {"clean": False})
    print(json.dumps(r, indent=1)[:800])


def status():
    for bld in call("GET", f"{base()}/buildtargets/{TARGET}/builds?per_page=3"):
        links = bld.get("links", {})
        print(bld.get("build"), bld.get("buildStatus"), bld.get("totalTimeInSeconds"), "s",
              "| ipa:", "yes" if links.get("download_primary") else "no")


def download(out):
    bld = call("GET", f"{base()}/buildtargets/{TARGET}/builds?per_page=1")[0]
    url = bld["links"]["download_primary"]["href"]
    urllib.request.urlretrieve(url, out)
    print("build", bld["build"], "saved,", Path(out).stat().st_size, "bytes")


if __name__ == "__main__":
    cmd = sys.argv[1]
    if cmd == "get":
        print(json.dumps(call("GET", sys.argv[2].replace("{base}", base() if "{base}" in sys.argv[2] else "")), indent=1)[:4000])
    elif cmd == "setup":
        setup(sys.argv[2])
    elif cmd == "build":
        build()
    elif cmd == "status":
        status()
    elif cmd == "download":
        download(sys.argv[2])
