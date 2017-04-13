using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSharpBatchTestCommon
{

    public class PSharpException : Exception
    {
        public PSharpException(string exceptionMessage) : base(exceptionMessage)
        {

        }
    }

    public class PSharpConfigValidateException : PSharpException
    {
        public PSharpConfigValidateException(string exceptionMessage) : base(exceptionMessage)
        {

        }
    }
}
