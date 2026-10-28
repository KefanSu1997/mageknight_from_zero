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

        /// <summary>Preview exactly the same transaction, including its skill and source color.</summary>
        public bool TryPreviewExactColors(IEnumerable<ManaColor> colors, ActionContext context,
            out Dictionary<ManaColor, int> remainingCrystals)
        {
            bool paid = Plan(colors, context, out var tokens, out var crystals, out _, out _);
            remainingCrystals = paid ? crystals : new Dictionary<ManaColor, int>(Crystals);
            return paid;
        }

        private bool Plan(IEnumerable<ManaColor> colors, ActionContext context,
            out Dictionary<ManaColor, int> tokens, out Dictionary<ManaColor, int> crystals,
            out List<ManaColor> effective, out ManaSkillPayment skill)
        {
            tokens = new Dictionary<ManaColor, int>(Tokens);
            crystals = new Dictionary<ManaColor, int>(Crystals);
            effective = new List<ManaColor>(colors);
            skill = context.ManaSkillPayment;
            if (skill != null && skill.Consumed) skill = null;
            var costs = new List<ManaColor>(effective);
            foreach (var color in costs)
                if (!System.Enum.IsDefined(typeof(ManaColor), color)) return false;
            // An empty/nonmatching original cost rejects before any effect can mutate the game.
            if (skill != null && !skill.ReplaceCost(costs, context)) return false;
            foreach (var color in costs)
            {
                if (context.InfiniteMana.Contains(color)) continue;
                if (tokens.GetValueOrDefault(color) > 0) tokens[color]--;
                else if (color.IsBasic() && (context.DayPart == DayPart.Day || context.AllowGoldAtNight || context.GoldManaWild)
                    && tokens.GetValueOrDefault(ManaColor.Gold) > 0) tokens[ManaColor.Gold]--;
                else if (crystals.GetValueOrDefault(color) > 0) crystals[color]--;
                else return false;
            }
            return true;
        }

        public bool TryPayExactColors(IEnumerable<ManaColor> colors, ActionContext context, PlayerState owner = null)
        {
            if (context.ManaSkillPayment != null && owner != null && !ReferenceEquals(owner, context.ManaSkillPayment.Owner))
                throw new System.InvalidOperationException("魔力技能付款不能转交其他玩家");
            if (!Plan(colors, context, out var tokens, out var crystals, out var effective, out var skill)) return false;
            var paid = effective.FindAll(color => !context.InfiniteMana.Contains(color));
            context.SharedSkills?.ValidateManaPayment(owner ?? skill?.Owner, context, paid);
            // All resources, source quota, material identity and skill uses were checked before this commit.
            skill?.Commit(context);
            foreach (var pair in Crystals)
            {
                int spent = pair.Value - crystals.GetValueOrDefault(pair.Key);
                if (spent > 0) context.SpentCrystals[pair.Key] = context.SpentCrystals.GetValueOrDefault(pair.Key) + spent;
            }
            Tokens.Clear();
            foreach (var pair in tokens) if (pair.Value > 0) Tokens[pair.Key] = pair.Value;
            Crystals.Clear();
            foreach (var pair in crystals) Crystals[pair.Key] = pair.Value;
            foreach (var color in effective)
                if (!context.InfiniteMana.Contains(color)) NotifyManaSpent(owner ?? skill?.Owner, color, 1);
            context.SharedSkills?.OnManaPaid(owner ?? skill?.Owner, context, paid);
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
                if (crystalHave < need) return false;
                Crystals[color] = crystalHave - need;
                NotifyManaSpent(owner, color, pair.Value);
            }
            return true;
        }

        private static void NotifyManaSpent(PlayerState owner, ManaColor color, int amount)
        {
            if (amount <= 0 || owner == null || owner.ManaCurseColor != color || owner.ManaCurseTriggered)
                return;
            owner.Wounds += 1;
            owner.ManaCurseTriggered = true;
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
