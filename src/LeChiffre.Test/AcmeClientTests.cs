using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LeChiffre.Core;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LightInject;
using NUnit.Framework;

namespace LeChiffre.Test
{
    public class AcmeClientTests
    {
        private IConfiguration _configuration;
        private ServiceContainer Container { get; set; }
        private TargetApplication TargetApplication { get; set; }
        
        [SetUp]
        public void Initialize()
        {
            var hostnames = new List<string> { "cork.nl", "www.cork.nl" };
            TargetApplication = new TargetApplication
            {
                CertificatePassword = "abcd1234",
                Hostnames = hostnames
            };

            Container = ContainerRegistration.SetupLightInjectContainer(TargetApplication);
            _configuration = Container.GetInstance<IConfiguration>();

            // Cleanup authorization files if they exist to make sure we start with a clean slate
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetApplication.Hostnames.First());
            var signerFile = Path.Combine(basePath, _configuration.SignerFilename);
            var registrationFile = Path.Combine(basePath, _configuration.RegistrationFilename);

            if (File.Exists(signerFile))
                File.Delete(signerFile);

            if (File.Exists(registrationFile))
                File.Delete(registrationFile);
        }

        [Test]
        public void Can_Configure_Clean_AcmeClient()
        {
            var acmeClientService = Container.GetInstance<IAcmeClientConfigurationService>();
            var acmeClient = acmeClientService.Configure(TargetApplication);

            Assert.IsTrue(acmeClient.Initialized);
            Assert.NotNull(acmeClient.Registration.Contacts.ToList().First(x => x == $"mailto:{_configuration.SignerEmail}"));
        }

        [Test]
        public void Can_Configure_AcmeClient_Repeatedly()
        {
            var acmeClientService = Container.GetInstance<IAcmeClientConfigurationService>();
            var acmeClient = acmeClientService.Configure(TargetApplication);

            Assert.IsTrue(acmeClient.Initialized);
            Assert.NotNull(acmeClient.Registration.Contacts.ToList().First(x => x == $"mailto:{_configuration.SignerEmail}"));

            // The client saves the Signer and Registration file so the registration can be reloaded, second attempt should load these
            acmeClient = acmeClientService.Configure(TargetApplication);
            Assert.IsTrue(acmeClient.Initialized);
            Assert.NotNull(acmeClient.Registration.Contacts.ToList().First(x => x == $"mailto:{_configuration.SignerEmail}"));
        }
    }
}
