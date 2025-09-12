using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 管理玩家法力的容器，包括永久的晶體與當回合可用的法力標記。
    /// </summary>
    public sealed class ManaPool
    {
        /// <summary>永久保留的晶體池。</summary>
        public Dictionary<ManaColor, int> Crystals { get; } = new();

        /// <summary>僅本回合可用的法力標記。</summary>
        public Dictionary<ManaColor, int> Tokens { get; } = new();

        /// <summary>
        /// 嘗試支付指定成本。法力標記會優先消耗，
        /// 若不足則使用晶體。資源不足時返回 false。
        /// </summary>
        public bool Pay(ManaCost cost, PlayerState? owner = null)
        {
            foreach (var pair in cost.Need)
            {
                var color = Map(pair.Key);
                int need = pair.Value;
                Tokens.TryGetValue(color, out int tokenHave);
                int useToken = System.Math.Min(tokenHave, need);
                tokenHave -= useToken;
                need -= useToken;
                if (tokenHave == 0) Tokens.Remove(color); else Tokens[color] = tokenHave;

                Crystals.TryGetValue(color, out int crystalHave);
                if (owner != null && owner.ManaCurseColor == color && !owner.ManaCurseTriggered && need > 0)
                {
                    owner.Wounds += 1;
                    owner.ManaCurseTriggered = true;
                }
                if (crystalHave < need) return false;
                Crystals[color] = crystalHave - need;
            }
            return true;
        }

        /// <summary>向法力標記池新增指定顏色。</summary>
        public void AddToken(ManaColor e, int n = 1)
        {
            Tokens[e] = Tokens.GetValueOrDefault(e) + n;
        }

        /// <summary>向晶體池新增指定顏色。</summary>
        public void AddCrystal(ManaColor e, int n = 1)
        {
            Crystals[e] = Crystals.GetValueOrDefault(e) + n;
        }

        /// <summary>結束回合時清空所有法力標記。</summary>
        public void ResetTokens() => Tokens.Clear();

        public int GetTotal()
        {
            int total = 0;
            foreach (var count in Crystals.Values)
                total += count;
            foreach (var count in Tokens.Values)
                total += count;
            return total;
        }

        private static ManaColor Map(Element e) => e.ToColor();
    }
}
