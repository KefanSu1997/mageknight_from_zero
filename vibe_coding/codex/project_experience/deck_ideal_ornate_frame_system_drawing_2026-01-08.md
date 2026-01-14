# DeckIdeal 本地程序化华丽卡面生成要点（System.Drawing）

## 结论摘要
- 使用 PowerShell + System.Drawing 可在无网络条件下生成华丽黑暗奇幻卡面/卡背/场地素材，并通过固定 seed 保证可复现。
- PowerShell 中创建 GDI+ 类型建议用 `[Type]::new(...)`，避免 `New-Object Type(args)` 解析错误。
- `System.Drawing.Drawing2D` 不需要单独 Add-Type；只需 `Add-Type -AssemblyName System.Drawing`。

## 关键做法
1. 统一脚本生成入口：`vibe_coding/codex/_generate_deck_ideal_art.ps1`。
2. 结构层次：
   - 深色渐变底 + 放射光晕
   - 星尘/粒子点
   - 符文环（短线 + 圈）
   - 金属镶边框（多层矩形 + 线描）
   - 中央徽记（剑/水晶/魔眼/王冠/涡旋）
3. 通过固定 seed 输出可追溯素材（例：board=4201，card back=4202，card faces=4311-4315）。

## 复现提示
- 生成脚本需 ASCII-only，确保 CLI 不因非 ASCII 崩溃。
- 避免 `New-Object Type(args)` 写法，改为：
  - `[System.Drawing.Rectangle]::new(x, y, w, h)`
  - `[System.Drawing.Drawing2D.LinearGradientBrush]::new(rect, colorA, colorB, angle)`
- 输出路径直接覆盖目标资源，便于 Unity 自动导入。

## 适用范围
- DeckIdeal 卡面、卡背、桌面背景等纹理升级。
- 无法访问外部文生图服务时的临时美术增强方案。
