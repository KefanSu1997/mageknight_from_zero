using System;
using System.Collections.Generic;
using System.Linq;
using MK.Logic.Core;

static partial class JourneyTraining
{
    static IEnumerable<Move> ConversionMoves()
    {
        // One-point operations compose arbitrary legal amounts; zero selects
        // the entire actual available pool without inventing resources.
        for(int amount=0;amount<=8;amount++)
        {
            yield return new Move("convertMovement","advanced_card_006","melee",Option:amount);
            yield return new Move("convertMovement","advanced_card_006","ranged",Option:amount);
            yield return new Move("convertInfluence","advanced_card_018",Option:amount);
        }
    }
    sealed partial class Env
    {
        int ConversionAmount(Move action)
        {
            if(action.Option>0)return action.Option;
            if(action.Kind=="convertInfluence")return Context.InfluencePool;
            int cost=action.Choice=="ranged"?Context.MoveCostRanged:Context.MoveCostAttack;
            return cost>0?Context.MovementPool/cost:0;
        }
        void AppendConversionFeatures(List<double> features)
        {
            features.Add(Context.MoveCostAttack/4.0);
            features.Add(Context.MoveCostRanged/4.0);
            features.Add(Context.InfluenceAsBlock?1:0);
            foreach(var element in Enum.GetValues<Element>())
                features.Add(Context.InfluenceAsBlock&&Context.InfluenceBlockElement==element?1:0);
        }
    }
}
