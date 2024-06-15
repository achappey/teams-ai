using Microsoft.Teams.AI.AI.Models;
using Microsoft.Teams.AI.State;
using OpenAI.Assistants;

// Assistants API is currently in beta and is subject to change.
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Teams.AI.AI.Planners.Experimental
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Model represents assistants state.
    /// A default implementation is <see cref="AssistantsState"/>.
    /// </summary>
    public interface IAssistantsState
    {
        /// <summary>
        /// Get or set the thread ID.
        /// </summary>
        string? ThreadId { get; set; }

        /// <summary>
        /// Get or set the run ID.
        /// </summary>
        string? RunId { get; set; }

        /// <summary>
        /// Get or set the last message ID.
        /// </summary>
        string? LastMessageId { get; set; }

        /// <summary>
        /// Get or set whether need to submit tool outputs.
        /// </summary>
        bool SubmitToolOutputs { get; set; }

        /// <summary>
        /// Get or set whether output is streamed.
        /// </summary>
        bool Streaming { get; set; }

        /// <summary>
        /// Get or set the submit tool map.
        /// </summary>
        Dictionary<string, List<string>> SubmitToolMap { get; set; }

        List<string> ImageFileIds { get; set; }

        /// <summary>
        /// Get or set the model.
        /// </summary>
        string? Model { get; set; }

        /// <summary>
        /// Get or set the assistant ID.
        /// </summary>
        string? AssistantId { get; set; }

        /// <summary>
        /// Get or set the assistant ID.
        /// </summary>
        string ToolChoice { get; set; }

        /// <summary>
        /// Get or set the temperature.
        /// </summary>
        double? Temperature { get; set; }

        /// <summary>
        /// Get or set the nucleus sampling.
        /// </summary>
        double? TopP { get; set; }

        /// <summary>
        /// Get or set the tools.
        /// </summary>
        Dictionary<string, ToolDefinition> ToolDefinitions { get; set; }

        /// <summary>
        /// Get or set the truncation strategy.
        /// </summary>
        string TruncationStrategy { get; set; }

        /// <summary>
        /// Get or set the truncation strategy last n messages.
        /// </summary>
        int TruncationStrategyLastNMessages { get; set; }

        bool DisableOutput { get; set; }

        /// <summary>
        /// Get or set the tools.
        /// </summary>
        int ChunkOverlapTokens { get; set; }

        /// <summary>
        /// Get or set the max chunk size tokens.
        /// </summary>
        int MaxChunkSizeTokens { get; set; }

        /// <summary>
        /// Get or set the max num results.
        /// </summary>
        int MaxNumResults { get; set; }

        /// <summary>
        /// Get or set the parallel tool calls.
        /// </summary>
        public bool ParallelToolCalls { get; set; }
    }

    /// <summary>
    /// The default implementation of <see cref="IAssistantsState"/>.
    /// </summary>
    public class AssistantsState : TurnState, IAssistantsState
    {

        /// <summary>
        /// Get or set the conversation assistant.
        /// Stored in ConversationState with key "conversation_assistant_id".
        /// </summary>
        public string? AssistantId
        {
            get => this.User?.Get<string>("conversation_assistant_id");
            set => this.User?.Set("conversation_assistant_id", value);
        }

        /// <summary>
        /// Get or set the conversation model.
        /// Stored in ConversationState with key "conversation_model".
        /// </summary>
        public string? Model
        {
            get => this.Conversation?.Get<string>("conversation_model");
            set => this.Conversation?.Set("conversation_model", value);
        }

        /// <summary>
        /// Get or set the images.
        /// Stored in ConversationState with key "conversation_images".
        /// </summary>
        public List<string> ImageFileIds
        {
            get => this.Temp?.Get<List<string>>("conversation_image_file_ids") ?? new List<string>();
            set => this.Temp?.Set("conversation_image_file_ids", value);
        }

        /// <summary>
        /// Get or set the tools.
        /// Stored in ConversationState with key "conversation_tooldefinitions".
        /// </summary>
        public Dictionary<string, ToolDefinition> ToolDefinitions
        {
            get => this.Temp?.Get<Dictionary<string, ToolDefinition>>("conversation_tooldefinitions") ?? new Dictionary<string, ToolDefinition>();
            set => this.Temp?.Set("conversation_tooldefinitions", value);
        }

        /// <summary>
        /// Get or set the thread ID.
        /// Stored in ConversationState with key "assistants_state_thread_id".
        /// </summary>
        public string? ThreadId
        {
            get => this.Conversation?.Get<string>("assistants_state_thread_id");
            set => this.Conversation?.Set("assistants_state_thread_id", value);
        }

        /// <summary>
        /// Get or set the run ID.
        /// Stored in ConversationState with key "assistants_state_run_id".
        /// </summary>
        public string? RunId
        {
            get => this.Conversation?.Get<string>("assistants_state_run_id");
            set => this.Conversation?.Set("assistants_state_run_id", value);
        }

        /// <summary>
        /// Get or set the last message ID.
        /// Stored in ConversationState with key "assistants_state_last_message_id".
        /// </summary>
        public string? LastMessageId
        {
            get => this.Conversation?.Get<string>("assistants_state_last_message_id");
            set => this.Conversation?.Set("assistants_state_last_message_id", value);
        }

        /// <summary>
        /// Get or set the tool choice.
        /// Stored in ConversationState with key "assistants_tool_choice".
        /// </summary>
        public string ToolChoice
        {
            get => this.Conversation?.Get<string?>("conversation_tool_choice") ?? string.Empty;
            set => this.Conversation?.Set("conversation_tool_choice", value);
        }

        /// <summary>
        /// Get or set whether need to submit tool outputs.
        /// Stored in TempState with key "assistants_state_submit_tool_outputs".
        /// </summary>
        public bool SubmitToolOutputs
        {
            get => this.Temp?.Get<bool>("assistants_state_submit_tool_outputs") ?? false;
            set => this.Temp?.Set("assistants_state_submit_tool_outputs", value);
        }

        /// <summary>
        /// Get or set whether output is streamed.
        /// Stored in TempState with key "assistants_streaming".
        /// </summary>
        public bool Streaming
        {
            get => this.User?.Get<bool?>("assistants_streaming") ?? true;
            set => this.User?.Set("assistants_streaming", value);
        }

        /// <summary>
        /// Get or set the submit tool map.
        /// Stored in TempState with key "assistants_state_submit_tool_map".
        /// </summary>
        public Dictionary<string, List<string>> SubmitToolMap
        {
            get => this.Temp?.Get<Dictionary<string, List<string>>>("assistants_state_submit_tool_map") ?? new Dictionary<string, List<string>>();
            set => this.Temp?.Set("assistants_state_submit_tool_map", value);
        }

        /// <summary>
        /// Get or set the temperature.
        /// Stored in ConversationState with key "conversation_temperature".
        /// </summary>
        public double? Temperature
        {
            get => this.Conversation?.Get<double?>("conversation_temperature");
            set
            {
                if (value == null)
                {
                    // Handle null by removing the key or another appropriate action
                    this.Conversation?.Remove("conversation_temperature");
                }
                else
                {
                    this.Conversation?.Set("conversation_temperature", value);
                }
            }

        }

        /// <summary>
        /// Get or set the nucleus sampling.
        /// Stored in ConversationState with key "conversation_top_p".
        /// </summary>
        public double? TopP
        {
            get => this.Conversation?.Get<double?>("conversation_top_p");
            set
            {
                if (value == null)
                {
                    this.Conversation?.Remove("conversation_top_p");
                }
                else
                {
                    this.Conversation?.Set("conversation_top_p", value);
                }
            }

        }

        public string TruncationStrategy
        {
            get => User?.Get<string?>("truncation_strategy") ?? "auto";
            set => User?.Set("truncation_strategy", value);
        }

        public int TruncationStrategyLastNMessages
        {
            get => (int?)User?.Get<long?>("truncation_strategy_last_messages") ?? 50;
            set => User?.Set("truncation_strategy_last_messages", (long?)value);
        }

        public int ChunkOverlapTokens
        {
            get => (int?)User?.Get<long?>("chunk_overlap_tokens") ?? 400;
            set => User?.Set("chunk_overlap_tokens", (long?)value);
        }

        public int MaxChunkSizeTokens
        {
            get => (int?)User?.Get<long?>("max_chunk_size_tokens") ?? 800;
            set => User?.Set("max_chunk_size_tokens", (long?)value);
        }

        public int MaxNumResults
        {
            get => (int?)User?.Get<long?>("max_num_results") ?? 5;
            set => User?.Set("max_num_results", (long?)value);
        }

        public bool DisableOutput
        {
            get => Temp?.Get<bool?>("disable_output") ?? false;
            set => Temp?.Set("disable_output", value);
        }

        public bool ParallelToolCalls
        {
            get => User?.Get<bool?>("parallel_tool_calls") ?? true;
            set => User?.Set("parallel_tool_calls", value);
        }

    }
}
