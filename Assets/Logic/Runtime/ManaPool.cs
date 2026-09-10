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
        /// Pay already-selected mana colors atomically, tokens before crystals.
        /// Wild source dice must first be converted to the selected color by the source flow.
        /// </summary>
        public bool TryPayExactColors(IEnumerable<ManaColor> colors, ActionContext context, PlayerState owner = null)
        {
            var needs = new Dictionary<ManaColor, int>();
            foreach (var color in colors)
            {
                if (!System.Enum.IsDefined(typeof(ManaColor), color)) return false;
                if (!context.InfiniteMana.Contains(color))
                    needs[color] = needs.GetValueOrDefault(color) + 1;
            }
            foreach (var need in needs)
                if (Tokens.GetValueOrDefault(need.Key) + Crystals.GetValueOrDefault(need.Key) < need.Value)
                    return false;

            // Nothing changes until every color, including repeated costs, is affordable.
            foreach (var need in needs)
            {
                int tokens = System.Math.Min(Tokens.GetValueOrDefault(need.Key), need.Value);
                int crystals = need.Value - tokens;
                if (tokens > 0)
                {
                    Tokens[need.Key] -= tokens;
                    if (Tokens[need.Key] == 0) Tokens.Remove(need.Key);
                }
                if (crystals == 0) continue;
                Crystals[need.Key] -= crystals;
                context.SpentCrystals[need.Key] = context.SpentCrystals.GetValueOrDefault(need.Key) + crystals;
                if (owner != null && owner.ManaCurseColor == need.Key && !owner.ManaCurseTriggered)
                {
                    owner.Wounds++;
                    owner.ManaCurseTriggered = true;
                }
            }
            return true;
        }

        /// <summary>
        /// 嘗試支付指定成本。法力標記會優先消耗，
        /// 若不足則使用晶體。資源不足時返回 false。
        /// </summary>
        public bool Pay(ManaCost cost, PlayerState? owner = null)
        {
            // 先验证整笔支付，失败时所有颜色和诅咒状态保持不变。
            foreach (var pair in cost.Need)
            {
                var color = Map(pair.Key);
                if (pair.Value < 0 || Tokens.GetValueOrDefault(color) + Crystals.GetValueOrDefault(color) < pair.Value)
                    return false;
            }
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
            if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n));
            if (e < ManaColor.Red || e > ManaColor.White)
                throw new System.InvalidOperationException("只有红、蓝、绿、白色可以成为魔晶");
            int stored = System.Math.Min(n, System.Math.Max(0, 3 - Crystals.GetValueOrDefault(e)));
            if (stored > 0) Crystals[e] = Crystals.GetValueOrDefault(e) + stored;
            if (n > stored) AddToken(e, n - stored);
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
