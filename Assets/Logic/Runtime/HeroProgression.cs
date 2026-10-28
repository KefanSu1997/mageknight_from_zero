using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MK.Logic.Runtime
{
    /// <summary>End-turn printed level rewards over the player's actual skill pile and shared offer.</summary>
    public sealed class HeroProgression
    {
        private readonly PlayerState _player;
        private readonly Queue<SkillCard> _unlearned;
        private readonly string _pool;
        private readonly AdvActionSupply _actions;
        private readonly SkillSupply _common;
        private readonly Func<bool> _canResolve;
        private readonly Random _random;
        private readonly UnitSupply _units;
        private readonly bool _solo;
        private readonly List<SkillCard> _choices=new();
        public IReadOnlyList<SkillCard> Choices=>_choices;
        public IReadOnlyList<AdvActionCard> Actions=>_actions.Offer;
        public IReadOnlyList<SkillCard> CommonSkills=>_common.Offer;
        public int PendingLevel=>_choices.Count==0?0:_player.Level+1;
        public bool Pending=>_choices.Count>0;
        public string LastAcquiredAction { get; private set; }="";
        internal Action AfterSkillAcquired { get; set; }
        public HeroProgression(PlayerState player,IEnumerable<SkillCard> originals,AdvActionSupply actions,SkillSupply common,Func<bool> canResolve,Random random=null,UnitSupply units=null,bool solo=false)
        {
            _player=player;_pool=OriginalHeroes.Get(player.HeroId).SkillPoolId;_canResolve=canResolve;
            var skills=originals?.ToArray()??throw new ArgumentNullException(nameof(originals));
            if(skills.Length!=10 || skills.Select(s=>s.SourceId).Distinct().Count()!=10
                || skills.Any(s=>!s.SourceId.StartsWith(_pool+":")))
                throw new InvalidOperationException("升级须使用该英雄完整的10个原自身技能");
            if(solo && (skills.Any(s=>s.IsInteractive==null) || skills.Count(s=>s.IsInteractive==true)!=1
                || player.Skills.Any(s=>s.IsInteractive!=false)))
                throw new InvalidOperationException("Solo progression requires verified interactive metadata and no owned interactive skill.");
            _solo=solo;
            _player.ResolvedLevel=_player.Level;
            var unlearned=skills.Where(s=>(!solo || s.IsInteractive==false) && !player.Skills.Any(owned=>owned.SourceId==s.SourceId)).ToArray();
            var rng=random??new Random();
            _random=rng;
            _units=units;
            for(int i=unlearned.Length-1;i>0;i--){int j=rng.Next(i+1);(unlearned[i],unlearned[j])=(unlearned[j],unlearned[i]);}
            _unlearned=new Queue<SkillCard>(unlearned);_actions=actions;_common=common;
        }
        public void PrepareNext()
        {
            RequireEndTurn();
            if(Pending)return;
            while(_player.Level<_player.FameLevel)
            {
                int next=_player.Level+1;
                if(next%2==0)
                {
                    var remaining=_unlearned.Where(s=>!_player.Skills.Any(owned=>owned.SourceId==s.SourceId)).ToArray();
                    _unlearned.Clear();foreach(var skill in remaining)_unlearned.Enqueue(skill);
                    if(_unlearned.Count<(_solo?1:2) || _actions.Offer.Count==0)throw new InvalidOperationException("缺少升级所需的实际技能或高级行动供应");
                    int count=Math.Min(2,_unlearned.Count);
                    for(int i=0;i<count;i++)_choices.Add(_unlearned.Dequeue());
                    return;
                }
                ApplyLevel(next);
            }
        }
        public void Choose(SkillCard skill,AdvActionCard action)
        {
            RequireEndTurn();
            bool own=_choices.Any(s=>ReferenceEquals(s,skill));
            bool common=_common.Offer.Any(s=>ReferenceEquals(s,skill)) && !skill.SourceId.StartsWith(_pool+":");
            int index=_actions.Offer.FindIndex(c=>ReferenceEquals(c,action));
            if(!Pending || (!own&&!common) || index<0 || common && index!=0
                || _player.Skills.Any(s=>s.SourceId==skill.SourceId))
                throw new InvalidOperationException("请选择本次翻出的自身技能和任意供应牌，或他人公共技能与最底供应牌");
            HeroSkillAcquisition.Validate(_player,skill,_units);
            foreach(var other in _choices.Where(s=>!ReferenceEquals(s,skill)))_common.Offer.Add(other);
            if(common)_common.Offer.Remove(skill);
            HeroSkillAcquisition.Acquire(_player,skill,_random,_units);
            var obtained=_actions.Take(index);LastAcquiredAction=obtained.Id;
            _player.Deck.PutOnTop(new DeedCard(obtained.Id,CardType.Action));
            _choices.Clear();ApplyLevel(_player.Level+1);
            AfterSkillAcquired?.Invoke();
            PrepareNext();
        }
        private void ApplyLevel(int level)
        {
            int previousArmor=2+(_player.Level+1)/4;
            _player.ResolvedLevel=level;
            _player.Armor+=2+(level+1)/4-previousArmor;
        }
        private void RequireEndTurn()
        {
            if(_canResolve==null || !_canResolve())throw new InvalidOperationException("升级只能在实际回合清理之后、补牌之前结算");
        }
    }
}
