using FinancialTracker.Common.Exceptions;
using FinancialTracker.Persistance;
using FinancialTracker.Services;
using FinancialTracker.Services.Common;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
        options.SlidingExpiration = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/User/Login";
        options.LogoutPath = "/User/Logout";
    });
            
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

builder.Services.AddScoped<IHttpContextHelperService, HttpContextHelperService>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddScoped<ISqlDataAccess, SqlDataAccess>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISavingsAccountService, SavingsAccountService>();
            
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsLocalHost8080", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://192.168.1.236:8080").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});


var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseCors("CorsLocalHost8080");
app.UseCookiePolicy();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
