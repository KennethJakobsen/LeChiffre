using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LeChiffre.Core.Plugins;
using LightInject;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace LeChiffre.Core
{
    public class ContainerRegistration
    {
        public static ServiceContainer SetupLightInjectContainer(TargetApplication targetApplication, ILogger logger = null)
        {
            using (var container = new ServiceContainer())
            {
                container.Register<IConfiguration, Configuration>();

                if (logger == null)
                    logger = Configuration.SetupLogger();
                container.Register(factory => logger);

                container.Register<IAcmeClientConfigurationService, AcmeClientConfigurationService>();

                // Get an AcmeClient so we can create a new AcmeClientService using the initialized instance
                var acmeClientService = container.GetInstance<IAcmeClientConfigurationService>();
                var acmeClient = acmeClientService.Configure(targetApplication);
                container.Register(factory => acmeClient);

                container.Register<IAcmeClientService, AcmeClientService>();
                container.Register<ICertificateService, CertificateService>();
                container.Register<IPlugin, DefaultPlugin>();

                RegisterPlugins(container);

                return container;
            }
        }

        private static void RegisterPlugins(ServiceContainer container)
        {
            var logger = container.GetInstance<ILogger>();

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var allDlls = Directory.EnumerateFileSystemEntries(baseDir, "*.LeChiffre.Plugin.dll");

            var assemblies = new List<Assembly>();

            foreach (var file in allDlls)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(file);
                    var assembly = Assembly.Load(assemblyName);
                    var types = assembly.GetExportedTypes();
                    foreach (var type in types)
                    {
                        if (type.IsClass == false || type.IsNotPublic)
                            continue;

                        if (typeof(IPlugin).IsAssignableFrom(type))
                            assemblies.Add(assembly);
                    }
                }
                catch (Exception e)
                {
                    logger.Debug(e, $"Couldn't load assembly from file {file}");
                }
            }

            foreach (var assembly in assemblies)
                container.RegisterAssembly(assembly);
        }
    }
}
