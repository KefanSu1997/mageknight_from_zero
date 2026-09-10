"""只归档本轮范围，按开始时备份导出差异，不触碰现有Git索引或历史合并。"""
import hashlib
import json
import subprocess
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "AutomationOutputs/ReusableAdventure/20260909"
BACKUP = Path("D:/study_and_work/multi_agent_system/recovery_backups/reusable_scenarios_20260909")
DIRECTORIES = ["Assets/Logic/Runtime/Adventure", "Assets/Scripts/Adventure", "Assets/Resources/Adventure"]
FILES = ["Assets/Scripts/Testing/RuleScenarioController.cs", "Assets/Scripts/SceneAutomation/Editor/RuleScenarioMenus.cs",
         "Assets/Scripts/SceneAutomation/Editor/AdventureContentBuilder.cs", "Assets/Tests/AdventureSessionTests.cs",
         "AGENTS.md", "tools/build_adventure_acceptance.py", "tools/summarize_adventure_acceptance.py",
         "tools/export_adventure_review.py", "vibe_coding/codex/adventure_reuse_guide.md",
         "vibe_coding/codex/adventure_art_prompts_2026-09-09.md", "vibe_coding/codex/worklog_2026-09-09_reusable_adventure.md",
         "vibe_coding/codex/project_experience/reusable_adventure_content_and_input_2026-09-10.md"]


def main():
    selected = {ROOT / p for p in FILES}
    for directory in DIRECTORIES:
        selected.update(p for p in (ROOT / directory).rglob("*") if p.is_file())
        selected.add(ROOT / (directory + ".meta"))
    selected.update((ROOT / "AutomationConfigs").glob("adventure_*.json"))
    for p in list(selected):
        if Path(str(p) + ".meta").exists(): selected.add(Path(str(p) + ".meta"))
    selected = sorted(p for p in selected if p.is_file())
    baseline = zipfile.ZipFile(BACKUP / "rules_baseline.zip")
    originals = {name:baseline.read(name) for name in baseline.namelist()}
    originals["AGENTS.md"] = (BACKUP / "AGENTS.md").read_bytes()
    manifests = []
    with tempfile.TemporaryDirectory(prefix="adventure_diff_", dir=BASE) as temp:
        temp = Path(temp); (temp/"before").mkdir(); (temp/"after").mkdir()
        for path in selected:
            name = path.relative_to(ROOT).as_posix(); data = path.read_bytes()
            before = originals.get(name)
            if before == data: continue
            dest = temp / "after" / name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(data)
            if before is not None:
                dest = temp / "before" / name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(before)
            if path.suffix in (".cs", ".py"):
                text = data.decode("utf-8-sig")
                assert all(line == line.rstrip() and not line.startswith("\t") for line in text.splitlines()), name
            manifests.append(dict(path=name, status="modified" if before is not None else "added", bytes=len(data),
                                  sha256=hashlib.sha256(data).hexdigest(),
                                  baselineSha256=hashlib.sha256(before).hexdigest() if before is not None else None))
        result = subprocess.run(["git","-c","core.quotePath=false","diff","--no-index","--no-ext-diff","--binary",
                                 "--src-prefix=a/","--dst-prefix=b/","before","after"],cwd=temp,capture_output=True)
        assert result.returncode in (0,1), result.stderr.decode(errors="replace")
        patch = result.stdout
        for prefix in (b"a/",b"b/"):
            for side in (b"before/",b"after/"):
                patch=patch.replace(prefix+side,prefix)
        (BASE/"本轮改动.patch").write_bytes(patch)
    manifest = dict(baseline=str(BACKUP/"rules_baseline.zip"),files=manifests,
                    note="基于本轮开始时工作区备份，不是origin/main；未暂存/提交现有历史合并。")
    (BASE/"change_manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    with zipfile.ZipFile(BASE/"本轮改动文件.zip","w",zipfile.ZIP_DEFLATED) as z:
        for item in manifests: z.write(ROOT/item["path"],item["path"])
        z.write(BASE/"本轮改动.patch","本轮改动.patch"); z.write(BASE/"change_manifest.json","change_manifest.json")
    result = subprocess.run(["git","apply","--reverse","--check",str(BASE/"本轮改动.patch")],cwd=ROOT,capture_output=True)
    assert result.returncode == 0, result.stderr.decode(errors="replace")
    print(json.dumps(dict(files=len(manifests),bytes=sum(m["bytes"] for m in manifests),
                          patchReverseCheck="passed",whitespaceCheck="passed")))


if __name__ == "__main__":
    main()
