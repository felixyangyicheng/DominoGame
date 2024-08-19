

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.OpenApi.Models;

namespace DominoGame.srv
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

#if DEBUG

#else
            builder.WebHost.ConfigureKestrel((context, options) =>
            {
                options.ListenAnyIP(80, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                    //listenOptions.UseHttps();
                });
            });
#endif

			// Add services to the container.
			//builder.Services.AddAuthorization();

			// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
			builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    b =>
                    {
                        b.SetIsOriginAllowed(origin => true);
                        b.AllowAnyMethod();
                        b.AllowAnyHeader();
                    }
                );

            });
			builder.Services.AddSignalR();

			builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "Some API v1", Version = "v1" });
                // some other configs
                options.AddSignalRSwaggerGen();
            });
            var app = builder.Build();

            // Configure the HTTP request pipeline.
     
                app.UseSwagger();
                app.UseSwaggerUI();

			app.UseRouting();

			// app.UseHttpsRedirection();
			app.UseCors("AllowAll");
            app.UseAuthorization();
            app.MapHub<GameHub>("/game")
				.RequireCors("AllowAll")
				;

			app.Run();
        }
    }
}
