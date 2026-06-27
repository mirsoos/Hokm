using Hokm.Infrastructure.Configurations;
using Hokm.Presentation.gRPC.Services;
using MediatR;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5128, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenAnyIP(5129, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

builder.Services.AddInfrastructure(builder.Configuration);

// ثبت GameStreamingService به عنوان Singleton
builder.Services.AddSingleton<GameStreamingService>();

// بازگرداندن MediatR
builder.Services.AddMediatR(cfg =>
{
    // اسکن اسمبلی Application
    cfg.RegisterServicesFromAssembly(typeof(Hokm.Application.Features.RegisterUser.Commands.SendVerificationCode.SendVerificationCodeCommand).Assembly);

    // استفاده از ServiceFactory پیش‌فرض که از DI خودمان می‌خواند
    // این خط باعث می‌شود MediatR برای پیدا کردن هندلرها از ServiceProvider ما استفاده کند
    // و در نتیجه همان GameStreamingService ای که ما Singleton کردیم را پیدا می‌کند.
    cfg.Lifetime = ServiceLifetime.Transient; // (اختیاری) ولی مهم نیست
});
// MediatR به طور پیش‌فرض از ServiceProvider اصلی استفاده می‌کند.
// پس وقتی GameStreamingService را به عنوان INotificationHandler ثبت کرده باشیم،
// خودش آن را از DI پیدا می‌کند.

// ثبت GameStreamingService به عنوان هندلر برای MediatR
// این کار باعث می‌شود MediatR بفهمد که برای INotificationHandler<GameEventNotification>
// باید از GameStreamingService استفاده کند، بدون اینکه نمونه جدید بسازد.
builder.Services.AddSingleton<INotificationHandler<Hokm.Application.Events.GameEventNotification>>(sp => sp.GetRequiredService<GameStreamingService>());
builder.Services.AddSingleton<INotificationHandler<Hokm.Application.Events.PlayerGameEventNotification>>(sp => sp.GetRequiredService<GameStreamingService>());


builder.Services.AddControllers();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Hokm API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT token into field. Example: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
});


builder.Services.AddGrpc();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hokm API V1");
    c.RoutePrefix = string.Empty;
});

app.UseRouting();

app.UseCors("AllowFrontend");
app.UseGrpcWeb(new GrpcWebOptions { DefaultEnabled = true });

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGrpcService<HokmGameService>().EnableGrpcWeb().RequireCors("AllowFrontend");

app.MapGet("/", () => "Hokm API is running");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var names = new[] { "Users", "Games" };
    var existing = await db.ListCollectionNames().ToListAsync();
    foreach (var n in names)
        if (!existing.Contains(n))
            await db.CreateCollectionAsync(n);
}

app.Run();