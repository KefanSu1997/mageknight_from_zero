namespace MK.Logic.Runtime
{
    /// <summary>
    /// 監控輪次結束條件的簡易計數器。
    /// 玩家宣告結束輪次後，剩餘玩家各進行一次回合即視為輪次結束。
    /// </summary>
    public sealed class RoundEndTracker
    {
        private int _remaining = -1;

        /// <summary>當前輪次是否已結束。</summary>
        public bool RoundEnded => _remaining == 0;

        /// <summary>倒數是否已經開始。</summary>
        public bool CountdownStarted => _remaining >= 0;

        /// <summary>開始倒數，數值等於玩家人數。</summary>
        public void StartCountdown(int playerCount) => _remaining = playerCount;

        /// <summary>通知有玩家結束回合，倒數遞減。</summary>
        public void NotifyTurnFinished()
        {
            if (_remaining > 0) _remaining--;
        }

        /// <summary>重置計數器以開始新輪次。</summary>
        public void Reset() => _remaining = -1;
    }
}

