using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json;
using Microsoft.Teams.AI.Exceptions;

// For Unit Tests - so the Moq framework can mock internal classes
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace Microsoft.Teams.AI.AI.OpenAI
{
    /// <summary>
    /// The client to make calls to OpenAI's API
    /// </summary>
    internal partial class OpenAIClient
    {
        private const string OpenAIFilesEndpoint = "https://api.openai.com/v1/files";

        public virtual async Task<Models.File> RetrieveFileAsync(string fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpResponseMessage httpResponse = await this._ExecuteGetRequestAsync($"{OpenAIFilesEndpoint}/{fileId}", null, null, cancellationToken);

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                Models.File result = JsonSerializer.Deserialize<Models.File>(responseJson) ?? throw new SerializationException($"Failed to deserialize file result response json: {responseJson}");

                return result;
            }
            catch (HttpOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new TeamsAIException($"Something went wrong: {e.Message}", e);
            }
        }

        public virtual async Task<byte[]> RetrieveFileContentAsync(string fileId, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpResponseMessage httpResponse = await this._ExecuteGetRequestAsync($"{OpenAIFilesEndpoint}/{fileId}/content", null, null, cancellationToken);

                byte[] responseBytes = await httpResponse.Content.ReadAsByteArrayAsync();

                return responseBytes;
            }
            catch (HttpOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new TeamsAIException($"Something went wrong: {e.Message}", e);
            }
        }
    }
}
