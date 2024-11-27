using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DicomProcessor
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                CreateHostBuilder(args).Build().Run();
                Console.WriteLine("应用程序已启动。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用程序启动失败: {ex.Message}");
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
