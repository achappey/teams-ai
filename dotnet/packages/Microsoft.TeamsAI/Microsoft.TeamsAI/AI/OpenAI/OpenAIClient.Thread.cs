using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using Microsoft.Teams.AI.AI.OpenAI.Models;
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
        private const string OpenAIThreadEndpoint = "https://api.openai.com/v1/threads";
        private static readonly IEnumerable<KeyValuePair<string, string>> LimitOneQuery =
            new List<KeyValuePair<string, string>> { new("limit", "1") }.AsReadOnly();

        /// <summary>
        /// Create a thread.
        /// </summary>
        /// <param name="threadCreateParams">The params to create the thread.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The created thread.</returns>
        /// <exception cref="HttpOperationException" />
        public virtual async Task<Models.Thread> CreateThreadAsync(ThreadCreateParams threadCreateParams, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpContent content = new StringContent(
                    JsonSerializer.Serialize(threadCreateParams, _serializerOptions),
                    Encoding.UTF8,
                    "application/json"
                );

                using HttpResponseMessage httpResponse = await this._ExecutePostRequestAsync(OpenAIThreadEndpoint, content, OpenAIBetaHeaders, cancellationToken);

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                Models.Thread result = JsonSerializer.Deserialize<Models.Thread>(responseJson) ?? throw new SerializationException($"Failed to deserialize thread result response json: {responseJson}");

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

        /// <summary>
        /// Create a message in a thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="messageCreateParams">The params to create the message.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The created message.</returns>
        /// <exception cref="HttpOperationException" />
        public virtual async Task<Message> CreateMessageAsync(string threadId, MessageCreateParams messageCreateParams, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpContent content = new StringContent(
                    JsonSerializer.Serialize(messageCreateParams, _serializerOptions),
                    Encoding.UTF8,
                    "application/json"
                );

                using HttpResponseMessage httpResponse = await this._ExecutePostRequestAsync($"{OpenAIThreadEndpoint}/{threadId}/messages", content, OpenAIBetaHeaders, cancellationToken);

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                Message result = JsonSerializer.Deserialize<Message>(responseJson) ?? throw new SerializationException($"Failed to deserialize message result response json: {responseJson}");

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

        /// <summary>
        /// List new messages of a thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="lastMessageId">The last message ID (exclude from the list results).</param>
        /// <param name="runId">The run ID.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The new messages ordered by created_at timestamp desc.</returns>
        /// <exception cref="HttpOperationException" />
        public virtual async IAsyncEnumerable<Message> ListNewMessagesAsync(string threadId, string? lastMessageId, string? runId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            bool hasMore;
            string? before = lastMessageId;
            string? after = null;
            do
            {
                ListResponse<Message> listResult;
                try
                {
                    using HttpResponseMessage httpResponse = await this._ExecuteGetRequestAsync($"{OpenAIThreadEndpoint}/{threadId}/messages", this.BuildListQuery(before, after, runId), OpenAIBetaHeaders, cancellationToken);
                    string responseJson = await httpResponse.Content.ReadAsStringAsync();
                    listResult = JsonSerializer.Deserialize<ListResponse<Message>>(responseJson) ?? throw new SerializationException($"Failed to deserialize message list result response json: {responseJson}");
                }
                catch (HttpOperationException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new TeamsAIException($"Something went wrong: {e.Message}", e);
                }

                foreach (Message message in listResult.Data)
                {
                    yield return message;
                }

                hasMore = listResult.HasMore;
                if (hasMore)
                {
                    after = listResult.LastId;
                }
            } while (hasMore);
        }

        public async IAsyncEnumerable<(string eventName, string result)> CreateRunStreamAsync(string threadId, RunCreateParams runCreateParams, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            runCreateParams.Stream = true;

            string requestUri = $"{OpenAIThreadEndpoint}/{threadId}/runs";

            await foreach ((string eventName, string result) item in CreateStreamAsync(requestUri,
                JsonSerializer.Serialize(runCreateParams, _serializerOptions), cancellationToken))
            {
                yield return item;
            }
        }

        private async IAsyncEnumerable<(string eventName, string result)> CreateStreamAsync(string requestUri, string jsonPostContent, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            using HttpContent content = new StringContent(
                  jsonPostContent,
                  Encoding.UTF8,
                  "application/json"
              );

            HttpRequestMessage request = new(HttpMethod.Post, requestUri);
            request.Content = content;

            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("User-Agent", HttpUserAgent);
            request.Headers.Add("Authorization", $"Bearer {this._options.ApiKey}");

            if (this._options.Organization != null)
            {
                request.Headers.Add("OpenAI-Organization", this._options.Organization);
            }

            foreach (KeyValuePair<string, string> header in OpenAIBetaHeaders)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new(stream);
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string eventType = "";
                StringBuilder eventDataBuilder = new();

                string line;
                while ((line = await reader.ReadLineAsync()) != string.Empty)
                {
                    if (line.StartsWith("event: "))
                    {
                        eventType = line.Substring(7);
                    }
                    else if (line.StartsWith("data: ") && !line.Contains("[DONE]"))
                    {
                        eventDataBuilder.AppendLine(line.Substring(6));
                    }
                    else if (line.Contains("data: [DONE]"))
                    {
                        yield break;
                    }

                    if (reader.EndOfStream)
                    {
                        break;
                    }
                }

                if (eventType == "thread.message.delta"
                    || eventType == "thread.run.requires_action"
                    || eventType == "thread.run.completed" || eventType == "thread.run.expired"
                    || eventType == "thread.run.failed" || eventType == "thread.run.cancelled")
                {
                    string eventData = eventDataBuilder.ToString();
                    yield return (eventName: eventType, result: eventData);
                }

                eventDataBuilder.Clear();
            }
        }

        /// <summary>
        /// Retrieve a run of a thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="runId">The run ID.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The run.</returns>
        /// <exception cref="HttpOperationException" />
        public virtual async Task<Run> RetrieveRunAsync(string threadId, string runId, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpResponseMessage httpResponse = await this._ExecuteGetRequestAsync($"{OpenAIThreadEndpoint}/{threadId}/runs/{runId}", null, OpenAIBetaHeaders, cancellationToken);

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                Run result = JsonSerializer.Deserialize<Run>(responseJson) ?? throw new SerializationException($"Failed to deserialize run result response json: {responseJson}");

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

        /// <summary>
        /// Retrieve the last run of a thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The last run if exist, otherwise null.</returns>
        /// <exception cref="HttpOperationException" />
        public virtual async Task<Run?> RetrieveLastRunAsync(string threadId, CancellationToken cancellationToken = default)
        {
            try
            {
                using HttpResponseMessage httpResponse = await this._ExecuteGetRequestAsync($"{OpenAIThreadEndpoint}/{threadId}/runs", LimitOneQuery, OpenAIBetaHeaders, cancellationToken);

                string responseJson = await httpResponse.Content.ReadAsStringAsync();
                ListResponse<Run> result = JsonSerializer.Deserialize<ListResponse<Run>>(responseJson) ?? throw new SerializationException($"Failed to deserialize run list result response json: {responseJson}");

                return result.Data?.Count > 0 ? result.Data[0] : null;
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

        public async IAsyncEnumerable<(string eventName, string result)> CreateSubmitOutputToolsStreamAsync(string threadId, string runId, SubmitToolOutputsParams submitToolOutputsParams, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            submitToolOutputsParams.Stream = true;

            string requestUri = $"{OpenAIThreadEndpoint}/{threadId}/runs/{runId}/submit_tool_outputs";

            await foreach ((string eventName, string result) item in CreateStreamAsync(requestUri,
                JsonSerializer.Serialize(submitToolOutputsParams, _serializerOptions), cancellationToken))
            {
                yield return item;
            }
        }

        private List<KeyValuePair<string, string>> BuildListQuery(string? before, string? after, string? runId)
        {
            List<KeyValuePair<string, string>> result = new();
            result.Add(new("order", "desc"));

            if (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after) && string.IsNullOrEmpty(runId))
            {
                return result;
            }

            if (!string.IsNullOrEmpty(before))
            {
                result.Add(new("before", before!));
            }
            if (!string.IsNullOrEmpty(after))
            {
                result.Add(new("after", after!));
            }
            if (!string.IsNullOrEmpty(runId))
            {
                result.Add(new("runId", runId!));
            }
            return result;
        }
    }
}
