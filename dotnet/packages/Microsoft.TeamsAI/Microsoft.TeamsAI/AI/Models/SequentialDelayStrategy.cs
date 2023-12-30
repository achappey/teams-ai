using Azure;
using Azure.Core;

namespace Microsoft.Teams.AI.AI.Models
{
    /// <summary>
    /// A customized delay strategy that uses a fixed sequence of delays that are iterated through as the number of retries increases.
    /// </summary>
    internal class SequentialDelayStrategy : DelayStrategy
    {
        private readonly List<TimeSpan> _delays;

        public SequentialDelayStrategy(List<TimeSpan> delays)
        {
            this._delays = delays;
        }

        protected override TimeSpan GetNextDelayCore(Response? response, int retryNumber)
        {
            int index = retryNumber - 1;
            return index >= this._delays.Count ? this._delays[this._delays.Count - 1] : this._delays[index];
        }
    }
}
