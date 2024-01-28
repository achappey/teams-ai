﻿using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.AI.AI.OpenAI;
using Microsoft.Teams.AI.AI.OpenAI.Models;
using Microsoft.Teams.AI.Exceptions;
using Microsoft.Teams.AI.State;
using Microsoft.Teams.AI.Utilities;
using System.Text.Json;

// Assistants API is currently in beta and is subject to change.
#pragma warning disable IDE0130 // Namespace does not match folder structure
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
        private readonly OpenAIClient _openAIClient;

        /// <summary>
        /// Static helper method for programmatically creating an assistant.
        /// </summary>
        /// <param name="apiKey">OpenAI API key.</param>
        /// <param name="organization">OpenAI organization.</param>
        /// <param name="request">Definition of the assistant to create.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The created assistant.</returns>
        public static async Task<Assistant> CreateAssistantAsync(string apiKey, string? organization, AssistantCreateParams request, CancellationToken cancellationToken = default)
        {
            Verify.ParamNotNull(apiKey);
            Verify.ParamNotNull(request);

            OpenAIClient client = new(new OpenAIClientOptions(apiKey)
            {
                Organization = organization
            });

            return await client.CreateAssistantAsync(request, cancellationToken);
        }

        /// <summary>
        /// Create new AssistantsPlanner.
        /// </summary>
        /// <param name="options">Options for configuring the AssistantsPlanner.</param>
        /// <param name="loggerFactory">The logger factory instance.</param>
        /// <param name="httpClient">HTTP client.</param>
        public AssistantsPlanner(AssistantsPlannerOptions options, ILoggerFactory? loggerFactory = null, HttpClient? httpClient = null)
        {
            Verify.ParamNotNull(options);
            Verify.ParamNotNull(options.ApiKey, "AssistantsPlannerOptions.ApiKey");
            Verify.ParamNotNull(options.AssistantId, "AssistantsPlannerOptions.AssistantId");

            this._options = new AssistantsPlannerOptions(options.ApiKey, options.AssistantId)
            {
                Organization = options.Organization,
                PollingInterval = options.PollingInterval ?? DEFAULT_POLLING_INTERVAL
            };
            this._openAIClient = new OpenAIClient(new OpenAIClientOptions(this._options.ApiKey)
            {
                Organization = this._options.Organization
            },
            loggerFactory,
            httpClient);
        }

        /// <inheritdoc/>
        public async Task<Plan> BeginTaskAsync(ITurnContext turnContext, TState turnState, AI<TState> ai, CancellationToken cancellationToken)
        {
            Verify.ParamNotNull(turnContext);
            Verify.ParamNotNull(turnState);
            Verify.ParamNotNull(ai);
            return await this.ContinueTaskAsync(turnContext, turnState, ai, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<Plan> ContinueTaskAsync(ITurnContext turnContext, TState turnState, AI<TState> ai, CancellationToken cancellationToken)
        {
            Verify.ParamNotNull(turnContext);
            Verify.ParamNotNull(turnState);
            Verify.ParamNotNull(ai);

            // Create a new thread if we don't have one already
            string threadId = await this._EnsureThreadCreatedAsync(turnState, cancellationToken);

            // Add the users input to the thread or send tool outputs
            if (turnState.SubmitToolOutputs)
            {
                // Send the tool output to the assistant
                return await this._SubmitActionResultsAsync(turnState, cancellationToken);
            }
            else
            {
                // Wait for any current runs to complete since you can't add messages or start new runs
                // if there's already one in progress
                await this._BlockOnInProgressRunsAsync(threadId, cancellationToken);

                // Submit user input
                return await this._SubmitUserInputAsync(turnState, cancellationToken);
            }
        }

        private async Task<string> _EnsureThreadCreatedAsync(TState state, CancellationToken cancellationToken)
        {
            if (state.ThreadId == null)
            {
                OpenAI.Models.Thread thread = await this._openAIClient.CreateThreadAsync(new(), cancellationToken);
                state.ThreadId = thread.Id;
            }

            return state.ThreadId;
        }

        private bool _IsRunCompleted(Run run)
        {
            switch (run.Status)
            {
                case "completed":
                case "failed":
                case "cancelled":
                case "expired":
                    return true;
                default: return false;
            }
        }

        private async Task<Run> _WaitForRunAsync(string threadId, string runId, bool handleActions, CancellationToken cancellationToken)
        {
            while (true)
            {
                await Task.Delay((TimeSpan)this._options.PollingInterval!, cancellationToken);

                Run run = await this._openAIClient.RetrieveRunAsync(threadId, runId, cancellationToken);
                switch (run.Status)
                {
                    case "requires_action":
                        if (handleActions)
                        {
                            return run;
                        }
                        break;
                    case "cancelled":
                    case "failed":
                    case "completed":
                    case "expired":
                        return run;
                    default:
                        break;
                }
            }
        }

        private async Task _BlockOnInProgressRunsAsync(string threadId, CancellationToken cancellationToken)
        {
            // Loop until the last run is completed
            while (true)
            {
                Run? run = await this._openAIClient.RetrieveLastRunAsync(threadId, cancellationToken);
                if (run == null || this._IsRunCompleted(run))
                {
                    return;
                }

                // Wait for the current run to complete and then loop to see if there's already a new run.
                await this._WaitForRunAsync(threadId, run.Id, false, cancellationToken);
            }
        }

        private async Task<Plan> _GeneratePlanFromMessagesAsync(string threadId, string lastMessageId, CancellationToken cancellationToken)
        {


            // Find the new messages
            IAsyncEnumerable<Message> messages = this._openAIClient.ListNewMessagesAsync(threadId, lastMessageId, cancellationToken);
            List<Message> newMessages = new();
            await foreach (Message message in messages.WithCancellation(cancellationToken))
            {
                if (string.Equals(message.Id, lastMessageId))
                {
                    break;
                }
                else
                {
                    newMessages.Add(message);
                }
            }

            // ListMessages return messages in desc, reverse to be in asc order
            newMessages.Reverse();

            // Convert the messages to SAY commands
            Plan plan = new();
            foreach (Message message in newMessages)
            {
                foreach (MessageContent content in message.Content)
                {
                    if (string.Equals(content.Type, "text"))
                    {
                        plan.Commands.Add(new PredictedSayCommand(content.Text?.Value ?? string.Empty));
                    }

                    if (string.Equals(content.Type, "image_file"))
                    {
                        if (content.ImageFile?.FileId != null)
                        {
                            OpenAI.Models.File file = await this._openAIClient.RetrieveFileAsync(content.ImageFile.FileId);
                            byte[] fileContent = await this._openAIClient.RetrieveFileContentAsync(content.ImageFile.FileId);

                            plan.Commands.Add(new PredictedDoCommand(content.Type,
                                     new Dictionary<string, object?>() {
                                    {"file_id", content.ImageFile?.FileId},
                                    {"filename", file.Filename},
                                    {"fileContent", fileContent}
                                         }));
                        }
                    }

                    IEnumerable<TextAnnotation>? citations = content.Text?.Annotations.Where(t => t.Type == "file_citation");
                    IEnumerable<GroupedTextAnnotation>? groupedCitations = citations?.GroupBy(y => y.Text).Select(y => new GroupedTextAnnotation()
                    {
                        Text = y.Key,
                        Ranges = y.Select(z => $"{z.StartIndex}-{z.EndIndex}"),
                        FileCitation = y.FirstOrDefault().FileCitation
                    });

                    if (groupedCitations != null)
                    {
                        foreach (GroupedTextAnnotation annotation in groupedCitations)
                        {
                            if (annotation.FileCitation != null && !string.IsNullOrEmpty(annotation.FileCitation.FileId))
                            {
                                OpenAI.Models.File file = await this._openAIClient.RetrieveFileAsync(annotation.FileCitation.FileId);

                                plan.Commands.Add(new PredictedDoCommand("file_citation",
                                    new Dictionary<string, object?>() {
                                    {"text", annotation.Text},
                                    {"ranges", string.Join(", ", annotation.Ranges)},
                                    {"file_id", annotation.FileCitation?.FileId},
                                    {"filename", file.Filename},
                                    {"quote", annotation.FileCitation?.Quote}
                                        }));
                            }
                        }
                    }

                    IEnumerable<TextAnnotation>? filePaths = content.Text?.Annotations?.Where(t => t.Type == "file_path");

                    foreach (TextAnnotation annotation in filePaths ?? new List<TextAnnotation>())
                    {
                        if (annotation.FilePath != null && !string.IsNullOrEmpty(annotation.FilePath.FileId))
                        {
                            OpenAI.Models.File file = await this._openAIClient.RetrieveFileAsync(annotation.FilePath.FileId);
                            byte[] fileContent = await this._openAIClient.RetrieveFileContentAsync(annotation.FilePath.FileId);

                            plan.Commands.Add(new PredictedDoCommand(annotation.Type,
                                new Dictionary<string, object?>() {
                                    {"text", annotation.Text},
                                    {"start_index", annotation.StartIndex},
                                    {"end_index", annotation.EndIndex},
                                    {"file_id", annotation.FilePath?.FileId},
                                    {"filename", Path.GetFileName(file.Filename)},
                                    {"fileContent", fileContent}
                                    }));
                        }
                    }

                }
            }

            return plan;
        }

        private Plan _GeneratePlanFromTools(TState state, RequiredAction requiredAction)
        {
            Plan plan = new();
            Dictionary<string, List<string>> toolMap = new();
            foreach (ToolCall toolCall in requiredAction.SubmitToolOutputs.ToolCalls)
            {
                // toolMap[toolCall.Function.Name] = toolCall.Id;
                if (!toolMap.ContainsKey(toolCall.Function.Name))
                {
                    toolMap[toolCall.Function.Name] = new List<string>();
                }
                toolMap[toolCall.Function.Name].Add(toolCall.Id);
                plan.Commands.Add(new PredictedDoCommand
                (
                    toolCall.Function.Name,
                    JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.Function.Arguments)
                    ?? new Dictionary<string, object?>()
                )
                { ToolCallId = toolCall.Id });
            }
            state.SubmitToolMap = toolMap;
            return plan;
        }

        private async Task<Plan> _SubmitActionResultsAsync(TState state, CancellationToken cancellationToken)
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

            // Submit the tool outputs
            Run run = await this._openAIClient.SubmitToolOutputsAsync(state.ThreadId!, state.RunId!, new()
            {
                ToolOutputs = toolOutputs
            }, cancellationToken);

            // Wait for the run to complete
            Run result = await this._WaitForRunAsync(state.ThreadId!, run.Id, true, cancellationToken);
            switch (result.Status)
            {
                case "requires_action":
                    state.SubmitToolOutputs = true;
                    return this._GeneratePlanFromTools(state, result.RequiredAction!);
                case "completed":
                    state.SubmitToolOutputs = false;
                    return await this._GeneratePlanFromMessagesAsync(state.ThreadId!, state.LastMessageId!, cancellationToken);
                case "cancelled":
                    return new Plan();
                case "expired":
                    return new Plan(new() { new PredictedDoCommand(AIConstants.TooManyStepsActionName) });
                default:
                    throw new TeamsAIException($"Run failed {result.Status}. ErrorCode: {result.LastError?.Code}. ErrorMessage: {result.LastError?.Message}");
            }
        }

        private async Task<Plan> _SubmitUserInputAsync(TState state, CancellationToken cancellationToken)
        {
            // Get the current thread_id
            string threadId = await this._EnsureThreadCreatedAsync(state, cancellationToken);

            // Add the users input to the thread
            Message message = await this._openAIClient.CreateMessageAsync(threadId, new()
            {
                Content = state.Temp?.Input ?? string.Empty,
                FileIds = state.Files.Any() ? state.Files : null
            }, cancellationToken);

            // Create a new run
            Run run = await this._openAIClient.CreateRunAsync(threadId, new RunCreateParams()
            {
                Model = state.Model,
                AssistantId = state.AssistantId != null && !string.IsNullOrEmpty(state.AssistantId) ? state.AssistantId : this._options.AssistantId,
                AdditionalInstructions = !string.IsNullOrEmpty(state.Temp?.AdditionalInstructions) ? state.Temp?.AdditionalInstructions : null,
                Tools = state.Tools.Any() ? state.Tools.Select(t => t.Value).ToList() : null
            }, cancellationToken);

            // Update state and wait for the run to complete
            state.ThreadId = threadId;
            state.RunId = run.Id;
            state.LastMessageId = message.Id;
            Run result = await this._WaitForRunAsync(threadId, run.Id, true, cancellationToken);
            switch (result.Status)
            {
                case "requires_action":
                    state.SubmitToolOutputs = true;
                    return this._GeneratePlanFromTools(state, result.RequiredAction!);
                case "completed":
                    state.SubmitToolOutputs = false;
                    return await this._GeneratePlanFromMessagesAsync(threadId, message.Id, cancellationToken);
                case "cancelled":
                    return new Plan();
                case "expired":
                    return new Plan(new() { new PredictedDoCommand(AIConstants.TooManyStepsActionName) });
                default:
                    throw new TeamsAIException($"Run failed {result.Status}. ErrorCode: {result.LastError?.Code}. ErrorMessage: {result.LastError?.Message}");
            }
        }
    }
}
