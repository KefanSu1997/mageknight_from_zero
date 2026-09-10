using System.Collections.Generic;

namespace MageKnight.SceneAutomation
{
    public interface ISceneAutomationStateSource
    {
        Dictionary<string, string> ReadAutomationState();
        string LastRule { get; }
    }
}
