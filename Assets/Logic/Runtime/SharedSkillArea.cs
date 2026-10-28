using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>An owned skill physically placed in the shared area, never a copied skill.</summary>
    public sealed class SharedSkillToken
    {
        public PlayerState Owner { get; }
        public SkillCard Skill { get; }
        public ManaColor? Color { get; internal set; }
        internal SharedSkillToken(PlayerState owner, SkillCard skill) { Owner=owner; Skill=skill; }
    }

    /// <summary>One shared area per actual game; all operations use that game's current turn context.</summary>
    public sealed partial class SharedSkillArea
    {
        public const string PainId="hero_skill_pool_000:09";
        public const string CalmId="hero_skill_pool_002:09";
        public const string NatureId="hero_skill_pool_001:09";
        public const string ManaEnhanceId="hero_skill_pool_003:09";
        private readonly Func<PlayerState,ActionContext,bool> _isActualTurn;
        private readonly List<SharedSkillToken> _tokens=new();
        public IReadOnlyList<SharedSkillToken> Tokens => _tokens.AsReadOnly();
        public SharedSkillArea(Func<PlayerState,ActionContext,bool> isActualTurn)
            => _isActualTurn=isActualTurn ?? throw new ArgumentNullException(nameof(isActualTurn));

        internal void RequireActual(PlayerState player,ActionContext context)
        {
            if(player==null || context==null || !_isActualTurn(player,context))
                throw new InvalidOperationException("共享技能只能在实际游戏的本人当前回合操作");
            RepeatedEffectResolution.RequireCompleted(context);
        }
        private void RequireUse(PlayerState player,ActionContext context,SkillCard skill,string id)
        {
            RequireActual(player,context);
            if(skill==null || skill.SourceId!=id || skill.Frequency!=SkillFrequency.OncePerRound
                || !player.Skills.Any(s=>ReferenceEquals(s,skill)) || SkillActions.IsUsed(player,skill)
                || _tokens.Any(t=>ReferenceEquals(t.Owner,player)&&ReferenceEquals(t.Skill,skill)))
                throw new InvalidOperationException("需要未使用且不在中央的本人原技能");
        }
        internal SharedSkillToken Publish(PlayerState player,SkillCard skill)
        {
            var token=new SharedSkillToken(player,skill);_tokens.Add(token);
            player.SkillsUsedThisRound.Add(skill.SourceId);
            return token;
        }
        internal void RequireClaim(PlayerState player,ActionContext context,SharedSkillToken token,string id)
        {
            RequireActual(player,context);
            if(token==null || token.Skill.SourceId!=id || !_tokens.Any(t=>ReferenceEquals(t,token)))
                throw new InvalidOperationException("必须选择本游戏中央的实际技能标记");
        }
        internal void Return(SharedSkillToken token)
        {
            _tokens.Remove(token);
            token.Owner.SkillsUsedThisRound.Add(token.Skill.SourceId);
        }
        /// <summary>Round cleanup returns central physical skills; StartRound restores their round usage.</summary>
        public void EndRound()
        {
            foreach(var token in _tokens)token.Owner.SkillsUsedThisRound.Add(token.Skill.SourceId);
            _tokens.Clear();
        }
        public void BeginTurn(PlayerState player)
        {
            foreach(var token in _tokens.Where(t=>ReferenceEquals(t.Owner,player)
                && (t.Skill.SourceId==NatureId || t.Skill.SourceId==ManaEnhanceId)).ToArray())Return(token);
        }
        public SharedSkillToken UseNature(PlayerState player,ActionContext context,SkillCard skill,Monster enemy,int attackIndex=0)
        {
            RequireUse(player,context,skill,NatureId);
            NatureRevengeActions.ValidateGrant(context,enemy,attackIndex);
            NatureRevengeActions.Grant(context,enemy,attackIndex);
            return Publish(player,skill);
        }
        public void ClaimNature(PlayerState player,ActionContext context,SharedSkillToken token,Monster enemy,int attackIndex=0)
        {
            RequireClaim(player,context,token,NatureId);
            NatureRevengeActions.ValidateGrant(context,enemy,attackIndex);
            NatureRevengeActions.Grant(context,enemy,attackIndex);Return(token);
        }
        public SharedSkillToken UsePain(PlayerState player,ActionContext context,SkillCard skill,IReadOnlyList<DeedCard> wounds)
        {
            RequireUse(player,context,skill,PainId);
            if(context.InBattle || wounds==null || wounds.Count>2
                || wounds.Any(w=>w==null || w.Type!=CardType.Wound || !player.Deck.Hand.Any(h=>ReferenceEquals(h,w)))
                || wounds.Select((w,i)=>wounds.Take(i).Any(previous=>ReferenceEquals(previous,w))).Any(duplicate=>duplicate))
                throw new InvalidOperationException("战斗外选择零至两张不同的实际手牌创伤");
            // Wounds return to the unlimited public supply, as in RegenerationSkillRules; this is not healing.
            foreach(var wound in wounds)player.Deck.Hand.RemoveAt(player.Deck.Hand.FindIndex(h=>ReferenceEquals(h,wound)));
            return Publish(player,skill);
        }
        public void ClaimPain(PlayerState player,ActionContext context,SharedSkillToken token,DeedCard wound,SidewaysType type)
        {
            RequireClaim(player,context,token,PainId);
            if(wound==null || wound.Type!=CardType.Wound || !player.Deck.Hand.Any(h=>ReferenceEquals(h,wound))
                || !Enum.IsDefined(typeof(SidewaysType),type))
                throw new InvalidOperationException("必须选择真实手牌创伤与横置用途");
            if((type==SidewaysType.Attack && (!context.InBattle || context.CombatPhase!=Phase.Melee))
                || (type==SidewaysType.Block && (!context.InBattle || context.CombatPhase!=Phase.Block))
                || (type==SidewaysType.Influence && context.InBattle)
                || (type==SidewaysType.Move && (context.InBattle || context.TurnActionCommitted || context.RestedThisTurn)))
                throw new InvalidOperationException("当前阶段没有所选横置用途的正式消费者");
            int previous=context.SidewaysWoundValue;
            try
            {
                context.SidewaysWoundValue=3;
                if(!new ActionSystem(false).PlaySideways(wound,player,context,type,3))
                    throw new InvalidOperationException("伤牌横置失败");
            }
            finally { context.SidewaysWoundValue=previous; }
            Return(token);
        }
        public SharedSkillToken UseCalm(PlayerState player,ActionContext context,SkillCard skill)
        {
            RequireUse(player,context,skill,CalmId);
            AddCalm(context,2);
            return Publish(player,skill);
        }
        public void ClaimCalm(PlayerState player,ActionContext context,SharedSkillToken token)
        {
            RequireClaim(player,context,token,CalmId);AddCalm(context,1);Return(token);
        }
        private static void AddCalm(ActionContext context,int amount)
        {
            foreach(TerrainType terrain in Enum.GetValues(typeof(TerrainType)))
                context.TerrainMoveReductionToOne[terrain]=context.TerrainMoveReductionToOne.GetValueOrDefault(terrain)+amount;
        }
    }
}
