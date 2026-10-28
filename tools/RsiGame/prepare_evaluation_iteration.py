"""Freeze the same32-item rubric with new, measured iteration evidence."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/RsiGame'))
from prepare_retrospective_review import SP


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def freeze(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    data = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()
    if path.exists():
        if json.loads(path.read_text(encoding='utf8')) != value:
            raise FileExistsError(path)
        return  # Preserve exact existing bytes and hashes, including formatting.
    path.write_bytes(data)


def compact_facts(root, facts):
    result = json.loads(json.dumps(facts))
    for mode, version in result['versions'].items():
        for view in version['views']:
            detailed = view['context'] in ['ranged', 'block', 'assign_damage', 'selected_action_payment', 'three_enemies']
            view['raw_report'] = str(root / mode / 'evaluation_analysis/key_phases' / view['context'] / 'report.json')
            view['texts'] = [{k: t[k] for k in ['name', 'fullText', 'fontSize', 'overflow', 'glyph_height_range']}
                             for t in view['texts'] if detailed and t['name'] in ['Numbers', 'Details', 'Phase']]
            view['enemy_images'] = [{k: image[k] for k in ['texture', 'sourceWidth', 'sourceHeight', 'screenRect', 'uvRect', 'uv_full', 'relative_display_vs_source_ratio_difference']}
                                    for image in view['enemy_images']]
            view['pixel_contrast'] = [dict(path=sample['path'].split('BattleTable/')[-1],
                measurement={k: value for k, value in sample['measurement'].items() if k != 'samples'})
                for sample in view['pixel_contrast'] if detailed and sample['path'].split('/')[-1] in ['Numbers', 'Details']]
    result['sampling_definition'] = 'Pixel proxy uses generated visible non-whitespace glyph quad interiors and surrounding rings, actual PNG luminance distributions; per-region coordinates and counts retained in raw_report. It is not segmented glyph contrast or WCAG certification. Geometry does not prove absence of overlay occlusion.'
    return result


def prepare(root, review_name='review_v2'):
    metadata = json.loads((root / 'cycle.json').read_text(encoding='utf8')) if (root / 'cycle.json').is_file() else {}
    cycle = metadata.get('cycle', 'cycle01')
    candidate = metadata.get('candidate', 'after_v2')
    old = ROOT / 'AutomationOutputs/RsiGame/experiments/20261021_mageknight_unity_30/reviews/rounds_01_to_12/rescore_v1'
    protocol = json.loads((old / 'protocol.json').read_text(encoding='utf8'))
    protocol['version'] = 'evaluation_iteration_v1'
    protocol['scope'] = metadata.get('review_scope', 'New candidate vs same-fixture current baseline. Historic R12 images separately labelled; existing art/ability fixes predate this cycle. No historical ratings supplied.')
    review = root / review_name
    review.mkdir(exist_ok=True)
    freeze(review / 'protocol.json', protocol)
    prompt = (SP.replace('{protocol}', json.dumps(protocol, ensure_ascii=False, indent=2))
              .replace('对历史版本评分', '对当前迭代候选评分')
              .replace('retrospective_rescore_v1', 'evaluation_iteration_v1'))
    prompt += '\n当前证据补充：measured_facts是原始测量摘要，不是既有评分。sampled frame times仅为指定60帧的编辑器样本；不是有效反馈延迟。字形quad不是像素mask；局部像素对比度是有采样定义的proxy。源图全UV不能单独证明主体完整。字段presence不能单独证明未被弹层遮挡。实际路径是经规则核对的参考路径，未证全局最短。请认真区分已测原始值、部分证据和仍缺的评分锚点。\n'
    prompt_path = review / 'system_prompt.txt'
    if prompt_path.exists() and prompt_path.read_text(encoding='utf8') != prompt:
        raise FileExistsError(prompt_path)
    prompt_path.write_text(prompt, encoding='utf8')
    facts = dict(versions={}, scope='Scoped measured facts only, no generated model ratings or human-performance claims.')
    for mode in ['before', candidate]:
        intent = json.loads((root / mode / 'source_intent.json').read_text(encoding='utf8'))
        events = json.loads((root / mode / 'pointer_events.json').read_text(encoding='utf8'))['events']
        source_events = [e for e in events if e['request_id'] in intent['event_ids']]
        def panel(observation):
            names = {c['name'] for c in observation['controls'] if c['clickable']}
            return 'SourceTools' if 'Btn_CloseTools' in names else 'BattleMenu' if 'Btn_CloseBattleMenu' in names else 'Battle'
        transitions = sum(panel(e['before']) != panel(e['after']) for e in source_events)
        source_index = next(i for i,e in enumerate(source_events) if e['name'] == 'Btn_UseSource')
        card_index = next(i for i,e in enumerate(source_events) if i > source_index and e['name'].startswith('Btn_Card_'))
        transition = source_events[source_index + 1:card_index + 1]
        samples, views = [], []
        for capture_path in sorted((root / mode / 'key_phases').glob('*/capture.json')):
            capture = json.loads(capture_path.read_text(encoding='utf8'))
            evaluation = capture['evaluation']
            report = json.loads((root / mode / 'evaluation_analysis/key_phases' / capture_path.parent.name / 'report.json').read_text(encoding='utf8'))
            samples.extend(x['unscaledDeltaMilliseconds'] for x in capture.get('evaluationFrames') or [])
            texts = evaluation['texts']
            views.append(dict(context=capture_path.parent.name, canvas=[evaluation['screenWidth'], evaluation['screenHeight']],
                texts=[dict(path=t['path'], name=t['name'], fullText=t['fullText'], fontSize=t['fontSize'], overflow=t['isTextOverflowing'], rect=t['screenRect'], glyph_height_range=[t['minGlyphHeightPixels'],t['maxGlyphHeightPixels']]) for t in texts if t['name'] in ['Details','Numbers','Phase','Heading','Status','HandLabel','DamagePlan']],
                overflow_count=report['overflow_count'], text_denominator=report['text_count'],
                control_bounds=dict(denominator=report['control_rect_denominator'], out_of_bounds=report['control_rect_out_of_bounds_count']),
                enemy_images=report['enemy_illustrations'],
                pixel_contrast=[dict(path=t['path'], measurement=t['pixel_contrast']) for t in report['texts'] if 'pixel_contrast' in t]))
        samples.sort()
        facts['versions'][mode] = dict(source_intent=intent, panel_transitions=transitions,
            source_commit_to_next_card=dict(definition='After actual source commit until next card selection, including the first card click, not counted again in total intent', clicks=len(transition), distance_pixels=sum(e['pointer_segment_pixels'] or 0 for e in transition)),
            frame_sample=dict(n=len(samples), median_ms=samples[len(samples)//2] if samples else None, p95_ms=samples[min(len(samples)-1,math.ceil(len(samples)*.95)-1)] if samples else None, scope='Idle ranged Game View sample, not input feedback latency or general performance'), views=views)
    freeze(root / 'measured_facts.json', facts)
    freeze(root / 'measured_facts_compact.json', compact_facts(root, facts))
    images = []
    def add_image(path, role):
        images.append(dict(path=str(path.resolve()), sha256=sha(path), role=role))
    for path in sorted((root / candidate / 'key_phases').glob('*/screen.png')):
        add_image(path, 'candidate')
    for label in ['ranged','block','assign_damage','source_play','three_enemies','melee_two_enemies']:
        add_image(root / f'before/key_phases/{label}/screen.png', 'baseline_current_before')
    for path in sorted((root / candidate / 'interaction_checks/key_phases').glob('*/screen.png')):
        add_image(path, 'candidate_actual_interaction')
    for path in sorted((root / candidate / 'recall_checks/key_phases').glob('*/screen.png')):
        add_image(path, 'candidate_actual_recall_interaction')
    for path in metadata.get('additional_review_images', []):
        add_image(root / path, 'candidate_additional_actual_interaction')
    text_paths = [root / 'measured_facts_compact.json', root / candidate / 'interaction_checks/validation.json', root / candidate / 'compile_receipt.json', root / candidate / 'source_manifest.json']
    if (root / 'after/rejection.json').is_file():
        text_paths.insert(3, root / 'after/rejection.json')
    text_paths += [root / p for p in metadata.get('additional_review_text', [])]
    packet = dict(round=metadata.get('review_round',16), review_version=protocol['version'], sp_path=str(prompt_path.resolve()), sp_sha256=sha(prompt_path),
        required_images=images, required_text=[dict(path=str(p.resolve()),sha256=sha(p)) for p in text_paths], optional_observations=[],
        context=metadata.get('context', f'New separately versioned {cycle}; original RSI round history is not overwritten.'))
    freeze(review / 'input.json', packet)
    freeze(review / 'run_manifest.json', dict(sp_sha256=sha(prompt_path), protocol_sha256=sha(review/'protocol.json'), input_sha256=sha(review/'input.json')))
    return dict(images=len(images), required_text=len(text_paths), input=str(review/'input.json'), sp=str(prompt_path))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--review-name', required=True, help='Fresh immutable input version; never overwrite an active review')
    args=parser.parse_args()
    if Path(args.review_name).name != args.review_name:
        parser.error('Review name must be a single directory name')
    print(json.dumps(prepare(args.root.resolve(), args.review_name),ensure_ascii=False))
