using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.DBModels;

namespace WebApplication1.Services
{
    /// <summary>
    /// Сервис для авторизации API запросов
    /// </summary>
    public class WebApiAuthService
    {
        private readonly AppDbContext _db;

        public WebApiAuthService(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Авторизация по логину и паролю API
        /// </summary>
        /// <param name="login">Логин API</param>
        /// <param name="password">Пароль API</param>
        /// <returns>Организация, если авторизация успешна, иначе null</returns>
        public async Task<Organization?> AuthorizeAsync(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            // Ищем организацию по логину API
            var organization = await _db.Organizations
                .FirstOrDefaultAsync(o => o.ApiLogin == login);

            // Если организации с таким логином нет - возвращаем null
            if (organization == null)
            {
                return null;
            }

            // Если пароль не установлен - возвращаем null
            if (string.IsNullOrWhiteSpace(organization.ApiPassword))
            {
                return null;
            }

            // Проверяем пароль (хеш в БД)
            if (password != organization.ApiPassword)
            {
                return null;
            }

            return organization;
        }
    }
}

