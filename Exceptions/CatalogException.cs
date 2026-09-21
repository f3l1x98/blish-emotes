using System;

namespace felix.BlishEmotes.Exceptions
{
    class CatalogException : Exception
    {
        public CatalogException()
        {
        }

        public CatalogException(string message)
            : base(message)
        {
        }

        public CatalogException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
