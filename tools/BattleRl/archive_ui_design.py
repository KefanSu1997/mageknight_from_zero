"""Create immutable, SHA-bound UI design archives; never overwrites a version.

Examples:
  python tools/BattleRl/archive_ui_design.py --version v2 --status candidate --notes "..." --screenshots-dir path
  python tools/BattleRl/archive_ui_design.py --verify v2

Archive before every UI edit. Baseline mode is a reconstructable configuration,
not a claim to possess historical source code. Large external evidence is linked
with its SHA; screenshots and explicitly supplied design artifacts are copied.
"""
import argparse
import hashlib
import json
import re
import shutil
from datetime import datetime, timezone, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / "AutomationOutputs/UiFluency/design_versions"
EXTENSIONS = {".png", ".jpg", ".jpeg", ".html", ".csv", ".json", ".jsonl", ".md", ".txt"}


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def verify(version):
    folder = REGISTRY / version
    manifest_path = folder / "manifest.json"
    entry = json.loads((REGISTRY / "registry.json").read_text(encoding="utf8"))["versions"][version]
    if digest(manifest_path) != entry["manifest_sha256"]:
        raise ValueError("Manifest modified: " + version)
    manifest = json.loads(manifest_path.read_text(encoding="utf8"))
    for row in manifest["files"]:
        path = folder / row["archive_path"]
        if not path.is_file() or digest(path) != row["sha256"]:
            raise ValueError("Archive modified: " + str(path))
    expected = {r["archive_path"] for r in manifest["files"]} | {"manifest.json"}
    actual = {p.relative_to(folder).as_posix() for p in folder.rglob("*") if p.is_file()}
    if expected != actual:
        raise ValueError("Unexpected or missing archive files: " + version)
    return {"version": version, "verified_files": len(manifest["files"]), "manifest_sha256": entry["manifest_sha256"]}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--version")
    p.add_argument("--status", default="candidate")
    p.add_argument("--notes", default="")
    p.add_argument("--screenshots-dir", action="append", default=[])
    p.add_argument("--artifacts-dir", action="append", default=[])
    p.add_argument("--reference", action="append", default=[])
    p.add_argument("--baseline", action="store_true")
    p.add_argument("--source-version", help="Copy source from an already verified archive, not the changing worktree")
    p.add_argument("--verify")
    args = p.parse_args()
    if args.verify:
        print(json.dumps(verify(args.verify), ensure_ascii=False))
        return
    if not args.version or not re.fullmatch(r"[A-Za-z0-9_-]+", args.version):
        p.error("--version must be a safe unique name")
    REGISTRY.mkdir(parents=True, exist_ok=True)
    registry_path = REGISTRY / "registry.json"
    registry = json.loads(registry_path.read_text(encoding="utf8")) if registry_path.exists() else {"schema_version": 1, "versions": {}}
    for old in registry["versions"]:
        verify(old)
    dest = REGISTRY / args.version
    if dest.exists() or args.version in registry["versions"]:
        raise FileExistsError("Version already exists; use a NEW version: " + str(dest))
    # Exclusive creation prevents accidental replacement even with concurrent calls.
    dest.mkdir()
    source_root = ROOT
    if args.source_version:
        verify(args.source_version)
        source_root = REGISTRY / args.source_version / "source"
    sources = set((source_root / "Assets/Scripts/Battle").glob("PveBattleController*.cs*"))
    sources.update((source_root / "Assets/Scripts/Adventure/Presentation").glob("AdventureWidgets.cs*"))
    for relative in ("Assets/Scripts/Battle/BattleCardHover.cs", "Assets/Scripts/SceneAutomation/StructuredUiPilot.cs",
                     "Assets/Scripts/SceneAutomation/Editor/UiDesignCaptureBridge.cs"):
        src=source_root/relative
        if src.exists():sources.add(src)
        if src.with_suffix(src.suffix+".meta").exists():sources.add(src.with_suffix(src.suffix+".meta"))
    for name in ("capture_ui_design_pairs.py","capture_ui_round_pairs.py","capture_ui_edge_pairs.py","capture_ui_hand_pages.py","audit_ui_design_constraints.py","build_ui_design_review_report.py","build_context_ui_report.py","calculate_context_ui_cost.py","extract_ui_route_context.py","validate_ui_cost_geometry.py"):
        src=source_root/"tools/BattleRl"/name
        if src.exists():sources.add(src)
    for pattern in ("*ui*cost*.py", "*ui*iteration*.py", "ui_cost_views.py", "archive_ui_design.py"):
        sources.update((source_root / "tools/BattleRl").glob(pattern))
    for name in ("capture_ui_supply_reward_pairs.py","capture_ui_hand_commit_pairs.py","find_ui_reward_fixtures.py","audit_ui_goal_coverage.py","inventory_uncovered_ui_costs.py","capture_ui_matched_hand_selection.py","plan_uncovered_ui_routes.py","resolve_uncovered_ui_costs_offline.py","plan_final_ui_routes.py","capture_split_block_contract.py","complete_offline_ui_evidence.py","aggregate_complete_ui_costs.py","capture_enemy_readability_pair.py","audit_ui_visual_review_coverage.py"):
        src=source_root/"tools/BattleRl"/name
        if src.exists():sources.add(src)
    for name in ("capture_ui_phase_readability.py","plan_split_block_ui_route.py","build_ui_readability_report.py"):
        src=source_root/"tools/BattleRl"/name
        if src.exists():sources.add(src)
    rows = []

    def copy(src, relative):
        target = dest / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.exists():
            if digest(target) != digest(src):
                raise ValueError("Conflicting archive path: " + str(relative))
            return
        before = digest(src)
        shutil.copyfile(src, target)
        if digest(target) != before or digest(src) != before:
            raise ValueError("Source changed while archiving: " + str(src))
        rows.append({"archive_path": relative.as_posix(), "source_path": str(src.resolve()), "sha256": before, "bytes": target.stat().st_size})

    for src in sorted(sources):
        copy(src, Path("source") / src.relative_to(source_root))
    for kind, folders in (("screenshots", args.screenshots_dir), ("artifacts", args.artifacts_dir)):
        for i, value in enumerate(folders):
            folder = Path(value).resolve()
            if not folder.is_dir():
                raise FileNotFoundError(folder)
            for src in sorted(folder.rglob("*")):
                relative = src.relative_to(folder)
                if not src.is_file() or src.suffix.lower() not in EXTENSIONS:
                    continue
                if any(part.lower().startswith("preview") or part.lower() in {"live", "__pycache__"} for part in relative.parts[:-1]):
                    continue
                if kind == "screenshots" and src.suffix.lower() not in {".png", ".jpg", ".jpeg"}:
                    continue
                copy(src, Path(kind) / (str(i) + "_" + folder.name) / relative)
    references = []
    for value in args.reference:
        src = Path(value).resolve()
        references.append({"source_path": str(src), "sha256": digest(src), "bytes": src.stat().st_size, "storage": "external_immutable_evidence_reference"})
    created = datetime.now(timezone(timedelta(hours=8), "Asia/Shanghai")).isoformat()
    manifest = {"schema_version": 1, "version": args.version, "status": args.status, "created_at": created,
                "notes": args.notes, "source_version": args.source_version, "baseline_reconstruction": {"optimizedInteractionUi": False, "historical_source_restored": False} if args.baseline else None,
                "files": rows, "references": references}
    manifest_path = dest / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf8")
    registry["versions"][args.version] = {"path": str(dest), "status": args.status, "created_at": created, "manifest_sha256": digest(manifest_path)}
    temporary = registry_path.with_suffix(".tmp")
    temporary.write_text(json.dumps(registry, ensure_ascii=False, indent=2), encoding="utf8")
    temporary.replace(registry_path)
    print(json.dumps(verify(args.version), ensure_ascii=False))


if __name__ == "__main__":
    main()
