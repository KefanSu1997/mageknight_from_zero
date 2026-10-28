"""Build immutable actual pretrained vectors once, using the existing local reader."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from frozen_ui_text import FrozenTextEncoder
from printed_entity_text import public_description
from public_entity_tokens import PublicEntityTokens, original_vocabulary

def sha(path): return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
    p=argparse.ArgumentParser();p.add_argument('--catalog',required=True);p.add_argument('--out',required=True)
    a=p.parse_args();out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    root=Path(__file__).resolve().parents[2]
    layout=PublicEntityTokens(original_vocabulary());texts={};sources={}
    for name in ('basic_card','advanced_card','magic','items','skill','army','army_2','monster'):
        path=root/'resources/text_json'/(name+'.json');sources[name]=sha(path)
        for row in json.loads(path.read_text(encoding='utf-8-sig')):
            if name=='skill':
                for i,s in enumerate(row['skills']):texts[f"{row['id']}:{i:02d}"]='原技能：'+public_description(s)
            else:texts[row['id']]='原印刷规则：'+public_description(row)
    texts['_public_global_']='当前公开战斗状态。'
    entity_texts=[texts[i] for i in layout.ids]
    actions=json.loads(Path(a.catalog).read_text(encoding='utf-8'))
    # Static operation type/mode text is encoded separately from exact printed
    # entity text; all payment/target/material numbers remain in numeric descriptors.
    modes=sorted(set('操作：'+x['Kind']+'；模式：'+x.get('Choice','') for x in actions))
    encoder=FrozenTextEncoder(out/'cache',local_only=True)
    entities=encoder.encode(entity_texts);mode_vectors=encoder.encode(modes)
    by_mode=dict(zip(modes,mode_vectors));by_id=dict(zip(layout.ids,entities))
    result=[]
    for action in actions:
        vectors=[by_mode['操作：'+action['Kind']+'；模式：'+action.get('Choice','')]]
        if action.get('Card') in by_id:vectors.append(by_id[action['Card']])
        fields=dict(x.split('=',1) for x in action.get('Input','').split(';') if '=' in x)
        for key in ('spellId','skill','advanced','material'):
            if fields.get(key) in by_id:vectors.append(by_id[fields[key]])
        result.append(np.mean(vectors,axis=0))
    np.save(out/'entity_vectors.npy',entities,allow_pickle=False)
    np.save(out/'action_vectors.npy',np.asarray(result,np.float32),allow_pickle=False)
    (out/'texts.json').write_text(json.dumps(dict(entities=dict(zip(layout.ids,entity_texts)),modes=modes),ensure_ascii=False),encoding='utf-8')
    manifest=dict(encoder=encoder.signature,freeze_gate=encoder.verify_frozen(),entity_ids=layout.ids,
        original_sources_sha256=sources,catalog_sha256=sha(a.catalog),
        action_keys_sha256=hashlib.sha256(json.dumps([x['Key'] for x in actions],separators=(',',':')).encode()).hexdigest(),
        assets_sha256={n:sha(out/n) for n in ('entity_vectors.npy','action_vectors.npy','texts.json')},
        scope='Actual frozen mmBERT; pooled visible entity text plus original numeric public state; candidate type/mode plus printed entity text. No calibrated outcome head or Jev recipe claim.')
    (out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
    print(json.dumps(dict(entities=len(entities),modes=len(modes),actions=len(actions),freeze_gate=manifest['freeze_gate'])),flush=True)

if __name__=='__main__':main()
