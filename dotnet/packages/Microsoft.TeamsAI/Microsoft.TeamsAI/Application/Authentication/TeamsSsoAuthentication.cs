using Microsoft.Bot.Builder;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;

namespace Microsoft.Teams.AI
{
    /// <summary>
    /// Handles authentication based on Teams SSO.
    /// </summary>
    public class TeamsSsoAuthentication<TState> : IAuthentication<TState>
        where TState : TurnState, new()
    {
        private readonly TeamsSsoBotAuthentication<TState>? _botAuth;
        private readonly TeamsSsoMessageExtensionsAuthentication? _messageExtensionsAuth;
        private readonly TeamsSsoSettings _settings;

        /// <summary>
        /// Initialize instance for current class
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="name">The authentication name.</param>
        /// <param name="settings">The settings to initialize the class</param>
        /// <param name="storage">The storage to use.</param>
        public TeamsSsoAuthentication(Application<TState> app, string name, TeamsSsoSettings settings, IStorage? storage = null)
        {
            this._settings = settings;
            this._botAuth = new TeamsSsoBotAuthentication<TState>(app, name, this._settings, storage);
            this._messageExtensionsAuth = new TeamsSsoMessageExtensionsAuthentication(this._settings);
        }

        /// <summary>
        /// Sign in current user
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The sign in response</returns>
        public async Task<string?> SignInUserAsync(ITurnContext context, TState state, CancellationToken cancellationToken = default)
        {
            string token = await this._TryGetUserToken(context);
            return !string.IsNullOrEmpty(token)
                ? token
                : this._botAuth != null && this._botAuth.IsValidActivity(context)
                ? await this._botAuth.AuthenticateAsync(context, state)
                : this._messageExtensionsAuth != null && this._messageExtensionsAuth.IsValidActivity(context)
                ? await this._messageExtensionsAuth.AuthenticateAsync(context)
                : throw new AuthException("Incoming activity is not a valid activity to initiate authentication flow.", AuthExceptionReason.InvalidActivity);
        }

        /// <summary>
        /// Sign out current user
        /// </summary>
        /// <param name="context">The turn context</param>
        /// <param name="state">The turn state</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public async Task SignOutUserAsync(ITurnContext context, TState state, CancellationToken cancellationToken = default)
        {
            string homeAccountId = $"{context.Activity.From.AadObjectId}.{context.Activity.Conversation.TenantId}";

            if (this._settings.MSAL is ILongRunningWebApi oboCca)
            {
                await oboCca.StopLongRunningProcessInWebApiAsync(homeAccountId, cancellationToken);
            }
        }

        /// <summary>
        /// The handler function is called when the user has successfully signed in
        /// </summary>
        /// <param name="handler">The handler function to call when the user has successfully signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public IAuthentication<TState> OnUserSignInSuccess(Func<ITurnContext, TState, Task> handler)
        {
            this._botAuth?.OnUserSignInSuccess(handler);
            return this;
        }

        /// <summary>
        /// The handler function is called when the user sign in flow fails
        /// </summary>
        /// <param name="handler">The handler function to call when the user failed to signed in</param>
        /// <returns>The class itself for chaining purpose</returns>
        public IAuthentication<TState> OnUserSignInFailure(Func<ITurnContext, TState, AuthException, Task> handler)
        {
            this._botAuth?.OnUserSignInFailure(handler);
            return this;
        }

        /// <summary>
        /// Check if the user is signed, if they are then return the token.
        /// </summary>
        /// <param name="context">The turn context.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The token if the user is signed. Otherwise null.</returns>
        public async Task<string?> IsUserSignedInAsync(ITurnContext context, CancellationToken cancellationToken = default)
        {
            string token = await this._TryGetUserToken(context);
            return token == "" ? null : token;
        }

        private async Task<string> _TryGetUserToken(ITurnContext context)
        {
            string homeAccountId = $"{context.Activity.From.AadObjectId}.{context.Activity.Conversation.TenantId}";
            try
            {
                AuthenticationResult result = await ((ILongRunningWebApi)this._settings.MSAL).AcquireTokenInLongRunningProcess(
                    this._settings.Scopes,
                            homeAccountId
                        ).ExecuteAsync();
                return result.AccessToken;
            }
            catch (MsalClientException)
            {
                // Cannot acquire token from cache
            }

            return ""; // Return empty indication no token found in cache
        }
    }
}
