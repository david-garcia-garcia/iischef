using System;

namespace iischef.utils
{
    public static class PasswordHelper
    {
        private static readonly string AllowedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%&*";
        private static readonly Random Random = new Random();

        public static string GenerateRandomPassword(int length)
        {
            char[] passwordChars = new char[length];

            for (int i = 0; i < length; i++)
            {
                passwordChars[i] = AllowedChars[Random.Next(AllowedChars.Length)];
            }

            return new string(passwordChars);
        }
    }
}
