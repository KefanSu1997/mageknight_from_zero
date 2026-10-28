"""Publish reviewed local Markdown as three private Feishu docs; checkpoint every write."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sqlite3
import uuid

from feishu_setup import RUNTIME, protect

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'vibe_coding/codex'
MANIFEST = OUT / 'feishu_project_docs_manifest.json'
DOCS = {
    'principles': ('MageKnight｜开发原则与执行约定', 'project_principles.md'),
    'design': ('MageKnight｜整体规则与游戏策划', 'game_rules_and_design.md'),
    'progress': ('MageKnight｜开发进度：规则—实现—验证', 'rules_progress_verification.md'),
}


def check(response):
    if not response.success():
        raise RuntimeError(f'Feishu code={response.code}: {response.msg}')
    return response.data


def paragraphs(markdown):
    result = []
    for line in markdown.splitlines():
        line = line.strip()
        if not line or line.startswith('|---'):
            continue
        heading = re.match(r'^(#{1,6}) (.*)', line)
        if heading:
            kind, field, text = len(heading[1])+2, 'heading'+str(len(heading[1])), heading[2]
        elif line.startswith('- '):
            kind, field, text = 12, 'bullet', line[2:]
        else:
            kind, field, text = 2, 'text', line
        # Each row is a small source paragraph; no images or large attachments.
        for offset in range(0, len(text), 1800):
            value = text[offset:offset+1800]
            result.append({'block_type':kind, field:{'elements':[{'text_run':{'content':value}}]}})
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('keys', nargs='+', choices=DOCS)
    parser.add_argument('--domain', help='Verified tenant domain, e.g. example.feishu.cn')
    args = parser.parse_args()
    if args.domain and not re.fullmatch(r'[a-zA-Z0-9-]+\.(feishu\.cn|larksuite\.com)', args.domain):
        parser.error('Expected a tenant domain only')
    import lark_oapi as lark
    from lark_oapi.api.docx.v1 import (Block, CreateDocumentRequest, CreateDocumentRequestBody,
        CreateDocumentBlockChildrenRequest, CreateDocumentBlockChildrenRequestBody, RawContentDocumentRequest)
    from lark_oapi.api.drive.v1 import CreatePermissionMemberRequest, BaseMember
    credentials = json.loads(protect((RUNTIME/'credentials.dpapi').read_bytes(), True))
    client = lark.Client.builder().app_id(credentials['app_id']).app_secret(credentials['app_secret']).timeout(25).log_level(lark.LogLevel.ERROR).build()
    with sqlite3.connect(RUNTIME/'messages.sqlite') as db:
        owner = db.execute("SELECT value FROM settings WHERE key='sender'").fetchone()[0]
    manifest = json.loads(MANIFEST.read_text(encoding='utf-8')) if MANIFEST.exists() else {}
    def save():
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n',encoding='utf-8')
    for key in args.keys:
        title, filename = DOCS[key]
        source = (OUT/filename).read_text(encoding='utf-8')
        digest = hashlib.sha256(source.encode()).hexdigest()
        blocks = paragraphs(source)
        body_bytes = len(json.dumps(blocks,ensure_ascii=False).encode())
        print(f'{key}: text payload {body_bytes} bytes, {len(blocks)} blocks', flush=True)
        entry = manifest.get(key)
        if entry and entry['sha256'] != digest:
            raise RuntimeError('Source changed after publication; review the existing document before updating.')
        if not entry:
            data = check(client.docx.v1.document.create(CreateDocumentRequest.builder().request_body(
                CreateDocumentRequestBody.builder().title(title).build()).build()))
            entry = manifest[key] = {'title':title, 'document_id':data.document.document_id,
                'source':str((OUT/filename).relative_to(ROOT)), 'sha256':digest,
                'blocks_written':0, 'text_payload_bytes':body_bytes}
            save()
        doc_id = entry['document_id']
        for offset in range(entry['blocks_written'], len(blocks), 40):
            chunk = [Block(d) for d in blocks[offset:offset+40]]
            token = str(uuid.uuid5(uuid.NAMESPACE_URL, doc_id+digest+str(offset)))
            check(client.docx.v1.document_block_children.create(
                CreateDocumentBlockChildrenRequest.builder().document_id(doc_id).block_id(doc_id)
                .client_token(token).request_body(CreateDocumentBlockChildrenRequestBody.builder()
                .children(chunk).index(-1).build()).build()))
            entry['blocks_written'] = offset + len(chunk)
            save()
        if not entry.get('shared_with_user'):
            check(client.drive.v1.permission_member.create(CreatePermissionMemberRequest.builder()
                .token(doc_id).type('docx').need_notification(False).request_body(BaseMember.builder()
                .member_type('openid').member_id(owner).perm('edit').build()).build()))
            entry['shared_with_user'] = True
            save()
        if args.domain:
            entry['url'] = 'https://' + args.domain + '/docx/' + doc_id
        actual = check(client.docx.v1.document.raw_content(RawContentDocumentRequest.builder().document_id(doc_id).build())).content
        expected = [next(v for k,v in block.items() if k!='block_type')['elements'][0]['text_run']['content'] for block in blocks]
        missing = [x for x in expected if x not in actual]
        if missing:
            raise RuntimeError(f'Document read-back missing {len(missing)} paragraphs')
        entry['verified_paragraphs'] = len(expected)
        entry['status'] = 'published_and_readback_verified'
        save()
        print(json.dumps({'key':key,'url':entry.get('url'), 'document_id':doc_id,
                         'verified_paragraphs':len(expected)}, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
