using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    public enum DungeonRewardKind { None, Spell, Artifact, SpellAndArtifact }

    /// <summary>One actual dungeon, tomb or monastery encounter and its deferred original reward.</summary>
    public sealed class DungeonActions
    {
        private readonly PlayerState _player;
        private readonly ActionContext _context;
        private readonly EnemyTokenSupply _enemies;
        private readonly bool _wasConquered;
        private readonly DayPart _outsideDayPart;
        private readonly Random _random;
        private readonly List<ArtifactCard> _artifacts = new();
        public PlaceState Site { get; }
        public Monster Enemy { get; }
        public bool Completed { get; private set; }
        public bool Won { get; private set; }
        public ManaColor? RewardDie { get; private set; }
        public DungeonRewardKind Reward { get; private set; }
        public IReadOnlyList<ArtifactCard> ArtifactChoices => _artifacts;
        public ArtifactCard SelectedArtifact { get; private set; }
        public bool RewardGranted { get; private set; }
        private bool HasArtifactReward => Reward == DungeonRewardKind.Artifact || Reward == DungeonRewardKind.SpellAndArtifact;
        private bool HasSpellReward => Reward == DungeonRewardKind.Spell || Reward == DungeonRewardKind.SpellAndArtifact;

        internal DungeonActions(PlayerState player, ActionContext context, PlaceState site,
            Monster enemy, EnemyTokenSupply enemies, Random random)
        {
            _player = player; _context = context; Site = site; Enemy = enemy; _enemies = enemies;
            _wasConquered = site.Conquered; _outsideDayPart = context.DayPart; _random = random;
        }

        public void Complete(PlayerState player, ActionContext context)
        {
            if (Completed) return;
            if (!ReferenceEquals(player, _player) || !ReferenceEquals(context, _context)
                || context.InBattle || context.Enemies.Count != 1 || !ReferenceEquals(context.Enemies[0], Enemy))
                throw new InvalidOperationException("地下城结算必须沿用本次实际玩家、敌人和已结束战斗");
            Won = context.DefeatedEnemyIndices.Contains(0);
            _enemies.Discard(Enemy);
            if (Won && !_wasConquered)
            {
                if(Site.Type==PlaceType.Monastery)Site.MonasteryBurned=true;
                else Site.DungeonConquered = true;
                Site.OwnerId = player.Id;
                if (Site.Type == PlaceType.Tomb) Reward = DungeonRewardKind.SpellAndArtifact;
                else if(Site.Type==PlaceType.Monastery)Reward=DungeonRewardKind.Artifact;
                else
                {
                    var die = new ManaDie(_random);
                    die.RollUnrestricted();
                    RewardDie = die.Face;
                    Reward = die.Face == ManaColor.Gold || die.Face == ManaColor.Black
                        ? DungeonRewardKind.Spell : DungeonRewardKind.Artifact;
                }
                if (HasSpellReward) context.PendingSpellReward = true;
            }
            context.DayPart = _outsideDayPart;
            context.UnitsAllowed = true;
            Completed = true;
        }

        public IReadOnlyList<ArtifactCard> RevealArtifacts()
        {
            if (!Completed || !HasArtifactReward || RewardGranted)
                throw new InvalidOperationException("当前没有地下城神器奖励");
            if (_artifacts.Count != 0) return _artifacts;
            if (_context.ArtifactSupply == null || _context.ArtifactSupply.Count < 2)
                throw new InvalidOperationException("实际神器供应不足两张，尚不能揭示奖励");
            var pair = _context.ArtifactSupply.DrawTwo();
            _artifacts.Add(pair.a); _artifacts.Add(pair.b);
            return _artifacts;
        }

        public void SelectArtifact(ArtifactCard artifact)
        {
            if (!Completed || RewardGranted || !_artifacts.Any(a => ReferenceEquals(a, artifact)))
                throw new InvalidOperationException("请选择地下城实际翻出的两张神器之一");
            SelectedArtifact = artifact;
        }

        public void ValidateReward()
        {
            if (!Completed) throw new InvalidOperationException("必须先结束实际地下城战斗");
            if (RewardGranted) return;
            if (HasArtifactReward && (SelectedArtifact == null || _artifacts.Count != 2
                || !_artifacts.Any(a => ReferenceEquals(a, SelectedArtifact))))
                throw new InvalidOperationException("结束回合前须翻出并选择实际地下城神器奖励");
            if (HasSpellReward) FortifiedSiteActions.ValidateSpellReward(_context);
        }

        internal void GrantReward(PlayerState player)
        {
            ValidateReward();
            if (!ReferenceEquals(player, _player)) throw new InvalidOperationException("地下城奖励属于实际参战玩家");
            if (RewardGranted) return;
            if (HasArtifactReward)
            {
                var other = _artifacts.Single(a => !ReferenceEquals(a, SelectedArtifact));
                _context.ArtifactSupply.KeepOneReturn(SelectedArtifact, other);
                player.Deck.PutOnTop(new DeedCard(SelectedArtifact.Id, CardType.Artifact));
            }
            // Spell reward is granted by the shared formal end-turn site reward path.
            RewardGranted = true;
        }
    }
}
