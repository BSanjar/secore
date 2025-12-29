using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using WebApplication1.Models.BaseModels;
using WebApplication1.Models.DBModels;
using WebApplication1.Modules.GenericModule.Services;
using WebApplication1.Areas.Simple.ViewModels;
using WebApplication1.Areas.Simple.Services;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Удаляем стандартный XML input форматтер и добавляем кастомный с обработкой ошибок
    var xmlInputFormatter = options.InputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.XmlSerializerInputFormatter>().FirstOrDefault();
    if (xmlInputFormatter != null)
    {
        options.InputFormatters.Remove(xmlInputFormatter);
    }
    options.InputFormatters.Add(new WebApplication1.Formatters.CustomXmlSerializerInputFormatter(options));
    
    // Удаляем стандартный XML output форматтер и добавляем кастомный без пространств имен
    var xmlOutputFormatter = options.OutputFormatters.OfType<Microsoft.AspNetCore.Mvc.Formatters.XmlSerializerOutputFormatter>().FirstOrDefault();
    if (xmlOutputFormatter != null)
    {
        options.OutputFormatters.Remove(xmlOutputFormatter);
    }
    options.OutputFormatters.Add(new WebApplication1.Formatters.CustomXmlSerializerOutputFormatter());
});

// Регистрируем фильтр
builder.Services.AddScoped<WebApplication1.Filters.XmlValidationFilter>();

// Регистрируем сервис авторизации API
builder.Services.AddScoped<WebApplication1.Services.WebApiAuthService>();

// Настраиваем поведение API для обработки ошибок валидации
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(options =>
{
    // Отключаем автоматическую валидацию модели для XML запросов
    // Мы обрабатываем это вручную через фильтр
    options.SuppressModelStateInvalidFilter = false;
});

builder.Services.AddScoped<ITableSource<PaymentListItemVm>, SimplePaymentsTableSource>();

builder.Services.AddDbContext<AppDbContext>(
    options =>
    {
        options
        .UseLoggerFactory(LoggerFactory.Create(builder => { }))
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });


builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));


var app = builder.Build();

// Configure the HTTP request pipeline.
// Обработка ошибок должна быть первой в pipeline
if (app.Environment.IsDevelopment())
{
    // Детальная страница ошибок для режима разработки - показывает полный стек вызовов
    app.UseDeveloperExceptionPage();
}
else
{
    // Обработка ошибок для продакшена - перенаправляет на страницу ошибки
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Добавляем middleware для обработки ошибок XML десериализации и валидации
// Должен быть после UseRouting, чтобы перехватывать ответы контроллеров
app.UseMiddleware<WebApplication1.Middleware.XmlExceptionMiddleware>();
app.UseMiddleware<WebApplication1.Middleware.XmlErrorResponseMiddleware>();

app.UseAuthorization();

// Маршрутизация для Areas
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Cabinet}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
