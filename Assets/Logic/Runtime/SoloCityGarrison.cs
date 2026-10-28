using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// Standard solo conquest levels 5/8. Composition independently read from the city-level
    /// icon table in Complete Rules Reference p4 and cross-checked against the external catalog.
    /// Other levels must be explicitly implemented; never clamp or invent a composition.
    /// </summary>
    public static class SoloCityGarrison
    {
        public static EnemyTokenColor[] Composition(CityColor color, int level)
        {
            if(level!=5 && level!=8)throw new ArgumentOutOfRangeException(nameof(level),"Standard solo levels are 5 and 8");
            var first=color switch
            {
                CityColor.Blue => new[]{EnemyTokenColor.Purple,EnemyTokenColor.Purple,EnemyTokenColor.White},
                CityColor.Green => new[]{EnemyTokenColor.Brown,EnemyTokenColor.Brown,EnemyTokenColor.White},
                CityColor.Red => new[]{EnemyTokenColor.Brown,EnemyTokenColor.Purple,EnemyTokenColor.White},
                CityColor.White => new[]{EnemyTokenColor.Gray,EnemyTokenColor.White,EnemyTokenColor.White},
                _ => throw new ArgumentOutOfRangeException(nameof(color))
            };
            return level==5?first:first.Concat(new[]{EnemyTokenColor.White}).ToArray();
        }
        public static PlaceState Reveal(CityColor color, int level, EnemyTokenSupply supply)
        {
            if(supply==null)throw new ArgumentNullException(nameof(supply));
            var colors=Composition(color,level);
            foreach(var group in colors.GroupBy(c=>c))
                if(supply.DrawPile.Concat(supply.DiscardPile).Count(e=>e.TokenColor==group.Key)<group.Count())
                    throw new InvalidOperationException("Insufficient actual city guards; supply remains untouched");
            var site=new PlaceState(PlaceType.City){CityColor=color,CityLevel=level};
            var guards=colors.Select(supply.Draw).ToArray();
            site.Enemy=guards[0];site.AdditionalGuards.AddRange(guards.Skip(1));
            // Do not mark the site revealed: original city card reveals identity only from adjacency.
            return site;
        }
    }
}
