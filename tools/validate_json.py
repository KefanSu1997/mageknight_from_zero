#!/usr/bin/env python3
import argparse, json, sys, pathlib
from jsonschema import validate, Draft202012Validator

p = argparse.ArgumentParser()
p.add_argument("--input",   required=False)
p.add_argument("--fallback",required=True)
p.add_argument("--schema",  required=True)
p.add_argument("--output",  required=True)
args = p.parse_args()

def read_json(pth):
    pth = pathlib.Path(pth)
    if not pth.exists(): return None
    # [修改点 1] 这里改为 utf-8-sig，兼容带BOM和不带BOM的文件
    with open(pth, "r", encoding="utf-8-sig") as f:
        return json.load(f)

data = read_json(args.input) or read_json(args.fallback)
if data is None:
    print("no proposal json found", file=sys.stderr); sys.exit(2)

# [修改点 2] Schema 文件也可能有这个问题，建议一并修改
with open(args.schema, "r", encoding="utf-8-sig") as f:
    schema = json.load(f)

Draft202012Validator.check_schema(schema)
validate(instance=data, schema=schema, cls=Draft202012Validator)

data.setdefault("hints", [])

# [保持不变] 写入时我们依然使用标准的 utf-8 (无BOM)，这是 Linux 的最佳实践
with open(args.output, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

print("ok")
