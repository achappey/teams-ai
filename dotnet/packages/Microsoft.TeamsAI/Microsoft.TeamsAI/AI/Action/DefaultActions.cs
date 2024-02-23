using Microsoft.Teams.AI.AI.Planners;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;
using Microsoft.Teams.AI.Utilities;
using Microsoft.Bot.Connector;
using Microsoft.Extensions.Logging;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging.Abstractions;
using AdaptiveCards;
using Microsoft.Bot.Schema;

namespace Microsoft.Teams.AI.AI.Action
{
    internal class DefaultActions<TState> where TState : TurnState
    {
        private readonly ILogger _logger;

        public DefaultActions(ILoggerFactory? loggerFactory = null)
        {
            this._logger = loggerFactory is null ? NullLogger.Instance : loggerFactory.CreateLogger(typeof(DefaultActions<TState>));
        }

        [Action(AIConstants.UnknownActionName, isDefault: true)]
        public Task<string> UnknownAction([ActionName] string action)
        {
            this._logger.LogError($"An AI action named \"{action}\" was predicted but no handler was registered");
            return Task.FromResult(AIConstants.StopCommand);
        }

        [Action(AIConstants.FlaggedInputActionName, isDefault: true)]
        public Task<string> FlaggedInputAction()
        {
            this._logger.LogError($"The users input has been moderated but no handler was registered for {AIConstants.FlaggedInputActionName}");
            return Task.FromResult(AIConstants.StopCommand);
        }

        [Action(AIConstants.FlaggedOutputActionName, isDefault: true)]
        public Task<string> FlaggedOutputAction()
        {
            this._logger.LogError($"The bots output has been moderated but no handler was registered for {AIConstants.FlaggedOutputActionName}");
            return Task.FromResult(AIConstants.StopCommand);
        }

        [Action(AIConstants.HttpErrorActionName, isDefault: true)]
        public Task<string> HttpErrorAction()
        {
            throw new TeamsAIException("An AI http request failed");
        }

        [Action(AIConstants.PlanReadyActionName, isDefault: true)]
        public Task<string> PlanReadyAction([ActionParameters] Plan plan)
        {
            Verify.ParamNotNull(plan);

            return Task.FromResult(plan.Commands.Count > 0 ? string.Empty : AIConstants.StopCommand);
        }

        [Action(AIConstants.DoCommandActionName, isDefault: true)]
        public async Task<string> DoCommandAsync([ActionTurnContext] ITurnContext turnContext, [ActionTurnState] TState turnState, [ActionParameters] DoCommandActionData<TState> doCommandActionData, CancellationToken cancellationToken = default)
        {
            Verify.ParamNotNull(doCommandActionData);

            if (doCommandActionData.Handler == null)
            {
                throw new ArgumentException("Unexpected `data` object: Handler does not exist");
            }

            if (doCommandActionData.PredictedDoCommand == null)
            {
                throw new ArgumentException("Unexpected `data` object: PredictedDoCommand does not exist");
            }

            IActionHandler<TState> handler = doCommandActionData.Handler;

            return await handler.PerformActionAsync(turnContext, turnState, doCommandActionData.PredictedDoCommand.Parameters, doCommandActionData.PredictedDoCommand.Action, cancellationToken);
        }

        [Action(AIConstants.SayCommandActionName, isDefault: true)]
        public async Task<string> SayCommandAsync([ActionTurnContext] ITurnContext turnContext, [ActionParameters] PredictedSayCommand command, CancellationToken cancellationToken = default)
        {
            Verify.ParamNotNull(command);
            Verify.ParamNotNull(command.Response);

            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                await turnContext.SendActivityAsync(command.Response.Replace("\n", "<br>"), null, null, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(command.Response, null, null, cancellationToken);
            };

            return string.Empty;
        }

        [Action(AIConstants.TooManyStepsActionName, isDefault: true)]
        public Task<string> TooManyStepsAction([ActionParameters] TooManyStepsParameters parameters)
        {
            if (parameters.StepCount > parameters.MaxSteps)
            {
                throw new TeamsAIException("The AI system has exceeded the maximum number of steps allowed.");
            }
            else
            {
                throw new TeamsAIException("The AI system has exceeded the maximum amount of time allowed.");
            }
        }

        [Action("file_citation", isDefault: true)]
        public async Task<string> DisplayCitation([ActionTurnContext] ITurnContext turnContext, [ActionParameters] Dictionary<string, object> parameters)
        {
            AdaptiveCard card = new(new AdaptiveSchemaVersion(1, 5));

            card.Body.Add(new AdaptiveTextBlock
            {
                Text = parameters["text"].ToString(),
                Weight = AdaptiveTextWeight.Bolder,
                Size = AdaptiveTextSize.Medium
            });

            AdaptiveFactSet factSet = new();

            factSet.Facts.Add(new AdaptiveFact("Filename", parameters["filename"].ToString()));
            factSet.Facts.Add(new AdaptiveFact("Ranges", parameters["ranges"].ToString()));

            card.Body.Add(factSet);

            string quote = parameters["quote"].ToString();

            if (!string.IsNullOrEmpty(quote))
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"Quote",
                    Size = AdaptiveTextSize.Default,
                    Weight = AdaptiveTextWeight.Bolder
                });

                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = parameters["quote"].ToString(),
                    Wrap = true
                });
            }

            Attachment attachment = new()
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card
            };

            IMessageActivity reply = MessageFactory.Attachment(attachment);
            await turnContext.SendActivityAsync(reply);

            return string.Empty;
        }

        [Action("file_path", isDefault: true)]
        public async Task<string> DownloadFile([ActionTurnContext] ITurnContext turnContext, [ActionParameters] Dictionary<string, object> parameters)
        {
            // Create a new Adaptive Card
            AdaptiveCard card = new(new AdaptiveSchemaVersion(1, 5));

            // Add a text block to the card
            card.Body.Add(new AdaptiveTextBlock
            {
                Text = parameters["filename"].ToString(),
                Weight = AdaptiveTextWeight.Bolder,
                Size = AdaptiveTextSize.Medium
            });

            // Create a fact set and add it to the card
            AdaptiveFactSet factSet = new();
            factSet.Facts.Add(new AdaptiveFact("Start index", parameters["start_index"].ToString()));
            factSet.Facts.Add(new AdaptiveFact("End index", parameters["end_index"].ToString()));
            card.Body.Add(factSet);

            IMessageActivity reply = MessageFactory.Attachment(new Attachment()
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = card
            });

            // Check if file content is not null
            if (parameters["fileContent"] is byte[] fileContent)
            {
                // Convert the byte array to a stream
                string base64File = Convert.ToBase64String(fileContent);

                // Assuming 'filename' is the name of the file to download
                string filename = parameters["filename"].ToString();
                string contentUrl = $"data:application/octet-stream;base64,{base64File}";

                // Create an attachment from the stream
                Attachment fileAttachment = new()
                {
                    ContentType = "application/octet-stream",
                    ContentUrl = contentUrl,
                    Name = filename
                };

                // Attach the file to the message activity
                reply.Attachments.Add(fileAttachment);
            }

            // Send the message activity with the Adaptive Card and the file attachment
            await turnContext.SendActivityAsync(reply);

            return string.Empty;
        }

        [Action("image_file", isDefault: true)]
        public async Task<string> DisplayImageFile([ActionTurnContext] ITurnContext turnContext, [ActionParameters] Dictionary<string, object> parameters)
        {
            // Check if file content is not null
            if (parameters["fileContent"] is byte[] fileContent)
            {
                // Convert the byte array to a Base64 string
                string base64Image = Convert.ToBase64String(fileContent);

                // Create a new message with the image
                IMessageActivity imageMessage = MessageFactory.Text(null);
                imageMessage.Attachments = new List<Attachment>
                {
                    new() {
                        ContentType = "image/png",
                        ContentUrl = $"data:image/png;base64,{base64Image}"
                    }
                };

                // Send the image message
                await turnContext.SendActivityAsync(imageMessage);
            }
            return string.Empty;
        }
    }
}
