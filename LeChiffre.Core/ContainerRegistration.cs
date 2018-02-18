using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LightInject;
using Serilog;

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

                return container;
            }
        }
    }
}
