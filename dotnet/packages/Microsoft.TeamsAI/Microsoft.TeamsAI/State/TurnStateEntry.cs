using Microsoft.Teams.AI.Utilities;
using System.Runtime.CompilerServices;
using System.Text.Json;

[assembly: InternalsVisibleTo("Microsoft.Teams.AI.Tests")]
namespace Microsoft.Teams.AI.State
{
    /// <summary>
    /// Accessor class for managing an individual state scope.
    /// </summary>
    public class TurnStateEntry
    {
        private Record _value;
        private readonly string _hash;
        private static readonly JsonSerializerOptions _serializerOptions = new() { MaxDepth = 64 };

        /// <summary>
        /// Constructs the turn state entry.
        /// </summary>
        /// <param name="value">Value to initialize the state scope with. The default is an {} object.</param>
        /// <param name="storageKey">Storage key to use when persisting the state scope.</param>
        public TurnStateEntry(Record value, string? storageKey = null)
        {
            Verify.ParamNotNull(value);
            this._value = value;
            this.StorageKey = storageKey;
            this._hash = ComputeHash(value);
        }

        /// <inheritdoc />
        public bool HasChanged => ComputeHash(this._value!) != this._hash;

        /// <inheritdoc />
        public bool IsDeleted { get; private set; } = false;

        /// <inheritdoc />
        public Record? Value
        {
            get
            {
                if (this.IsDeleted)
                {
                    this._value = new();
                    this.IsDeleted = false;
                }

                return this._value;
            }
        }

        /// <inheritdoc />
        public string? StorageKey { get; }

        /// <summary>
        /// Clears the state scope.
        /// </summary>
        public void Delete()
        {
            this.IsDeleted = true;
        }

        /// <summary>
        /// Replaces the state scope with a new value.
        /// </summary>
        /// <param name="value">New value to replace the state scope with.</param>
        public void Replace(Record value)
        {
            Verify.ParamNotNull(value);
            this._value = value;
        }

        // TODO: Optimize if possible
        /// <summary>
        /// Computes the hash from the object
        /// </summary>
        /// <param name="obj">The object to compute has from</param>
        /// <returns>Returns a Json object representation </returns>
        internal static string ComputeHash(object obj)
        {
            Verify.ParamNotNull(obj);

            return JsonSerializer.Serialize(obj, _serializerOptions);
        }
    }
}
