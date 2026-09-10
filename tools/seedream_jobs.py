"""Resumable Seedream/Jimeng 4.0 jobs. Credentials stay in .env/environment.

Each invocation performs one bounded operation. A submitted job is never submitted
again; an ambiguous POST requires an explicitly supplied task ID to resume.
"""

import argparse
import base64
import contextlib
import datetime as dt
import hashlib
import hmac
import io
import json
import os
from pathlib import Path
import sys
import urllib.error
import urllib.request


ROOT = Path(__file__).resolve().parents[1]
HOST = "visual.volcengineapi.com"


def utc():
    return dt.datetime.now(dt.timezone.utc).isoformat()


def atomic_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(temporary, path)


@contextlib.contextmanager
def job_lock(path):
    """OS lock is released on process exit, including crashes (no stale PID lock)."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.with_suffix(path.suffix + ".lock").open("a+b") as stream:
        stream.seek(0)
        if stream.read(1) == b"":
            stream.write(b"0")
            stream.flush()
        stream.seek(0)
        if os.name == "nt":
            import msvcrt

            msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl

            fcntl.flock(stream, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            yield
        finally:
            stream.seek(0)
            if os.name == "nt":
                msvcrt.locking(stream.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(stream, fcntl.LOCK_UN)


def load_env():
    path = ROOT / ".env"
    if path.exists():
        for line in path.read_text(encoding="utf-8-sig").splitlines():
            if "=" in line and not line.lstrip().startswith("#"):
                key, value = line.split("=", 1)
                os.environ.setdefault(key.strip(), value.strip().strip("\"'"))


def headers_for(action, body, secret, stamp=None):
    stamp = stamp or dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    body_hash = hashlib.sha256(body).hexdigest()
    headers = {
        "content-type": "application/json",
        "host": HOST,
        "x-content-sha256": body_hash,
        "x-date": stamp,
    }
    if os.environ.get("IMDREAM_SESSION_TOKEN"):
        headers["x-security-token"] = os.environ["IMDREAM_SESSION_TOKEN"]
    signed = ";".join(headers)
    query = f"Action={action}&Version=2022-08-31"
    canonical = "\n".join(
        ["POST", "/", query, "\n".join(f"{k}:{v}" for k, v in headers.items()), "", signed, body_hash]
    )
    scope = f"{stamp[:8]}/cn-north-1/cv/request"
    to_sign = "\n".join(["HMAC-SHA256", stamp, scope, hashlib.sha256(canonical.encode()).hexdigest()])
    key = secret
    for value in (stamp[:8], "cn-north-1", "cv", "request"):
        key = hmac.new(key, value.encode(), hashlib.sha256).digest()
    signature = hmac.new(key, to_sign.encode(), hashlib.sha256).hexdigest()
    headers["Authorization"] = (
        f"HMAC-SHA256 Credential={os.environ['IMDREAM_ACCESS_KEY']}/{scope}, "
        f"SignedHeaders={signed}, Signature={signature}"
    )
    return headers, query


def api(action, payload):
    load_env()
    if not all(os.environ.get(k) for k in ("IMDREAM_ACCESS_KEY", "IMDREAM_SECRET_KEY")):
        raise ValueError("Set IMDREAM_ACCESS_KEY and IMDREAM_SECRET_KEY locally in .env")
    raw = os.environ["IMDREAM_SECRET_KEY"].strip().encode()
    candidates = [raw]
    # Preserve the previously working credentials without guessing on timeouts.
    from imdream_sign_helper import ensure_bytes, maybe_decode_base64

    decoded = ensure_bytes(maybe_decode_base64(raw))
    for key in (decoded, b"VC3" + raw, b"VC3" + decoded):
        if key not in candidates:
            candidates.append(key)
    body = json.dumps(payload, ensure_ascii=False, separators=(",", ":")).encode()
    for secret in candidates:
        headers, query = headers_for(action, body, secret)
        request = urllib.request.Request(f"https://{HOST}/?{query}", data=body, headers=headers)
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            result = json.loads(error.read().decode())
        # A rejected signature is the only submission that is safe to retry.
        code = result.get("ResponseMetadata", {}).get("Error", {}).get("Code", "")
        if code == "SignatureDoesNotMatch":
            continue
        return result
    raise ValueError("Seedream rejected the configured signing key")


def validate_spec(spec):
    required = ("prompt", "width", "height")
    if any(not spec.get(k) for k in required):
        raise ValueError("Spec requires prompt, width and height")
    width, height = spec["width"], spec["height"]
    if not isinstance(width, int) or not isinstance(height, int):
        raise ValueError("Dimensions must be integers")
    if not 1024 * 1024 <= width * height <= 4096 * 4096 or not 1 / 3 <= width / height <= 3:
        raise ValueError("Seedream 4.0 requires 1-16 megapixels and aspect ratio 1/3-3")
    if len(spec["prompt"]) > 800 or len(spec.get("image_urls", [])) > 10:
        raise ValueError("Prompt exceeds 800 characters or references exceed 10")
    return {
        "req_key": spec.get("model", "jimeng_t2i_v40"),
        "prompt": spec["prompt"],
        "width": width,
        "height": height,
        "scale": spec.get("scale", 0.65),
        "force_single": True,
        **({"image_urls": spec["image_urls"]} if spec.get("image_urls") else {}),
    }


def submit(job_path, spec, call=api):
    payload = validate_spec(spec)
    if Path(job_path).exists():
        previous = json.loads(Path(job_path).read_text(encoding="utf-8"))
        if previous.get("spec") != spec:
            raise ValueError("Job already exists with a different spec; choose a new job file")
        return previous
    job = {"schemaVersion": 1, "status": "submission_unknown", "createdAtUtc": utc(), "spec": spec}
    # Persist before the billable POST. A process crash must not duplicate charges.
    atomic_json(job_path, job)
    result = call("CVSync2AsyncSubmitTask", payload)
    if result.get("code") != 10000:
        job.update(status="failed", response=result)
    else:
        task_id = result.get("data", {}).get("task_id")
        if not task_id:
            raise ValueError("Submission returned no task ID; keep submission_unknown")
        job.update(status="submitted", taskId=str(task_id), submittedAtUtc=utc())
    atomic_json(job_path, job)
    return job


def poll(job_path, call=api):
    job = json.loads(Path(job_path).read_text(encoding="utf-8"))
    if job["status"] in ("failed", "expired", "downloaded"):
        return job
    if not job.get("taskId"):
        raise ValueError("Submission outcome unknown; use attach --task-id after checking provider, never resubmit")
    result = call("CVSync2AsyncGetResult", {
        "req_key": job["spec"].get("model", "jimeng_t2i_v40"),
        "task_id": job["taskId"], "req_json": json.dumps({"return_url": True}),
    })
    job["checkedAtUtc"] = utc()
    if result.get("code") != 10000:
        job["lastQueryError"] = result
        atomic_json(job_path, job)
        raise ValueError("Seedream query rejected; details saved locally in job file")
    data = result.get("data", {})
    state = data.get("status", "unknown")
    job["providerStatus"] = state
    if state in ("not_found", "expired", "failed"):
        job.update(status="expired" if state in ("expired", "not_found") else "failed", response=result)
    elif state == "done":
        if not data.get("image_urls") and not data.get("binary_data_base64"):
            job.update(status="failed", error="Provider finished without an image", response=result)
        else:
            job.update(status="ready", images=data, generatedAtUtc=utc())
    else:
        job["status"] = "running"
    atomic_json(job_path, job)
    return job


def download(job_path):
    from PIL import Image

    path = Path(job_path)
    job = json.loads(path.read_text(encoding="utf-8"))
    if job["status"] == "downloaded":
        for output in job["outputs"]:
            data = Path(output["path"]).read_bytes()
            if hashlib.sha256(data).hexdigest() != output["sha256"]:
                raise ValueError("Previously downloaded asset changed; use a new output")
        return job
    if job["status"] != "ready":
        raise ValueError("Poll until ready before download")
    images = job["images"]
    urls = images.get("image_urls", [])
    encoded = images.get("binary_data_base64", [])
    outputs = []
    for index in range(len(urls) or len(encoded)):
        if urls:
            with urllib.request.urlopen(urls[index], timeout=45) as response:
                data = response.read(40 * 1024 * 1024)
        else:
            data = base64.b64decode(encoded[index])
        with Image.open(io.BytesIO(data)) as picture:
            picture.verify()
        with Image.open(io.BytesIO(data)) as picture:
            width, height = picture.size
            if (width, height) != (job["spec"]["width"], job["spec"]["height"]):
                raise ValueError(f"Unexpected dimensions: {width}x{height}")
            # Decode to PNG so a JPEG response never masquerades as a PNG asset.
            destination = path.parent / f"{path.stem}_{index}.png"
            temporary = destination.with_suffix(".png.tmp")
            picture.save(temporary, format="PNG")
            os.replace(temporary, destination)
        outputs.append({"path": str(destination.resolve()), "width": width, "height": height,
                        "sha256": hashlib.sha256(destination.read_bytes()).hexdigest()})
    job.update(status="downloaded", outputs=outputs, downloadedAtUtc=utc())
    atomic_json(path, job)
    return job


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("submit", "poll", "download", "status", "attach"))
    parser.add_argument("--job", required=True, type=Path)
    parser.add_argument("--spec", type=Path)
    parser.add_argument("--task-id")
    args = parser.parse_args()
    with job_lock(args.job):
        if args.action == "submit":
            if not args.spec:
                parser.error("submit requires --spec")
            result = submit(args.job, json.loads(args.spec.read_text(encoding="utf-8-sig")))
        elif args.action == "poll":
            result = poll(args.job)
        elif args.action == "download":
            result = download(args.job)
        else:
            result = json.loads(args.job.read_text(encoding="utf-8"))
            if args.action == "attach":
                if result.get("taskId") or result["status"] != "submission_unknown" or not args.task_id:
                    raise ValueError("attach requires submission_unknown and --task-id")
                result.update(status="submitted", taskId=args.task_id)
                atomic_json(args.job, result)
        # Signed URLs, raw provider messages and credentials are not echoed.
        print(json.dumps({k: result[k] for k in ("status", "taskId", "outputs") if k in result}, ensure_ascii=False))
        if result["status"] in ("failed", "expired", "submission_unknown"):
            return 2
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as error:
        print(f"{type(error).__name__}: operation did not complete; retain the job file and inspect locally", file=sys.stderr)
        sys.exit(1)
