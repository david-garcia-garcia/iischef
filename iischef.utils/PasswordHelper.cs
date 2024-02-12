using System;

namespace iischef.utils
{
    public static class PasswordHelper
    {
        private static readonly string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
        private static readonly string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private static readonly string NumericChars = "1234567890";
        private static readonly string SpecialChars = "!@#$%&*";
        private static readonly string AllowedChars = LowercaseChars + UppercaseChars + NumericChars + SpecialChars;
        private static readonly Random Random = new Random();

        public static string GenerateRandomPassword(int length)
        {
            if (length < 4) // Ensure minimum length to include all types of characters
            {
                throw new ArgumentException("Password length must be at least 4 characters to include all character types.");
            }

            char[] passwordChars = new char[length];

            // Ensure at least one character of each type is present
            passwordChars[0] = LowercaseChars[Random.Next(LowercaseChars.Length)];
            passwordChars[1] = UppercaseChars[Random.Next(UppercaseChars.Length)];
            passwordChars[2] = NumericChars[Random.Next(NumericChars.Length)];
            passwordChars[3] = SpecialChars[Random.Next(SpecialChars.Length)];

            // Fill the rest of the password length with random characters from all allowed
            for (int i = 4; i < length; i++)
            {
                passwordChars[i] = AllowedChars[Random.Next(AllowedChars.Length)];
            }

            // Shuffle the characters so that the fixed positions of character types are randomized
            return ShuffleCharArray(passwordChars);
        }

        private static string ShuffleCharArray(char[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int j = Random.Next(i + 1);
                char temp = array[i];
                array[i] = array[j];
                array[j] = temp;
            }

            return new string(array);
        }
    }
}
