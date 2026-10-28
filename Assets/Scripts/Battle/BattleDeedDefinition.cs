using System;
using MK.Logic.Data.Cards;
using UnityEngine;

namespace MageKnight.Battle
{
    /// <summary>A binding to an existing original card and artwork; never defines effect values.</summary>
    public sealed class BattleDeedDefinition : ScriptableObject
    {
        public CardSO source;
        public Sprite artwork;
        public string id => source.Id;
        public void Validate()
        {
            if(source==null || artwork==null || artwork.name!=source.Id)
                throw new InvalidOperationException("原卡必须绑定同源定义和原卡面");
        }
        public ActionCardData Action()
        {
            Validate();
            if(source is ActionCardSO a)
                return new(a.Id,a.NameCn,a.Set,a.ImagePath,a.EnImagePath,a.BaseEffect,a.EnhancedEffect,a.RequiredCrystals);
            if(source is SpellCardSO s)
                return new(s.Id,s.NameCn,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost);
            throw new InvalidOperationException("此原卡不是行动牌或法术");
        }
        public SpellCardData Spell()
        {
            Validate();
            var s=source as SpellCardSO ?? throw new InvalidOperationException("此原卡不是法术");
            return new(s.Id,s.NameCn,s.Set,s.ImagePath,s.EnImagePath,s.ManaColor,
                new HalfSpell(s.Top.Name,s.Top.Effect,s.Top.ManaCost),new HalfSpell(s.Bottom.Name,s.Bottom.Effect,s.Bottom.ManaCost));
        }
    }
}
