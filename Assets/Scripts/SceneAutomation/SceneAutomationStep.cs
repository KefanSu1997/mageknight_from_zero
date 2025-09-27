using System;

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
        public float waitAfterSeconds = 1f;
    }
}
