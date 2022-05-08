namespace Gear.Library.Engine
{
    /// <summary>
    /// State is an abstract class that defines the Enter, Tick, and Exit methods.
    /// The abstract keyword means that this class cannot be instantiated. It can only be inherited.
    /// The abstract keyword also means that any class that inherits from State 
    /// must implement the Enter, Tick, and Exit methods.
    /// </summary>
    public abstract class State
    {
        /// <summary>
        /// This function is called when the state is entered.
        /// </summary>
        public abstract void Enter();
        
        /// <summary>
        /// Tick is called every frame
        /// </summary>
        /// <param name="deltaTime">The time in seconds since the last frame.</param>
        public abstract void Tick(float deltaTime);
        
        /// <summary>
        /// This function is called when the state is exited
        /// </summary>
        public abstract void Exit();
    }
}
