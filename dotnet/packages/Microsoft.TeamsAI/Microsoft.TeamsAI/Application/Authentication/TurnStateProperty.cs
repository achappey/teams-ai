using Microsoft.Bot.Builder;
using Microsoft.Teams.AI.State;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Bot.Builder.Dialogs;

namespace Microsoft.Teams.AI
{
    /// <summary>
    /// Maps the turn state property to a bot State property.
    /// Note: Used to inject data into <seealso cref="DialogSet"/>.
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    internal class TurnStateProperty<TState> : IStatePropertyAccessor<TState>
        where TState : new()
    {
        private readonly string _propertyName;
        private readonly TurnStateEntry _state;

        public TurnStateProperty(TurnState state, string scopeName, string propertyName)
        {
            this._propertyName = propertyName;

            TurnStateEntry? scope = state.GetScope(scopeName);
            if (scope == null)
            {
                throw new TeamsAIException($"TurnStateProperty: TurnState missing state scope named {scope}");
            }

            this._state = scope;
        }

        public string Name => throw new NotImplementedException();

        public Task DeleteAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            this._state.Value?.Remove(this._propertyName);
            return Task.CompletedTask;
        }

        public Task<TState> GetAsync(ITurnContext turnContext, Func<TState>? defaultValueFactory = null, CancellationToken cancellationToken = default)
        {
            if (this._state.Value != null)
            {
                if (this._state.Value.TryGetValue(this._propertyName, out TState result))
                {
                    return Task.FromResult(result);
                }
                else
                {
                    if (defaultValueFactory == null)
                    {
                        throw new ArgumentNullException(nameof(defaultValueFactory));
                    }
                    TState defaultValue = defaultValueFactory();
                    if (defaultValue == null)
                    {
                        throw new ArgumentNullException(nameof(defaultValue));
                    }
                    this._state.Value[this._propertyName] = defaultValue;
                    return Task.FromResult(defaultValue);
                }
            }

            throw new TeamsAIException("No state value available");
        }

        public Task SetAsync(ITurnContext turnContext, TState value, CancellationToken cancellationToken = default)
        {
            this._state.Value?.Set(this._propertyName, value);
            return Task.CompletedTask;
        }
    }
}
