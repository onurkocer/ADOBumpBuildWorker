/*
 * This Sample Code is provided for the purpose of illustration only and is not
 * intended to be used in a production environment.
 * THIS SAMPLE CODE AND ANY RELATED INFORMATION ARE PROVIDED "AS IS" WITHOUT 
 * WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED
 * TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR 
 * PURPOSE.  
 * We (Microsoft) grant You a nonexclusive, royalty-free right to use and modify the Sample
 * Code and to reproduce and distribute the object code form of the Sample 
 * Code, provided that You agree: 
 *   (i)  to not use Our name, logo, or trademarks to market Your software 
 *        product in which the Sample Code is embedded; 
 *   (ii) to include a valid copyright notice on Your software product in which 
 *        the Sample Code is embedded; and 
 *  (iii) to indemnify, hold harmless, and defend Us and Our suppliers from and 
 *        against any claims or lawsuits, including attorneys’ fees, that arise 
 *        or result from the use or distribution of the Sample Code.
*/


using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;

namespace BumpBuildWorker
{   
    public class Program
    {
        private static string cronExpression;
        private static string organizationName;
        private static string organizationBaseUrl;
        private static string pat;
        private static string credentials;
        private static IList<string> poolNames;
        private const string BaseUrl = "https://dev.azure.com/";
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
               .ReadFrom.Configuration(Configuration)
               .Enrich.FromLogContext()
               .CreateLogger();

            Log.Logger.Information("Worker Service is UP!!!");

            // Read job configuration
            cronExpression = Configuration.GetSection("CronExpression").Value;

            // Read Azure DevOps configuration from appsettings file
            organizationName = Configuration.GetSection("OrganizationName").Value;
            pat = Configuration.GetSection("Pat").Value;
            poolNames = Configuration.GetSection("PoolNames")?.Get<IList<string>>();

            // Set org baseUrl and credentials with using pat value.
            organizationBaseUrl = $"{BaseUrl}{organizationName}/";
            credentials = Convert.ToBase64String(ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", pat)));

            if (!ValidateArguments(organizationName, pat, cronExpression, 
                poolNames, out string validationMessage))
            {
                Log.Logger.Error($"Could not validate arguments from config file. {validationMessage}");
                return;
            }

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddCronJob<BuildBumpJob>(c =>
                    {
                        c.TimeZoneInfo = TimeZoneInfo.Local;
                        c.CronExpression = cronExpression;
                    });

                    // Adding httpclient service to use httpclients from same pool for multiple requests.
                    services.AddHttpClient("adoclient", c =>
                    {
                        c.BaseAddress = new Uri(organizationBaseUrl);
                        c.DefaultRequestHeaders.Accept.Clear();
                        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    });
                })
            .UseSerilog();

        private static bool ValidateArguments(string organizationName, string pat, 
            string cronExpression, IList<string> poolNames, out string validationMessage)
        {
            bool result = true;
            validationMessage = "Missing arguments: ";
            List<string> missingArgs = new List<string>();

            if (string.IsNullOrEmpty(organizationName))
            {
                missingArgs.Add("Azure DevOps organization name");
                result = false;
            }

            if (string.IsNullOrEmpty(pat))
            {
                missingArgs.Add("Azure DevOps pat value");
                result = false;
            }

            if (string.IsNullOrEmpty(cronExpression))
            {
                missingArgs.Add("cronExpression value");
                result = false;
            }

            if (poolNames == null || !poolNames.Any())
            {
                missingArgs.Add("Pool Names");
                result = false;
            }

            validationMessage += string.Join(",", missingArgs);

            return result;
        }
    }
}