using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage.WebStorage
{
    /// <summary>
    /// Indicates a temportary failure. Trying again later may yield success.
    /// </summary>
    public class TemporaryFailureException : Exception
    {
        /// <summary>
        /// Create a new TemporaryFailureException with just a message.
        /// </summary>
        public TemporaryFailureException(string message) : base(message)
        {
        }

        /// <summary>
        /// Create a new TemporaryFailureException with a message and include an inner exception.
        /// </summary>
        public TemporaryFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
