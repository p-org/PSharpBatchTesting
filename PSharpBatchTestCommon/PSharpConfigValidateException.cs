using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTestCommon
{
    public class PSharpConfigValidateException : Exception
    {
        public PSharpConfigValidateException(string exceptionMessage) : base(exceptionMessage)
        {

        }
    }
}
