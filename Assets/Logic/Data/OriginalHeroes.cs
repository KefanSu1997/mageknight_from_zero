using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime;

namespace MK.Logic.Data
{
    /// <summary>The seven original heroes with both printed unique starting actions (expansion set).
    /// A setup creates cards once; later turns/rounds must retain those same instances.</summary>
    public sealed class OriginalHero
    {
        public string Id { get; }
        public string Name { get; }
        public string SkillPoolId { get; }
        public IReadOnlyList<string> StartingCards { get; }
        public IReadOnlyList<string> UniqueCards { get; }

        internal OriginalHero(string id,string name,string pool,int replaceFirst,int uniqueFirst,int replaceSecond,int uniqueSecond)
        {
            Id=id;Name=name;SkillPoolId=pool;
            var cards=new List<int>{21,21,16,7,7,0,0,14,14,1,8,22,15,9,2,23};
            cards[cards.IndexOf(replaceFirst)]=uniqueFirst;
            cards[cards.IndexOf(replaceSecond)]=uniqueSecond;
            StartingCards=Array.AsReadOnly(cards.Select(Key).ToArray());
            UniqueCards=Array.AsReadOnly(new[]{Key(uniqueFirst),Key(uniqueSecond)});
        }
        private static string Key(int index)=>"basic_card_"+index.ToString("000");

        public PlayerState CreatePlayer(IEnumerable<ActionCardData> originals,Random shuffle=null)
        {
            var source=originals?.ToDictionary(c=>c.Id) ?? throw new ArgumentNullException(nameof(originals));
            if(StartingCards.Any(id=>!source.TryGetValue(id,out var card) || card.Set!=CardSet.BasicAction))
                throw new InvalidOperationException("英雄起始牌库缺少原版基础行动定义");
            var player=new PlayerState(0,Name){HeroId=Id};
            var cards=StartingCards.Select(id=>new DeedCard(id,CardType.Action)).ToList();
            var random=shuffle??new Random();
            for(int i=cards.Count-1;i>0;i--){int j=random.Next(i+1);(cards[i],cards[j])=(cards[j],cards[i]);}
            player.Deck.SetDeck(cards);
            // GameEngine.PrepareNewRound performs the actual opening draw before tactic selection.
            return player;
        }
    }
    public static class OriginalHeroes
    {
        public static IReadOnlyList<OriginalHero> All { get; }=Array.AsReadOnly(new[]{
            new OriginalHero("Arythea","阿瑞希亚","hero_skill_pool_000",21,24,9,13),
            new OriginalHero("Tovak","托瓦克","hero_skill_pool_007",16,20,23,27),
            new OriginalHero("Goldyx","高蒂克斯","hero_skill_pool_005",2,6,15,19),
            new OriginalHero("Norowas","诺洛瓦斯","hero_skill_pool_002",8,12,1,5),
            new OriginalHero("Wolfhawk","Wolfhawk","hero_skill_pool_004",7,10,14,17),
            new OriginalHero("Krang","Krang","hero_skill_pool_003",0,3,22,26),
            new OriginalHero("Braevalar","Braevalar","hero_skill_pool_001",0,4,14,18)
        });
        public static OriginalHero Get(string id)=>All.SingleOrDefault(h=>h.Id==id)
            ?? throw new InvalidOperationException("请选择七位原版英雄之一");
    }
}
