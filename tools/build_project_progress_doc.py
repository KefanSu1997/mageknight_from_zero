"""Build a text-only progress document from the reviewed historical evidence index."""
import json
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'vibe_coding/codex'


def read(path):
    return json.loads((ROOT / path).read_text(encoding='utf-8-sig'))


def main():
    index = read('vibe_coding/codex/card_verification_history_index_2026-09-15.json')
    inventory = read('AutomationOutputs/HeroSkills/20260910_232750_4614deb3/source_inventory.json')
    units = read('Assets/Resources/CardVerification/unit_cases.json')['cases']
    unit_counts = Counter(c['cardId'] for c in units)
    lines = ['# MageKnight｜开发进度：规则—实现—验证', '',
        '更新：2026-09-15。本文是本地已有证据的盘点，不是一次新全量测试。规则编号对应《整体规则与游戏策划》。所有截图及大报告保留本地；飞书只同步本文文字。', '',
        '## 1. 当前结论', '',
        '原版卡规则尚未全部通过。当前行动/法术/宝物定义 657 个用例；626 个有已完成历史场景结果。按每个用例最近的历史结果索引：156 passed、455 partial、15 failed；31 个新增用例尚无完整场景结果。不同批次来源时间不同，这不是当前代码全库认证。', '',
        '125 条行动/法术/宝物资产记录对应 121 张可读卡面；高级行动 044—046 是近黑空图，047 是牌背。这四条的 8 个失败是资产来源问题，保留在问题清单，不伪造效果，也不把删除失败记录当作测试通过。另有 7 个可读卡面的失败待处理。', '',
        '部队有 32 条资产记录、30 张有效卡面，目前 8 种部队的列明能力/招募/承伤范围有场景证据，另外 22 种待覆盖。技能有 8 个英雄池、82 条技能，目前前 5 条的 39 个场景案例通过，其余 77 条待覆盖。', '',
        '已通过用例退出日常执行队列，保留历史证据和回归库。partial 不是失败断言，但仍有明确未覆盖行为；后续用新增或受影响分支补齐，不重复整批跑库。', '',
        '## 2. 规则—实现—验证模块账本', '',
        'R01/R02 轮次、昼夜与回合：TurnEngine、PlayerTurnEngine、GameEngine 等已有实现，部队重整已从普通回合移至新轮次；技能有回合/轮次次数测试。完整剧本生命周期、所有战术和最终计分仍待系统验收。', '',
        'R03/R04 原卡、费用与魔力：复用 Assets/GameData/CardsAssets、Assets/GameData/cards、Assets/Logic/Runtime/ActionSystem.cs 与 CardEffects。已逐批修复基础二选一、原法术费用、实际伤牌守恒、魔晶和后续施法等。逐卡状态见第 5 节；完整出牌区生命周期仍是多张 partial 的共同缺口。', '',
        'R05 旅途与探索：Map/TerrainCost、TilePlacementService、ExplorationService 及 AdventureSession 已连接场景。Journey 31 次操作/141 断言，Exploration 22 次/76 断言通过；原移动卡有实际支付与后续移动联验。迷雾的不安全格结束回合撤离等组合仍待完成。', '',
        'R06 交涉与招募：RecruitmentService、原部队加载/能力入口、AdventureSession 已连接真实供给。Recruitment 场景 16 次操作/58 断言通过。魅惑的一次招募减三、地点与失败守恒已修改且 18 项定向 EditMode 通过，当前相关场景验收尚未完成。', '',
        'R07 战斗：BattleResolver、ActionContext 和原效果贡献保留攻击种类/元素/来源；有实际目标、抗性合计取整、城防阶段、分次格挡消费等证据。Combat 场景 45 次操作/202 断言通过。仍须逐卡补齐完整后续行为和阶段边界，不能由该场景通过推导所有战斗卡通过。', '',
        'R08 部队承伤与治疗：普通部队伤害余量、抗性同场一次、毒素双伤、麻痹销毁释放槽、自伤与下轮重整等已在原部队场景验证。最新范围 156 例/865 操作/6914 断言；复杂组合与其他来源的临时状态不能据此概括为全通过。', '',
        'R09 原技能：SkillActions、HeroSkillEffects 处理来源身份、所有权、次数与生命周期。前五技能 39 例/240 操作/1614 断言通过。其他 77 条保持待验收。', '',
        'R10 成长、奖励、地点、剧本：存在局部代码与卡牌后续操作证据；尚未取得完整连续剧本、所有城市/冒险点、PVP/合作和扩展规则验收，因此仍列后续目标。', '',
        '场景与表现复用：Assets/Scripts/Adventure/Content 的角色/地点/遭遇定义、Presentation 和 Assets/Logic/Runtime/Adventure/AdventureSession 已拆分内容与操作。四场景为 Part1_JourneyRules、Part1_ExplorationRules、Part1_RecruitmentRules、Part1_CombatRules；抽牌展示 Part1_DeckIdeal/Part1_DeckManaTest 保留。当前测试/demo须继续服务完整游戏，不能用独立一次性脚本替代正式规则。', '',
        '## 3. 最近完成批次与当前中断点', '',
        '迷雾形态/帷幕 magic_014：62 例、1246 效果断言通过，接通全体部队抗性、英雄首伤与附加效果、战斗/回合失效；仍为待联验。证据 AutomationOutputs/AllOriginalCards/20260911_205758_b83addf3/magic；说明 worklog_2026-09-11_mist_form_resolution.md。', '',
        '献祭/牺牲 magic_011：58 例，49 partial/9 passed，197 实际操作/1411 效果断言；真实弃牌实例、颜色/神器选色、付费后晶体配对、实际元素攻城与远程结算通过。证据 20260911_213849_0b76d08a；完整正在施放卡及出牌区生命周期仍待补齐。', '',
        '附身（magic_021 强化面）：24 个选中用例，19 partial/5 passed、495 断言；来源限制单笔攻击、保留其他合法攻击、实际敌人攻击数值及战斗失效通过。第一次只有 magic_021_13 的预期键名错误，修正后只重测这一例；没有重测整个卡库。证据 AutomationOutputs/Possess/20260911/verification_summary.json。', '',
        '魅惑（magic_021 基础面）：在上述附身基础上新增 31 例，当前计划只验 magic_021_00、01、22—52 共 33 例。已修改四种晶体选择/非法选择提前拒绝、交涉时一次招募减费、成功消费/失败保留、真实供给与地点；18 个定向 EditMode 通过（AutomationOutputs/EditMode/20260911_222437_8cdde8d7）。', '',
        '魅惑场景运行 AutomationOutputs/AllOriginalCards/20260911_222541_14bcee92 只有请求与部分截图，未找到完整 effects.json/report.json；不得把它记为场景通过。已有 magic_021 历史结果仅代表修改前相应分支，当前基础面改动须补实测。下一步恢复这 33 例并取得 Console 两次零错误，发现失败只修复并复测相关例。', '',
        '飞书：机器人已完成配对、真实收发以及原桌面任务派发；长任务等待时后续消息积压的问题已修复，三条积压消息真实送达。CLI/API 优先，不再用浏览器逐项操作业务。创建文档使用官方 SDK，凭证只在本机 DPAPI 保存；最终答复回传尚需随本次任务完成观察。', '',
        '## 4. 证据定位与可信范围', '',
        '所有相对路径均相对于项目根目录 D:/study_and_work/unity-MK-test/MageKnight_from_zero。effects.json 是规则检查，report.json 是场景操作流程；两者不能互相替代。用例定义 Assets/Resources/CardVerification/cases.json；历史精简索引 vibe_coding/codex/card_verification_history_index_2026-09-15.json 保留用例号、原文、失败期望/实际值、缺口及报告哈希。', '',
        '历史证据编号：']
    for n, run in enumerate(index['runs'], 1):
        lines += [f"- E{n}：AutomationOutputs/AllOriginalCards/{run['run']}/{run['batch']}；{run['cases']} 例；{run['results']}。"]
    lines += ['', 'U：AutomationOutputs/OriginalUnits/20260911_210433_6cebc470；S：AutomationOutputs/HeroSkills/20260911_193604_558bedfd；A：AutomationOutputs/AdventureRegression/20260911_210739_ca201dcd。', '',
        '代码与报告有各自时间和版本。历史结果用于确定已测范围及补测队列，不把多批结果伪装为一次当前全量测试。原卡 15 个失败逐项保留在下面；截图均引用本地，不传飞书或 LFS。', '',
        '## 5. 行动、法术和宝物逐卡账本', '',
        '每张卡的实现入口是原定义/SO→ActionSystem→CardEffects；基础与强化规则来自既有来源清单。P=已测分支通过，L=待联验，F=失败，N=无完整场景证据。P 不等于整张卡所有可能组合通过。源清单中没有名称/文字的记录明确标为资产缺失，不补造。', '']
    for card in index['cards']:
        result = card['historicalResults']
        label = '来源资产问题' if card['category'] != 'playable_face' else '发现不符' if result.get('failed') else '待联验' if result.get('partial') or card['missing'] else '已测分支通过'
        lines += [f"### {card['id']} · {card['name'] or '无有效牌面'} · {label}", '',
            '规则：基础：'+(card['baseText'] or '原文缺失')+'；强化：'+(card['enhancedText'] or '原文缺失'), '',
            f"进度/验证：定义 {card['planned']} 例；历史 P{result.get('passed',0)} / L{result.get('partial',0)} / F{result.get('failed',0)} / N{len(card['missing'])}；来源批次：" + '、'.join(card['evidenceRuns']) + '。', '']
        if card['id'] == 'magic_021':
            lines += ['当前提示：魅惑基础面已有新修改，上述历史结果不是当前修改验收；33 例定向场景尚待完成。', '']
        for failure in card['failures']:
            checks = '；'.join(f"{v['key']} 预期 {v['expected']}，实际 {v['actual']}" for v in failure['failed'])
            lines += [f"失败 {failure['id']}：{checks}。", '']
        if card['remaining']:
            lines += ['待补齐：'+'；'.join(card['remaining']), '']
    lines += ['## 6. 原部队逐项范围', '',
        '规则来源为对应原部队卡；共 30 个有效面、2 个牌背。已覆盖的 8 种按当前 156 例定义统计，证据 U；其余只是发现原资产，不能认定能力通过。army_003 已从原图恢复“烈焰枪手”，旧来源清单缺名不再代表当前数据。', '']
    for unit in inventory['unitRecords']:
        name = '烈焰枪手' if unit['id']=='army_003' else unit['name'] or '牌背'
        count = unit_counts[unit['id']]
        status = f'证据 U，{count} 个相关场景案例通过；范围为已列能力/费用/招募/承伤，复杂组合仍按缺口补齐' if count else '非可用牌面，保留资产身份' if unit['status']=='back_artwork_not_playable' else '已有原资产，主动能力与实际场景联验待完成'
        lines += [f"- {unit['id']} · {name}：{status}。"]
    lines += ['', '## 7. 原技能逐项范围', '',
        '规则来源为原技能池与技能说明；同名不代表同一来源身份。前五条按证据 S 验证费用/操作/数值/次数与生命周期，其他条目尚无本账本所需的独立场景结果。', '']
    for i, skill in enumerate(inventory['skillRecords']):
        status = '已列场景分支通过（证据 S）' if i < 5 else '原表已有，效果/费用/目标/次数/持续期的场景联验待完成'
        lines += [f"- {skill['sourceId']} · {skill['name']}：{status}。"]
    lines += ['', '## 8. 后续执行队列与完成标准', '',
        '先收尾魅惑 33 例，再修复下面七张可读卡面的已知失败：魔法天赋、仪式攻击、时光传送、清风之翼、公正之剑、星辰之弓、法术宝典。每次先对照原面和合法夹具，判断是规则实现还是测试夹具错误；不得直接改成当前输出值。四条无有效卡面资产另行追溯来源。', '',
        '然后围绕 partial 的实际缺口，补卡牌实例/已出牌区/弃牌生命周期、后续实际支付与消费、目标和阶段限制、效果过期，以及未覆盖原部队和原技能。通过分支离开日常队列；修改公共逻辑时按影响选择回归。', '',
        '每个完成记录必须同时给出原版来源、实际实现、真实场景操作、独立预期与实际数值、必要截图、Console 双零错误和剩余边界。整个原版范围仍为未完成，不能用单元测试数、场景按钮成功数或画面好看替代完整验收。', '']
    text = '\n'.join(lines)
    target = OUT / 'rules_progress_verification.md'
    target.write_text(text, encoding='utf-8')
    assert len(index['cards']) == 125 and len(inventory['skillRecords']) == 82
    assert sum(unit_counts.values()) == 156 and len(unit_counts) == 8
    print(f'{target}: {len(text.encode())} bytes; 125 card records, 32 unit records, 82 skill records')


if __name__ == '__main__':
    main()
