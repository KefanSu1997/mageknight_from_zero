# 手工编写 Unity 场景 YAML 的注意事项

## 触发背景
- 在无法启动 Unity 编辑器的环境中，需要为自动化测试提供包含新脚本的场景。

## 操作要点
- 序列化对象顺序需包含 `OcclusionCullingSettings`、`RenderSettings`、`LightmapSettings`、`NavMeshSettings` 以及 `SceneRoots`，否则 Unity 会在加载时自动重写并可能引入冲突。
- `SceneRoots.m_Roots` 必须引用根节点的 Transform 文件 ID；缺失时场景层级会丢失。
- 绑定自定义脚本时，`MonoBehaviour.m_Script` 的 GUID 必须与 `.cs.meta` 中一致，使用 `uuid4` 生成并写入可保持稳定引用。
- 使用内置资源（如 Sphere Mesh、默认材质）时，可引用 `0000000000000000e000000000000000` 等内置 GUID，避免额外依赖。

## 复盘结论
- 手工维护场景文件虽然繁琐，但在 CI 或编辑器缺失情况下可行。后续若环境恢复，建议通过 Unity 重新保存一次以规范化格式。
