# URP 可选化摄像机数据处理

## 背景
- 问题：`Part1SceneHarness` 直接引用 `UnityEngine.Rendering.Universal`，在项目未安装 Universal RP 包时触发 CS0234 编译错误。
- 目标：保留现有功能（复制主摄像机的后处理配置），同时在缺失 URP 包时仍能编译并运行。

## 解决方案
1. **移除编译期依赖**：删除 `using UnityEngine.Rendering.Universal`，避免脚本编译阶段查找缺失命名空间。
2. **反射获取组件**：使用 `Type.GetType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime")` 并在 `AppDomain.CurrentDomain.GetAssemblies()` 中兜底查找，动态判定 URP 组件是否存在。
3. **安全复制配置**：通过 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic` 反射复制 `renderPostProcessing`、`antialiasing`、`requiresColorTexture` 等属性；若缺少来源组件，则至少启用 `renderPostProcessing` 作为默认值。
4. **缓存类型**：使用静态字段缓存查找结果，避免每次构建 UI 时重复扫描所有程序集。
5. **编译验证**：脚本改动后执行一次 `Assets/Refresh`，确保 Unity 重新编译并清空旧的错误记录。

## 验证与收益
- Unity Console 返回 0 条编译错误，运行时在 URP 缺失/存在两种环境下皆可正常创建测试 UI 摄像机。
- 将来如需恢复 URP 特性，只需安装包即可触发相同反射路径，无需再次修改代码。

## 后续建议
- 若后续需要适配 HDRP，可复用同样的反射模式，按需复制 HDRP 摄像机扩展组件的配置。
- 可以在自动化回归脚本中新增一次无 URP 包的编译检查，以防后续代码再次引入硬依赖。
