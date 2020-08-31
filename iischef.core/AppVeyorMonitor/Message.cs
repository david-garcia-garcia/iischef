using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace iischef.core.AppVeyorMonitor
{
    /// <summary>
    /// Represents a message comming from AppVeyor
    /// </summary>
    public class Message
    {
        /// <summary>
        /// The pattern used in commit messages
        /// </summary>
        public const string PATTERN = "\\[chef-(\\w+)\\((.*)\\)\\]";

        /// <summary>
        /// Get an instance of Message
        /// </summary>
        /// <param name="message">The source commit message</param>
        public Message(string message)
        {
            // Parse the requested lifetime
            var match = System.Text.RegularExpressions.Regex.Match(message, PATTERN);

            this.command = match.Groups[1].Value;

            this.arguments = System.Text.RegularExpressions.Regex.Split(match.Groups[2].Value, ",")
                .Where((i) => !string.IsNullOrWhiteSpace(i))
                .ToList();
        }

        /// <summary>
        /// Get an instance of Message
        /// </summary>
        public Message() 
        { 
        }

        /// <summary>
        /// The command name
        /// </summary>
        public string command { get; set; }

        /// <summary>
        /// The command arguments
        /// </summary>
        public List<string> arguments { get; set; }
    }
}
