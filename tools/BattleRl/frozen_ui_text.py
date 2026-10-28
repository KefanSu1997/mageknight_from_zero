"""Pinned pretrained mmBERT text reader. Inference only; no fallback hash vectors."""
import hashlib
import json
from pathlib import Path
import re
import numpy as np
from structured_ui_ppo import ROOT, NUMERIC_KEYS, TEXT_KEYS, state
from printed_entity_text import public_description

MODEL_ID = 'jhu-clsp/mmBERT-small'
REVISION = 'abc32620dd4f6ab06f5fbe905dc25f310618e09f'
WIDTH = 384


def file_sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class FrozenTextEncoder:
    def __init__(self, cache_directory, local_only=False):
        import torch
        from huggingface_hub import snapshot_download
        from transformers import AutoTokenizer, AutoModel
        self.torch = torch
        torch.set_num_threads(4)
        self.snapshot = Path(snapshot_download(MODEL_ID, revision=REVISION, local_files_only=local_only,
            allow_patterns=['config.json', 'tokenizer.json', 'tokenizer_config.json',
                            'special_tokens_map.json', 'pytorch_model.bin']))
        self.tokenizer = AutoTokenizer.from_pretrained(self.snapshot, local_files_only=True, trust_remote_code=False)
        self.model, loading = AutoModel.from_pretrained(self.snapshot, local_files_only=True, trust_remote_code=False,
            dtype=torch.float32, attn_implementation='sdpa', weights_only=True, output_loading_info=True)
        allowed_unused = {'decoder.weight', 'decoder.bias', 'head.norm.weight', 'head.dense.weight'}
        if loading.get('missing_keys') or loading.get('mismatched_keys') or set(loading.get('unexpected_keys', []))-allowed_unused:
            raise ValueError('Pretrained encoder weights are missing/mismatched; no random reader allowed')
        self.model.eval()
        for parameter in self.model.parameters():
            parameter.requires_grad_(False)
        assert self.model.config.hidden_size == WIDTH
        self.signature = {'model_id': MODEL_ID, 'revision': REVISION, 'width': WIDTH,
            'pooling': 'attention-mask weighted token mean, weighted chunk merge, L2 normalization',
            'chunk_tokens': 256, 'device': 'cpu', 'dtype': 'float32', 'frozen': True,
            'files_sha256': {p.name: file_sha(p) for p in sorted(self.snapshot.iterdir()) if p.is_file()},
            'parameter_count': sum(p.numel() for p in self.model.parameters())}
        self.cache_directory = Path(cache_directory)
        self.cache_directory.mkdir(parents=True, exist_ok=True)
        self.cache_signature = hashlib.sha256(json.dumps(self.signature, sort_keys=True).encode()).hexdigest()
        self.cache = {}
        self.fingerprint_before = self.fingerprint()
        self.computations = 0

    def fingerprint(self):
        digest = hashlib.sha256()
        for name, tensor in self.model.state_dict().items():
            digest.update(name.encode())
            digest.update(tensor.detach().cpu().contiguous().numpy().tobytes())
        return digest.hexdigest()

    def encode(self, texts):
        torch = self.torch
        outputs = []
        for text in texts:
            if not isinstance(text, str):
                raise TypeError('Exact public text required')
            key = hashlib.sha256((self.cache_signature+'\n'+text).encode('utf-8')).hexdigest()
            path = self.cache_directory/(key+'.npz')
            if key not in self.cache:
                if path.exists():
                    with np.load(path, allow_pickle=False) as saved:
                        if str(saved['text']) != text or str(saved['signature']) != self.cache_signature:
                            raise ValueError('Text cache provenance mismatch')
                        vector = saved['vector'].copy()
                else:
                    encoded = self.tokenizer(text, return_tensors='pt', truncation=True, max_length=256,
                        return_overflowing_tokens=True, padding=True)
                    encoded.pop('overflow_to_sample_mapping', None)
                    weighted, count = np.zeros(WIDTH, dtype=np.float64), 0.
                    # Every chunk is encoded; long public text is not silently discarded.
                    for index in range(encoded['input_ids'].shape[0]):
                        inputs = {k: v[index:index+1] for k, v in encoded.items()}
                        with torch.inference_mode():
                            hidden = self.model(**inputs).last_hidden_state
                            mask = inputs['attention_mask'].unsqueeze(-1)
                            weighted += (hidden*mask).sum(dim=1)[0].cpu().numpy()
                            count += float(mask.sum())
                    vector = (weighted/max(count, 1)).astype(np.float32)
                    norm = np.linalg.norm(vector)
                    if not np.isfinite(vector).all() or norm <= 0:
                        raise ValueError('Invalid actual encoder output')
                    vector /= norm
                    np.savez(path, text=np.array(text), signature=np.array(self.cache_signature), vector=vector)
                    self.computations += 1
                if vector.shape != (WIDTH,) or not np.isfinite(vector).all():
                    raise ValueError('Invalid cached text vector')
                vector.setflags(write=False)
                self.cache[key] = vector
            outputs.append(self.cache[key])
        return np.stack(outputs) if outputs else np.zeros((0, WIDTH), dtype=np.float32)

    def verify_frozen(self):
        result = {'before': self.fingerprint_before, 'after': self.fingerprint(),
            'trainable_encoder_parameters': sum(p.numel() for p in self.model.parameters() if p.requires_grad),
            'encoder_gradient_tensors': sum(p.grad is not None for p in self.model.parameters()),
            'new_text_computations': self.computations}
        assert result['before'] == result['after']
        assert result['trainable_encoder_parameters'] == result['encoder_gradient_tensors'] == 0
        return result


class PublicUiTexts:
    def __init__(self):
        self.cards, self.names, self.enemies = {}, {}, {}
        self.signature = {}
        folder = ROOT/'resources/text_json'
        for name in ('basic_card', 'advanced_card', 'magic', 'items', 'monster'):
            path = folder/(name+'.json')
            self.signature[name] = file_sha(path)
            for row in json.loads(path.read_text(encoding='utf-8')):
                if name in ('basic_card', 'advanced_card'):
                    text = '基础：'+row.get('base_effect', '')+'；强化费用：'+row.get('required_crystals', '')+'；强化：'+row.get('enhanced_effect', '')
                elif name == 'magic':
                    text = '；'.join(section+'：'+public_description(row[section]) for section in ('spell_top', 'spell_bottom'))
                elif name == 'items':
                    text = '普通：'+row['assign_effect']+'；焚毁：'+row['once_per_round']
                else:
                    self.enemies[row['id']] = public_description(row)
                    continue
                self.cards[row['id']] = text
                for key in ('name', 'cn_name', 'chinese_name'):
                    value = row.get(key)
                    if isinstance(value, str):
                        self.names[value] = text

    def control_text(self, control, observation):
        text = '控件：'+control['name']+'；显示：'+control['text']
        match = re.fullmatch(r'Btn_Card_(.+)_(\d+)', control['name'])
        if match:
            identity = match.group(1)
            if identity == 'w':
                text += '；创伤牌。'
            elif identity in self.cards:
                text += '；原印刷规则：'+self.cards[identity]
            else:
                raise ValueError('Visible card lacks exact original description: '+identity)
        public = state(observation)
        # These are actual displayed inspector fields, never rl.sharedRuleState.
        text += '；当前卡牌模式：'+public.get('ui.cardMode', '')
        text += '；当前说明：'+public.get('ui.Details', '')
        if '/Enemy_' in control['path']:
            # The target's own rendered labels are already in control.text.
            text += '；目标说明：'+public.get('ui.TargetPreview', '')
        return text

    def state_text(self, observation, controls):
        public = state(observation)
        lines = ['阶段：'+public.get('phase', '')]
        lines += [key+'：'+public.get(key, '') for key in (*NUMERIC_KEYS, *TEXT_KEYS)]
        lines += ['当前可操作控件：'+c['name']+' '+c['text'] for c in controls]
        return '\n'.join(lines)
