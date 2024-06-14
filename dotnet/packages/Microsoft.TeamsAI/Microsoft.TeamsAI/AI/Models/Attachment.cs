using System.Text.Json.Serialization;

namespace Microsoft.Teams.AI.AI.Models
{
    public class Attachment
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = null!;

        [JsonPropertyName("tools")]
        public IEnumerable<string> Tools { get; set; } = null!;
    }
}
