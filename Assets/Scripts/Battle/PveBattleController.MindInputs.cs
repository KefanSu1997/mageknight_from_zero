using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using W=MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private readonly List<PveMindReadResponse> _mindResponses=new();
        private int _mindResponder=-1,_mindPage;
        private bool _mindResponseStarted,_mindStealDecided,_mindResultOpen;
        private DeedCard _mindResponseCard,_mindStealCard;
        private void ResetMindInputs()
        {
            _mindResponses.Clear();_mindResponder=-1;_mindPage=0;
            _mindResponseStarted=_mindStealDecided=_mindResultOpen=false;
            _mindResponseCard=_mindStealCard=null;
        }
        private bool MindReady(bool strong)
        {
            var ctx=_journey?.Context??_battle.Context;
            return _mindResponses.Count==ctx.OtherPlayers.Count&&(!strong||ctx.OtherPlayers.Count==0||_mindStealDecided);
        }
        private void DrawMindInputs(Transform panel,bool strong)
        {
            var ctx=_journey?.Context??_battle.Context;
            W.Text(panel,"MindRule","其他玩家各自选择同色行动或法术；没有同色牌时展示手牌。\n响应不推进对方回合。当前共 "+ctx.OtherPlayers.Count+" 位响应玩家。",40,565,1050,112,24,W.Gold);
            if(ctx.OtherPlayers.Count==0)return;
            W.Text(panel,"MindProgress","已确认 "+_mindResponses.Count+" / "+ctx.OtherPlayers.Count,1140,415,726,48,24,W.Gold);
            if(_mindResponses.Count<ctx.OtherPlayers.Count)
                Button(panel,"Btn_MindRespond","交给下一位玩家响应",1140,480,726,68,()=>{_mindResponder=_mindResponses.Count;_mindPage=0;_mindResponseStarted=false;_mindResponseCard=null;});
            else
            {
                W.Text(panel,"MindResponses",string.Join("\n",_mindResponses.Select(r=>r.Player.Name+"："+(r.Card==null?"无同色，结算时展示手牌":CardName(r.Card)))),1140,472,726,150,23,W.Cream);
                if(strong)
                {
                    Button(panel,"Btn_MindStealNone",_mindStealDecided&&_mindStealCard==null?"● 不偷取":"不偷取",1140,643,726,53,()=>{_mindStealCard=null;_mindStealDecided=true;});
                    for(int i=0;i<_mindResponses.Count;i++)
                    {
                        var r=_mindResponses[i];if(r.Card?.Type!=CardType.Action)continue;
                        var card=r.Card;
                        Button(panel,"Btn_MindSteal_"+i,(ReferenceEquals(card,_mindStealCard)?"● ":"")+"永久取得 · "+CardName(card),1140,699+i*50,726,45,()=>{_mindStealCard=card;_mindStealDecided=true;});
                    }
                }
            }
            Button(panel,"Btn_MindReset","重新选择响应",40,718,1050,55,ResetMindInputs);
        }
        private void DrawMindResponse()
        {
            var ctx=_journey?.Context??_battle.Context;var other=ctx.OtherPlayers[_mindResponder];
            var panel=W.Panel(_root,"MindResponse",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","请交给 "+other.Name+" · 思维读取响应",40,30,1820,65,34,W.Gold);
            W.Text(panel,"Rule","本机共享设备：由这位玩家选择自己的牌。当前仍是施法者的回合。\n所选颜色："+ColorName(_inputColors[0])+"；确认前不支付魔力、不移动任何牌。",40,113,1800,109,25,W.Cream);
            if(!_mindResponseStarted)
            {
                Button(panel,"Btn_MindBeginResponse","我是 "+other.Name+"，查看并选择",380,470,1160,90,()=>_mindResponseStarted=true);
            }
            else
            {
                var cards=other.Deck.Hand.Where(c=>(c.Type==CardType.Action||c.Type==CardType.Spell)&&CardColorHelper.Get(c.Id)==_inputColors[0]).ToArray();
                _mindPage=Math.Min(_mindPage,Math.Max(0,(cards.Length-1)/6));
                W.Text(panel,"ResponseRule",cards.Length==0?"没有所选颜色的行动或法术，请确认展示实际手牌。":"请选择一张实际同色牌，确认后交回施法者。",40,241,1800,56,25,W.Gold);
                for(int slot=0;slot<6&&_mindPage*6+slot<cards.Length;slot++)
                {
                    int i=_mindPage*6+slot;var card=cards[i];
                    var b=Button(panel,"Btn_MindCard_"+i,"",40+slot%3*615,325+slot/3*241,590,225,()=>_mindResponseCard=card);
                    W.Sprite(b.transform,"OriginalCard_"+card.Id,Binding(card).artwork,14,12,136,198);
                    W.Text(b.transform,"MindCardName",(ReferenceEquals(card,_mindResponseCard)?"● ":"")+CardName(card),175,60,390,100,28,W.Cream);
                }
                if(cards.Length>6)
                {
                    Button(panel,"Btn_MindPrevious","上一页",40,830,890,52,()=>_mindPage--).interactable=_mindPage>0;
                    Button(panel,"Btn_MindNext","下一页",970,830,890,52,()=>_mindPage++).interactable=(_mindPage+1)*6<cards.Length;
                }
                Button(panel,"Btn_MindConfirmResponse",cards.Length==0?"确认没有同色牌，展示手牌":"确认本人选择",970,1000,890,55,()=>
                {
                    _mindResponses.Add(new PveMindReadResponse(other,cards.Length==0?null:_mindResponseCard));
                    _mindResponder=-1;_mindResponseStarted=false;_mindResponseCard=null;_mindPage=0;
                }).interactable=cards.Length==0||cards.Any(c=>ReferenceEquals(c,_mindResponseCard));
            }
            Button(panel,"Btn_MindCancelResponse","取消本次响应，返回施法者",40,1000,890,55,()=>{_mindResponder=-1;_mindResponseStarted=false;_mindResponseCard=null;});
        }
        private void DrawMindResult()
        {
            var ctx=_journey?.Context??_battle.Context;var player=_journey?.Player??_battle.Player;
            var panel=W.Panel(_root,"MindResult",0,0,1920,1080,new Color(.025f,.065f,.06f,1));panel.GetComponent<Image>().raycastTarget=true;
            W.Text(panel,"Title","思维读取 · 原效果已结算",40,25,1820,63,35,W.Gold);
            W.Text(panel,"Result",player.Name+" · "+ColorName(_inputColors[0])+"库存魔晶 "+player.Mana.Crystals.GetValueOrDefault(_inputColors[0])+"\n"+
                string.Join("\n",_mindResponses.Select(r=>r.Player.Name+"：手牌 "+r.Player.Deck.Hand.Count+" / 弃牌 "+r.Player.Deck.DiscardPile.Count+" · "+(r.Card==null?"已展示":ReferenceEquals(r.Card,_mindStealCard)?"所选行动已永久转移":"所选牌已弃置")))+
                "\n"+(_mindStealCard==null?"未偷取行动牌":"实际取得："+CardName(_mindStealCard)),40,105,1800,185,25,W.Cream);
            var revealed=ctx.MindReadRevealedHands.SelectMany(pair=>pair.Value.Select(card=>(Owner:pair.Key,Card:card))).ToArray();
            _mindPage=Math.Min(_mindPage,Math.Max(0,(revealed.Length-1)/6));
            W.Text(panel,"RevealTitle",ctx.MindReadRevealedHands.Count==0?"本次无人需要展示手牌。":"无同色牌玩家的实际展示手牌",40,304,1810,53,25,W.Gold);
            for(int slot=0;slot<6&&_mindPage*6+slot<revealed.Length;slot++)
            {
                var item=revealed[_mindPage*6+slot];var tile=W.Panel(panel,"Revealed_"+slot,40+slot%3*615,375+slot/3*240,590,225,W.Ink);
                var art=Binding(item.Card)?.artwork;if(art!=null)W.Sprite(tile,"OriginalReveal",art,14,12,136,198);
                W.Text(tile,"RevealName",item.Owner.Name+"\n"+CardName(item.Card),175,45,390,145,25,W.Cream);
            }
            if(revealed.Length>6)
            {
                Button(panel,"Btn_RevealPrevious","上一页展示",40,878,890,50,()=>_mindPage--).interactable=_mindPage>0;
                Button(panel,"Btn_RevealNext","下一页展示",970,878,890,50,()=>_mindPage++).interactable=(_mindPage+1)*6<revealed.Length;
            }
            Button(panel,"Btn_CloseMindResult","返回施法者当前行动",40,1000,1830,55,()=>{_mindResultOpen=false;_inputCard=null;});
        }
    }
}
