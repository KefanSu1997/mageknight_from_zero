using System;
using System.Collections.Generic;

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 承载运行指定场景并自动执行按钮交互所需的配置信息。
    /// </summary>
    [Serializable]
    public class SceneAutomationRequest
    {
        public string scenePath;
        public string screenshotsDirectory;
        public bool useRunSubfolder = true;
        public string reportPath;
        public bool captureInitialView = true;
        public float initialDelaySeconds = 2f;
        public float defaultWaitAfterSeconds = 1f;
        public float maxRunSeconds = 120f;
        public List<SceneAutomationStep> steps = new();
    }
}
