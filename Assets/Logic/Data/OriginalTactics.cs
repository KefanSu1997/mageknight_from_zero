using System;
using System.Linq;
using MK.Logic.Core;

namespace MK.Logic.Data
{
    public sealed record TacticDefinition(TacticCard Card, string Name, string PrintedRule, string ArtworkPath);

    /// <summary>Audited against the twelve existing original strategy images, not generated tactics.</summary>
    public static class OriginalTactics
    {
        private const string Prefix = "resources/images/strategy/httpssteamusercontentaakamaihdnetugc101944874359390";
        public static readonly TacticDefinition[] All = {
            Day(1,"捷足先登","无额外效果。","25545D6126220952C290F208347BC4074739124A5068"),
            Day(2,"三思后行","取得时弃至多3张手牌（可含创伤），抽同数量，再将弃牌堆洗回牌库。","3875CA71F4BCED53F056CEA437F310E6B404D1A59C81"),
            Day(3,"魔力窃取","取得时保留一颗基本色源骰。本昼任一自己回合可使用，使用回合末重掷归还。","4313C14CF9D1E30455A88F4F3413ECC14880933330D1"),
            Day(4,"周密计划","每回合末抽牌前若至少剩2张手牌，本次补牌上限+1。","4675239F8043D7A72F182B0363B9C72491755B892660"),
            Day(5,"完美开端","取得时立即抽2张牌。","5007601606FC4B1CB6C89B9FAC60D3859FF0C1B23E94"),
            Day(6,"最佳时机","本昼一次，在自己的回合中声明：回合结束后立即再进行一回合，然后翻面。","5317A7A4EE2DA08FCBD2C9F52C87133B6D26EC82F899"),
            Night(1,"华灯初上","无额外效果。","568492549C4931B7636EF89A34E853EA4877C61C9DA4"),
            Night(2,"漫漫长夜","本夜一次，牌库空时洗混弃牌堆，将随机3张放回牌库，然后翻面。","60598497C0B7B93F8D5CE68484E6BAF90DAF4E347239"),
            Night(3,"魔力搜寻","每回合一次，使用魔力源之前重掷至多2颗源骰，优先选择金色枯竭骰。","6500D10C9DFDAE02ADFB1E0110F9825A97AE2FB09651"),
            Night(4,"午夜冥想","本夜一次，自己回合开始前将至多5张手牌（可含创伤）洗回牌库，再抽同数量，然后翻面。","691121CE2FFE12018311B226592B07E55E6C48680059"),
            Night(5,"万全准备","取得时从牌库选择一张入手，然后洗混牌库。","7299F29A68A86926B9E8F544FF8FBD142908E8AA76AE"),
            Night(6,"积蓄力量","每个自己回合开始前：将牌库顶牌盖放卡下，或翻面并取回卡下全部牌。","768218F0892AFCAA11157E5143CD267240FDEE1F8EB4")
        };
        private static TacticDefinition Day(int n,string name,string rule,string image) => Make(DayPart.Day,n,name,rule,image);
        private static TacticDefinition Night(int n,string name,string rule,string image) => Make(DayPart.Night,n,name,rule,image);
        private static TacticDefinition Make(DayPart part,int n,string name,string rule,string image)
            => new(new TacticCard((part == DayPart.Day ? "day_" : "night_") + n,part,n,0),name,rule,Prefix+image+".jpg");
        public static TacticDefinition Get(string id) => All.Single(t => t.Card.Id == id);
        public static bool IsOriginal(TacticCard card) => card != null && All.Any(t => t.Card == card);
        internal static void ValidateIdentity(TacticCard card)
        {
            if (All.Any(t => t.Card.Id == card.Id) && !IsOriginal(card))
                throw new InvalidOperationException("原战术的昼夜、编号和效果不能由调用者改写");
        }
    }
}
