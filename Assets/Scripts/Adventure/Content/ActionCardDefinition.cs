using System;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.Adventure;
using UnityEngine;

namespace MageKnight.Adventure.Content
{
    /// <summary>只绑定既有正式卡牌与原始卡面，不提供自定义效果数值字段。</summary>
    public sealed class ActionCardDefinition : ScriptableObject
    {
        public ActionCardSO source;
        public Sprite artwork;
        public string id => source == null ? "" : source.Id;
        public CardSpec Snapshot()
        {
            if (source == null || artwork == null)
                throw new InvalidOperationException(name + " 未绑定原卡资产或原卡面。");
            return new CardSpec(new ActionCardData(source.Id, source.NameCn, source.Set,
                source.ImagePath, source.EnImagePath, source.BaseEffect, source.EnhancedEffect,
                (MK.Logic.Core.ManaColor[])source.RequiredCrystals.Clone()));
        }
    }
}
