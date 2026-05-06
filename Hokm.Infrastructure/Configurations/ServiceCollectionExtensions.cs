using Hokm.Infrastructure.Persistence.Mongo.Configurations;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Repositories.Implementations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using StackExchange.Redis;
using MassTransit;
using System.Text;
using Hokm.Infrastructure.Services.Redis.Interfaces;
using Hokm.Infrastructure.Services.Redis.Implemetations;

namespace Hokm.Infrastructure.Configurations
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var infraSettings = configuration.GetSection("InfrastructureSettings").Get<InfrastructureSettings>();

            MongoDbConfiguration.Configure();

            services.Configure<InfrastructureSettings>(configuration.GetSection("InfrastructureSettings"));

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

            services.AddSingleton<IMongoClient>(sp =>
                new MongoClient(infraSettings.MongoConnection));
            services.AddSingleton(sp =>
            {
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(infraSettings.MongoDatabaseName);
            });
            services.AddScoped<MongoDbContext>();

            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(infraSettings.RedisConnection));

            services.AddMassTransit(x =>
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(infraSettings.RabbitMqHost, "/", h =>
                    {
                        h.Username(infraSettings.RabbitMqUser);
                        h.Password(infraSettings.RabbitMqPassword);
                    });

                    cfg.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
                    cfg.ConfigureEndpoints(context);
                });
            });

            services.AddScoped<IGameRepository, MongoGameRepository>();
            services.AddScoped<IRedisCacheService, RedisCacheService>();

            return services;
        }
    }
}