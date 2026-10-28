using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public string ArtifactEffectId(DeedCard card)=>card?.Id=="items_024" && Context?.MysticBox!=null && ReferenceEquals(Context.MysticBox.Box,card)
            ? Context.MysticBox.RevealedArtifact.Id:card?.Id;
        private void RequireArtifactTurn()
        {
            if(!Game.HasActiveTurn || Game.IsEndingTurn || Context==null)throw new InvalidOperationException("请在本人的实际回合操作神器");
        }
        private void ExecuteArtifact(DeedCard physical,bool burn,int option)
        {
            if(physical.Id=="items_024")CardPlayActions.UseMysticBox(Player,Context,physical,burn,option);
            else CardPlayActions.PlayArtifact(Player,Context,physical,_artifacts.Single(a=>a.Id==physical.Id),burn,option);
        }
        public void RevealBox(DeedCard card)
        {
            RequireArtifactTurn();if(card?.Id!="items_024")throw new InvalidOperationException("请选择实际神秘宝盒");
            CardPlayActions.PlayArtifact(Player,Context,card,_artifacts.Single(a=>a.Id==card.Id));
        }
        public BattleSkillChoice[] HeadbandChoices(SkillCard skill)=>skill==null?Array.Empty<BattleSkillChoice>():
            BattleSkillChoice.For(skill.SourceId).Where(c=>c.Phases.Contains(Context.InBattle?Battle.Phase:Phase.End)).ToArray();
        public void PlaySpecialArtifact(DeedCard card,bool burn,UnitCard offer=null,UnitState disband=null,
            SkillCard skill=null,int skillOption=0,int enemy=-1,UnitState unit=null,params ManaColor[] colors)
        {
            RequireArtifactTurn();string id=ArtifactEffectId(card);int option=0;
            if(id=="items_017" && burn)
            {
                option=Game.Globals.Units.Offer.FindIndex(c=>ReferenceEquals(c,offer));
                if(option<0)throw new InvalidOperationException("请选择实际部队供应");
            }
            else if(id=="items_022")
            {
                option=Game.Globals.Skills.Offer.FindIndex(c=>ReferenceEquals(c,skill));
                if(option<0)throw new InvalidOperationException("请选择实际公共技能供应");
                if(!burn && !HeadbandChoices(skill).Any(c=>c.Option==skillOption))throw new InvalidOperationException("该技能当前阶段尚无合法输入分支");
                if(!burn && skill.SourceId=="hero_skill_pool_007:04")
                {
                    if(Battle==null || enemy<0 || enemy>=Battle.Enemies.Count || Context.DefeatedEnemyIndices.Contains(enemy))throw new InvalidOperationException("请选择存活的实际敌人");
                    skillOption=enemy;
                }
            }
            else if(PveArtifactInputs.IsResource(id))option=PveArtifactInputs.ColorOption(id,burn,colors);
            else if(PveArtifactInputs.IsAttachedBanner(id) || id=="items_012")
            {
                if(id=="items_012" && Context.InBattle)throw new InvalidOperationException("请在战斗外使用原圣杯治疗");
                if(!PveArtifactInputs.OutsideAllowed(id,burn) && !Context.InBattle)throw new InvalidOperationException("此选择需要对应战斗阶段");
                if(id=="items_001" && burn)throw new InvalidOperationException("宝盒借恐惧旌旗焚毁的多目标输入尚未接入");
                if((id=="items_003" || id=="items_012" || id=="items_018") && burn && Context.InBattle)throw new InvalidOperationException("请在战斗外使用原恢复效果");
                if(!burn && PveArtifactInputs.IsAttachedBanner(id) && (unit==null || !Player.Units.Contains(unit) || unit.IsDestroyed))throw new InvalidOperationException("请选择实际自有部队附旗");
            }
            else if(id!="items_017")throw new InvalidOperationException("所揭示神器的此输入请使用对应材料入口；武器专用选择尚待接入");
            var oldDisband=Context.UnitToDisband;var oldUnit=Context.TargetUnit;int oldSkill=Context.HeadbandSkillOption;
            try
            {
                Context.UnitToDisband=disband;Context.TargetUnit=unit;Context.HeadbandSkillOption=skillOption;
                ExecuteArtifact(card,burn,option);
            }
            finally {Context.UnitToDisband=oldDisband;Context.TargetUnit=oldUnit;Context.HeadbandSkillOption=oldSkill;}
        }
        public void UseHeadbandFlip(DeedCard card, SkillCard skill, int option)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseFlipSkill(Player, Context, card, skill, option);
        }

        public void UseHeadbandSocial(DeedCard card, SkillCard skill, int option)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseSocial(Player, Context, card, skill, option);
        }

        public void UseHeadbandSideways(DeedCard card, SkillCard skill, DeedCard material, SidewaysType type,
            ManaColor? color = null, bool forgoSourceDice = false)
        {
            RequireArtifactTurn();
            if (Context.InBattle)
            {
                if (Battle == null || (Battle.Phase != Phase.Block && Battle.Phase != Phase.Melee)
                    || type != (Battle.Phase == Phase.Block ? SidewaysType.Block : SidewaysType.Attack))
                    throw new InvalidOperationException("请在当前格挡或近战阶段选择相应横置用途");
            }
            else
            {
                if (type != SidewaysType.Move && type != SidewaysType.Influence)
                    throw new InvalidOperationException("战斗外请选择移动或影响力");
                if (type == SidewaysType.Move && Context.TurnActionCommitted)
                    throw new InvalidOperationException("本回合行动已开始，不能再产生移动力");
            }
            HeadbandBorrowResolution.UseSideways(Player, Context, card, skill, material, type, color, forgoSourceDice);
        }
        public void UseHeadbandBullseye(DeedCard card, SkillCard skill, AttackProfile attack)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseBullseye(Player, Context, card, skill, attack);
        }
        public void UseHeadbandWound(DeedCard card, SkillCard skill, DeedCard wound, ManaColor? color = null)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseWound(Player, Context, card, skill, wound, color);
        }
        public void UseHeadbandPain(DeedCard card, SkillCard skill, DeedCard wound, SidewaysType type)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UsePain(Player, Context, card, skill, wound, type);
        }
        public void UseHeadbandUnit(DeedCard card, SkillCard skill, UnitState unit, bool heal)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseUnit(Player, Context, card, skill, unit, heal);
        }
        public void UseHeadbandLeadership(DeedCard card, SkillCard skill, UnitState unit, int abilityIndex,
            int option = 0, int target = -1, ManaColor[] manaColors = null, int boost = 0, int attackIndex = 0)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseLeadership(Player, Context, card, skill, unit, abilityIndex,
                option, target, manaColors, boost, attackIndex);
        }
        public void FinishHeadbandBorrowIfImpossible()
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.FinishIfImpossible(Player, Context);
        }
        public void UseHeadbandShapeshift(DeedCard card, SkillCard skill, DeedCard input, bool enhanced,
            int option, FixedOutputKind target, DeedCard material = null, Map.AxialCoord? hex = null, TerrainType? terrain = null)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseShapeshift(Player, Context, card, skill, input, enhanced, option, target, material, hex, terrain);
        }
        public void UseHeadbandForkedLightning(DeedCard card, SkillCard skill, System.Collections.Generic.IReadOnlyList<Monster> targets)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UseForkedLightning(Player, Context, card, skill, targets);
        }
        public void UseHeadbandPuppet(DeedCard card, SkillCard skill, Monster enemy, int mode)
        {
            RequireArtifactTurn();
            HeadbandBorrowResolution.UsePuppet(Player, Context, card, skill, enemy, mode);
        }
        public void UseHeadbandDuel(DeedCard card, SkillCard skill, Monster enemy, int attackIndex = 0)
        {
            RequireArtifactTurn();
            if (Battle == null || Battle.Phase != Phase.Block || !Context.InBattle
                || !Battle.Enemies.Any(e => ReferenceEquals(e, enemy)))
                throw new InvalidOperationException("请选择当前战斗格挡阶段的实际敌人");
            int index = Enumerable.Range(0, Battle.Enemies.Count).First(i => ReferenceEquals(Battle.Enemies[i], enemy));
            if (Battle.IsBlocked(index, attackIndex))
                throw new InvalidOperationException("该攻击已完成格挡安排，不能再借用决斗");
            HeadbandBorrowResolution.UseDuel(Player, Context, card, skill, enemy, attackIndex);
        }

        public void UseHeadbandFlight(DeedCard card, SkillCard skill, Map.AxialCoord target)
        {
            RequireOutsideBattle();
            HeadbandBorrowResolution.UseFlight(Player, Context, card, skill, target, Context.OtherPlayers);
        }
        public DeedCard[] AvailableCommandBanners()=>Player.Deck.PlayedCards.Where(c=>BannerActions.CommandAvailable(Player,Context,c)).ToArray();
        public void SelectCommandBanner(DeedCard card){RequireArtifactTurn();BannerActions.SelectCommandForRecruit(Player,Context,card);}
        public void AssignCommandBanner(DeedCard card,UnitState unit){RequireArtifactTurn();BannerActions.AssignCommand(Player,Context,unit,card);}
    }
}
