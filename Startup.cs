using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DicomProcessor
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            // 如果需要跨域访问，可以配置 CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", builder =>
                {
                    builder.AllowAnyOrigin()
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                });
            });

            // 注册其他服务，例如数据库上下文、身份验证等
            // services.AddDbContext<MyDbContext>(options => ...);
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // app.UseHsts(); // 去掉 HSTS 支持
            }

            // 去掉 HTTPS 重定向
            // app.UseHttpsRedirection(); 

            app.UseStaticFiles();
            app.UseRouting();

            // 使用 CORS 策略
            app.UseCors("AllowAllOrigins");

            app.UseAuthorization(); // 如果使用身份验证，确保在路由之前调用

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
