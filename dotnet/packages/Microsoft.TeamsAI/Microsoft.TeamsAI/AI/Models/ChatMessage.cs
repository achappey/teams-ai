﻿namespace Microsoft.Teams.AI.AI.Models
{
    /// <summary>
    /// Represents a message that will be passed to the Chat Completions API
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// The role associated with this message payload.
        /// </summary>
        public ChatRole Role { get; set; }

        /// <summary>
        /// The text associated with this message payload.
        /// </summary>
        public object? Content;

        /// <summary>
        /// The name of the author of this message. `name` is required if role is `function`, and it should be the name of the
        /// function whose response is in the `content`. May contain a-z, A-Z, 0-9, and underscores, with a maximum length of
        /// 64 characters.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// The name and arguments of a function that should be called, as generated by the model.
        /// </summary>
        public FunctionCall? FunctionCall { get; set; }

        /// <summary>
        /// The ID of the tool call resolved by the provided content. `toolCallId` is required if role is `tool`.
        /// </summary>
        public string? ToolCallId { get; set; }

        /// <summary>
        /// The context used for this message.
        /// </summary>
        public MessageContext? Context { get; set; }

        /// <summary>
        /// The tool calls generated by the model, such as function calls.
        /// </summary>
        public IList<ChatCompletionsToolCall>? ToolCalls { get; set; }


        /// <summary>
        /// Gets the content with the given type.
        /// Will throw an exception if the content is not of the given type.
        /// </summary>
        /// <returns>The content.</returns>
        public TContent GetContent<TContent>()
        {
            return (TContent)Content!;
        }

        /// <summary> Initializes a new instance of ChatMessage. </summary>
        /// <param name="role"> The role associated with this message payload. </param>
        public ChatMessage(ChatRole role)
        {
            this.Role = role;
        }
    }

    /// <summary>
    /// The name and arguments of a function that should be called, as generated by the model.
    /// </summary>
    public class FunctionCall
    {
        /// <summary>
        /// The name of the function to call.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The arguments to call the function with, as generated by the model in JSON format.
        /// Note that the model does not always generate valid JSON, and may hallucinate parameters
        /// not defined by your function schema. Validate the arguments in your code before calling
        /// your function.
        /// </summary>
        public string Arguments { get; set; } = string.Empty;

        /// <summary>
        /// Creates an instance of `FunctionCall`.
        /// </summary>
        /// <param name="name">function name</param>
        /// <param name="arguments">function arguments</param>
        public FunctionCall(string name, string arguments)
        {
            this.Name = name;
            this.Arguments = arguments;
        }
    }

    /// <summary>
    /// Represents the ChatMessage content.
    /// </summary>
    public abstract class MessageContentParts
    {
        /// <summary>
        /// The type of message content.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The chat message content.
        /// </summary>
        /// <param name="type"></param>
        public MessageContentParts(string type)
        {
            this.Type = type;
        }
    }

    /// <summary>
    /// The image content part of the ChatMessage
    /// </summary>
    public class TextContentPart : MessageContentParts
    {
        /// <summary>
        /// The constructor
        /// </summary>
        public TextContentPart() : base("text") { }

        /// <summary>
        /// The text of the message
        /// </summary>
        public string Text = string.Empty;
    }

    /// <summary>
    /// The image content part of the ChatMessage
    /// </summary>
    public class ImageContentPart : MessageContentParts
    {
        /// <summary>
        /// The constructor
        /// </summary>
        public ImageContentPart() : base("image") { }

        /// <summary>
        /// The URL of the image.
        /// </summary>
        public string ImageUrl = string.Empty;
    }
}
