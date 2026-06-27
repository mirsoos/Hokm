using Hokm.Application.Interfaces;
using Hokm.Application.Realtime.Execution;
using Hokm.Infrastructure.Persistence.Mongo.Configurations;
using Hokm.Infrastructure.Persistence.Mongo.Context;
using Hokm.Infrastructure.Repositories.Implementations;
using Hokm.Infrastructure.Security;
using Hokm.Infrastructure.Services.Redis.Implemetations;
using Hokm.Infrastructure.Services.Sms;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Text;

namespace Hokm.Infrastructure.Configurations
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var infraSettings = configuration.GetSection("InfrastructureSettings").Get<InfrastructureSettings>();

            MongoDbConfiguration.Configure();

            services.Configure<InfrastructureSettings>(configuration.GetSection("InfrastructureSettings"));
            services.AddSingleton(infraSettings);

            // SMS Service
            services.AddHttpClient<ISmsService, SmsIrService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    UseProxy = false,
                    // 👈 اضافه کردن این خط به هندلر کلاینت پیامک جهت نادیده گرفتن ارور SSL خارجی سرور پیامک
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                });

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

            services.AddScoped<IGameRepository, MongoGameRepository>();
            services.AddScoped<IUserRepository, MongoUserRepository>();
            services.AddScoped<Services.Redis.Interfaces.IRedisCacheService, RedisCacheService>();
            services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddSingleton<GameExecutionCoordinator>();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend", policy =>
                {
                    policy.SetIsOriginAllowed(origin => true)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials()
                        .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");

                });
            });

            //BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));


            return services;
        }
    }
}