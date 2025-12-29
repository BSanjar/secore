using BCrypt.Net;

namespace WebApplication1.Utils
{
    /// <summary>
    /// Утилита для генерации хешей паролей
    /// </summary>
    public static class PasswordHashGenerator
    {
        /// <summary>
        /// Генерирует BCrypt хеш для пароля
        /// </summary>
        /// <param name="password">Пароль для хеширования</param>
        /// <returns>Хеш пароля</returns>
        public static string GenerateHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        /// <summary>
        /// Проверяет пароль против хеша
        /// </summary>
        /// <param name="password">Пароль для проверки</param>
        /// <param name="hash">Хеш для сравнения</param>
        /// <returns>True если пароль совпадает</returns>
        public static bool Verify(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}

