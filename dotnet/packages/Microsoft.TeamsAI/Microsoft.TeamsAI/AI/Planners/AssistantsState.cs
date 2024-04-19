using Microsoft.Teams.AI.AI.OpenAI.Models;
using Microsoft.Teams.AI.State;

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
        /// Get or set the submit tool map.
        /// </summary>
        Dictionary<string, List<string>> SubmitToolMap { get; set; }

        /// <summary>
        /// Get or set the files.
        /// </summary>
        List<string> Files { get; set; }

        /// <summary>
        /// Get or set the model.
        /// </summary>
        string? Model { get; set; }

        /// <summary>
        /// Get or set the assistant ID.
        /// </summary>
        string? AssistantId { get; set; }

        /// <summary>
        /// Get or set the temperature.
        /// </summary>
        double Temperature { get; set; }

        /// <summary>
        /// Get or set the tools.
        /// </summary>
        Dictionary<string, Tool> Tools { get; set; }

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
        /// Get or set the files.
        /// Stored in ConversationState with key "conversation_files".
        /// </summary>
        public List<string> Files
        {
            get => this.Conversation?.Get<List<string>>("conversation_files") ?? new List<string>();
            set => this.Conversation?.Set("conversation_files", value);
        }

        /// <summary>
        /// Get or set the tools.
        /// Stored in ConversationState with key "conversation_tools".
        /// </summary>
        public Dictionary<string, Tool> Tools
        {
            get => this.Conversation?.Get<Dictionary<string, Tool>>("conversation_tools") ?? new Dictionary<string, Tool>();
            set => this.Conversation?.Set("conversation_tools", value);
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
        /// Get or set whether need to submit tool outputs.
        /// Stored in TempState with key "assistants_state_submit_tool_outputs".
        /// </summary>
        public bool SubmitToolOutputs
        {
            get => this.Temp?.Get<bool>("assistants_state_submit_tool_outputs") ?? false;
            set => this.Temp?.Set("assistants_state_submit_tool_outputs", value);
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
        public double Temperature
        {
            get => this.User?.Get<double?>("conversation_temperature") ?? 0.3;
            set => this.User?.Set("conversation_temperature", value);
        }
    }
}
