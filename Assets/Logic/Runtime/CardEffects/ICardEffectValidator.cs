namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>在支付任何卡牌费用之前验证选项；模块直接执行时也应调用相同验证。</summary>
    public interface ICardEffectValidator
    {
        void Validate(PlayerState player, ActionContext context, int option);
    }
}
