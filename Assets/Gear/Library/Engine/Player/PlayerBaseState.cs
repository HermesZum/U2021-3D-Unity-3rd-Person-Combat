using UnityEngine;

namespace Gear.Library.Engine.Player
{
    public abstract class PlayerBaseState : State
    {
        protected PlayerBaseState(PlayerStateMachine playerStateMachine)
        {
            PlayerStateMachine = playerStateMachine;
        }

        public PlayerStateMachine PlayerStateMachine { get; private set; }
    }
}
