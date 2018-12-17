using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MStorage.WebStorage
{
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
