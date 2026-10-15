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


def unity_signing():
    """Makes an Apple Distribution certificate and an App Store profile for the Unity app,
    for Unity Build Automation (which wants a .p12 and a .mobileprovision). Files go to secrets/."""
    import base64
    import secrets as pysecrets
    from cryptography import x509
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography.hazmat.primitives.serialization import pkcs12
    from cryptography.x509.oid import NameOID

    out = ROOT / "secrets"
    key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
    csr = (x509.CertificateSigningRequestBuilder()
           .subject_name(x509.Name([x509.NameAttribute(NameOID.COMMON_NAME, "EngineBakeoff Unity")]))
           .sign(key, hashes.SHA256()))
    csr_pem = csr.public_bytes(serialization.Encoding.PEM).decode()
    body = {"data": {"type": "certificates", "attributes": {"certificateType": "DISTRIBUTION", "csrContent": csr_pem}}}
    cert_data = call("POST", "/v1/certificates", body)["data"]
    cert = x509.load_der_x509_certificate(base64.b64decode(cert_data["attributes"]["certificateContent"]))
    password = pysecrets.token_urlsafe(18)
    p12 = pkcs12.serialize_key_and_certificates(
        b"EngineBakeoff Unity", key, cert, None, serialization.BestAvailableEncryption(password.encode()))
    (out / "unity_distribution.p12").write_bytes(p12)
    (out / "unity_distribution_p12_password.txt").write_text(password)
    print("certificate created:", cert_data["attributes"]["name"], "expires", cert_data["attributes"]["expirationDate"])

    bundle = [b for b in call("GET", "/v1/bundleIds?limit=200")["data"]
              if b["attributes"]["identifier"] == "dev.zazag.bakeoffunity"][0]
    body = {"data": {"type": "profiles", "attributes": {"name": "EngineBakeoff Unity App Store", "profileType": "IOS_APP_STORE"},
                     "relationships": {"bundleId": {"data": {"type": "bundleIds", "id": bundle["id"]}},
                                       "certificates": {"data": [{"type": "certificates", "id": cert_data["id"]}]}}}}
    prof = call("POST", "/v1/profiles", body)["data"]
    (out / "unity_appstore.mobileprovision").write_bytes(base64.b64decode(prof["attributes"]["profileContent"]))
    print("profile created:", prof["attributes"]["name"])


def testflight():
    """Gives both apps an internal TestFlight group that gets every build, with every
    App Store Connect user of the team in it."""
    users = call("GET", "/v1/users?limit=50")["data"]
    for a in call("GET", "/v1/apps?limit=50")["data"]:
        if a["attributes"]["bundleId"] not in BUNDLES:
            continue
        groups = call("GET", f"/v1/apps/{a['id']}/betaGroups")["data"]
        group = next((g for g in groups if g["attributes"]["name"] == "Owner"), None)
        if group is None:
            body = {"data": {"type": "betaGroups",
                             "attributes": {"name": "Owner", "isInternalGroup": True, "hasAccessToAllBuilds": True},
                             "relationships": {"app": {"data": {"type": "apps", "id": a["id"]}}}}}
            group = call("POST", "/v1/betaGroups", body)["data"]
        for u in users:
            at = u["attributes"]
            body = {"data": {"type": "betaTesters",
                             "attributes": {"email": at["username"], "firstName": at["firstName"], "lastName": at["lastName"]},
                             "relationships": {"betaGroups": {"data": [{"type": "betaGroups", "id": group["id"]}]}}}}
            call("POST", "/v1/betaTesters", body)
        print(a["attributes"]["name"], ": internal group ready,", len(users), "tester(s)")


def certs():
    for c in call("GET", "/v1/certificates?limit=50")["data"]:
        at = c["attributes"]
        print(at["certificateType"], "|", at["name"], "|", at["expirationDate"])


if __name__ == "__main__":
    {"bundle-ids": bundle_ids, "apps": apps, "builds": builds, "certs": certs, "testflight": testflight,
     "unity-signing": unity_signing}[sys.argv[1]]()
