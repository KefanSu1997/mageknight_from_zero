using MK.Logic.Runtime;

namespace MK.Logic.Runtime.Interactions
{
    /// <summary>
    /// 地点交互接口，所有需要玩家交互的地点都需要实现此接口
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// 玩家与地点交互的逻辑
        /// </summary>
        /// <param name="player">执行交互的玩家状态</param>
        /// <param name="parameter">交互参数，根据不同地点可能包含不同信息</param>
        /// <returns>交互是否成功</returns>
        bool Interact(PlayerState player, InteractParameter parameter);

        /// <summary>
        /// 检查玩家是否可以与当前地点进行交互
        /// </summary>
        /// <param name="player">进行检查的玩家状态</param>
        /// <returns>是否可以交互</returns>
        bool CanInteract(PlayerState player);

        /// <summary>
        /// 获取交互的详细描述
        /// </summary>
        /// <returns>交互描述文本</returns>
        string GetInteractionDescription();
    }
}