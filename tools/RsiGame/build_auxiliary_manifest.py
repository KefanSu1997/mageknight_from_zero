"""Package an actual verified review for the opt-in original RSI transport."""
import argparse
import hashlib
import json
import os
from pathlib import Path
from auxiliary_evidence import AuxiliaryEvidence


def build(review, attempt, out, project):
    packet=json.loads((review/'input.json').read_text(encoding='utf8'))
    def required(name):
        matches=[Path(x['path']) for x in packet['required_text'] if Path(x['path']).name==name]
        if len(matches)!=1:raise ValueError('Exactly one reviewed '+name+' required')
        return matches[0]
    values=dict(feedback=review/attempt/'review.json',review=review/attempt/'verification.json',
                input=review/'input.json',run_manifest=review/'run_manifest.json',protocol=review/'protocol.json',
                system_prompt=review/'system_prompt.txt',source_manifest=required('source_manifest.json'),
                compile_receipt=required('compile_receipt.json'))
    result={'schema':2}
    for key,p in values.items():
        result[key]=dict(path=os.path.relpath(p,out.parent).replace('\\','/'),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
    out.parent.mkdir(parents=True,exist_ok=True)
    raw=json.dumps(result,ensure_ascii=False,indent=2).encode('utf8')
    # Publish only a validated new manifest. A failed package remains explicitly
    # named for diagnosis rather than being passed to an experiment runner.
    draft=out.with_name(out.name+'.candidate')
    with draft.open('xb') as stream:stream.write(raw)
    try:
        evidence=AuxiliaryEvidence(draft,project)
        status=evidence.classification()
    except Exception:
        raise
    with out.open('xb') as stream:stream.write(raw)
    return dict(manifest=str(out),classification=status,source_paths=len(evidence.snapshot),
                raw_final_sha256=result['feedback']['sha256'],scope='Actual existing final and proof; no new scores or independent attestation generated.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--review',type=Path,required=True);parser.add_argument('--attempt',required=True)
    parser.add_argument('--out',type=Path,required=True);parser.add_argument('--project',type=Path,default=Path(__file__).resolve().parents[2])
    a=parser.parse_args();print(json.dumps(build(a.review.resolve(),a.attempt,a.out.resolve(),a.project.resolve()),ensure_ascii=False))
