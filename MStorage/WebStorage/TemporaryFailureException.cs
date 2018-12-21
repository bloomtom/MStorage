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
        public TemporaryFailureException(string message) : base(message)
        {
        }

        public TemporaryFailureException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
