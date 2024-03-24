using System.Text.Json.Serialization;

namespace Microsoft.Teams.AI.AI.OpenAI.Models
{
    internal class MessageDeltaEvent : IEventDataType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("delta")]
        public MessageDelta? Delta { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; } = "thread.run";
    }

    internal class MessageDelta
    {
        [JsonPropertyName("content")]
        public List<Content>? Content { get; set; }
    }

    internal class Content
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public Text? Text { get; set; }
    }

    internal class Text
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }

        [JsonPropertyName("annotations")]
        public List<object>? Annotations { get; set; }
    }
}
