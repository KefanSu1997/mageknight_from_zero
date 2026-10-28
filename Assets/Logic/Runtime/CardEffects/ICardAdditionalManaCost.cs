using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Additional selected mana is paid atomically together with the printed casting cost.</summary>
    public interface ICardAdditionalManaCost
    {
        ManaColor[] GetAdditionalManaCost(int option);
    }

    /// <summary>Costs selected from a real offer depend on the current action context.</summary>
    public interface ICardContextualManaCost
    {
        ManaColor[] GetAdditionalManaCost(PlayerState player, ActionContext context, int option);
    }
}
