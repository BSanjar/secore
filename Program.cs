using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using WebApplication1.Models.BaseModels;
using WebApplication1.Models.DBModels;
using WebApplication1.Modules.GenericModule.Services;
using WebApplication1.Areas.Simple.ViewModels;
using WebApplication1.Areas.Simple.Services;
using System.Linq;
using WebApplication1.Services;
using WebApplication1.Helpers;
using Microsoft.OpenApi.Models;
using WebApplication1.Swagger;

// Npgsql: разрешить запись DateTime с Kind=UTC в колонки timestamp without time zone
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Настройка локализации
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "ru", "en", "ky" };
    var supportedUICultures = new[] { "ru", "en", "ky" };

    options.SetDefaultCulture("ru")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedUICultures)
        .RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<WebApplication1.Filters.SetCabinetLayoutFilter>();
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
})
    .AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization(); 

// Swagger (beautiful connector docs)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("connector", new OpenApiInfo
    {
        Title = "Secore Connector API",
        Version = "v1",
        Description = "Billing connector (JSON + XML)."
    });

    // Basic auth for JSON connector
    c.AddSecurityDefinition("basicAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        Description = "Basic auth for JSON connector: base64(login:password)"
    });

    c.DocumentFilter<ConnectorDocumentFilter>();
    c.DocumentFilter<SortSchemasDocumentFilter>();
    c.OperationFilter<ConnectorOperationFilter>();

    // Keep only connector endpoints in this swagger doc
    c.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (!string.Equals(docName, "connector", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = apiDesc.RelativePath ?? string.Empty;
        return path.StartsWith("WebApi/", StringComparison.OrdinalIgnoreCase)
               ;
    });
});

// Регистрируем фильтры
builder.Services.AddScoped<WebApplication1.Filters.XmlValidationFilter>();
builder.Services.AddScoped<WebApplication1.Filters.SetCabinetLayoutFilter>();
builder.Services.AddScoped<OperationsByInvoices>();
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

// Регистрация сервисов

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<TransactionCommissionService>();
builder.Services.AddScoped<ViewRenderService>();


// Добавление поддержки сессий
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

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

app.UseSession();

// Настройка локализации (должна быть до UseRouting)
var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseRouting();

// Добавляем middleware для обработки ошибок XML десериализации и валидации
// Должен быть после UseRouting, чтобы перехватывать ответы контроллеров
app.UseMiddleware<WebApplication1.Middleware.XmlExceptionMiddleware>();
app.UseMiddleware<WebApplication1.Middleware.XmlErrorResponseMiddleware>();

app.UseAuthorization();

// Swagger UI (served from /swagger)
app.UseSwagger(c =>
{
    c.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/connector/swagger.json", "Secore Connector API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "Secore Connector API";
    c.EnableDeepLinking();
    c.DisplayRequestDuration();
    c.InjectStylesheet("/swagger-ui/connector-theme.css");
});

// Маршрутизация для Language (должен быть до маршрута Areas)
app.MapControllerRoute(
    name: "language",
    pattern: "Language/{action=SetLanguage}",
    defaults: new { controller = "Language" });

// Регистрация API контроллеров (должно быть ПЕРВЫМ, до всех других маршрутов)
// Это регистрирует все контроллеры с атрибутом [ApiController] и [Route]
app.MapControllers();

// Маршрутизация для Account (должен быть до маршрута Areas, более специфичный)
app.MapControllerRoute(
    name: "account",
    pattern: "Account/{action=Login}/{id?}",
    defaults: new { controller = "Account", action = "Login" });

// Маршрутизация для Areas (не должен перехватывать /api/*)
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Cabinet}/{action=Index}/{id?}");

// Маршрутизация по умолчанию (должен быть последним)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
