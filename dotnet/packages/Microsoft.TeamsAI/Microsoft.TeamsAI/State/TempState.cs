
using Microsoft.Teams.AI.Application;

namespace Microsoft.Teams.AI.State
{
    /// <summary>
    /// Temporary state.
    /// </summary>
    /// <remarks>
    /// Inherit a new class from this base abstract class to strongly type the applications temp state.
    /// </remarks>
    public class TempState : Record
    {

        /// <summary>
        /// Name of the input property.
        /// </summary>
        public const string AdditionalInstructionsKey = "additionalInstructions";

        /// <summary>
        /// Name of the input property.
        /// </summary>
        public const string InputKey = "input";

        public const string AdditionalMessagesKey = "additionalMessages";

        /// <summary>
        /// Name of the output property.
        /// </summary>
        public const string OutputKey = "output";

        /// <summary>
        /// Name of the action outputs property.
        /// </summary>
        public const string ActionOutputsKey = "actionOutputs";

        /// <summary>
        /// Name of the auth tokens property.
        /// </summary>
        public const string AuthTokenKey = "authTokens";

        /// <summary>
        /// Name of the duplicate token exchange property
        /// </summary>
        public const string DuplicateTokenExchangeKey = "duplicateTokenExchange";

        /// <summary>
        /// Name of the input files key
        /// </summary>
        public const string InputFilesKey = "inputFiles";

        /// <summary>
        /// Creates a new instance of the <see cref="TempState"/> class.
        /// </summary>
        public TempState() : base()
        {
            this[AdditionalInstructionsKey] = string.Empty;
            this[InputKey] = string.Empty;
            this[OutputKey] = string.Empty;
            this[ActionOutputsKey] = new Dictionary<string, string>();
            this[AuthTokenKey] = new Dictionary<string, string>();
            this[DuplicateTokenExchangeKey] = false;
            this["last_streamed_reply_id"] = string.Empty;
            this[InputFilesKey] = new List<InputFile>();
        }

        /// <summary>
        /// Additional instructions passed to an AI run
        /// </summary>
        public string AdditionalInstructions
        {
            get => this.Get<string>(AdditionalInstructionsKey)!;
            set => this.Set(AdditionalInstructionsKey, value);
        }

        /// <summary>
        /// Input passed to an AI prompt
        /// </summary>
        public string Input
        {
            get => this.Get<string>(InputKey)!;
            set => this.Set(InputKey, value);
        }

        /*   public IEnumerable<MessageCreateParams> AdditionalMessages
           {
               get => this.Get<IEnumerable<MessageCreateParams>?>(AdditionalMessagesKey) ?? [];
               set => this.Set(AdditionalMessagesKey, value);
           }*/

        // TODO: This is currently not used, should store AI prompt/function output here
        /// <summary>
        /// Output returned from an AI prompt or function
        /// </summary>
        public string Output
        {
            get => this.Get<string>(OutputKey)!;
            set => this.Set(OutputKey, value);
        }

        /// <summary>
        /// All outputs returned from the action sequence that was executed.
        /// </summary>
        public Dictionary<string, string> ActionOutputs
        {
            get => this.Get<Dictionary<string, string>>(ActionOutputsKey)!;
            set => this.Set(ActionOutputsKey, value);
        }

        /// <summary>
        /// All tokens acquired after sign-in for current activity
        /// </summary>
        public Dictionary<string, string> AuthTokens
        {
            get => this.Get<Dictionary<string, string>>(AuthTokenKey)!;
            set => this.Set(AuthTokenKey, value);
        }

        /// <summary>
        /// Whether current token exchange is a duplicate one
        /// </summary>
        public bool DuplicateTokenExchange
        {
            get => this.Get<bool>(DuplicateTokenExchangeKey)!;
            set => this.Set(DuplicateTokenExchangeKey, value);
        }

        public string LastStreamedReplyId
        {
            get => this.Get<string>("last_streamed_reply_id") ?? string.Empty;
            set => this.Set("last_streamed_reply_id", value);
        }

        /// <summary>
        /// Downloaded files passed by the user to the AI library
        /// </summary>
        public List<InputFile> InputFiles
        {
            get => Get<List<InputFile>>(InputFilesKey)!;
            set => Set(InputFilesKey, value);
        }
    }
}
