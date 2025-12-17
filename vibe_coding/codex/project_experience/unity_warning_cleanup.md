# Unity 2023 警告清理要点

## `FindObjectOfType` → `FindFirstObjectByType`
- Unity 2023.1 起标记 `Object.FindObjectOfType` / `FindObjectsOfType` 为过时，需要改用 `Object.FindFirstObjectByType` 与 `Object.FindObjectsByType`。
- 注意 `FindObjectOfType(true)` 以前会包含未激活对象，迁移时要显式传入 `FindObjectsInactive.Include`。
- `FindObjectsByType` 需要指定排序模式；若不关心顺序，传 `FindObjectsSortMode.None` 可避免多余开销。

示例：
```csharp
var canvas = Object.FindFirstObjectByType<Canvas>();
var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None, FindObjectsInactive.Include);
```

## TMP `enableWordWrapping` 替代
- TextMeshPro 3.x 弃用了 `enableWordWrapping`，需改用 `textWrappingMode`。
- 映射关系：
  - `enableWordWrapping = true` → `textWrappingMode = TextWrappingModes.Normal`
  - `enableWordWrapping = false` → `textWrappingMode = TextWrappingModes.NoWrap`

## 非空字段警告（CS8618）
- 对于 Inspector 赋值的引用，可用 `= null!;` 或 `[field: SerializeField] public ... { get; private set; } = null!;` 取消编译器警告。
- 若脚本运行时会动态查找引用，应在 Awake/Start 提前赋值，并保留校验日志。

## 自动化建议
1. 批量替换前先确认 Unity 版本 ≥ 2022.2，以确保新 API 可用。
2. 修改完运行 `dotnet build` + Unity 编译日志，确认 `warning` 卖掉；必要时通过 MCP `read_console(types=[\"warning\"])` 检查。
3. 将常用替换封装成工具方法（如 `SceneObjectLocator.FindFirst<T>()`），减少重复样板。
