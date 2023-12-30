using System.Net;

namespace Microsoft.Teams.AI.Exceptions
{
    /// <summary>
    /// Exception thrown when an HTTP operation fails.
    /// </summary>
    public sealed class HttpOperationException : Exception
    {
        /// <summary>
        /// HTTP status code.
        /// </summary>
        public readonly HttpStatusCode? StatusCode;

        /// <summary>
        /// HTTP response content.
        /// </summary>
        public readonly string? ResponseContent;

        /// <summary>
        /// Create an instance of the HttpOperationException class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="httpStatusCode">The HTTP status code.</param>
        /// <param name="responseContent">The HTTP response content.</param>
        public HttpOperationException(string message, HttpStatusCode? httpStatusCode = null, string? responseContent = null) : base(message)
        {
            this.StatusCode = httpStatusCode;
            this.ResponseContent = responseContent;
        }

        /// <summary>
        /// Checks status code is a http error status code.
        /// </summary>
        /// <returns>Returns true if the status code is a http error status code.</returns>
        internal bool isHttpErrorStatusCode()
        {
            // HttpStatusCode.TooManyRequests is not available in .NET Standard 2.0, this is a workaround.
            return this.StatusCode == (HttpStatusCode)429;
        }
    }
}
