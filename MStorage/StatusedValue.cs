using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// Encapsulates an arbitrary value along with a success flag.
    /// </summary>
    public class StatusedValue<T> : IDisposable
    {
        /// <summary>
        /// The object which this status code applies to.
        /// </summary>
        public T Value { get; private set; }
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        public bool Success { get; private set; }
        /// <summary>
        /// If Success is false, this may be populated with exception details.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Create a new StatusedValue.
        /// </summary>
        /// <param name="value">The object the operation was performed on.</param>
        /// <param name="success">True if the operation succeeded.</param>
        /// <param name="ex">An exception, if success is false and an exception is available.</param>
        public StatusedValue(T value, bool success, Exception ex = null)
        {
            Success = success;
            Value = value;
            Exception = ex;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (Value is IDisposable d)
                    {
                        d.Dispose();
                    }
                }
                disposedValue = true;
            }
        }
        
        /// <summary>
        /// Dispose this instance along with Value if it implements IDisposable.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}