#nullable enable
using System;

namespace UniversalFermenter
{
    public class UFException : Exception
    {
        public UFException(string message, Exception? innerException = null) : base($"Universal Fermenter :: {message}", innerException)
        {
        }
    }
}
