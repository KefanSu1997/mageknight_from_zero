using MK.Logic.Core;

namespace MK.Logic.Runtime.CardEffects
{
    /// <summary>Additional selected mana is paid atomically together with the printed casting cost.</summary>
    public interface ICardAdditionalManaCost
    {
        ManaColor[] GetAdditionalManaCost(int option);
    }
}
