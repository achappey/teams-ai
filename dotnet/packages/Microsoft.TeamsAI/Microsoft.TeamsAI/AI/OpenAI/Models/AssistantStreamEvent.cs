using System.Text.Json.Serialization;

namespace Microsoft.Teams.AI.AI.OpenAI.Models
{
    internal class AssistantStreamEvent
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = null!;

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    internal interface IEventDataType { }

}
