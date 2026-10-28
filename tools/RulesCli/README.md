# 后台状态先行，再做场景验收

从项目根目录运行（本机已安装 .NET 8）：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
dotnet run --project tools/RulesCli -- . MK.Tests.Cards.RitualAttackStateTests
```

必须给出完整测试类或方法名；可给多个名字。参数化测试可传XML报告里的精确 fullname，仅补测失败项。未指定/未知/空筛选均失败，不默认跑全库。NUnit失败、跳过、不确定结果返回非零退出码；报告在 AutomationOutputs/RulesState 的独立目录。

直接编译 Assets/Logic 正式源码，复用 Assets/Tests/RulesState 中Unity也会发现的同一份NUnit测试。没有UnityEngine依赖，不启动Unity.exe，不使用batchmode，不修改生成的Unity csproj。依赖仅引用已有PackageCache的Newtonsoft和NUnit DLL，NuGet来源与Targeting Pack下载关闭；缓存不存在则明确失败，不能自动下载。

第一阶段核对真实牌区、费用、效果、目标、抗性、击杀等后台状态，失败在此修复。第二阶段仅在相应后台范围通过后，执行实际Unity按钮、原卡来源、显示数值及结果验收；挑选能覆盖不同交互路径的场景案例，避免将所有后台参数排列都重复截图。修改共享规则时按影响范围选回归，仍不得用代表性场景掩盖未验证规则。

报告的 `scope` 明确仅为后台验证，保留测试筛选、时间、源码/原JSON指纹和NUnit XML。新增测试放在RulesState，不引用Unity API；需要Unity资源加载的测试继续用已有EditMode入口，不用假资源替代。
