using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Interactions;
using MK.Logic.Runtime.Map;
using Monster = MK.Logic.Runtime.Map.Monster;

namespace MK.Logic.Runtime.Locations
{
    /// <summary>
    /// 城市交互实现类 - 综合了村庄、法师塔、要塞的功能
    /// </summary>
    public class CityInteraction : IInteractable
    {
        private readonly PlaceState _placeState;
        private readonly VillageInteraction _villageInteraction;
        private readonly MageTowerInteraction _mageTowerInteraction;
        private readonly KeepInteraction _keepInteraction;
        private readonly PlaceManager _placeManager;

public CityInteraction(PlaceState placeState, PlaceManager placeManager,
            VillageInteraction villageInteraction, MageTowerInteraction mageTowerInteraction,
            KeepInteraction keepInteraction)
        {
            _placeState = placeState;
            _placeManager = placeManager;
            _villageInteraction = villageInteraction;
            _mageTowerInteraction = mageTowerInteraction;
            _keepInteraction = keepInteraction;
        }

        public bool Interact(PlayerState player, InteractParameter parameter)
        {
            if (_placeState.Type != PlaceType.City) return false;

            switch (parameter.SubType)
            {
                case InteractType.VillageHeal:
                case InteractType.VillageRecruit:
                    return _villageInteraction.Interact(player, parameter);
                    
                case InteractType.MageTowerBuySpell:
                case InteractType.MageTowerBuyCrystal:
                    return _mageTowerInteraction.Interact(player, parameter);
                    
                case InteractType.KeepInteract:
                    return _keepInteraction.Interact(player, parameter);
                    
                case InteractType.KeepSiege:
                    return SiegeCity(player, parameter);
                    
                default:
                    return false;
            }
        }

        public bool CanInteract(PlayerState player)
        {
            if (_placeState.Type != PlaceType.City) return false;
            return true;
        }

        public string GetInteractionDescription()
        {
            return "城市 - 村庄功能, 法师塔功能, 要塞功能, 攻城战";
        }

        /// <summary>
        /// 城市攻城战 - 比要塞更困难的挑战
        /// </summary>
        private bool SiegeCity(PlayerState player, InteractParameter parameter)
        {
            if (!parameter.ChooseAttack) return false;

            // 城市攻城战比要塞更加困难
            var cityDefenders = GenerateCityDefenders();
            
            // 执行攻城战
            var battleInitiative = DetermineCitySiegeInitiative(player, cityDefenders);
            
            // 将运行时 Monster 转换为数据 Monster
            var enemies = ConvertToDataMonsters(cityDefenders);
            
            // 创建攻城战上下文
            var siegeContext = new ActionContext();
            var siegeBattle = BattleResolver.Resolve(player, enemies, null, null, null, 0, siegeContext);
            
            if (siegeBattle.AllKilled)
            {
                // 攻城成功：获得大量声望和战利品
                player.Reputation += 5;
                player.Fame += 10;
                
                // 征服城市
                _placeManager.ConquerPlace(_placeState);
                
                // 从城市获得多个精英单位和奖励
                var eliteUnits = GenerateEliteUnitsFromCity();
                foreach (var unitCard in eliteUnits)
                {
                    var unitState = new UnitState(unitCard);
                    player.Units.Add(unitState);
                }
                
                // 获得多个神器 TODO: 等Artifact系统完善
                var artifacts = GenerateCityLoot();
                // foreach (var artifact in artifacts)
                // {
                //     player.Artifacts.Add(artifact); // Artifact系统待完善
                // }
                
                // 一次性获得大量资源
                player.Reputation += 5;
                foreach (var color in new[] { ManaColor.Red, ManaColor.Blue, ManaColor.Green })
                {
                    player.Mana.AddCrystal(color, 2);
                }
            }
            else
            {
                // 攻城失败：承受严重损伤和声望损失
                player.Wounds += 4;
                player.Reputation -= 3;
                
                // 可能失去单位
                if (player.Units.Count > 0)
                {
                    var lostUnit = player.Units[player.Units.Count - 1];
                    player.Units.Remove(lostUnit);
                }
            }

            return true;
        }

        /// <summary>
        /// 生成城市守军 - 比要塞更强大
        /// </summary>
private List<Monster> GenerateCityDefenders()
        {
            return new List<Monster>
            {
                new Monster(MonsterType.Orc, new AxialCoord(0, 0)) { Force = 8, Hull = 6 },
                new Monster(MonsterType.Dragon, new AxialCoord(0, 0)) { Force = 6, Hull = 4 },
                new Monster(MonsterType.Gargoyle, new AxialCoord(0, 0)) { Force = 10, Hull = 3 },
                new Monster(MonsterType.Minotaur, new AxialCoord(0, 0)) { Force = 10, Hull = 6 }
            };
        }

        /// <summary>
        /// 确定城市攻城战主动权
        /// </summary>
private bool DetermineCitySiegeInitiative(PlayerState player, List<Monster> defenders)
        {
            var playerPower = player.GetTotalAttackPower() + player.GetTotalSpellPower() + player.Reputation;
            var defenderPower = 0;
            foreach (var monster in defenders)
            {
                defenderPower += monster.Force;
            }
            
            // 城市防守更加坚固，需要更强的攻击力才能获得主动权
            return playerPower >= defenderPower * 1.2;
        }

        /// <summary>
        /// 从城市生成多个精英单位
        /// </summary>
private List<UnitCard> GenerateEliteUnitsFromCity()
        {
            return new List<UnitCard>
            {
                new UnitCard("elite_city_guard", "城市精英守卫", "Elite City Guard", 3, 3, 7, RecruitLocation.City,
                    new[] { new AttackProfile(AttackType.Melee, 10, Element.Physical) },
                    new[] { Ability.Enduring, Ability.Guard }, true),
                new UnitCard("elite_city_mage", "城市法师", "City Mage", 2, 3, 5, RecruitLocation.City,
                    new[] { new AttackProfile(AttackType.Siege, 8, Element.Fire) },
                    new[] { Ability.Heal }, true)
            };
        }

        /// <summary>
        /// 将运行时 Monster 转换为数据 Monster
        /// </summary>
        private List<MK.Logic.Data.Monster> ConvertToDataMonsters(List<Monster> runtimeMonsters)
        {
            return runtimeMonsters.Select(monster => new MK.Logic.Data.Monster(
                Id: monster.Type.ToString(),
                Armor: monster.Hull,
                Attack: monster.Force,
                AttackElement: Element.Physical,
                Fame: monster.Force * 2, // 简单的 fame 计算
                Abilities: new List<Ability>(),
                MonsterType: monster.Type.ToString()
            )).ToList();
        }

        /// <summary>
        /// 生成城市战利品
        /// </summary>
        private List<SimpleArtifact> GenerateCityLoot()
        {
            return new List<SimpleArtifact>
            {
                new SimpleArtifact { Name = "城市皇冠", Fame = 5, CardText = "获得5点声望，每回合+1影响力" },
                new SimpleArtifact { Name = "城市权杖", Fame = 3, CardText = "获得3点声望，攻击力+2" },
                new SimpleArtifact { Name = "城市护甲", Fame = 4, CardText = "获得4点声望，格挡力+3" }
            };
        }
    }
}