using Microsoft.Teams.AI.State;
using System.Runtime.CompilerServices;

// For Unit Testing
[assembly: InternalsVisibleTo("Microsoft.Teams.AI.Tests")]
namespace Microsoft.Teams.AI.AI.Action
{
    internal class ActionCollection<TState> : IActionCollection<TState> where TState : TurnState
    {
        private readonly Dictionary<string, ActionEntry<TState>> _actions;

        public ActionCollection()
        {
            this._actions = new Dictionary<string, ActionEntry<TState>>();
        }

        /// <inheritdoc />
        public ActionEntry<TState> this[string actionName]
        {
            get
            {
                return !this._actions.ContainsKey(actionName)
                    ? throw new ArgumentException($"`{actionName}` action does not exist")
                    : this._actions[actionName];
            }
        }

        /// <inheritdoc />
        public void AddAction(string actionName, IActionHandler<TState> handler, bool allowOverrides = false)
        {
            if (this._actions.ContainsKey(actionName))
            {
                if (!this._actions[actionName].AllowOverrides)
                {
                    throw new ArgumentException($"Action {actionName} already exists and does not allow overrides");
                }
            }
            this._actions[actionName] = new ActionEntry<TState>(actionName, handler, allowOverrides);
        }

        /// <inheritdoc />
        public bool ContainsAction(string actionName)
        {
            return this._actions.ContainsKey(actionName);
        }

        public bool TryGetAction(string actionName, out ActionEntry<TState> actionEntry)
        {
            return this._actions.TryGetValue(actionName, out actionEntry);
        }
    }
}
