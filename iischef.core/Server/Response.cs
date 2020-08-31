using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace iischef.core.Server
{
    public class Response
    {
        // Json encoded call result.
        public object result { get; set; }

        // If there has been an error
        public object unhandled_exception { get; set; }

        // If there have been business logic failure.
        public List<string> business_exception { get; set; }
    }
}
