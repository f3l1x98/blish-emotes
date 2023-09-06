using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace felix.BlishEmotes.Exceptions
{
    class UniqueViolationException : Exception
    {
        public UniqueViolationException() : base("Violated unique constraint")
        {
        }

        public UniqueViolationException(string message)
            : base(message)
        {
        }

        public UniqueViolationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
