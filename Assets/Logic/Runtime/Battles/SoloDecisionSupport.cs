using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Diagnostics;

namespace MK.Logic.Runtime.Battles
{
    public sealed record SoloTrajectoryDecision(int Version,int BeforeStep,string SupportSha256,string ObservationBefore,
        IReadOnlyList<string> CommandsJson,int SelectedIndex,double? BehaviorLogProbability,double? ValuePrediction,
        SoloConquestTransition Transition,string ObservationAfter,double SupportSeconds,double ExecuteAndObserveSeconds,
        SoloConquestScore ScoreBefore,double RewardScale,int InitialScore,string PrivateStateBefore,string PrivateStateAfter);
    /// <summary>Journal verification against a separately reconstructed episode. This does not
    /// serialize hidden state or certify that the caller reconstructed all random streams.</summary>
    public static class SoloTrajectoryReplay
    {
        public static void Apply(SoloConquestEpisode episode,IEnumerable<SoloTrajectoryDecision> records)
        {
            if(episode==null||records==null)throw new ArgumentNullException();
            var boundary=DemoStateCheckpoint.Capture(episode);
            try
            {
                foreach(var row in records)
                {
                    if(row==null||row.Version!=3||row.BeforeStep!=episode.Steps
                        ||row.InitialScore!=episode.InitialScore||row.RewardScale!=episode.RewardScale)
                        throw new InvalidOperationException("Journal version or episode accounting mismatch.");
                    var support=SoloDecisionSupport.Capture(episode);
                    if(row.PrivateStateBefore!=SoloStateDigest.Compute(episode))
                        throw new InvalidOperationException("Journal private state differs before execution.");
                    if(row.SupportSha256!=support.Sha256||row.ObservationBefore!=support.ObservationJson
                        ||row.CommandsJson==null||!row.CommandsJson.SequenceEqual(support.CommandsJson))
                        throw new InvalidOperationException("Journal legal support differs from reconstructed boundary.");
                    var actual=support.ExecuteRecorded(episode,row.SelectedIndex,row.BehaviorLogProbability,row.ValuePrediction);
                    if(actual.PrivateStateAfter!=row.PrivateStateAfter||actual.ObservationAfter!=row.ObservationAfter
                        ||JsonConvert.SerializeObject(actual.ScoreBefore)!=JsonConvert.SerializeObject(row.ScoreBefore)
                        ||JsonConvert.SerializeObject(actual.Transition)!=JsonConvert.SerializeObject(row.Transition))
                        throw new InvalidOperationException("Journal transition differs from native replay.");
                }
            }
            catch {boundary.Restore();throw;}
        }
    }
    /// <summary>Immutable archive of currently implemented choices, not certification of full action coverage.</summary>
    public sealed class SoloDecisionSupport
    {
        private readonly SoloConquestEpisode _episode;
        private readonly string _privateState;
        public int Version => 1;
        public int Step { get; }
        public string ObservationJson { get; }
        public IReadOnlyList<string> CommandsJson { get; }
        public string Sha256 { get; }
        public string PublicSigningPayload { get; }
        public double CaptureSeconds { get; private set; }
        private SoloDecisionSupport(SoloConquestEpisode episode,int step,string observation,string[] commands,double seconds,string privateState)
        {
            _episode=episode;Step=step;ObservationJson=observation;
            _privateState=privateState;
            CaptureSeconds=seconds;
            CommandsJson=Array.AsReadOnly((string[])commands.Clone());
            PublicSigningPayload=JsonConvert.SerializeObject(new {version=Version,step,observation,commands});
            using(var sha=SHA256.Create())
                Sha256=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PublicSigningPayload)))
                    .Replace("-","").ToLowerInvariant();
        }
        public static SoloDecisionSupport Capture(SoloConquestEpisode episode)
        {
            if(episode==null)throw new ArgumentNullException(nameof(episode));
            var timer=Stopwatch.StartNew();
            int step=episode.Steps;
            string observation=JsonConvert.SerializeObject(episode.Observe());
            string privateState=SoloStateDigest.Compute(episode);
            var boundary=DemoStateCheckpoint.Capture(episode);
            SoloDecisionSupport result;
            try
            {
                var commands=SoloPolicyCommand.ImplementedChoices(episode).Select(JsonConvert.SerializeObject).ToArray();
                if(step!=episode.Steps||observation!=JsonConvert.SerializeObject(episode.Observe())
                    ||privateState!=SoloStateDigest.Compute(episode))
                    throw new InvalidOperationException("Support enumeration changed the decision boundary.");
                if(commands.Distinct(StringComparer.Ordinal).Count()!=commands.Length)
                    throw new InvalidOperationException("Duplicate serialized commands in decision support.");
                result=new SoloDecisionSupport(episode,step,observation,commands,0,privateState);
            }
            finally {boundary.Restore();}
            result.CaptureSeconds=timer.Elapsed.TotalSeconds;
            return result;
        }
        public SoloConquestTransition Execute(SoloConquestEpisode episode,int index)
        {
            if(!ReferenceEquals(episode,_episode)||episode.Steps!=Step
                ||_privateState!=SoloStateDigest.Compute(episode)
                ||ObservationJson!=JsonConvert.SerializeObject(episode.Observe()))
                throw new InvalidOperationException("Support belongs to a different episode or decision boundary.");
            if(index<0||index>=CommandsJson.Count)throw new ArgumentOutOfRangeException(nameof(index));
            var command=JsonConvert.DeserializeObject<SoloPolicyCommand>(CommandsJson[index]);
            return command.Execute(episode);
        }
        public SoloTrajectoryDecision ExecuteRecorded(SoloConquestEpisode episode,int index,
            double? behaviorLogProbability=null,double? valuePrediction=null)
        {
            if(behaviorLogProbability.HasValue&&(double.IsNaN(behaviorLogProbability.Value)
                ||double.IsInfinity(behaviorLogProbability.Value)||behaviorLogProbability.Value>0)
                ||valuePrediction.HasValue&&(double.IsNaN(valuePrediction.Value)||double.IsInfinity(valuePrediction.Value)))
                throw new InvalidOperationException("Invalid behavior probability or value prediction.");
            var timer=Stopwatch.StartNew();
            var boundary=DemoStateCheckpoint.Capture(episode);
            try
            {
                var scoreBefore=episode.CaptureAuditScore();
                var transition=Execute(episode,index);
                double expected=episode.RewardScale*(transition.Score.Total-scoreBefore.Total);
                if(Math.Abs(transition.Reward-expected)>1e-12)
                    throw new InvalidOperationException("Trajectory reward disagrees with native score delta.");
                string after=JsonConvert.SerializeObject(episode.Observe());
                string privateAfter=SoloStateDigest.Compute(episode);
                return new SoloTrajectoryDecision(3,Step,Sha256,ObservationJson,CommandsJson,index,
                    behaviorLogProbability,valuePrediction,transition,after,CaptureSeconds,timer.Elapsed.TotalSeconds,
                    scoreBefore,episode.RewardScale,episode.InitialScore,_privateState,privateAfter);
            }
            catch{boundary.Restore();throw;}
        }
    }
}
