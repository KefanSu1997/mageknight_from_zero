# DeckIdeal 素材替换审计记录（输出/替换/导入要点）

## 背景
- 由于网络受限，采用本地 System.Drawing 生成卡背/卡面/场地候选图。
- 审计目标是让素材来源、输出路径、最终替换路径与导入设置可追溯。

## 记录模板（建议每次替换都写）
1) 生成方式
- generation_method: local_procedural | imdream
- prompt_or_style_target: （Imdream 写提示词；本地生成写风格目标）
- reference_images: none / URL 列表
- generator/script: tools/xxx.ps1 或工具链

2) 候选输出路径（至少包含最终选用项）
- CardBack candidate: Assets/UI/Images/DeckTheme/Ideal/Candidates/CardBack/...
- CardFace candidate: Assets/UI/Images/DeckTheme/Ideal/Candidates/CardFace/...
- Board candidate: Assets/UI/Images/DeckTheme/Ideal/Candidates/Board/...

3) 最终替换路径
- CardBack: Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png
- CardFace: Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_face_v1.png
- Board: Assets/UI/Images/DeckTheme/Ideal/deck_ideal_full_1378x1204_v2.png

4) 导入设置要点（Meta 关注项）
- TextureType=Sprite，SpriteMode=Single
- sRGB=On, MipMap=Off, AlphaIsTransparency=On
- PPU/FilterMode 与 UI 布局匹配（CardBack/Board PPU=100；CardFace PPU=512）

## 经验要点
- 本地生成没有 Task ID 时，用“风格目标+脚本+种子+输出路径”替代提示词日志。
- 最终替换路径必须写清楚，否则审计无法确认素材与场景绑定关系。
- 导入设置不一致会导致卡面/卡背模糊或比例偏差，应记录关键字段。
