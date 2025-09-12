// ────────────────────────────────────────────────────────
// Runtime/RoundClock.cs
// -------------------------------------------------------
using MK.Logic.Core;

namespace MK.Logic.Runtime
{
    /// <summary>
    /// 表示遊戲進行中的日夜與輪次節拍器。
    /// 在《魔法騎士》裡，一個白晝或黑夜會包含多個輪次，
    /// 當白晝和黑夜結束後便算作過去一天。
    /// </summary>
    public sealed class RoundClock
    {
        /// <summary>當前是第幾天，從 1 開始計數。</summary>
        public int DayIndex { get; private set; } = 1;

        /// <summary>當前處於白天或黑夜。</summary>
        public DayPart DayPart { get; private set; } = DayPart.Day;

        /// <summary>全局輪次計數，不因日夜切換而歸零。</summary>
        public int RoundIndex { get; private set; } = 1;

        /// <summary>
        /// 推進到下一輪，同時在白晝/黑夜間交替。
        /// 從黑夜轉回白晝時視為跨過一天，因此天數加一。
        /// </summary>
        public void NextRound()
        {
            RoundIndex++;
            DayPart = DayPart == DayPart.Day ? DayPart.Night : DayPart.Day;
            if (DayPart == DayPart.Day)
                DayIndex++; // 夜 -> 昼 時天數遞增
        }

        /// <summary>
        /// 檢查劇本是否已達指定的天數上限。
        /// </summary>
        public bool ScenarioEnded(int maxDays) => DayIndex > maxDays;
    }
}

