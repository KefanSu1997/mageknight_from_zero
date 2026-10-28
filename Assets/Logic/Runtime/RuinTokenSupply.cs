using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    public enum RuinReward { Artifact, Spell, AdvancedAction, Unit, FourBasicCrystals }

    /// <summary>One physical original token. Inventory must come from verified printed data.</summary>
    public sealed class RuinToken
    {
        public string Id { get; }
        public RuinTokenType Kind { get; }
        public IReadOnlyList<ManaColor> ManaCost { get; }
        public int Fame { get; }
        public IReadOnlyList<EnemyTokenColor> Enemies { get; }
        public IReadOnlyList<RuinReward> Rewards { get; }
        public RuinToken(string id, IEnumerable<ManaColor> manaCost, int fame,
            IEnumerable<EnemyTokenColor> enemies, IEnumerable<RuinReward> rewards)
        {
            if(string.IsNullOrWhiteSpace(id)||manaCost==null||enemies==null||rewards==null)throw new ArgumentException("Explicit token data required");
            var cost=manaCost.ToArray();var foes=enemies.ToArray();var prize=rewards.ToArray();
            bool altar=cost.Length>0;
            bool basic(ManaColor c)=>c==ManaColor.Red||c==ManaColor.Blue||c==ManaColor.Green||c==ManaColor.White;
            if(altar ? cost.Any(c=>!basic(c))||fame<=0||foes.Length!=0||prize.Length!=0
                : fame!=0||foes.Length==0||foes.Any(c=>c==EnemyTokenColor.Unknown||!Enum.IsDefined(typeof(EnemyTokenColor),c))||prize.Length==0)
                throw new ArgumentException("Invalid altar/enemy token data");
            if(prize.Any(r=>!Enum.IsDefined(typeof(RuinReward),r)))throw new ArgumentException("Unknown ruin reward");
            Id=id;Kind=altar?RuinTokenType.Altar:RuinTokenType.Enemy;Fame=fame;
            ManaCost=Array.AsReadOnly(cost);Enemies=Array.AsReadOnly(foes);Rewards=Array.AsReadOnly(prize);
        }
    }

    public sealed class RuinTokenSupply
    {
        private readonly List<RuinToken> _draw, _inUse=new(), _discard=new();
        public int Remaining => _draw.Count;
        public bool IsInUse(RuinToken token)=>_inUse.Contains(token);
        public RuinTokenSupply(IEnumerable<RuinToken> tokens, Random random)
        {
            if(tokens==null||random==null)throw new ArgumentNullException();
            _draw=tokens.ToList();
            if(_draw.Count==0||_draw.Any(t=>t==null)||_draw.Select(t=>t.Id).Distinct().Count()!=_draw.Count)
                throw new ArgumentException("Distinct physical ruin inventory required");
            for(int i=_draw.Count-1;i>0;i--){int j=random.Next(i+1);(_draw[i],_draw[j])=(_draw[j],_draw[i]);}
        }
        public RuinToken Draw()
        {
            if(_draw.Count==0)throw new InvalidOperationException("Original ruin token supply exhausted");
            var token=_draw[0];_draw.RemoveAt(0);_inUse.Add(token);return token;
        }
        public void Discard(RuinToken token)
        {
            if(!_inUse.Remove(token))throw new InvalidOperationException("Ruin token is not in use");
            _discard.Add(token);
        }
    }
}
