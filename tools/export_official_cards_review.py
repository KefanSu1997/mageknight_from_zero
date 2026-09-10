"""基于上轮完整文件包导出本次修正；保留工作区现有未完成的历史合并。"""
import hashlib
import json
import subprocess
import tempfile
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "AutomationOutputs/OfficialCards/20260910"
BACKUP = BASE / "before/本轮改动文件.zip"
PREFIXES = ("Assets/Logic/Runtime/Adventure/", "Assets/Scripts/Adventure/",
            "Assets/Resources/Adventure/Cards/", "Assets/Resources/Adventure/Scenarios/")
FILES = ["Assets/Logic/Runtime/ActionSystem.cs", "Assets/Scripts/SceneAutomation/Editor/AdventureContentBuilder.cs",
         "Assets/Scripts/SceneAutomation/Editor/RuleScenarioMenus.cs", "Assets/Tests/AdventureSessionTests.cs",
         "Assets/Tests/OfficialAdventureCardsTests.cs", "AGENTS.md", "tools/build_adventure_acceptance.py",
         "tools/summarize_adventure_acceptance.py", "tools/export_official_cards_review.py",
         "vibe_coding/codex/adventure_reuse_guide.md", "vibe_coding/codex/worklog_2026-09-10_official_cards.md",
         "vibe_coding/codex/project_experience/official_card_reuse_and_acceptance_2026-09-10.md"]


def main():
    with zipfile.ZipFile(BASE / "before/rules_baseline.zip") as z:
        originals = {name: z.read(name) for name in z.namelist()}
    with zipfile.ZipFile(BACKUP) as z:
        originals.update({name: z.read(name) for name in z.namelist()})
    # 此文件未包含于上轮包；其本次差异仅为可选的法术元数据加载开关。
    result = subprocess.run(["git", "show", ":Assets/Logic/Runtime/ActionSystem.cs"], cwd=ROOT, capture_output=True, check=True)
    originals["Assets/Logic/Runtime/ActionSystem.cs"] = result.stdout
    result = subprocess.run(["git", "show", ":Assets/Logic/Runtime/ActionSystem.cs.meta"], cwd=ROOT, capture_output=True, check=True)
    originals["Assets/Logic/Runtime/ActionSystem.cs.meta"] = result.stdout
    selected = set(FILES)
    for prefix in PREFIXES:
        selected.update(p.relative_to(ROOT).as_posix() for p in (ROOT / prefix).rglob("*") if p.is_file())
        selected.update(name for name in originals if name.startswith(prefix))
    selected.update(p.relative_to(ROOT).as_posix() for p in (ROOT / "AutomationConfigs").glob("adventure_*.json"))
    for name in list(selected):
        if (ROOT / (name + ".meta")).exists() or name + ".meta" in originals:
            selected.add(name + ".meta")
    changes = []
    with tempfile.TemporaryDirectory(prefix="official_cards_diff_", dir=BASE) as directory:
        temp = Path(directory)
        (temp / "before").mkdir()
        (temp / "after").mkdir()
        for name in sorted(selected):
            path = ROOT / name
            before = originals.get(name)
            after = path.read_bytes() if path.is_file() else None
            if before == after:
                continue
            for side, data in (("before", before), ("after", after)):
                if data is not None:
                    target = temp / side / name
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(data)
            if after is not None and path.suffix in (".cs", ".py"):
                assert all(line == line.rstrip() and not line.startswith("\t")
                           for line in after.decode("utf-8-sig").splitlines()), name
            changes.append(dict(path=name, status="deleted" if after is None else "added" if before is None else "modified",
                                sha256=hashlib.sha256(after).hexdigest() if after is not None else None))
        diff = subprocess.run(["git", "-c", "core.quotePath=false", "diff", "--no-index", "--no-ext-diff", "--no-renames",
                               "--binary", "--src-prefix=a/", "--dst-prefix=b/", "before", "after"], cwd=temp, capture_output=True)
        assert diff.returncode in (0, 1), diff.stderr.decode(errors="replace")
        patch = diff.stdout
        for prefix in (b"a/", b"b/"):
            for side in (b"before/", b"after/"):
                patch = patch.replace(prefix + side, prefix)
        (BASE / "本次修正.patch").write_bytes(patch)
    manifest = dict(baseline=str(BACKUP), files=changes,
                    note="上轮原始基线与上轮修改包叠加构成本轮前状态；ActionSystem及meta以未修改的现有索引版本为基线；不改变索引或历史合并。")
    (BASE / "change_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    with zipfile.ZipFile(BASE / "本次修正文件.zip", "w", zipfile.ZIP_DEFLATED) as z:
        for change in changes:
            if change["status"] != "deleted":
                z.write(ROOT / change["path"], change["path"])
        z.write(BASE / "本次修正.patch", "本次修正.patch")
        z.write(BASE / "change_manifest.json", "change_manifest.json")
    subprocess.run(["git", "apply", "--reverse", "--check", str(BASE / "本次修正.patch")], cwd=ROOT, check=True)
    print(json.dumps(dict(files=len(changes), added=sum(c["status"] == "added" for c in changes),
                          modified=sum(c["status"] == "modified" for c in changes),
                          deleted=sum(c["status"] == "deleted" for c in changes), reverseCheck="passed")))


if __name__ == "__main__":
    main()
