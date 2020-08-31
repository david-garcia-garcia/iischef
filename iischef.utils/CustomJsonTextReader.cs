using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace iischef.utils
{
    public class CustomJsonTextReader : JsonTextReader
    {
        public CustomJsonTextReader(TextReader textReader) : base(textReader) 
        { 
        }

        public override bool Read()
        {
            return base.Read();
        }
    }
}
