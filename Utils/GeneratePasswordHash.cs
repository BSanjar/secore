using BCrypt.Net;

namespace WebApplication1.Utils
{
    /// <summary>
    /// Утилита для генерации хеша пароля "kelechek"
    /// Запустите этот код в консоли или используйте для генерации хеша
    /// </summary>
    public class GeneratePasswordHash
    {
        public static void Main()
        {
            string password = "kelechek";
            string hash = BCrypt.Net.BCrypt.HashPassword(password);
            Console.WriteLine($"Пароль: {password}");
            Console.WriteLine($"Хеш: {hash}");
            
            // Пример хеша для пароля "kelechek":
            // $2a$11$KIXvZ8QZ8QZ8QZ8QZ8QZ8OeQZ8QZ8QZ8QZ8QZ8QZ8QZ8QZ8QZ8QZ
        }
    }
}

