using System.Linq;
using MK.Logic.Runtime;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        public Texture2D dungeonRuleArtwork;
        private bool _dungeonIntro;
        private void DrawDungeonIntro()
        {
            var panel=W.Panel(_root,"DungeonIntro",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","地下城 · 当前实际地点",40,25,1800,60,36,W.Gold);
            var image=W.Rect(panel,"OriginalDungeonRule",40,150,1080,751).gameObject.AddComponent<RawImage>();image.texture=dungeonRuleArtwork;
            W.Text(panel,"Rule","消耗本回合行动，抽取实际棕色敌人。\n仅本场战斗按黑夜规则，禁止使用部队；\n忠诚契约实际指挥的部队例外。\n战后无论胜负弃置敌人标记。\n首次胜利掷骰：金/黑得法术，否则神器。\n再次进入已征服地点只获得名望。",1160,170,710,450,26,W.Cream);
            Button(panel,"Btn_EnterDungeon","进入地下城 · 开始真实夜战",1160,680,710,75,()=>{_journey.EnterDungeon(Loadout.replayEffectRandom?new System.Random(Loadout.effectRandomSeed):null);_dungeonIntro=false;AdoptWorldBattle();},true);
            Button(panel,"Btn_Back","返回地图",1160,805,710,65,()=>_dungeonIntro=false);
        }
        private void DrawDungeonArtifactReward()
        {
            var panel=W.Panel(_root,"SiteReward",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","地下城 · 原神器翻二选一",40,30,1800,65,36,W.Gold);
            W.Text(panel,"Rule","选择实际翻出的神器；正式回合结束置于牌库顶，其余归底。随后按原上限补牌。",40,115,1800,90,26,W.Cream);
            var offer=_journey.Context.Dungeon.ArtifactChoices;
            for(int i=0;i<offer.Count;i++)
            {
                var card=offer[i];var binding=artifactCatalog.Single(a=>a.source.Id==card.Id);
                var b=Button(panel,"Btn_Reward_"+i,"",420+i*570,255,480,640,()=>{_journey.SelectDungeonArtifactReward(card);_siteRewardOpen=false;_feedback="已选 "+binding.source.NameCn+"，正式结束回合领取。";});
                W.Sprite(b.transform,"OriginalReward",binding.artwork,84,25,312,450);
                W.Text(b.transform,"Name",binding.source.NameCn,35,520,410,65,29,W.Cream);
            }
        }
    }
}
