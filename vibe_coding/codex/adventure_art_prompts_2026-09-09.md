# 可复用角色与地点素材生成记录

生成方式：内置 imagegen 工具。人物/怪物使用独立 RGBA PNG，地点使用不含人物的 RGB 背景。原始生成文件保留，复制进入 Assets/Resources/Adventure/Art 后由 Unity 导入为 Sprite。没有用 Python 抠图或修改像素。

统一风格：写实绘制的奇幻桌游插画，低饱和青绿与黄铜，左上方暖光。角色要求全身、完整四肢和武器、透明背景，背景要求前景留出落脚空间。

| ID | 工作区文件 | 原始生成文件 |
| --- | --- | --- |
| hero | Assets/Resources/Adventure/Art/hero_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-00375e87-390b-41e6-a039-a7dbaf85c55a.png |
| orc | Assets/Resources/Adventure/Art/orc_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-6b08e768-158c-4045-bbb0-f9a5865f5afc.png |
| wolf | Assets/Resources/Adventure/Art/wolf_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-9c59007b-44f7-4f0f-9049-b38849e29dc3.png |
| forest | Assets/Resources/Adventure/Art/forest_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-57c457b6-34d5-4ce0-a909-ba3348ea7716.png |
| village | Assets/Resources/Adventure/Art/village_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-86ceef3f-4b43-4129-b55a-e662b16a784e.png |
| guard | Assets/Resources/Adventure/Art/guard_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-0827995f-da1a-4b99-96d3-d5e2d06f0d2b.png |
| ranger | Assets/Resources/Adventure/Art/ranger_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-31fedf57-6d19-42e8-bc20-d5eebb0c4262.png |
| monk | Assets/Resources/Adventure/Art/monk_v1.png | C:/Users/sukefan/.codex/generated_images/01a0816b-a230-7462-a3e4-556ffba50c9f/exec-3572f798-70d5-4be7-9065-baefeeb40d68.png |

valley_v1.png 是上一轮 exploration_v1.png 的原样副本，原提示词见 rules_scene_art_prompts_2026-09-09.md。

以下保留当前会话可直接导出的精确提示词。hero/orc 使用上述统一风格，分别为持剑盾的青绿披风骑士、持斧的荒野兽人；这里是需求摘要，未伪称为逐字提示词。

```json
[
  {
    "key": "wolf",
    "prompt": "Reusable Unity 2D enemy sprite: one large grey dire wolf, full body three-quarter side view facing LEFT, all four paws and full tail visible, head low and amber eyes alert, strong realistic readable silhouette, no rider and no accessories. Painterly realistic fantasy board-game art, dark grey fur with pale silver highlights, soft warm upper-left lighting. Important: clean isolated cutout on TRUE transparent alpha background, absolutely no environment, no opaque colored backdrop, no vignette or haze around figure, no floor or ground shadow, no lettering or borders. Generous transparent margins, horizontal canvas. Independent creature to be composited onto different forest and village scenes."
  },
  {
    "key": "forest",
    "prompt": "Reusable Unity 2D battle environment BACKGROUND ONLY, no people, no creatures, no weapons, no UI. Wide horizontal 16:9 painterly realistic fantasy forest clearing with old stone road and ruined arches at far sides. Camera at standing person's waist height, spacious flat stone/earth stage spanning lower half; easy to layer full-body character sprites over it. Distant misty forest and subtle ancient gate at upper center, warm afternoon light from upper left, dark teal forest foliage and weathered grey stone with restrained brass-gold sunlight. Clear foreground staging area with no large obstacles at left quarter or right quarter. Calm richly illustrated believable environment, not a poster. No text."
  }
]
```

```json
[
  {
    "key": "village",
    "prompt": "Reusable fantasy game village BACKGROUND only, absolutely no people or creatures. Wide 16:9 landscape, painterly realistic board-game style. A welcoming medieval village square at late afternoon: timber-and-stone inn at rear right, chapel and distant hill at rear left, banners and lanterns, restrained dark teal foliage and warm golden light from upper left. Camera at waist height. Large flat cobblestone staging area fills lower half and stays clear across full width, so independent full-body characters may be placed there later in Unity. Rich detailed architecture concentrated in back and sides, clear subjectless foreground, no text or UI."
  },
  {
    "key": "guard",
    "prompt": "One full-body village guard as a reusable fantasy game cutout, an adult human man with short dark beard, tan gambeson and worn steel breastplate, dark forest-green cloak, round shield and spear held vertically close to body, three-quarter pose facing slightly LEFT, both feet visible. Realistic painterly fantasy board-game illustration, warm upper-left light. True transparent alpha background, clean silhouette without backdrop, haze, floor, ground shadow, border or text. Entire character with generous 8% margins, vertical canvas. This is a separate recruitable ally asset, composed later over different location backgrounds."
  }
]
```

```json
[
  {
    "key": "ranger",
    "prompt": "Reusable Unity ally asset: one full-body adult female woodland ranger, believable leather tunic and green-grey cloak, simple bow held at rest, small quiver, sturdy boots, calm alert face, three-quarter view facing slightly LEFT, all of body and bow visible. Painterly realistic fantasy board-game illustration, warm upper-left light, muted forest colours. Genuine TRANSPARENT alpha background, no environment, no colored backdrop, no haze or vignette, no ground plane or pedestal, no text/frame. Clean isolated cutout with 8% margin, vertical canvas; this ally will stand on different game backgrounds."
  },
  {
    "key": "monk",
    "prompt": "Reusable Unity ally asset: one full-body elderly male monastery healer, kindly weathered face, short grey beard, simple ivory and muted brown robes, leather belt with small herb pouches, wooden walking staff, both feet visible, three-quarter view facing slightly LEFT. High-quality painterly realistic fantasy board-game illustration, warm upper-left light matching other fantasy characters. Genuine TRANSPARENT alpha background, no backdrop/scenery, no haze/vignette, no ground plane or shadow, no border, no text. Clean isolated character with generous transparent margins, vertical canvas, designed for reuse in different locations."
  }
]
```
