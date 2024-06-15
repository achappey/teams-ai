using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Teams.AI.AI.Action;
using Microsoft.Teams.AI.AI.Models;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;
using Microsoft.Teams.AI.Utilities;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Files;
using OpenAI.VectorStores;
using System.ClientModel;
using System.Text;
using System.Runtime.CompilerServices;
using System.Text.Json;

// Assistants API is currently in beta and is subject to change.
#pragma warning disable IDE0130 // Namespace does not match folder structure
[assembly: InternalsVisibleTo("Microsoft.Teams.AI.Tests")]
namespace Microsoft.Teams.AI.AI.Planners.Experimental
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// A planner that uses OpenAI's Assistants APIs to generate plans.
    /// </summary>
    public class AssistantsPlanner<TState> : IPlanner<TState>
        where TState : TurnState, IAssistantsState
    {
        private static readonly TimeSpan DEFAULT_POLLING_INTERVAL = TimeSpan.FromSeconds(1);

        private readonly AssistantsPlannerOptions _options;

        private readonly AssistantClient _client;

        private readonly VectorStoreClient _vectorStoreClient;

        private readonly FileClient _fileClient;

        private readonly ILogger _logger;

        /// <summary>
        /// Create new AssistantsPlanner.
        /// </summary>
        /// <param name="options">Options for configuring the AssistantsPlanner.</param>
        /// <param name="loggerFactory">The logger factory instance.</param>
        public AssistantsPlanner(AssistantsPlannerOptions options, ILoggerFactory? loggerFactory = null)
        {
            Verify.ParamNotNull(options);
            Verify.ParamNotNull(options.ApiKey, "AssistantsPlannerOptions.ApiKey");
            Verify.ParamNotNull(options.AssistantId, "AssistantsPlannerOptions.AssistantId");

            this._options = new AssistantsPlannerOptions(options.ApiKey, options.AssistantId)
            {
                Organization = options.Organization,
                PollingInterval = options.PollingInterval ?? DEFAULT_POLLING_INTERVAL
            };

            this._logger = loggerFactory == null ? NullLogger.Instance : loggerFactory.CreateLogger<AssistantsPlanner<TState>>();
            this._client = _CreateClient(options.ApiKey);
            this._vectorStoreClient = new VectorStoreClient(options.ApiKey);
            this._fileClient = new FileClient(options.ApiKey);
        }

        /// <summary>
        /// Static helper method for programmatically creating an assistant.
        /// </summary>
        /// <param name="apiKey">OpenAI API key.</param>
        /// <param name="model">OpenAI model.</param>
        /// <param name="request">Definition of the assistant to create.</param>
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The created assistant.</returns>
        public static async Task<Assistant> CreateAssistantAsync(string apiKey, string model, AssistantCreationOptions request)
        {
            Verify.ParamNotNull(apiKey);
            Verify.ParamNotNull(request);

            AssistantClient client = _CreateClient(apiKey);

            return await client.CreateAssistantAsync(model, request);
        }

        /// <inheritdoc/>
        public async Task<Plan> BeginTaskAsync(ITurnContext turnContext, TState turnState, AI<TState> ai, CancellationToken cancellationToken)
        {
            Verify.ParamNotNull(turnContext);
            Verify.ParamNotNull(turnState);
            Verify.ParamNotNull(ai);
            return await ContinueTaskAsync(turnContext, turnState, ai, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Plan> ContinueTaskAsync(ITurnContext turnContext, TState turnState, AI<TState> ai, CancellationToken cancellationToken)
        {
            Verify.ParamNotNull(turnContext);
            Verify.ParamNotNull(turnState);
            Verify.ParamNotNull(ai);

            // Create a new thread if we don't have one already
            string threadId = await _EnsureThreadCreatedAsync(turnState);

            // Add the users input to the thread or send tool outputs
            if (turnState.SubmitToolOutputs)
            {
                // Send the tool output to the assistant
                return await _SubmitActionResultsAsync(turnContext, turnState, cancellationToken);
            }
            else
            {
                // Wait for any current runs to complete since you can't add messages or start new runs
                // if there's already one in progress
                await _BlockOnInProgressRunsAsync(threadId, cancellationToken);

                // Submit user input
                return await _SubmitUserInputAsync(turnContext, turnState, cancellationToken);
            }
        }

        private async Task<string> _EnsureThreadCreatedAsync(TState state)
        {
            if (state.ThreadId == null)
            {
                AssistantThread thread = await _client.CreateThreadAsync(new ThreadCreationOptions());
                state.ThreadId = thread.Id;
            }

            return state.ThreadId;
        }

        private bool _IsRunCompleted(ThreadRun run)
        {
            return run.Status.IsTerminal;
        }

        private async Task<ThreadRun> _WaitForRunAsync(string threadId, string runId, bool handleActions, CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay((TimeSpan)this._options.PollingInterval!, cancellationToken);

                ClientResult<ThreadRun> run = await this._client.GetRunAsync(threadId, runId);

                if ((run.Value.Status == RunStatus.RequiresAction && handleActions) || run.Value.Status.IsTerminal)
                {
                    return run.Value;
                }
            }
        }

        private async Task _BlockOnInProgressRunsAsync(string threadId, CancellationToken cancellationToken)
        {
            // Loop until the last run is completed
            while (true)
            {
                AsyncPageableCollection<ThreadRun> runs = _client.GetRunsAsync(threadId, ListOrder.NewestFirst);
                List<ThreadRun> allRuns = [];
                await foreach (ThreadRun threadRun in runs)
                {
                    allRuns.Add(threadRun);
                }

                if (allRuns == null || allRuns.Count() == 0)
                {
                    return;
                }

                ThreadRun? lastRun = allRuns.ElementAt(0);
                if (lastRun == null || _IsRunCompleted(lastRun))
                {
                    return;
                }

                // Wait for the current run to complete and then loop to see if there's already a new run.
                await _WaitForRunAsync(threadId, lastRun.Id, false, cancellationToken);
            }
        }

        private async Task<Plan> _GeneratePlanFromMessagesAsync(string threadId, TState state)
        {
            AsyncPageableCollection<ThreadMessage> messageResponse = this._client.GetMessagesAsync(threadId, ListOrder.NewestFirst);

            List<ThreadMessage> newMessages = new();
            await foreach (ThreadMessage message in messageResponse)
            {
                if (string.Equals(message.Id, state.LastMessageId))
                {
                    break;
                }
                else
                {
                    if (message.Role == MessageRole.Assistant)
                    {
                        newMessages.Add(message);
                    }
                }
            }

            state.LastMessageId = newMessages.FirstOrDefault()?.Id;
            newMessages.Reverse();

            Plan plan = new();

            foreach (ThreadMessage message in newMessages)
            {
                IEnumerable<MessageContent> textMessageContentItems = message.Content.Where(a => !string.IsNullOrEmpty(a.Text));

                foreach (MessageContent content in textMessageContentItems)
                {
                    MessageContext context = new();
                    Dictionary<string, OpenAIFileInfo> files = new();

                    foreach (IGrouping<string, TextAnnotation>? annotation in content.TextAnnotations.GroupBy(t => t.InputFileId ?? t.OutputFileId) ?? [])
                    {
                        ClientResult<OpenAIFileInfo> file = await this._fileClient.GetFileAsync(annotation.Key);

                        files.Add(file.Value.Id, file);
                    }

                    foreach (TextAnnotation annotation in content.TextAnnotations.Where(a => a.InputFileId != null))
                    {
                        context.Citations.Add(new($"{annotation.StartIndex}-{annotation.EndIndex}", annotation.TextToReplace, files[annotation.InputFileId].Filename));
                    }

                    foreach (TextAnnotation annotation in content.TextAnnotations.Where(a => a.OutputFileId != null))
                    {
                        context.Citations.Add(new(!string.IsNullOrEmpty(annotation.TextToReplace) ?
                              $"{annotation.StartIndex}-{annotation.EndIndex}: {annotation.TextToReplace}"
                              : $"{annotation.StartIndex}-{annotation.EndIndex}", annotation.TextToReplace, $"{annotation.TextToReplace}?file_id={annotation.OutputFileId}"));
                    }

                    plan.Commands.Add(new PredictedSayCommand(new ChatMessage(ChatRole.Assistant)
                    {
                        Content = content.Text ?? string.Empty,
                        Context = context
                    }));
                }

                foreach (MessageContent content in message.Content.Where(a => !string.IsNullOrEmpty(a.ImageFileId)))
                {
                    OpenAIFileInfo file = await this._fileClient.GetFileAsync(content.ImageFileId);
                    ClientResult<BinaryData> fileContent = await this._fileClient.DownloadFileAsync(content.ImageFileId);

                    plan.Commands.Insert(0, new PredictedDoCommand("image_file",
                             new Dictionary<string, object?>() {
                                    {"file_id", content.ImageFileId},
                                    {"filename", file.Filename},
                                    {"fileContent", fileContent.Value.ToArray()}
                        }));
                }
            }

            return plan;
        }

        private Plan _GeneratePlanFromTools(TState state, IReadOnlyList<RequiredAction> requiredActions)
        {
            Plan plan = new();
            Dictionary<string, List<string>> toolMap = new();
            foreach (RequiredAction toolCall in requiredActions)
            {
                if (!toolMap.ContainsKey(toolCall.FunctionName))
                {
                    toolMap[toolCall.FunctionName] = new List<string>();
                }
                toolMap[toolCall.FunctionName].Add(toolCall.ToolCallId);
                plan.Commands.Add(new PredictedDoCommand
                (
                    toolCall.FunctionName,
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.FunctionArguments)
                    ?? new Dictionary<string, object?>()
                )
                { ToolCallId = toolCall.ToolCallId });
            }
            state.SubmitToolMap = toolMap;
            return plan;
        }

        private async Task<Plan> _SubmitActionResultsAsync(ITurnContext turnContext, TState state, CancellationToken cancellationToken)
        {
            // Map the action outputs to tool outputs
            List<ToolOutput> toolOutputs = new();
            Dictionary<string, List<string>> toolMap = state.SubmitToolMap;
            foreach (KeyValuePair<string, List<string>> requiredAction in toolMap)
            {
                foreach (string value in requiredAction.Value)
                {
                    toolOutputs.Add(new()
                    {
                        ToolCallId = value,
                        Output = state.Temp!.ActionOutputs.ContainsKey(value) ? state.Temp!.ActionOutputs[value]
                            : state.Temp!.ActionOutputs.ContainsKey(requiredAction.Key) ? state.Temp!.ActionOutputs[requiredAction.Key]
                            : string.Empty
                    });
                }
            }

            ThreadRun? run;

            if (state.Streaming)
            {
                run = await _CreateSubmitOutputToolsStream(turnContext, state, state.ThreadId!, state.RunId!, toolOutputs, cancellationToken);
            }
            else
            {
                ClientResult<ThreadRun> submitResult = await _client.SubmitToolOutputsToRunAsync(state.ThreadId!, state.RunId!, toolOutputs);
                run = submitResult.Value;
                run = await _WaitForRunAsync(state.ThreadId!, run.Id, true, cancellationToken);
            }

            while (run?.Status == RunStatus.InProgress)
            {
                run = await _WaitForRunAsync(state.ThreadId!, run.Id, true, cancellationToken);
            }

            if (run!.Status == RunStatus.RequiresAction)
            {
                if (run.RequiredActions == null)
                {
                    return new Plan();
                }

                state.SubmitToolOutputs = true;

                return _GeneratePlanFromTools(state, run.RequiredActions);
            }
            else if (run.Status == RunStatus.Completed)
            {
                state.SubmitToolOutputs = false;
                return await _GeneratePlanFromMessagesAsync(state.ThreadId!, state);
            }
            else if (run.Status == RunStatus.Cancelled)
            {
                return new Plan();
            }
            else if (run.Status == RunStatus.Expired)
            {
                return new Plan(new() { new PredictedDoCommand(AIConstants.TooManyStepsActionName) });
            }
            else
            {
                throw new TeamsAIException($"Run failed {run.Status}. ErrorCode: {run.LastError?.Code}. ErrorMessage: {run.LastError?.Message}");
            }
        }

        private Task<ThreadRun?> _CreateRunStream(ITurnContext turnContext, TState state, string threadId, string assistantId, RunCreationOptions runCreationOptions, CancellationToken cancellationToken)
        {
            AsyncResultCollection<StreamingUpdate> eventStream = this._client.CreateRunStreamingAsync(threadId, assistantId, runCreationOptions);

            return ProcessEventStreamAsync(eventStream, turnContext, state, cancellationToken);
        }

        private Task<ThreadRun?> _CreateSubmitOutputToolsStream(ITurnContext turnContext, TState state, string threadId, string runId, IEnumerable<ToolOutput> toolOutputs, CancellationToken cancellationToken)
        {
            AsyncResultCollection<StreamingUpdate> eventStream = this._client.SubmitToolOutputsToRunStreamingAsync(threadId, runId, toolOutputs);

            return ProcessEventStreamAsync(eventStream, turnContext, state, cancellationToken);
        }

        private async Task<ThreadRun?> ProcessEventStreamAsync(
            AsyncResultCollection<StreamingUpdate> eventStream,
            ITurnContext turnContext,
            TState state,
            CancellationToken cancellationToken)
        {
            ThreadRun? run = null;
            StringBuilder messageBuilder = new();
            string? itemId = null;
            int newCharsSinceLastUpdate = 0;
            AIEntity entity = new();

            Activity sendMessageActivity = new()
            {
                Type = ActivityTypes.Message,
                ChannelData = new
                {
                    feedbackLoopEnabled = true
                },
                Entities = new List<Entity>() { entity }
            };

            async Task SendMessageOrUpdateActivityAsync()
            {
                string messageText = messageBuilder.ToString();

                if (messageText.Contains("\n"))
                {
                    messageText = messageText.Replace("\n", "<br>");
                }

                sendMessageActivity.Text = messageText;

                if (itemId != null)
                {
                    sendMessageActivity.Id = itemId;

                    await turnContext.UpdateActivityAsync(sendMessageActivity);
                }
                else
                {
                    ResourceResponse response = await turnContext.SendActivityAsync(sendMessageActivity);
                    itemId = response?.Id;
                }

                newCharsSinceLastUpdate = 0;
            }

            await foreach (StreamingUpdate? streamingUpdate in eventStream.WithCancellation(cancellationToken))
            {
                if (streamingUpdate is MessageContentUpdate contentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentUpdate.Text))
                    {
                        messageBuilder.Append(contentUpdate.Text);
                        newCharsSinceLastUpdate += contentUpdate.Text.Length;

                        if (newCharsSinceLastUpdate >= 100)
                        {
                            await SendMessageOrUpdateActivityAsync();
                        }
                    }
                }
                else if (streamingUpdate is RunUpdate runUpdate)
                {
                    run = runUpdate.Value;
                }
            }


            if (newCharsSinceLastUpdate > 0)
            {
                await SendMessageOrUpdateActivityAsync();
            }

            state.Temp.LastStreamedReplyId = itemId ?? string.Empty;

            return run;
        }

        private async Task<Plan> _SubmitUserInputAsync(ITurnContext turnContext, TState state, CancellationToken cancellationToken)
        {
            string threadId = await this._EnsureThreadCreatedAsync(state);

            List<ThreadInitializationMessage> input = new([
                new ThreadInitializationMessage(
                    [state.Temp?.Input ?? string.Empty,
                    .. state.ImageFileIds.Select(a => MessageContent.FromImageFileId(a))
                    ])
                ]);

            ToolConstraint? toolchoice = null;

            if (!string.IsNullOrEmpty(state.ToolChoice))
            {
                switch (state.ToolChoice)
                {
                    case "file_search":
                        toolchoice = new(ToolDefinition.CreateFileSearch());
                        break;
                    case "code_interpreter":
                        toolchoice = new(ToolDefinition.CreateCodeInterpreter());
                        break;
                    default:
                        toolchoice = new(ToolDefinition.CreateFunction(state.ToolChoice));
                        break;
                }
            }

            RunCreationOptions runCreateParams =
             new()
             {
                 ModelOverride = !string.IsNullOrEmpty(state.Model) ? state.Model : null,
                 Temperature = (float?)state.Temperature,
                 ToolConstraint = toolchoice,
                 NucleusSamplingFactor = (float?)state.TopP,
                 ParallelToolCalls = state.ParallelToolCalls,
                 AdditionalInstructions = state.Temp?.AdditionalInstructions,
                 TruncationStrategy = state.TruncationStrategy != "auto"
                        ? RunTruncationStrategy.CreateLastMessagesStrategy(state.TruncationStrategyLastNMessages)
                        : null
             };

            foreach (ThreadInitializationMessage message in input)
            {
                runCreateParams.AdditionalMessages.Add(message);
            }

            foreach (KeyValuePair<string, ToolDefinition> tool in state.ToolDefinitions)
            {
                runCreateParams.ToolsOverride.Add(tool.Value);
            }

            ThreadRun? run;
            string assistantId = !string.IsNullOrEmpty(state.AssistantId) ? state.AssistantId! : this._options.AssistantId;

            if (state.Streaming && !state.DisableOutput)
            {
                run = await _CreateRunStream(turnContext, state, threadId,
                    assistantId,
                    runCreateParams, cancellationToken);

                state.ThreadId = threadId;
                state.RunId = run?.Id;
            }
            else
            {
                run = await _client.CreateRunAsync(threadId, assistantId, runCreateParams);

                // Update state and wait for the run to complete
                state.ThreadId = threadId;
                state.RunId = run.Id;

                run = await _WaitForRunAsync(threadId, run.Id, true, cancellationToken);
            }

            while (run?.Status == RunStatus.InProgress)
            {
                run = await _WaitForRunAsync(threadId, run.Id, true, cancellationToken);
            }

            if (run?.Status == RunStatus.RequiresAction)
            {
                if (run.RequiredActions == null)
                {
                    return new();
                }

                state.SubmitToolOutputs = true;

                return _GeneratePlanFromTools(state, run.RequiredActions);
            }
            else if (run?.Status == RunStatus.Completed)
            {
                state.SubmitToolOutputs = false;
                return await _GeneratePlanFromMessagesAsync(state.ThreadId!, state);
            }
            else if (run?.Status == RunStatus.Cancelled)
            {
                return new();
            }
            else if (run?.Status == RunStatus.Expired)
            {
                return new(new() { new PredictedDoCommand(AIConstants.TooManyStepsActionName) });
            }
            else
            {
                throw new TeamsAIException($"Run failed {run?.Status}. ErrorCode: {run?.LastError?.Code}. ErrorMessage: {run?.LastError?.Message}");
            }
        }

        internal static AssistantClient _CreateClient(string apiKey, string? endpoint = null)
        {
            Verify.ParamNotNull(apiKey);

            if (endpoint != null)
            {
                // Azure OpenAI
                return new(apiKey, new()
                {
                    // Endpoint = new Uri(endpoint)
                });
            }
            else
            {
                // OpenAI
                return new AssistantClient(apiKey);
            }
        }

    }
}
