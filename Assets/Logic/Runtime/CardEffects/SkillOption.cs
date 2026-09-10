using System;

namespace MK.Logic.Runtime.CardEffects
{
    internal static class SkillOption
    {
        internal static void Require(int option, int maximum)
        {
            if (option < 0 || option > maximum) throw new InvalidOperationException("技能选项无效");
        }
    }
}
