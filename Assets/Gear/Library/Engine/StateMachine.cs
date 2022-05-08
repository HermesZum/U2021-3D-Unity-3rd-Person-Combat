using UnityEngine;

namespace Gear.Library.Engine
{
    /// <summary>
    /// It's an abstract class that holds a reference to a state and calls the Enter, Tick and Exit methods on it.
    /// The abstract keyword means that this class cannot be instantiated. It can only be inherited.
    /// </summary>
    public abstract class StateMachine : MonoBehaviour
    {
        /* It's a property of the StateMachine current state. It's a way to access a variable from outside the class. */
        public State CurrentState { get; private set; }

        /// <summary>
        /// It switches the current state to the new state
        /// </summary>
        /// <param name="newState"></param>
        public void SwitchState(State newState)
        {
            /* It's a null-conditional operator. It's the same as writing `if (CurrentState != null) CurrentState.Exit();` */
            CurrentState?.Exit();
            /* It's assigning the new state to the current state. */
            CurrentState = newState;
            /* It's calling the Enter method on the current state with a null check. */
            CurrentState?.Enter();
        }

        /// <summary>
        /// If the current state is not null, call the Tick function on it, passing in the delta time
        /// </summary>
        private void Update()
        {
            /* It's calling the Tick function on the current state with a null check. */
            CurrentState?.Tick(Time.deltaTime);
        }
    }
}
