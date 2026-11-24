using UnityEngine;
using UnityEngine.Events;

namespace EscapeFromWork.Core
{
    /// <summary>
    /// Parameterless ScriptableObject event. Create via Assets > Create > Events > GameEvent.
    /// Listeners register via AddListener/RemoveListener; call Raise() to invoke all registered callbacks.
    /// </summary>
    [CreateAssetMenu(menuName = "Events/GameEvent", fileName = "NewGameEvent")]
    public class GameEvent : ScriptableObject
    {
        private readonly UnityEvent _event = new UnityEvent();

        /// <summary>
        /// Invoke all registered listeners.
        /// </summary>
        public void Raise()
        {
            _event.Invoke();
        }

        /// <summary>
        /// Register a listener callback.
        /// </summary>
        public void AddListener(UnityAction callback)
        {
            _event.AddListener(callback);
        }

        /// <summary>
        /// Unregister a listener callback.
        /// </summary>
        public void RemoveListener(UnityAction callback)
        {
            _event.RemoveListener(callback);
        }

        /// <summary>
        /// Remove all registered listeners. Useful when exiting play mode or
        /// transitioning between scenes to prevent stale listener leaks.
        /// </summary>
        public void ClearAllListeners()
        {
            _event.RemoveAllListeners();
        }
    }

    /// <summary>
    /// Generic typed ScriptableObject event. Unity cannot instantiate generic ScriptableObjects directly,
    /// so this class is abstract. Create concrete subclasses (e.g. IntEvent, FloatEvent) with their own
    /// [CreateAssetMenu] attributes.
    /// </summary>
    public abstract class GameEvent<T> : ScriptableObject
    {
        private readonly UnityEvent<T> _event = new UnityEvent<T>();

        /// <summary>
        /// Invoke all registered listeners with the given payload.
        /// </summary>
        public void Raise(T value)
        {
            _event.Invoke(value);
        }

        /// <summary>
        /// Register a listener callback that receives the event payload.
        /// </summary>
        public void AddListener(UnityAction<T> callback)
        {
            _event.AddListener(callback);
        }

        /// <summary>
        /// Unregister a listener callback.
        /// </summary>
        public void RemoveListener(UnityAction<T> callback)
        {
            _event.RemoveListener(callback);
        }

        /// <summary>
        /// Remove all registered listeners. Useful when exiting play mode or
        /// transitioning between scenes to prevent stale listener leaks.
        /// </summary>
        public void ClearAllListeners()
        {
            _event.RemoveAllListeners();
        }
    }

    // ---------------------------------------------------------------------------
    // Concrete typed event assets — one per payload type needed by the project.
    // Each has its own CreateAssetMenu so it appears in the Assets > Create > Events menu.
    // ---------------------------------------------------------------------------

    [CreateAssetMenu(menuName = "Events/IntEvent", fileName = "NewIntEvent")]
    public class IntEvent : GameEvent<int> { }

    [CreateAssetMenu(menuName = "Events/FloatEvent", fileName = "NewFloatEvent")]
    public class FloatEvent : GameEvent<float> { }

    [CreateAssetMenu(menuName = "Events/BoolEvent", fileName = "NewBoolEvent")]
    public class BoolEvent : GameEvent<bool> { }

    [CreateAssetMenu(menuName = "Events/StringEvent", fileName = "NewStringEvent")]
    public class StringEvent : GameEvent<string> { }

    [CreateAssetMenu(menuName = "Events/DeathContextEvent", fileName = "NewDeathContextEvent")]
    public class DeathContextEvent : GameEvent<DeathContext> { }
}
