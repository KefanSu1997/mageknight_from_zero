# 飞书图片漏接与桌面黑屏诊断

22:42用户通过配对飞书询问22:41图片未收到、电脑黑屏原因。本轮暂停开发，只做诊断；没有重启系统、改显示设置、关闭应用或再次启动验收。

确认：`tools/feishu_bridge.py:incoming`仅接受text/post，image直接return None，富文本中img也未提取。不是用户未发成功：通过已有配对私聊API读取最近24小时最多50条，找到22:41:17图片1张，下载640738字节至用户运行目录incoming（不纳入Git、未上传），已查看。当前正式桥接图片自动转交尚未修复，不能宣称图片接入已通。

照片显示外接屏及笔记本屏幕发黑，外接屏保留鼠标和性能叠层。当前Unity与飞书窗口抓图均黑，UIA仍能读取窗口；只做一次切换到飞书窗口，画面未恢复。上午在打开Unity前，Hub抓图已黑，不能凭时间把原因归到后来那批场景测试；当时仅按抓图异常处理，没有及时告知用户，需要如实说明。

诊断证据：Explorer、DWM与Unity进程仍响应，约2.9GiB物理内存及11.7GiB虚拟内存空闲；Intel/NVIDIA设备ConfigManagerErrorCode=0。近12小时指定显示驱动崩溃4101、资源耗尽2004、重启与应用挂起等事件查询未找到匹配项，Unity日志未搜到device lost/removed/OutOfMemory。未找到记录不代表驱动/合成器正常。MCP get_editor_state返回上午11:55的缓存时间，不能将该状态当作晚上实时渲染证据。

当前结论限于系统显示/合成环节异常的怀疑，根因未证实；不能断言内存耗尽、VPN、Unity或具体显卡软件导致。微软官方黑屏文档建议Win+Ctrl+Shift+B重置图形驱动；电脑操作技能禁止发送Windows键组合，因此该步骤需用户手动完成，不通过脚本绕过工具限制。随后根据用户是否恢复继续定位。

资料：https://support.microsoft.com/en-us/windows/hardware/display-graphics/troubleshooting-blank-screens-in-windows
