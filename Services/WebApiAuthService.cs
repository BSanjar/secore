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
        /// Авторизация по логину и паролю API (агент).
        /// </summary>
        /// <param name="login">Логин API</param>
        /// <param name="password">Пароль API</param>
        /// <returns>Агент, если авторизация успешна, иначе null</returns>
        public async Task<Agent?> AuthorizeAsync(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                return null;
            }

            var agent = await _db.Agents
                .FirstOrDefaultAsync(a => a.ApiLogin == login);

            if (agent == null)
                return null;

            if (string.IsNullOrWhiteSpace(agent.ApiPassword))
                return null;

            if (password != agent.ApiPassword)
                return null;

            return agent;
        }
    }
}

