"""Unity source facts for the unchanged RSIGame readers and project-map UI.

Counts are file/serialized-reference facts, never inferred gameplay content or
runtime use. The C# map is a scoped lexical index, not a compiler symbol table.
"""
from __future__ import annotations
import json
import hashlib
from pathlib import Path
import re

TOP = {'Assets', 'Packages', 'ProjectSettings', 'resources'}
SERIALIZED = {'.unity', '.prefab', '.asset', '.mat', '.controller', '.overridecontroller'}
IMAGES = {'.png', '.jpg', '.jpeg', '.webp', '.svg', '.bmp', '.tga', '.psd'}
AUDIO = {'.ogg', '.wav', '.mp3', '.aiff', '.flac'}
GUID = re.compile(rb'guid:[ \t]*([0-9a-fA-F]{32})\b')


def files(work):
    # Use the same physical-tree exclusions as snapshots, then scope source
    # interpretation to Unity's authored roots. No recursive Library walk.
    from run_unity_experiment import source_files
    work = Path(work)
    return sorted(p for p in source_files(work)
                  if p.relative_to(work).parts[0] in TOP)


def _guids(path):
    found = set()
    tail = b''
    with path.open('rb') as stream:
        while block := stream.read(1024 * 1024):
            joined = tail + block
            found.update(m.group(1).decode().lower() for m in GUID.finditer(joined)
                         if m.start() == 0 or not (chr(joined[m.start() - 1]).isalnum()
                                                 or joined[m.start() - 1] == 95))
            tail = joined[-96:]
    return found


def _lexical(text):
    """Blank comments/string literals while retaining exact source line breaks."""
    token = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
    return token.sub(lambda m: ''.join('\n' if c == '\n' else ' ' for c in m.group()), text)


METHOD = re.compile(
    r'^[ \t]*(?:\[[^\n]*\][ \t]*)?'
    r'(?:(?:public|private|protected|internal|static|virtual|override|sealed|abstract|async|extern|unsafe|new|partial)\s+)+'
    r'(?:[\w.<>,?\[\] \t]+\s+)?(?P<name>@?[A-Za-z_]\w*)'
    r'(?:<[^\n>]+>)?\s*\([^;{}]*\)\s*(?:where[^{};\n]+\s*)?(?:\{|=>|;)', re.M)


def scan(work, *, include_empty=False):
    work = Path(work)
    mapped = {}
    for path in files(work):
        if path.suffix.lower() != '.cs':
            continue
        text = path.read_text(encoding='utf8', errors='replace')
        lexical = _lexical(text)
        # end marks the next indexed declaration, exactly as the upstream map;
        # it does not claim a compiler-derived method body extent.
        hits = [(m.start(), m.group('name')) for m in METHOD.finditer(lexical)]
        rows = []
        for i, (pos, name) in enumerate(hits):
            start = lexical.count('\n', 0, pos) + 1
            end = lexical.count('\n', 0, hits[i + 1][0]) if i + 1 < len(hits) else text.count('\n') + 1
            rows.append({'name': name, 'line': start, 'end': max(start, end)})
        if rows or include_empty:
            # Keep upstream's platform-native relative keys for stored notes.
            mapped[str(path.relative_to(work))] = {'lines': text.count('\n') + 1, 'functions': rows}
    return {'files': mapped, 'n_functions': sum(len(v['functions']) for v in mapped.values()),
            'scan_scope': 'C# lexical member declarations with explicit modifiers; comments and strings blanked; incomplete symbol inventory; end is next indexed declaration boundary'}


def declared_scenarios(work):
    # Upstream callers require a list. Explain unknown separately in static view;
    # do not turn scene names, card IDs or test-case IDs into playable scenarios.
    return []


def fix_fake_pngs(work, editor_png_hashes=None):
    """Re-encode only PNG bytes changed by the immediately preceding Editor."""
    from PIL import Image
    fixed = []
    eligible = editor_png_hashes or {}
    for path in files(work):
        rel = path.relative_to(work).as_posix()
        if path.suffix.lower() != '.png' or rel not in eligible:
            continue
        if hashlib.sha256(path.read_bytes()).hexdigest() != eligible[rel]:
            raise RuntimeError(f'PNG changed after the Editor result; refusing conversion: {path}')
        with path.open('rb') as stream:
            header = stream.read(8)
        if header == b'\x89PNG\r\n\x1a\n':
            continue
        temporary = path.with_name(path.name + '.reencode.tmp')
        try:
            with Image.open(path) as image:
                image.convert('RGBA').save(temporary, format='PNG')
            temporary.replace(path)
        except Exception as exc:
            temporary.unlink(missing_ok=True)
            raise RuntimeError(f'Authored PNG re-encoding unavailable for {path}: {exc}') from exc
        fixed.append(str(path.relative_to(work)))
    return fixed


class EditorPngScope:
    """Short-lived changed-file ownership plus an explicit conversion receipt."""
    def __init__(self, project, out):
        self.project, self.out = Path(project).resolve(), Path(out)
        self.eligible = {}
        self.editor_sequence = 0
        self.invocation = 0

    def begin(self):
        self.eligible = {}
        self.editor_sequence += 1

    def capture(self, before, after):
        self.eligible = {p: digest for p, digest in after.items()
                         if p.lower().endswith('.png') and before.get(p) != digest
                         and Path(p).parts[0] in TOP}

    def fix(self, work):
        if Path(work).resolve() != self.project:
            raise RuntimeError('PNG conversion is bound to the actual Editor project')
        candidates = dict(self.eligible)
        fixed = []
        error = None
        try:
            fixed = fix_fake_pngs(Path(work), candidates)
            self.eligible = {}  # One Editor result authorizes one conversion pass.
            return fixed
        except Exception as exc:
            error = f'{type(exc).__name__}: {exc}'
            raise
        finally:
            self.invocation += 1
            audit = self.out / 'png_conversion_scopes'
            audit.mkdir(parents=True, exist_ok=True)
            # A fresh process can resume the same experiment without overwrites.
            import uuid
            receipt = audit / f'{uuid.uuid4().hex}.json'
            with receipt.open('x', encoding='utf8') as stream:
                json.dump({'schema': 1, 'editor_sequence': self.editor_sequence,
                           'scope': 'only authored PNG files added or changed by the immediately preceding Editor result, bound to after-content SHA256',
                           'eligible_after_sha256': candidates, 'converted': fixed, 'error': error,
                           'import_gate': 'not measured here; empty conversion list does not certify Unity import or compilation'}, stream, indent=2)


def serialized_links(work, paths):
    rel = lambda p: p.relative_to(work).as_posix()
    refs = set()
    for path in paths:
        if path.suffix.lower() in SERIALIZED:
            refs.update(_guids(path))
    guid_paths = {}
    for path in paths:
        if path.suffix.lower() == '.meta':
            # A .meta has one identity GUID; nested import data may also contain
            # references, so match only its exact root-level identity field.
            for line in path.read_text(encoding='utf8', errors='replace').splitlines():
                match = re.fullmatch(r'guid: ([0-9a-fA-F]{32})', line)
                if match:
                    guid_paths[rel(path)[:-5]] = match.group(1).lower()
                    break
    return refs, guid_paths


def build_static_view(work, *, declared=None, visited_now=None, visited_history=None):
    work = Path(work)
    paths = files(work)
    rel = lambda p: p.relative_to(work).as_posix()
    lines = ['WHAT THE BUILD CONTAINS', '',
             'Mechanically read from Unity authored source roots: Assets, Packages, ProjectSettings, resources.',
             'Scenario declarations: unavailable; no Unity scenario-dispatch contract is registered. An empty list is unknown, not evidence of no gameplay scenarios.',
             'Primitive drawing sites: unavailable; no Unity drawing-call inventory is registered.',
             'Serialized GUID references record textual links; runtime loading and reachability are unknown.']
    for suffix, label in (('.cs', 'C# source files'), ('.unity', 'scene files'), ('.prefab', 'prefab files')):
        subset = [rel(p) for p in paths if p.suffix.lower() == suffix]
        lines.append(f'{label}: {len(subset)}')
        lines.extend('  - ' + p for p in subset[:12])
        if len(subset) > 12:
            lines.append(f'  - additional paths: {len(subset) - 12}')
    refs, guid_paths = serialized_links(work, paths)
    for suffixes, label in (({'.cs'}, 'C# source'), (IMAGES, 'image'), (AUDIO, 'audio')):
        assets = [rel(p) for p in paths if p.suffix.lower() in suffixes]
        linked = [p for p in assets if guid_paths.get(p) in refs]
        lines.append(f'{label} files: {len(assets)} present; {len(linked)} with a serialized GUID link; runtime use unknown for all files.')
        lines.extend('  - GUID-linked: ' + p for p in linked[:12])
    lines.append('Original resource JSON structures (array lengths and object keys, not gameplay totals):')
    for path in paths:
        if path.suffix.lower() != '.json' or not rel(path).startswith('resources/text_json/'):
            continue
        try:
            value = json.loads(path.read_text(encoding='utf-8-sig'))
        except (ValueError, UnicodeError) as exc:
            lines.append(f'  - {rel(path)}: parse unavailable ({type(exc).__name__})')
            continue
        if isinstance(value, list):
            lines.append(f'  - {rel(path)}: top-level array length {len(value)}')
        elif isinstance(value, dict):
            keys = list(value)
            lines.append(f'  - {rel(path)}: top-level object with {len(keys)} keys')
            for key in keys[:12]:
                if isinstance(value[key], list):
                    lines.append(f'    - {key}: array length {len(value[key])}')
        else:
            lines.append(f'  - {rel(path)}: top-level {type(value).__name__}')
    return '\n'.join(lines)


def visual_facts(work):
    work = Path(work)
    paths = files(work)
    refs, identities = serialized_links(work, paths)
    images = [p.relative_to(work).as_posix() for p in paths
              if p.suffix.lower() in IMAGES and p.name not in {'_contact_sheet.png', 'style_anchor.png'}]
    linked = [p for p in images if identities.get(p) in refs]
    return {'engine': 'unity', 'images_present': len(images),
            'images_used': None, 'images_unused': None, 'unused_names': None,
            'primitive_draws': None, 'texture_draws': None, 'authored_share': None,
            'audio_present': sum(p.suffix.lower() in AUDIO for p in paths),
            'images_serialized_guid_linked': len(linked), 'serialized_guid_linked_names': linked[:12],
            'coverage': {'roots': sorted(TOP), 'file_presence': 'authored files only; generated caches and evidence excluded',
                         'guid_links': 'serialized textual links only; runtime usage and reachability unknown',
                         'runtime_usage': 'unknown; no runtime asset inventory registered',
                         'drawing_share': 'unknown; no Unity drawing-call inventory registered'}}


def render_visual_facts(facts):
    return '\n'.join([
        'WHAT THE BUILD ACTUALLY CONTAINS  (Unity source facts; not a verdict)', '',
        f"  image files present   {facts['images_present']}",
        f"  audio files present   {facts['audio_present']}",
        f"  images with serialized GUID links   {facts['images_serialized_guid_linked']}",
        '  runtime images used / unused   UNKNOWN',
        '  primitive / texture drawing calls   UNKNOWN',
        '  share of drawing using authored art   UNKNOWN',
        '  Coverage: authored Assets, Packages, ProjectSettings, resources; generated caches and evidence excluded.',
        '  Serialized GUID links are textual source facts; they do not establish runtime usage or reachability.',
        '  No Unity runtime asset or drawing-call inventory is registered.'])


def install_polish_facts():
    import rsigame.polish.polish_evidence as evidence
    import rsigame.evidence.polish_goal as goal
    import rsigame.polish.polish_review as review
    original_facts, original_render = evidence.visual_facts, evidence.render_facts
    original_peers, original_goal = evidence.render_facts_with_peers, goal._facts_text
    original_saturated = review.saturated
    evidence.visual_facts = lambda work: (visual_facts(work) if
        (Path(work) / 'ProjectSettings/ProjectVersion.txt').is_file() else original_facts(work))
    evidence.render_facts = lambda facts: (render_visual_facts(facts) if
        facts.get('engine') == 'unity' else original_render(facts))
    evidence.render_facts_with_peers = lambda facts, dist: (
        render_visual_facts(facts) + '\nPeer comparison: UNKNOWN; no comparable Unity drawing-share inventory.'
        if facts.get('engine') == 'unity' else original_peers(facts, dist))
    goal._facts_text = lambda facts: (render_visual_facts(facts) if
        facts.get('engine') == 'unity' else original_goal(facts))
    def saturated(rev, facts):
        if facts.get('engine') == 'unity' and (
                facts.get('images_unused') is None or facts.get('authored_share') is None):
            return False, 'Unity runtime asset usage and drawing share are unknown; source facts cannot certify saturation'
        return original_saturated(rev, facts)
    review.saturated = saturated


ANNOTATION_INSTRUCTION = ('  If you learn what an undescribed part is for, say so in your '
    'final answer as `project_map: {"<file>::<function>": "<one '
    'line>"}` and it will be here next round.')


def render_compact(mapping, archive_directory):
    """Lossless archive plus complete C# file index; never a code review."""
    archive_directory = Path(archive_directory).resolve()
    archive_directory.mkdir(parents=True, exist_ok=True)
    raw = (json.dumps(mapping, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf8')
    digest = hashlib.sha256(raw).hexdigest()
    archive = archive_directory / (digest + '.json')
    try:
        with archive.open('xb') as stream:
            stream.write(raw)
    except FileExistsError:
        if archive.read_bytes() != raw:
            raise RuntimeError('Immutable source map archive content mismatch: ' + str(archive))
    out = ['WHERE THINGS ARE IN THIS GAME (compact Unity source map v1)', '',
           'Complete C# file index in authored roots Assets, Packages, ProjectSettings, resources; includes zero indexed-method files.',
           'Positions and lexical counts only; not a code review, runtime coverage, compiler symbol inventory, or correctness verdict.',
           'Method names and line ranges are available on demand in the full immutable JSON archive; read relevant source before editing.',
           'Full mapping archive: ' + str(archive), 'Archive SHA256: ' + digest,
           'C# map scope: ' + mapping.get('scan_scope', 'unavailable'), '']
    for path, data in mapping.get('files', {}).items():
        out.append(f"  {path}   {data['lines']} lines, {len(data['functions'])} methods")
    out.extend(['', 'Stored annotations (all supplied notes, unchanged):'])
    for key, value in (mapping.get('notes') or {}).items():
        out.append('  ' + json.dumps(key, ensure_ascii=False) + ': ' + json.dumps(value, ensure_ascii=False))
    out.append('Gone since last round (all supplied lost annotation keys; renamed or deleted):')
    out.extend('  ' + json.dumps(key, ensure_ascii=False) for key in mapping.get('lost_notes', []))
    out.extend(['', ANNOTATION_INSTRUCTION])
    return '\n'.join(out)


def install(project, loop, *, compact_source_map=False, archive_directory=None):
    import rsigame.view.project_map as project_map
    import rsigame.view.static_view as static_view
    if compact_source_map and archive_directory is None:
        raise ValueError('compact source map requires an archive directory')
    project_map.scan = (lambda work: scan(work, include_empty=True)) if compact_source_map else scan
    original_render = project_map.render
    project_map.render = lambda mapping, cap_per_file=60: (render_compact(mapping, archive_directory) if compact_source_map else
        original_render(mapping, cap_per_file=cap_per_file) + '\nC# map scope: ' + mapping.get('scan_scope', 'unavailable'))
    static_view.build_static_view = build_static_view
    project.declared_scenarios = declared_scenarios
    install_polish_facts()
