using System;

namespace iischef.utils
{
    public class DomainUserParser
    {
        public string Username { get; private set; }

        public string Domain { get; private set; }

        public string OriginalFormat { get; private set; }

        public DomainUserParser(string input)
        {
            this.OriginalFormat = input;
            this.ParseInput(input);
        }

        private void ParseInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or whitespace.", nameof(input));
            }

            // Check if input is in the format DOMAIN\username
            if (input.Contains("\\"))
            {
                var parts = input.Split('\\');
                if (parts.Length == 2)
                {
                    this.Domain = parts[0];
                    this.Username = parts[1];
                }
                else
                {
                    throw new ArgumentException("Input format is incorrect.", nameof(input));
                }
            }
            else if (input.Contains("@"))
            {
                var parts = input.Split('@');
                if (parts.Length == 2)
                {
                    this.Username = parts[0];
                    this.Domain = parts[1];
                }
                else
                {
                    throw new ArgumentException("Input format is incorrect.", nameof(input));
                }
            }
            else
            {
                this.Username = input;
                this.Domain = string.Empty; // No domain provided
            }
        }
    }
}
