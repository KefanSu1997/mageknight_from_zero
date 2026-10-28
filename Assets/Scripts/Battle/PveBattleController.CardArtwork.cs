using MK.Logic.Data;
using MK.Logic.Core;
using MK.Logic.Runtime;
using UnityEngine;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        private static Sprite _originalWoundArt;
        private Sprite CardArtwork(DeedCard card)
        {
            if(card.Type!=CardType.Wound)return Binding(card)?.artwork ?? ArtifactBinding(card)?.artwork;
            if(_originalWoundArt==null)
            {
                var texture=Resources.Load<Texture2D>("Adventure/Art/original_wound");
                if(texture!=null)_originalWoundArt=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f));
            }
            return _originalWoundArt;
        }
    }
}
