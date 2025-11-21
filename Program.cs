using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using WebApplication1.Models.BaseModels;
using WebApplication1.Models.DBModels;
using WebApplication1.Modules.GenericModule.Services;
using WebApplication1.Areas.Simple.ViewModels;
using WebApplication1.Areas.Simple.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
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

app.UseAuthorization();

// Маршрутизация для Areas
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Cabinet}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
