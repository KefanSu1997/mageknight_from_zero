namespace MK.Logic.Runtime
{
    using MK.Logic.Data;
    using MK.Logic.Core;

    /// <summary>
    /// 运行时的单位状态，与静态 <see cref="UnitCard"/> 对应。
    /// </summary>
    public sealed class UnitState
    {
        /// <summary>卡牌静态信息引用。</summary>
        public UnitCard Card { get; init; } = null!;

        /// <summary>
        /// 建構函式中強制指定卡牌，以取代 C#11 的 <c>required</c> 關鍵字。
        /// </summary>
        public UnitState(UnitCard card)
        {
            Card = card;
        }

        // 預留無參建構，供序列化等情境使用。
        public UnitState() { }

        /// <summary>当前是否处于就绪状态。</summary>
        public bool IsReady { get; private set; } = true;

        /// <summary>已承受的创伤数量。</summary>
        public int Wounds { get; private set; } = 0;

        /// <summary>本次戰鬥臨時獲得的物理抗性。</summary>
        public bool PhysicalResistTemp { get; set; } = false;

        /// <summary>該部隊是否被永久摧毀。</summary>
        public bool IsDestroyed { get; private set; } = false;

        /// <summary>当伤口数达到等级时，单位被视为“受重创”。</summary>
        public bool Fatigued => Wounds >= Card.Level;

        /// <summary>當前狀態枚舉值，便於外部檢查。</summary>
        public UnitStatus Status =>
            Fatigued ? UnitStatus.Fatigued : (IsReady ? UnitStatus.Ready : UnitStatus.Exhausted);
        
        /// <summary>测试兼容属性</summary>
        public bool IsExhausted => Status == UnitStatus.Exhausted || Status == UnitStatus.Fatigued;
        
        /// <summary>测试兼容属性</summary>
        public int Hull => 0; // Placeholder for compatibility
        
        /// <summary>测试兼容属性</summary> 
        public int Force => Card.Level; // Approximate for compatibility

        /// <summary>测试兼容方法</summary>
        public void Restore() => NewRound();

        /// <summary>发动部队能力后横置。</summary>
        public void Exhaust() => IsReady = false;

        /// <summary>接口用：与 Exhaust 含义相同，便于语义化调用。</summary>
        public void UseAbility() => Exhaust();

        /// <summary>若未受重创，则使单位重新就绪。</summary>
        public void Ready()
        {
            if (!Fatigued) IsReady = true;
        }

        /// <summary>
        /// 新回合刷新：若未重创则恢复为就绪状态。
        /// </summary>
        public void NewRound() => Ready();

        /// <summary>给单位造成伤口，返回实际增加的伤口数。</summary>
        public int AddWounds(int count)
        {
            int before = Wounds;
            Wounds += count;
            if (Wounds < 0) Wounds = 0;
            if (Fatigued) IsReady = false;
            return Wounds - before;
        }

        /// <summary>尝试移除一定数量的伤口。</summary>
        public int Heal(int count)
        {
            int before = Wounds;
            Wounds = System.Math.Max(0, Wounds - count);
            if (!Fatigued) IsReady = true;
            return before - Wounds;
        }

        /// <summary>將部隊標記為已被摧毀。</summary>
        public void Destroy()
        {
            IsDestroyed = true;
            IsReady = false;
        }
    }
}
