using MageKnight.SceneAutomation;
using UnityEditor;

namespace MageKnight.SceneAutomation.Editor
{
    public static class RecurrentPpoUiMenus
    {
        [MenuItem("Tools/Mage Knight/Battle/Run PPO UI Bridge")]
        public static void RunBridge()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/rl_ui_bridge_acceptance.json");
        }

        [MenuItem("Tools/Mage Knight/Battle/Run PPO UI Phase Reset")]
        public static void RunPhaseReset()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/rl_ui_phase_reset_acceptance.json");
        }

        [MenuItem("Tools/Mage Knight/Battle/Run PPO Original Opening")]
        public static void RunOriginalOpening()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/rl_ui_original_opening.json");
        }

        [MenuItem("Tools/Mage Knight/Battle/Run Generated PPO Semantics")]
        public static void RunGeneratedSemantics()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/rl_ui_generated_semantics.json");
        }

        [MenuItem("Tools/Mage Knight/Battle/Run PPO Semantic Attack")]
        public static void RunSemanticAttack()
        {
            SceneAutomationCommand.RunFromProjectRelativeConfig("AutomationConfigs/rl_ui_semantic_attack.json");
        }
    }
}
