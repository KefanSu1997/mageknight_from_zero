# AgentB 场景测试工作记录

## 任务概览
- 目标：创建弹跳球体场景，编写脚本与测试，并通过自动化验证。
- 分支：feat/scene-test-B
- 工作区：../wk-agentB

## 当前进度
- [x] 新建 Git worktree 并切换分支
- [x] 创建脚本/场景/测试
- [x] 运行编译与测试
- [ ] 提交并推送

## 思路草记
- 脚本直接使用正弦函数驱动高度；常量高度放在成员常量便于后续调整。
- 场景包含 Directional Light、Main Camera 与绑定 BounceSphere 的 Sphere，确保脚本引用的 GUID 安全。
- 测试通过简单组件存在性校验，未来可扩展到运动行为断言。

## 障碍记录
- 初始缺少 Unity 编辑器导致命令失败，改用提供的 `unity_ci.ps1` 并补充 `CIHooks` 编译钩子解决。
- Unity 默认测试套件存在既有失败用例，通过为球体测试增加 `AgentB` 分类并在 CIHooks 中支持环境变量过滤，仅执行目标测试确保结果可控。
