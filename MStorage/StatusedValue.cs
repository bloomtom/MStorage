using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MStorage
{
    /// <summary>
    /// Encapsulates an arbitrary value along with an exception.
    /// </summary>
    public class ExceptionWithValue<T> : IDisposable
    {
        /// <summary>
        /// The object which this status code applies to.
        /// </summary>
        public T Value { get; private set; }
        /// <summary>
        /// Populated with exception details.
        /// </summary>
        public Exception Exception { get; private set; }

        /// <summary>
        /// Create a new ExceptionWithValue.
        /// </summary>
        /// <param name="value">The object the operation was performed on.</param>
        /// <param name="ex">An exception..</param>
        public ExceptionWithValue(T value, Exception ex = null)
        {
            Value = value;
            Exception = ex;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        /// <summary>
        /// Implementation of IDisposable.
        /// </summary>
        /// <param name="disposing"></param>
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