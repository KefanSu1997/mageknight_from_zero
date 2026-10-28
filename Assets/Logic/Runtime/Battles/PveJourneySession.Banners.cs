using System;
using System.Linq;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public UnitState[] RoundBannerUnits(PlayerState owner)
            => !Game.CanChooseRoundBannerReturns || owner == null || !Players.Any(p => ReferenceEquals(p, owner))
                ? Array.Empty<UnitState>()
                : owner.Units.Where(u => !u.IsDestroyed && u.Banner != null && ReferenceEquals(u.BannerOwner, owner)).ToArray();

        public bool IsBannerReturning(PlayerState owner, UnitState unit)
            => RoundBannerUnits(owner).Any(u => ReferenceEquals(u, unit)) && owner.BannersReturningAtRoundEnd.Contains(unit);

        public void ChooseBannerRoundReturn(PlayerState owner, UnitState unit, bool returnBanner)
        {
            if (!Game.CanChooseRoundBannerReturns)
                throw new InvalidOperationException("请在所有玩家最后回合结束后、正式轮末清理前选择附旗去留");
            if (!RoundBannerUnits(owner).Any(u => ReferenceEquals(u, unit)))
                throw new InvalidOperationException("请选择本局指定玩家实际拥有部队上的原附旗");
            BannerActions.ChooseRoundReturn(owner, unit, returnBanner);
        }
    }
}
