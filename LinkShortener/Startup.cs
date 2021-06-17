using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using System.Linq;

namespace LinkShortener
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // Этот метод вызывается средой выполнения.Используйте этот метод для добавления служб в контейнер.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();
            services.AddSingleton<ILiteDatabase, LiteDatabase>(_ => new LiteDatabase("short-links.db"));//Создаем базу данных LiteDB
        }

        //Этот метод вызывается средой выполнения.Используйте этот метод для настройки конвейера HTTP-запросов.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapPost("/shorten", HandleShortenUrl);

                endpoints.MapFallback(HandleRedirect);

            });

        }

        static Task HandleRedirect(HttpContext context)
        {
            var db = context.RequestServices.GetService<ILiteDatabase>();
            var collection = db.GetCollection<ShortLink>();

            var path = context.Request.Path.ToUriComponent().Trim('/');
            var id = ShortLink.GetId(path);
            var entry = collection.Find(p => p.Id == id).FirstOrDefault();

            if (entry != null)
                context.Response.Redirect(entry.Url);
            else
                context.Response.Redirect("/");

            return Task.CompletedTask;
        }

        static Task HandleShortenUrl(HttpContext context)
        {
            // Perform basic form validation
            if (!context.Request.HasFormContentType || !context.Request.Form.ContainsKey("url"))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return context.Response.WriteAsync("Cannot process request");
            }

            context.Request.Form.TryGetValue("url", out var formData);
            var requestedUrl = formData.ToString();

            // Test our URL
            if (!Uri.TryCreate(requestedUrl, UriKind.Absolute, out Uri result))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return context.Response.WriteAsync("Could not understand URL.");
            }


            var url = result.ToString();

            var liteDB = context.RequestServices.GetService<ILiteDatabase>();
            var links = liteDB.GetCollection<ShortLink>(BsonAutoId.Int32);

            // Temporary short link 
            var entry = new ShortLink
            {
                Url = url
            };

            links.Insert(entry);


            var urlChunk = entry.GetUrlChunk();
            var responseUri = $"{context.Request.Scheme}://{context.Request.Host}/{urlChunk}";
            context.Response.Redirect($"/#{responseUri}");
            return Task.CompletedTask;
        }
    }
}
