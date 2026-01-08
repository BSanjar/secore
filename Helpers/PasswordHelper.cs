using System.Security.Cryptography;
using System.Text;

namespace WebApplication1.Helpers
{
    public static class PasswordHelper
    {
        /// <summary>
        /// Хеширует пароль с использованием SHA256
        /// </summary>
        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        /// <summary>
        /// Проверяет пароль
        /// </summary>
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput == hashedPassword;
        }
    }
}

