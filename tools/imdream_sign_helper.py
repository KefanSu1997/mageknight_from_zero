#!/usr/bin/env python3
import argparse
import base64
import datetime
import hashlib
import hmac
import json
import os
import string
import sys
from urllib.parse import quote


def norm_query(params):
    items = []
    for key in sorted(params.keys()):
        value = params[key]
        if isinstance(value, (list, tuple)):
            values = value
        else:
            values = [value]
        for v in values:
            items.append(
                f"{quote(str(key), safe='-_.~')}={quote(str(v), safe='-_.~')}"
            )
    return "&".join(items)


def maybe_decode_base64(value):
    if not isinstance(value, (bytes, bytearray, str)):
        return value
    if isinstance(value, bytes):
        candidate = bytes(value).strip()
    else:
        candidate = value.encode().strip()

    for _ in range(5):
        padded = candidate + b"=" * (-len(candidate) % 4)
        try:
            decoded = base64.b64decode(padded, validate=True)
        except Exception:
            break
        if not decoded or decoded == candidate:
            break
        candidate = decoded
        try:
            decoded_str = candidate.decode().strip()
        except UnicodeDecodeError:
            continue
        if decoded_str and all(ch in string.hexdigits for ch in decoded_str):
            return decoded_str
        candidate = decoded_str.encode()

    if isinstance(candidate, bytes):
        try:
            candidate = candidate.decode().strip()
        except UnicodeDecodeError:
            return candidate
    return candidate


def ensure_bytes(value):
    if isinstance(value, bytes):
        return value
    return value.encode("utf-8")


def main():
    parser = argparse.ArgumentParser(description="Compute Volcengine authorization headers")
    parser.add_argument("--action", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--region", default="cn-north-1")
    parser.add_argument("--service", default="cv")
    parser.add_argument("--host", required=True)
    parser.add_argument("--method", default="POST")
    parser.add_argument("--path", default="/")
    parser.add_argument("--content-type", default="application/json")
    parser.add_argument("--ak-env", default="IMDREAM_ACCESS_KEY")
    parser.add_argument("--sk-env", default="IMDREAM_SECRET_KEY")
    parser.add_argument("--session-token-env", default="IMDREAM_SESSION_TOKEN")
    parser.add_argument("--timestamp", help="Override timestamp (UTC) in format YYYYMMDDTHHMMSSZ")
    args = parser.parse_args()

    access_key = os.environ.get(args.ak_env)
    secret_key = os.environ.get(args.sk_env)
    if not access_key or not secret_key:
        raise SystemExit("AK/SK environment variables are not set")
    session_token = os.environ.get(args.session_token_env)
    access_key = access_key.strip()

    decode_flag_env = f"{args.sk_env}_DECODE_BASE64"
    decode_flag = os.environ.get(decode_flag_env, "0").lower() in {"1", "true", "yes", "on"}

    if decode_flag:
        key_candidate = maybe_decode_base64(secret_key)
    else:
        key_candidate = secret_key.strip()

    key_bytes = ensure_bytes(key_candidate)

    body = sys.stdin.read()

    body_bytes = body.encode("utf-8")
    x_content_sha256 = hashlib.sha256(body_bytes).hexdigest()

    now = datetime.datetime.utcnow()
    if args.timestamp:
        now = datetime.datetime.strptime(args.timestamp, "%Y%m%dT%H%M%SZ")
    x_date = now.strftime("%Y%m%dT%H%M%SZ")
    short_date = x_date[:8]

    query = {"Action": args.action, "Version": args.version}
    canonical_query = norm_query(query)

    canonical_headers_list = [
        f"content-type:{args.content_type}",
        f"host:{args.host}",
        f"x-content-sha256:{x_content_sha256}",
        f"x-date:{x_date}",
    ]
    signed_headers = ["content-type", "host", "x-content-sha256", "x-date"]
    if session_token:
        canonical_headers_list.append(f"x-security-token:{session_token}")
        signed_headers.append("x-security-token")

    canonical_headers = "\n".join(canonical_headers_list)

    canonical_request = "\n".join([
        args.method.upper(),
        args.path,
        canonical_query,
        canonical_headers,
        "",
        ";".join(signed_headers),
        x_content_sha256,
    ])

    hashed_canonical_request = hashlib.sha256(canonical_request.encode("utf-8")).hexdigest()

    credential_scope = f"{short_date}/{args.region}/{args.service}/request"
    string_to_sign = "\n".join([
        "HMAC-SHA256",
        x_date,
        credential_scope,
        hashed_canonical_request,
    ])

    k_date = hmac.new(key_bytes, short_date.encode("utf-8"), hashlib.sha256).digest()
    k_region = hmac.new(k_date, args.region.encode("utf-8"), hashlib.sha256).digest()
    k_service = hmac.new(k_region, args.service.encode("utf-8"), hashlib.sha256).digest()
    k_signing = hmac.new(k_service, b"request", hashlib.sha256).digest()

    signature = hmac.new(k_signing, string_to_sign.encode("utf-8"), hashlib.sha256).hexdigest()

    authorization = (
        "HMAC-SHA256 "
        f"Credential={access_key}/{credential_scope}, "
        f"SignedHeaders={';'.join(signed_headers)}, "
        f"Signature={signature}"
    )

    result = {
        "authorization": authorization,
        "x_date": x_date,
        "x_content_sha256": x_content_sha256,
        "canonical_request": canonical_request,
        "string_to_sign": string_to_sign,
        "hashed_canonical_request": hashed_canonical_request,
    }
    if session_token:
        result["x_security_token"] = session_token

    print(json.dumps(result))


if __name__ == "__main__":
    import sys
    try:
        main()
    except Exception as exc:
        print(json.dumps({"error": str(exc)}))
        sys.exit(1)
