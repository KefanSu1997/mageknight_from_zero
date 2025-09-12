using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Interactions
{
    /// <summary>
    /// 交互参数类, 用于传递交互的具体信息
    /// </summary>
    public class InteractParameter
    {
        /// <summary>
        /// 交互类型
        /// </summary>
        public InteractType Type { get; set; }

        /// <summary>
        /// 支付的影响力点数（如治疗、招募等）
        /// </summary>
        public int Influence { get; set; }

        /// <summary>
        /// 用于支付的法术类型
        /// </summary>
        public CardType? SpellType { get; set; }

        /// <summary>
        /// 选择的单位（用于招募、治疗等）
        /// </summary>
        public UnitCard? SelectedUnit { get; set; }

        /// <summary>
        /// 支付的水晶颜色（用于购买、献祭等）
        
        /// </summary>
        public ManaColor? ManaColor { get; set; }

        /// <summary>
        /// 选择的法术
        /// </summary>
        public SpellCard? SelectedSpell { get; set; }

        /// <summary>
        /// 是否选择攻击（用于攻城战）
        /// </summary>
        public bool ChooseAttack { get; set; }

            /// <summary>
        /// 子类型交互参数
        /// </summary>
        public InteractType? SubType { get; set; }
    /// <summary>
        /// 是否选择祭坛献祭（用于遗迹）
        /// </summary>
        public bool UseAltar { get; set; }

        public static InteractParameter CreateVillageHealing() => new() { Type = InteractType.VillageHeal };
        public static InteractParameter CreateVillageRecruit() => new() { Type = InteractType.VillageRecruit };
        public static InteractParameter CreateVillagePlunder() => new() { Type = InteractType.VillagePlunder };
        public static InteractParameter CreateMonasteryHeal() => new() { Type = InteractType.MonasteryHeal, Influence = 6 };
        public static InteractParameter CreateMonasteryLearnSkill() => new() { Type = InteractType.MonasteryLearnSkill };
        public static InteractParameter CreateMageTowerBuySpell() => new() { Type = InteractType.MageTowerBuySpell };
        public static InteractParameter CreateMageTowerBuyCrystal() => new() { Type = InteractType.MageTowerBuyCrystal, Influence = 2 };
        public static InteractParameter CreateKeepInteract() => new() { Type = InteractType.KeepInteract };
        public static InteractParameter CreateKeepSiege(bool attack) => new() { Type = InteractType.KeepSiege, ChooseAttack = attack };
        public static InteractParameter CreateCityInteract(InteractType subType) => new() { Type = InteractType.CityInteraction, SubType = subType };
    }
}