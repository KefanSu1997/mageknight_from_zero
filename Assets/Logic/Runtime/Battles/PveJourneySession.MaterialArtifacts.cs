using System;
using System.Linq;
using System.Collections.Generic;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime.Map;

namespace MK.Logic.Runtime.Battles
{
    public sealed partial class PveJourneySession
    {
        public DeedCard PurchaseLearnedCard(AdvActionCard offered,bool enhanced)
        {
            if(!Game.HasActiveTurn || Game.IsEndingTurn || Context==null)
                throw new InvalidOperationException("请在自己的实际回合使用学习购牌");
            int index=Game.Globals.AdvActions.Offer.FindIndex(c=>ReferenceEquals(c,offered));
            if(index<0)throw new InvalidOperationException("请选择当前实际高级行动供应中的原牌");
            return CardLearningActions.Purchase(Player,Context,index,enhanced);
        }
        private void InitializeDeedSupplies(Random random)
        {
            // A custom demo loadout also owns its physical cards: don't place those unique
            // advanced actions, spells or artifacts back in the shared supply.
            var owned=Players.SelectMany(p=>p.Deck.Hand.Concat(p.Deck.DrawPile).Concat(p.Deck.DiscardPile)
                .Concat(p.Deck.PlayedCards).Concat(p.Deck.RemovedCards)
                .Concat(p.Deck.RoundAsideCards).Concat(p.Deck.TacticAsideCards)
                .Select(c=>c.Id).Concat(p.Units.Where(u=>u.Banner!=null).Select(u=>u.Banner.Id))).ToHashSet();
            T[] Shuffle<T>(IEnumerable<T> source)
            {
                var values=source.ToArray();
                for(int i=values.Length-1;i>0;i--){int j=random.Next(i+1);(values[i],values[j])=(values[j],values[i]);}
                return values;
            }
            Game.Globals.AdvActions.SetDeck(Shuffle(_cards.Where(c=>c.Set==CardSet.AdvancedAction && !string.IsNullOrWhiteSpace(c.BaseEffect) && !owned.Contains(c.Id)).Select(c=>c.Id).Distinct().Select(id=>new AdvActionCard(id))));
            Game.Globals.AdvActions.FillVacancies();
            Game.Globals.Spells.SetDeck(Shuffle((_spells??Array.Empty<MK.Logic.Data.Cards.SpellCardData>()).Where(c=>!owned.Contains(c.Id)).Select(c=>c.Id).Distinct().Select(id=>new SpellCard(id))));
            Game.Globals.Spells.FillVacancies();
            Game.Globals.Artifacts.SetDeck(Shuffle(_artifacts.Where(c=>!owned.Contains(c.Id)).Select(c=>c.Id).Distinct().Select(id=>new ArtifactCard(id))));
        }

        public BattleCardChoice[] TomeChoices(SpellCard spell,bool burn) => spell==null?Array.Empty<BattleCardChoice>():
            BattleCardChoice.For(spell.Id).Where(c=>c.Enhanced==burn && c.Allows(Context.InBattle?Battle.Phase:Phase.End)).ToArray();

        // Real hand material + actual supply identity. Context inputs are scoped to this
        // command; formal effects retain responsibility for payment and card movement.
        public void PlayMaterialArtifact(DeedCard artifact,bool burn,DeedCard material=null,
            AdvActionCard advanced=null,SpellCard spell=null,string spellChoice=null,int target=-1,
            UnitState unit=null,int first=1,int second=3,ManaColor crystal=ManaColor.Red,
            IEnumerable<AxialCoord> path=null,PveDeedSelection inputs=null)
        {
            if(!Game.HasActiveTurn || Game.IsEndingTurn || Context==null)
                throw new InvalidOperationException("请在自己的实际回合使用神器");
            string id=ArtifactEffectId(artifact);
            if(artifact==null || !PveArtifactInputs.IsMaterial(id))throw new InvalidOperationException("请选择原材料神器");
            int option=0;
            BattleCardChoice choice=null;
            if(id=="items_013")
            {
                option=burn?Game.Globals.Spells.Offer.FindIndex(c=>ReferenceEquals(c,spell)):
                    Game.Globals.AdvActions.Offer.FindIndex(c=>ReferenceEquals(c,advanced));
                if(option<0)throw new InvalidOperationException("请选择当前实际供应中的同色原牌");
            }
            else if(id=="items_023")
            {
                option=Game.Globals.Spells.Offer.FindIndex(c=>ReferenceEquals(c,spell));
                choice=TomeChoices(spell,burn).SingleOrDefault(c=>c.Id==spellChoice);
                if(option<0 || choice==null)throw new InvalidOperationException("请选择实际供应法术在当前阶段可用的原效果");
                if (!ReferenceEquals(Context.SpellSupply, Game.Globals.Spells))
                    throw new InvalidOperationException("宝典必须使用当前旅程的实际法术供应");
                inputs ??= new PveDeedSelection { TravelPath = path?.ToArray() ?? Array.Empty<AxialCoord>() };
                if ((inputs.Cards ?? Array.Empty<DeedCard>()).Any(c => ReferenceEquals(c, artifact)
                    || ReferenceEquals(c, material)))
                    throw new InvalidOperationException("宝典本体及同色支付牌不能再次用作所借法术的材料");
                unit ??= inputs.Unit;
                ValidateWorldSpell(spell.Id, choice, inputs);
                if(choice.Target!=BattleCardTarget.None && (Battle==null || target<0 || target>=Battle.Enemies.Count || Context.DefeatedEnemyIndices.Contains(target)))
                    throw new InvalidOperationException("请选择实际存活的敌人目标");
            }
            else
            {
                if(first<0 || first>3 || second<0 || second>3 || (int)crystal<0 || (int)crystal>3)
                    throw new InvalidOperationException("请选择法杖原四项效果与基本色魔晶");
                option=first|(first==1?(int)crystal<<8:0);
                if(burn)option|=second<<16|(second==1?(int)crystal<<24:0);
            }
            if(unit!=null && (!Player.Units.Contains(unit) || unit.IsDestroyed))throw new InvalidOperationException("请选择实际自有部队");
            var oldRemove=Context.CardToRemove;var oldDiscard=Context.CardToDiscard;var oldUnit=Context.TargetUnit;
            var oldMany=Context.ReadyUnitTargets;var oldPath=Context.TravelPath.ToArray();int oldOption=Context.TomeSpellOption;
            try
            {
                Context.CardToRemove=id=="items_013"?material:null;
                Context.CardToDiscard=id=="items_023" || id=="items_021"&&!burn?material:null;
                Context.TargetUnit=unit;Context.ReadyUnitTargets=unit==null?new List<UnitState>():new List<UnitState>{unit};
                Context.TravelPath.Clear();if(path!=null)Context.TravelPath.AddRange(path);
                Context.TomeSpellOption=choice?.Target==BattleCardTarget.EnemyIndex?target:choice?.Option??0;
                if (id == "items_023")
                {
                    // A schema descriptor only; the original spell stays in supply and the artifact is the physical source.
                    var descriptor = new DeedCard(spell.Id, CardType.Spell);
                    PveDeedInputs.Execute(Player, Context, descriptor, choice, inputs, selectedOption =>
                    {
                        Context.TomeSpellOption = choice.Target == BattleCardTarget.EnemyIndex ? target : selectedOption;
                        ExecuteArtifact(artifact, burn, option);
                    });
                    AttachTunnelBattle();
                }
                else ExecuteArtifact(artifact,burn,option);
            }
            finally
            {
                Context.CardToRemove=oldRemove;Context.CardToDiscard=oldDiscard;Context.TargetUnit=oldUnit;
                Context.ReadyUnitTargets=oldMany;Context.TomeSpellOption=oldOption;
                Context.TravelPath.Clear();Context.TravelPath.AddRange(oldPath);
            }
        }
    }
}
