# 工作记录：ElementCardsShowcase 卡面替换为魔法骑士

时间：2026-02-03

## 目标
- 将 ElementCardsShowcase 场景中的 4 张元素卡（地/水/风/火）的卡面替换为已准备好的魔法骑士卡面资源。
- 保持卡背与边框不变，保留翻面功能。

## 修改内容
- 更新 ElementCardsShowcaseBootstrapper 的前景卡面路径：
  - Earth -> Assets/UI/Images/ElementCards/element_card_face_earth_mk.png
  - Water -> Assets/UI/Images/ElementCards/element_card_face_water_mk.png
  - Wind -> Assets/UI/Images/ElementCards/element_card_face_air_mk.png
  - Fire -> Assets/UI/Images/ElementCards/element_card_face_fire_mk.png

## 影响范围
- 仅调整运行时绑定的卡面资源路径，场景结构与翻面逻辑未改动。

## 编译检查（Unity Console）
- 当前 Unity MCP 会话未就绪，无法拉取 Console。需要在 Unity 编辑器打开后重试：
  - clear -> get types=error -> get types=error

## 自动化测试
- 未发现 ElementCardsShowcase 对应的 Scene Automation 配置；待确认是否需要新建自动化配置或由人工触发。
