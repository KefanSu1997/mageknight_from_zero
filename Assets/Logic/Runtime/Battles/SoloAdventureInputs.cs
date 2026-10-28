using System;
using System.Collections.Generic;
using MK.Logic.Core;

namespace MK.Logic.Runtime.Battles
{
    public enum SoloAdventureKind { Dungeon, Tomb, Lair, EnemyRuin, Altar, BurnMonastery }
    public sealed record SoloAdventureInput(SoloAdventureKind Kind);
    public static class SoloAdventureInputs
    {
        public static SoloConquestTransition Apply(SoloConquestEpisode episode,SoloAdventureInput input)
        {
            if(episode==null||input==null)throw new ArgumentNullException();
            return episode.Apply(j=>
            {
                switch(input.Kind)
                {
                    case SoloAdventureKind.Dungeon:j.EnterDungeon(episode.AdventureRandom);break;
                    case SoloAdventureKind.Tomb:j.EnterTomb();break;
                    case SoloAdventureKind.BurnMonastery:j.BurnMonastery();break;
                    case SoloAdventureKind.Lair:j.EnterLair(episode.AdventureRandom);break;
                    case SoloAdventureKind.EnemyRuin:j.EnterOriginalEnemyRuin();break;
                    case SoloAdventureKind.Altar:j.ActivateOriginalAltar();break;
                    default:throw new ArgumentOutOfRangeException(nameof(input.Kind));
                }
            });
        }
        public static SoloAdventureInput[] Legal(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var j=episode.Journey;
            if(episode.Terminal||!j.Game.HasActiveTurn||j.Game.IsEndingTurn||j.Context.InBattle
                ||!j.Game.Map.Places.TryGetValue(j.Player.Position,out var site))return Array.Empty<SoloAdventureInput>();
            SoloAdventureKind? kind=site.Type switch
            {
                PlaceType.Dungeon=>SoloAdventureKind.Dungeon,PlaceType.Tomb=>SoloAdventureKind.Tomb,
                PlaceType.Monastery when !site.MonasteryBurned=>SoloAdventureKind.BurnMonastery,
                PlaceType.MonsterDen or PlaceType.SpawningGround=>SoloAdventureKind.Lair,
                PlaceType.AncientRuin when site.OriginalRuinToken?.Kind==RuinTokenType.Altar=>SoloAdventureKind.Altar,
                PlaceType.AncientRuin=>SoloAdventureKind.EnemyRuin,_=>null
            };
            if(!kind.HasValue)return Array.Empty<SoloAdventureInput>();
            var boundary=DemoStateCheckpoint.Capture(episode);
            try {var input=new SoloAdventureInput(kind.Value);Apply(episode,input);return new[]{input};}
            catch(InvalidOperationException){return Array.Empty<SoloAdventureInput>();}
            finally{boundary.Restore();}
        }
    }
}
