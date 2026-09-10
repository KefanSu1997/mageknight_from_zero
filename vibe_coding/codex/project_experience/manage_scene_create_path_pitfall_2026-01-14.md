# manage_scene 创建场景路径踩坑与规避（2026-01-14）

## 现象

- Unity 弹窗提示 “Moving file failed”，尝试将 `Temp/UnityTempFile-...` 移动到 `Assets/Scenes/.../Part1_ElementCardsShowcase.unity` 失败。
- 项目中出现 `Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity/Part1_ElementCardsShowcase.unity` 这样的结构（.unity 被创建成目录）。

## 根因

使用 unity-mcp 的 `manage_scene` 创建场景时，把带 `.unity` 扩展名的完整路径传给 `path`，工具将其当作目录创建，导致 `.unity` 成为文件夹，后续 Unity 无法把临时场景文件移动到正确位置。

## 规避方式（必须遵守）

- `manage_scene` 创建场景时：
  - `path` 只传目录：`Assets/Scenes/Part1`
  - `name` 传场景名且不带 `.unity`：`Part1_ElementCardsShowcase`
- 不要在 `path` 中包含 `.unity`。

## 处理步骤（已发生时）

1. 关闭 Unity Editor（释放文件锁）。
2. 删除误建的目录和 meta：
   - `Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity`
   - `Assets/Scenes/Part1/Part1_ElementCardsShowcase.unity.meta`
3. 用 Unity Editor 的 “Save Scene As...” 正常生成 `.unity` 文件，或按正确参数调用 `manage_scene create` 重新生成。
