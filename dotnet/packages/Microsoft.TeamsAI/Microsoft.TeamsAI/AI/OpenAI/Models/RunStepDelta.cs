using System.Text.Json.Serialization;

namespace Microsoft.Teams.AI.AI.OpenAI.Models
{
    internal class RunStepDeltaEvent : IEventDataType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("delta")]
        public RunStepDelta? Delta { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; } = "thread.run";
    }

    internal class RunStepDelta
    {
        [JsonPropertyName("step_details")]
        public StepDetails? StepDetails { get; set; }
    }

    internal class StepDetails
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<ToolCall>? ToolCalls { get; set; }
    }


    internal class CodeInterpreter
    {
        [JsonPropertyName("input")]
        public string? Input { get; set; }

        [JsonPropertyName("outputs")]
        public List<string>? Outputs { get; set; }
    }
}
