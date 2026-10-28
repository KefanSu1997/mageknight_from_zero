using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>Original place_014/015: persistent lair guards and once-only deferred rewards.</summary>
    public sealed class LairActions
    {
        private readonly PlayerState _player;
        private readonly ActionContext _context;
        private readonly EnemyTokenSupply _supply;
        private readonly Random _random;
        private readonly Monster[] _enemies;
        private readonly List<ArtifactCard> _artifacts = new();
        private readonly List<ManaColor> _dice = new();
        private readonly Dictionary<int,ManaColor> _gold = new();
        public PlaceState Site { get; }
        public bool Completed { get; private set; }
        public bool Won { get; private set; }
        public bool RewardGranted { get; private set; }
        public IReadOnlyList<ManaColor> RewardDice => _dice.AsReadOnly();
        public ArtifactCard SelectedArtifact { get; private set; }
        public IReadOnlyList<ArtifactCard> ArtifactChoices => _artifacts.AsReadOnly();
        internal LairActions(PlayerState player, ActionContext context, PlaceState site, EnemyTokenSupply supply, Random random)
        { _player=player;_context=context;Site=site;_supply=supply;_random=random;_enemies=site.Defenders.ToArray(); }
        public void Complete(PlayerState player, ActionContext context)
        {
            if(Completed)return;
            if(!ReferenceEquals(player,_player)||!ReferenceEquals(context,_context)||context.InBattle
                ||context.Enemies.Count!=_enemies.Length||_enemies.Where((e,i)=>!ReferenceEquals(e,context.Enemies[i])).Any())
                throw new InvalidOperationException("Lair completion requires the actual completed encounter");
            var defeated=_enemies.Where((e,i)=>context.DefeatedEnemyIndices.Contains(i)).ToArray();
            var survivors=_enemies.Where((e,i)=>!context.DefeatedEnemyIndices.Contains(i)).ToList();
            Won=survivors.Count==0;
            foreach(var enemy in defeated)_supply.Discard(enemy);
            Site.HiddenGuards.Clear();
            if(!Won && Site.Type==PlaceType.SpawningGround)
            {
                // Surviving guards stay face up; replacements are face down until next entry.
                for(int i=0;i<defeated.Length;i++)
                {
                    var replacement=_supply.Draw(EnemyTokenColor.Brown);
                    survivors.Add(replacement);Site.HiddenGuards.Add(replacement);
                }
            }
            Site.Enemy=survivors.FirstOrDefault();Site.AdditionalGuards.Clear();Site.AdditionalGuards.AddRange(survivors.Skip(1));
            if(Won)
            {
                Site.LairConquered=true;Site.OwnerId=player.Id;
                int rolls=Site.Type==PlaceType.MonsterDen?2:3;
                for(int i=0;i<rolls;i++){var die=new ManaDie(_random);die.RollUnrestricted();_dice.Add(die.Face);}
            }
            Completed=true;
        }
        public void ChooseGoldCrystal(int roll, ManaColor color)
        {
            if(!Completed||!Won||RewardGranted||roll<0||roll>=_dice.Count||_dice[roll]!=ManaColor.Gold||(int)color<0||(int)color>=4)
                throw new InvalidOperationException("Choose a basic color for an actual gold reward die");
            _gold[roll]=color;
        }
        public IReadOnlyList<ArtifactCard> RevealArtifacts()
        {
            if(!Completed||!Won||RewardGranted||Site.Type!=PlaceType.SpawningGround)throw new InvalidOperationException("No lair artifact reward");
            if(_artifacts.Count==0)
            {
                if(_context.ArtifactSupply==null||_context.ArtifactSupply.Count<2)throw new InvalidOperationException("Artifact supply requires two actual cards");
                var pair=_context.ArtifactSupply.DrawTwo();_artifacts.Add(pair.a);_artifacts.Add(pair.b);
            }
            return _artifacts.AsReadOnly();
        }
        public void SelectArtifact(ArtifactCard card)
        {
            if(RewardGranted||!_artifacts.Any(a=>ReferenceEquals(a,card)))throw new InvalidOperationException("Choose a revealed actual artifact");
            SelectedArtifact=card;
        }
        public void ValidateReward()
        {
            if(!Completed)throw new InvalidOperationException("Lair battle not complete");
            if(RewardGranted||!Won)return;
            for(int i=0;i<_dice.Count;i++)if(_dice[i]==ManaColor.Gold&&!_gold.ContainsKey(i))throw new InvalidOperationException("Unresolved gold crystal reward");
            if(Site.Type==PlaceType.SpawningGround && (SelectedArtifact==null||_artifacts.Count!=2))throw new InvalidOperationException("Unresolved lair artifact reward");
        }
        internal void GrantReward(PlayerState player)
        {
            ValidateReward();if(!ReferenceEquals(player,_player))throw new InvalidOperationException("Wrong reward owner");
            if(RewardGranted)return;
            if(Won)
            {
                for(int i=0;i<_dice.Count;i++)
                    if(_dice[i]==ManaColor.Black)player.Fame++;
                    else player.Mana.AddCrystal(_dice[i]==ManaColor.Gold?_gold[i]:_dice[i]);
                if(Site.Type==PlaceType.SpawningGround)
                {
                    var other=_artifacts.Single(a=>!ReferenceEquals(a,SelectedArtifact));
                    _context.ArtifactSupply.KeepOneReturn(SelectedArtifact,other);
                    player.Deck.PutOnTop(new DeedCard(SelectedArtifact.Id,CardType.Artifact));
                }
            }
            RewardGranted=true;
        }
    }
}
