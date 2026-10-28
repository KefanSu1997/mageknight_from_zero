"""Validate public JSON against the native DTO type graph before tensorization."""
import hashlib
import json
import math
from pathlib import Path


class PublicSchema:
    def __init__(self, path):
        self.document=json.loads(Path(path).read_text(encoding='utf-8-sig'))
        if self.document['version']!=1:
            raise ValueError('Unsupported schema')
        self.signature=hashlib.sha256(json.dumps(self.document,sort_keys=True,separators=(',',':')).encode()).hexdigest()

    def validate(self, value, root='observation'):
        name=self.document.get(root,root)
        def walk(value, name, path, nullable=False):
            node=self.document['types'][name];kind=node['kind']
            if value is None and (nullable or kind=='nullable'):
                return
            def fail():raise ValueError(f'Public schema mismatch at {path}: expected {name}')
            if kind=='nullable':return walk(value,node['item'],path)
            if kind=='object':
                if not isinstance(value,dict):fail()
                properties=node['properties']
                if set(value)!={p['name'] for p in properties}:fail()
                for p in properties:walk(value[p['name']],p['type'],path+(p['name'],),p['reference_nullable'])
            elif kind=='array':
                if not isinstance(value,list):fail()
                item_kind=self.document['types'][node['item']]['kind']
                for i,item in enumerate(value):walk(item,node['item'],path+(i,),item_kind in ('object','array','text'))
            elif kind=='text':
                if not isinstance(value,str):fail()
            elif kind=='bool':
                if type(value) is not bool:fail()
            elif kind=='integer':
                if type(value) is not int:fail()
            elif kind=='number':
                if type(value) not in (int,float) or not math.isfinite(value):fail()
            elif kind=='enum':
                if type(value) is not int or value not in {v['value'] for v in node['values']}:fail()
            else:raise ValueError('Unsupported schema node '+kind)
        walk(value,name,())
        return True
