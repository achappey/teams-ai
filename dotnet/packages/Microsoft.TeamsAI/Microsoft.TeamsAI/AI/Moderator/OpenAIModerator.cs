using Microsoft.Teams.AI.AI.Planners;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;
using Microsoft.Bot.Builder;
using OpenAI.Moderations;

namespace Microsoft.Teams.AI.AI.Moderator
{
    /// <summary>
    /// An moderator that uses OpenAI's moderation API.
    /// </summary>
    /// <typeparam name="TState">The turn state class.</typeparam>
    public class OpenAIModerator<TState> : IModerator<TState> where TState : TurnState
    {
        private readonly OpenAIModeratorOptions _options;
        private readonly ModerationClient _client;

        /// <summary>
        /// Constructs an instance of the moderator.
        /// </summary>
        /// <param name="options">Options to configure the moderator</param>
        public OpenAIModerator(OpenAIModeratorOptions options)
        {
            _options = options;

            this._client = new ModerationClient(options.Model, options.ApiKey);
        }

        /// <inheritdoc />
        public async Task<Plan?> ReviewInputAsync(ITurnContext turnContext, TState turnState, CancellationToken cancellationToken = default)
        {
            switch (_options.Moderate)
            {
                case ModerationType.Input:
                case ModerationType.Both:
                {
                    string input = turnState.Temp?.Input ?? turnContext.Activity.Text;

                    return await _HandleTextModerationAsync(input, true);
                }
                default:
                    break;
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<Plan> ReviewOutputAsync(ITurnContext turnContext, TState turnState, Plan plan, CancellationToken cancellationToken = default)
        {
            switch (_options.Moderate)
            {
                case ModerationType.Output:
                case ModerationType.Both:
                {
                    foreach (IPredictedCommand command in plan.Commands)
                    {
                        if (command is PredictedSayCommand sayCommand)
                        {
                            string output = sayCommand.Response.GetContent<string>();

                            // If plan is flagged it will be replaced
                            Plan? newPlan = await _HandleTextModerationAsync(output, false);

                            return newPlan ?? plan;
                        }
                    }

                    break;
                }
                default:
                    break;
            }

            return plan;
        }

        private async Task<Plan?> _HandleTextModerationAsync(string text, bool isModelInput)
        {
            try
            {
                System.ClientModel.ClientResult<OpenAI.Moderations.ModerationResult> response = await _client.ClassifyTextInputAsync(text);

                if (response.Value != null)
                {
                    if (response.Value.Flagged)
                    {
                        string actionName = isModelInput ? AIConstants.FlaggedInputActionName : AIConstants.FlaggedOutputActionName;

                        // Flagged
                        return new Plan()
                        {
                            Commands = new List<IPredictedCommand>
                            {
                                new PredictedDoCommand(actionName, new Dictionary<string, object?>
                                {
                                    { "Result", response.Value }
                                })
                            }
                        };
                    }
                }

                return null;

            }
            catch (HttpOperationException e)
            {
                // Http error
                if (e.StatusCode != null && (int)e.StatusCode == 429)
                {
                    return new Plan()
                    {
                        Commands = new List<IPredictedCommand>
                        {
                            new PredictedDoCommand(AIConstants.HttpErrorActionName)
                        }
                    };

                }

                throw;
            }
        }
    }
}