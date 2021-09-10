using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MyLaboratory.Common.DataAccess.Data;
using MyLaboratory.WebSite.Models;
using Microsoft.Extensions.Hosting;

namespace MyLaboratory.WebSite
{
    //public class Program
    //{
    //    public static void Main(string[] args)
    //    {
    //        CreateHostBuilder(args).Build().Run();
    //    }

    //    //log level severity: Trace = 0, Debug = 1, Information = 2, Warning = 3, Error = 4, Critical = 5

    //    //AzureAppService log activated by default if application hosted on Azure
    //    //https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1&tabs=aspnetcore2x#appservice

    //    public static IWebHost BuildWebHost(string[] args) =>
    //        WebHost.CreateDefaultBuilder(args)
    //             .ConfigureLogging((hostingContext, logging) =>
    //             {
    //                 logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
    //                 logging.AddConsole();
    //                 logging.AddDebug();
    //             })
    //            .UseStartup<Startup>()
    //            .Build();
    //}
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
