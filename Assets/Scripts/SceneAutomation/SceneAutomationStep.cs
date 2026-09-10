using System;
using System.Collections.Generic;

namespace MageKnight.SceneAutomation
{
    /// <summary>
    /// 描述单次按钮交互及其执行后的等待时间与说明。
    /// </summary>
    [Serializable]
    public class SceneAutomationStep
    {
        public string buttonPath;
        public string label;

        /// <summary>
        /// Seconds to wait after the step completes; set to -1 to use the request default.
        /// </summary>
        public float waitAfterSeconds = -1f;
        // Preparation steps still record pointer hits and assertions. Failures always capture.
        public bool skipScreenshot;
        public List<SceneAutomationExpectation> before = new();
        public List<SceneAutomationExpectation> after = new();
    }

    [Serializable]
    public class SceneAutomationExpectation
    {
        public string key;
        public string expected;
        public string rule;
    }
}
