using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Infrastructure.Persistence.Mongo.Configurations;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using Hokm.Infrastructure.Repositories.Implementations;
using Hokm.Infrastructure.Security;
using Hokm.Infrastructure.Services.Payment.Bazaar;
using Hokm.Infrastructure.Services.Payment.Zarinpal;
using Hokm.Infrastructure.Services.Redis.Implemetations;
using Hokm.Infrastructure.Services.Sms;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Text;

namespace Hokm.Infrastructure.Configurations
{
    public static class ServiceCollectionExtensions
    {
        public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
        {
            var services = builder.Services;
            var configuration = builder.Configuration;

            var infraSettings = configuration.GetSection("InfrastructureSettings")
                .Get<InfrastructureSettings>();

            MongoDbConfiguration.Configure();

            services.Configure<InfrastructureSettings>(
                configuration.GetSection("InfrastructureSettings"));
            services.AddSingleton(infraSettings);

            // SMS Service
            services.AddHttpClient<ISmsService, SmsIrService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseProxy = false,
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                });

            // Authentication & Authorization
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(infraSettings.JwtSecret)),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = true
                    };
                });
            services.AddAuthorization();

            builder.AddMongoDBClient("mongodb");
            builder.AddRedisClient("redis");

            services.AddSingleton(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(infraSettings.MongoDatabaseName);
            });

            services.AddScoped<MongoDbContext>();

            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitUri = new Uri($"rabbitmq://{infraSettings.RabbitMqHost}");

                    cfg.Host(rabbitUri, h =>
                    {
                        h.Username(infraSettings.RabbitMqUser);
                        h.Password(infraSettings.RabbitMqPassword);
                    });

                    cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    cfg.ConfigureEndpoints(context);
                });
            });

            // Repositories & Services
            services.AddScoped<IGameRepository, MongoGameRepository>();
            services.AddScoped<IUserRepository, MongoUserRepository>();
            services.AddScoped<IRedisCacheService, RedisCacheService>();
            services.AddScoped<IProductRepository, MongoProductRepository>();
            services.AddScoped<ITransactionRepository, MongoTransactionRepository>();
            services.AddScoped<IDirectPaymentService, ZarinpalService>();
            services.AddScoped<IStorePaymentService, BazaarService>();
            services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<ILeaderboardRepository, MongoLeaderboardRepository>();
            services.AddSingleton<GameExecutionCoordinator>();

            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()
                          .WithExposedHeaders(
                              "Grpc-Status",
                              "Grpc-Message",
                              "Grpc-Encoding",
                              "Grpc-Accept-Encoding");
                });
            });

            return builder;
        }
    }
}