using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using Serilog;

namespace LeChiffre.Core
{
    public class Configuration : IConfiguration
    {
        public Uri AcmeServerBaseUri { get; set; } = new Uri(System.Configuration.ConfigurationManager.AppSettings["baseUrl"]);
        public string SignerEmail { get; set; } = System.Configuration.ConfigurationManager.AppSettings["signerEmail"];
        public string SignerFilename { get; set; } = "AcmeClientRS256Signer.txt";
        public string RegistrationFilename { get; set; } = "AcmeClientRegistration.txt";
        public Dictionary<string, IPlugin> Plugins { get; set; } = new Dictionary<string, IPlugin>();

        public string GetBaseOutPutPath(TargetApplication targetApplication)
        {
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, targetApplication.Hostnames.First());
            if (Directory.Exists(basePath) == false)
                Directory.CreateDirectory(basePath);
            return basePath;
        }

        public static ILogger SetupLogger()
        {
            var logger = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: "{Message}{NewLine}{Exception}")
                .WriteTo.File("LeChiffre.log.txt", rollingInterval: RollingInterval.Day)
                .ReadFrom.AppSettings()
                .CreateLogger();
            Log.Logger = logger;
            return logger;
        }
    }
}
