using Amazon;
using Amazon.S3;
using MyHRExample.Services;

namespace MyHRExample
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var aws = builder.Configuration.GetSection("AWS");

            builder.Services.AddSingleton<IAmazonS3>(provider =>
            {
                return new AmazonS3Client(
                    aws["AccessKey"],
                    aws["SecretKey"],
                    RegionEndpoint.GetBySystemName(aws["Region"])
                );
            });

            builder.Services.AddScoped<S3Service>(provider =>
            {
                var s3 = provider.GetRequiredService<IAmazonS3>();
                return new S3Service(s3, aws["BucketName"]!);
            });

            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
