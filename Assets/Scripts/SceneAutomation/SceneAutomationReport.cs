using System;
using System.Collections.Generic;

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 自动化执行后的结果数据，用于输出测试报告。
    /// </summary>
    [Serializable]
    public class SceneAutomationReport
    {
        public string scenePath;
        public string startedAt;
        public string finishedAt;
        public string status;
        public string message;
        public List<SceneAutomationReportStep> steps = new();
    }

    [Serializable]
    public class SceneAutomationReportStep
    {
        public string label;
        public string buttonPath;
        public string screenshotPath;
        public bool success;
        public string message;
    }
}
