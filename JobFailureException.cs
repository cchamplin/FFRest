using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    public class JobFailureException : Exception
    {
        public JobFailureException(string message)
            : base(message)
        {
        }
    }
}
