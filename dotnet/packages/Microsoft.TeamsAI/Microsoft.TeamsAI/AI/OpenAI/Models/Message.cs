using System.Text.Json.Serialization;

namespace Microsoft.Teams.AI.AI.OpenAI.Models
{
    internal class Message : IEventDataType
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("assistant_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AssistantId { get; set; }

        [JsonPropertyName("content")]
        public List<MessageContent> Content { get; set; } = new List<MessageContent>();

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("file_ids")]
        public List<string> FileIds { get; set; } = new List<string>();

        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; } = "thread.message";

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("run_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RunId { get; set; }

        [JsonPropertyName("thread_id")]
        public string ThreadId { get; set; } = string.Empty;
    }

    internal class MessageContent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MessageContentText? Text { get; set; }

        [JsonPropertyName("image_file")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MessageImageFile? ImageFile { get; set; }
    }

    internal class MessageImageFile
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;
    }

    internal class MessageContentText
    {
        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("annotations")]
        public List<TextAnnotation>? Annotations { get; set; }
    }

    internal class GroupedTextAnnotation
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("ranges")]
        public IEnumerable<string>? Ranges { get; set; }

        [JsonPropertyName("file_citation")]
        public FileCitation? FileCitation { get; set; }
    }

    internal class TextAnnotation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("start_index")]
        public int? StartIndex { get; set; }

        [JsonPropertyName("end_index")]
        public int? EndIndex { get; set; }

        [JsonPropertyName("file_citation")]
        public FileCitation? FileCitation { get; set; }

        [JsonPropertyName("file_path")]
        public FilePath? FilePath { get; set; }
    }

    internal class FilePath
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;
    }

    internal class FileCitation
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("quote")]
        public string? Quote { get; set; }
    }

    internal class MessageCreateParams
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("role")]
        public string Role { get; } = "user";

        [JsonPropertyName("file_ids")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? FileIds { get; set; }

        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
