using System;
using System.Collections.Generic;
using System.Linq;

namespace iischef.utils
{
    public class ContainerParser
    {
        public List<Tuple<string, string>> Parts { get; set; }

        public ContainerParser(string container)
        {
            this.Parts = new List<Tuple<string, string>>();

            var parts = container.Split(",".ToCharArray());
            foreach (var p in parts)
            {
                string key = p.Split("=".ToCharArray()).First();
                string value = p.Split("=".ToCharArray()).Last();

                this.Parts.Add(new Tuple<string, string>(key, value));
            }
        }

        public string GetContainer()
        {
            List<string> parts = new List<string>();

            foreach (var p in this.Parts)
            {
                parts.Add($"{p.Item1}={p.Item2}");
            }

            return string.Join(",", parts);
        }
    }
}
