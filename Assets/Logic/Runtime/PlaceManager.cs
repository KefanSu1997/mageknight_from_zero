using System.Collections.Generic;
using System.Linq;
using MK.Logic.Data;
using MK.Logic.Core;
using Monster = MK.Logic.Data.Monster;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 管理特殊地點翻面與行為的服務，包含地点交互功能。
    /// </summary>
    public sealed class PlaceManager
    {
        private readonly List<Monster> _monsters;
        private readonly GlobalResources _global;

        public PlaceManager(List<Monster> monsters, GlobalResources global)
        {
            _monsters = monsters;
            _global = global;
        }

        /// <summary>
        /// 翻開指定類型的地點，若需放置敵人或牌張則在此執行。
        /// </summary>
        public PlaceState Flip(PlaceType type, ManaColor[]? colors = null, RuinTokenType? token = null)
        {
            PlaceState state;
            state = new PlaceState { Type = type };
            switch (type)
            {
                case PlaceType.OrcRampaging:
                    state = new PlaceState
                    {
                        Type = type,
                        Enemy = _monsters.First(m => m.MonsterType == "Orc")
                    };
                    break;
                case PlaceType.Dragon:
                    state = new PlaceState
                    {
                        Type = type,
                        Enemy = _monsters.First(m => m.MonsterType == "Draconum")
                    };
                    break;
                case PlaceType.Monastery:
                    if (_global.AdvActions.TryDraw(out var card))
                        _global.MonasteryOffer.Add(card);
                    break;
                case PlaceType.Village:
                    break;
                case PlaceType.CityWall:
                    break;
                case PlaceType.ManaMine:
                    state = new PlaceState
                    {
                        Type = type,
                        MineColors = colors ?? new[] { ManaColor.Red }
                    };
                    break;
                case PlaceType.DeepMine:
                    state = new PlaceState
                    {
                        Type = type,
                        MineColors = colors ?? new[] { ManaColor.Red, ManaColor.Blue }
                    };
                    break;
                case PlaceType.AncientRuin:
                    state = new PlaceState
                    {
                        Type = type,
                        RuinToken = token ?? RuinTokenType.Altar,
                        Enemy = (token ?? RuinTokenType.Altar) == RuinTokenType.Enemy ? _monsters.First() : null
                    };
                    break;
                case PlaceType.Dungeon:
                    state = new PlaceState { Type = type };
                    break;
            }
            return state;
        }

        /// <summary>
        /// 焚毀修道院的行動，勝利則獲得一件神器。
        /// </summary>
        public ArtifactCard? BurnMonastery(PlayerState player, bool win)
        {
            player.Reputation -= 3;
            if (!win) return null;
            return _global.Artifacts.Draw();
        }

        /// <summary>
        /// 進入地下城並與棕色敵人戰鬥，勝利時依骰色獲得獎勵。
        /// </summary>
        public (SpellCard?, ArtifactCard?) ExploreDungeon(
            PlaceState state,
            PlayerState player,
            bool win,
            ManaColor die)
        {
            if (state.Type != PlaceType.Dungeon)
                return (null, null);

            state.Enemy = _monsters.First();
            state.Enemy = null; // 戰鬥後標記移除

            if (!win) return (null, null);
            if (state.DungeonConquered)
                return (null, null);

            state.DungeonConquered = true;
            if (die is ManaColor.Gold or ManaColor.Black)
            {
                var sp = _global.Spells.Claim(0);
                return (sp, null);
            }
            else
            {
                var art = _global.Artifacts.Draw();
                return (null, art);
            }
        }

        /// <summary>
        /// 检查地点是否是法师塔位置
        /// </summary>
        public bool IsMageTowerLocation(PlaceState placeState)
        {
            // 魔法矿井被视为法师塔位置
            return placeState.Type == PlaceType.ManaMine || placeState.Type == PlaceType.DeepMine;
        }

        /// <summary>
        /// 征服地点
        /// </summary>
        public void ConquerPlace(PlaceState placeState)
        {
            // 根据地点类型设置征服状态
            switch (placeState.Type)
            {
                case PlaceType.AncientRuin:
                    placeState.RuinConquered = true;
                    break;
                case PlaceType.Dungeon:
                    placeState.DungeonConquered = true;
                    break;
                default:
                    // 其他地点通过清除敌人来表示征服
                    if (placeState.Enemy != null)
                    {
                        placeState.Enemy = null;
                    }
                    break;
            }
        }

        /// <summary>
        /// 触发守卫战斗
        /// </summary>
        public LocationBattleResult TriggerGuardBattle(PlaceState placeState, PlayerState player)
        {
            // 生成守卫敌人
            var guard = new MK.Logic.Runtime.Map.Monster(MK.Logic.Runtime.Map.MonsterType.Orc, new MK.Logic.Runtime.Map.AxialCoord(0, 0)) { Force = 4, Hull = 6 };
            
            // 执行战斗
            var battleResult = ResolveGuardBattle(player, guard);
            
            return battleResult;
        }

        /// <summary>
        /// 解析守卫战斗
        /// </summary>
        private LocationBattleResult ResolveGuardBattle(PlayerState player, MK.Logic.Runtime.Map.Monster guard)
        {
            // 简化的战斗逻辑
            var playerPower = player.GetTotalAttackPower();
            var guardPower = guard.Force;
            
            var success = playerPower >= guardPower;
            
            if (success)
            {
                // 胜利：玩家获得声望
                player.Reputation += 1;
            }
            else
            {
                // 失败：玩家受伤
                player.Wounds += 2;
            }
            
            return new LocationBattleResult
            {
                success = success,
                reputationChange = success ? 1 : 0,
                woundsInflicted = success ? 0 : 2
            };
        }
    }

    /// <summary>
    /// 战斗结果
    /// </summary>
    public class LocationBattleResult
    {
        public bool success;
        public int reputationChange;
        public int woundsInflicted;
    }
}
