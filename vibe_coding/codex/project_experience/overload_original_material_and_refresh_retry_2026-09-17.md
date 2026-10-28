# 效能过载原材料与刷新重试

## 已解决的问题

复制原卡不能接受任意ActionEffectId作为效果来源。效能过载必须从实际原行动定义解析ActionSystem正式映射；option留给原效果的选择。按ReferenceEquals验证手牌与永久移除，避免record值相等导致误删另一张同ID基础牌。源材料应在内层验证视图中排除，finally恢复原索引，保证非法内层材料在宿主扣费前拒绝；不能直接把陈旧、不相关的选择字段当作必需材料来拒绝所有原卡。

宿主红费、被复制卡的印刷强化费、文本额外费用是不同概念。当前固定选择路径中，前者正常支付，第二项免除，第三项每次都支付；真实魔法天赋用例验证红1/2不足、红3刚好支付且获得两张原法术。38个定向后台用例已通过。动态重复选择、材料/供给变化和许可组合仍未完成，不得把这一批通过等同于全卡完成。

## Unity6刷新与MCP重试

读取当前安装包 `Library/PackageCache/com.coplaydev.unity-mcp@8bd91ce7bd3a/Editor/Tools/RefreshUnity.cs` 后确认：

- if_dirty当前仍执行force语义；compile=request无条件请求脚本编译。
- Unity6在compile=request时故意跳过wait_for_ready等待，会提前返回idle，随后才发生程序集重载，端口6400暂时拒绝连接。
- 因此不要在每次Console重试前再次compile=request，否则会由自己的重试再次触发重载。
- 代码变动后只请求一次必要编译，观察真实Logic/Scripts/Tests DLL时间；重试调用使用refresh_unity(scope=scripts,mode=if_dirty,compile=none,wait_for_ready=true)，保留既有退避并等待。然后顺序取得Console两次error=0，再启动场景。
- 这次仅改变调用参数，未修改插件、Unity包或进程。成功恢复后端口可连续读Console并启动菜单。

场景中间步骤不能把完整operation快照原样塞进文本框。本次首轮在02案例的第一个后续步骤被ui:textFits阻止；应在PrepareNextOperation接入本卡专用摘要，并让最终独立预期成对排布。旧00/01已完成的检查点留存，补测失败02与尚未执行的03..21即可。

最终增量证据23个案例514断言均符合；额外魔力支付同时核对实际取得法术ID与留在供给的ID，不能只看数量。格挡10还必须实际对原敌人结算并消费至0。第三轮22准备状态暴露fixture只容两行，第三行被省略，修改成两行后只重跑22。一次性receipt按当前独立预期、结果、UI命中和截图校验各轮保留案例；完整报告中途失败不否定此前已经完成且逐步UI全部成功的案例，但必须保留其实际检查点和报告并明确证据来源。
