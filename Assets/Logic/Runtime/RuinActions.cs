using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Persistent original enemy-ruin encounter. No replacement of defeated guards.</summary>
    public sealed class RuinActions
    {
        private readonly PlayerState _player;
        private readonly ActionContext _context;
        private readonly EnemyTokenSupply _enemies;
        private readonly RuinTokenSupply _tokens;
        private readonly Monster[] _original;
        public PlaceState Site { get; }
        public bool Completed { get; private set; }
        public bool Won { get; private set; }
        public bool RewardGranted { get; private set; }
        private readonly List<ArtifactCard> _artifactChoices=new();
        public IReadOnlyList<ArtifactCard> ArtifactChoices => _artifactChoices.AsReadOnly();
        private ArtifactCard[] _selectedArtifacts;
        private SpellCard _spell;
        private AdvActionCard _advanced;
        private UnitCard _unit;
        private ActionContext _unitRewardContext;
        public IReadOnlyList<RuinReward> PendingRewards => Won&&!RewardGranted ? Site.OriginalRuinToken.Rewards : Array.Empty<RuinReward>();
        private int Count(RuinReward kind)=>Won?Site.OriginalRuinToken.Rewards.Count(r=>r==kind):0;
        private void RequireReward(RuinReward kind)
        {
            if(!Completed||!Won||RewardGranted||Count(kind)==0)throw new InvalidOperationException("No such pending ruin reward");
        }
        public IReadOnlyList<ArtifactCard> RevealArtifacts()
        {
            RequireReward(RuinReward.Artifact);
            if(_artifactChoices.Count==0)
            {
                int amount=Count(RuinReward.Artifact)+1;
                if(_context.ArtifactSupply==null||_context.ArtifactSupply.Count<amount)
                    throw new InvalidOperationException("Insufficient actual artifact supply");
                for(int i=0;i<amount;i++)_artifactChoices.Add(_context.ArtifactSupply.Draw());
            }
            return _artifactChoices.AsReadOnly();
        }
        public void SelectArtifacts(params ArtifactCard[] cards)
        {
            RequireReward(RuinReward.Artifact);
            if(cards==null||cards.Length!=Count(RuinReward.Artifact)||cards.Distinct().Count()!=cards.Length
                ||cards.Any(c=>!_artifactChoices.Any(a=>ReferenceEquals(a,c))))throw new InvalidOperationException("Select distinct revealed artifacts");
            _selectedArtifacts=cards.ToArray();
        }
        public void SelectSpell(int index)
        {
            RequireReward(RuinReward.Spell);
            if(Count(RuinReward.Spell)!=1||_context.SpellSupply==null||index<0||index>=_context.SpellSupply.Offer.Count)
                throw new InvalidOperationException("Select actual spell offer");
            _spell=_context.SpellSupply.Offer[index];
        }
        public void SelectAdvancedAction(int index)
        {
            RequireReward(RuinReward.AdvancedAction);
            if(Count(RuinReward.AdvancedAction)!=1||_context.AdvActionSupply==null||index<0||index>=_context.AdvActionSupply.Offer.Count)
                throw new InvalidOperationException("Select actual advanced action offer");
            _advanced=_context.AdvActionSupply.Offer[index];
        }
        public void SelectUnit(int index, UnitState disband = null, bool loyaltySlot = false)
        {
            RequireReward(RuinReward.Unit);
            if(Count(RuinReward.Unit)!=1)throw new InvalidOperationException("Expected one printed unit reward");
            // Rewards are obtained after normal turn effects expire. Keep the selected physical
            // offer and dismissal separate from transient interaction/command-banner fields.
            var selection=new ActionContext{UnitOffer=_context.UnitOffer,UnitToDisband=disband,UseLoyaltySlot=loyaltySlot};
            CardUnitActions.ValidateFreeRecruit(_player,selection,index);
            _unit=selection.UnitOffer[index];_unitRewardContext=selection;
        }
        internal RuinActions(PlayerState player,ActionContext context,PlaceState site,
            EnemyTokenSupply enemies,RuinTokenSupply tokens)
        { _player=player;_context=context;Site=site;_enemies=enemies;_tokens=tokens;_original=site.Defenders.ToArray(); }
        public void Complete(PlayerState player,ActionContext context)
        {
            if(Completed)return;
            if(!ReferenceEquals(player,_player)||!ReferenceEquals(context,_context)||context.InBattle
                ||context.Enemies.Count!=_original.Length||_original.Where((e,i)=>!ReferenceEquals(e,context.Enemies[i])).Any())
                throw new InvalidOperationException("Ruin completion requires actual finished combat");
            var survivors=_original.Where((e,i)=>!context.DefeatedEnemyIndices.Contains(i)).ToArray();
            foreach(var enemy in _original.Where((e,i)=>context.DefeatedEnemyIndices.Contains(i)))_enemies.Discard(enemy);
            Site.Enemy=survivors.FirstOrDefault();Site.AdditionalGuards.Clear();Site.AdditionalGuards.AddRange(survivors.Skip(1));
            Won=survivors.Length==0;
            if(Won){Site.RuinConquered=true;Site.OwnerId=player.Id;_tokens.Discard(Site.OriginalRuinToken);}
            Completed=true;
        }
        public void ValidateReward()
        {
            if(!Completed)throw new InvalidOperationException("Ruin battle not completed");
            if(RewardGranted||!Won)return;
            if(Count(RuinReward.Unit)>0)
            {
                if(_unit==null||_unitRewardContext==null)throw new InvalidOperationException("Unresolved actual unit reward");
                CardUnitActions.ValidateFreeRecruit(_player,_unitRewardContext,
                    Array.FindIndex(_unitRewardContext.UnitOffer.ToArray(),c=>ReferenceEquals(c,_unit)));
            }
            if(Count(RuinReward.Artifact)>0&&(_selectedArtifacts==null||_selectedArtifacts.Length!=Count(RuinReward.Artifact)
                ||_artifactChoices.Count!=Count(RuinReward.Artifact)+1))throw new InvalidOperationException("Unresolved artifact reward");
            if(Count(RuinReward.Spell)>0&&(_spell==null||!_context.SpellSupply.Offer.Any(c=>ReferenceEquals(c,_spell))))
                throw new InvalidOperationException("Unresolved actual spell reward");
            if(Count(RuinReward.AdvancedAction)>0&&(_advanced==null||!_context.AdvActionSupply.Offer.Any(c=>ReferenceEquals(c,_advanced))))
                throw new InvalidOperationException("Unresolved actual advanced action reward");
        }
        internal void GrantReward(PlayerState player)
        {
            ValidateReward();
            if(!ReferenceEquals(player,_player))throw new InvalidOperationException("Wrong ruin reward owner");
            if(RewardGranted)return;
            if(Won)
            {
                if(_unit!=null)CardUnitActions.RecruitFree(player,_unitRewardContext,
                    Array.FindIndex(_unitRewardContext.UnitOffer.ToArray(),c=>ReferenceEquals(c,_unit)));
                if(_selectedArtifacts!=null)
                {
                    foreach(var card in _artifactChoices.Where(c=>!_selectedArtifacts.Contains(c)))_context.ArtifactSupply.PutUnder(card);
                    foreach(var card in _selectedArtifacts.Reverse())player.Deck.PutOnTop(new DeedCard(card.Id,CardType.Artifact));
                }
                if(_spell!=null)
                {
                    var card=_context.SpellSupply.Claim(_context.SpellSupply.Offer.FindIndex(c=>ReferenceEquals(c,_spell)));
                    player.Deck.PutOnTop(new DeedCard(card.Id,CardType.Spell));_context.SpellSupply.FillVacancies();
                }
                if(_advanced!=null)
                {
                    var card=_context.AdvActionSupply.Take(_context.AdvActionSupply.Offer.FindIndex(c=>ReferenceEquals(c,_advanced)));
                    player.Deck.PutOnTop(new DeedCard(card.Id,CardType.Action));
                }
                for(int i=0;i<Count(RuinReward.FourBasicCrystals);i++)
                    foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})player.Mana.AddCrystal(color);
            }
            RewardGranted=true;
        }
    }
}
