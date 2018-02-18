using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACMESharp;
using LeChiffre.Core;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LightInject;
using NUnit.Framework;

namespace LeChiffre.Test
{
    public class AcmeAuthorizationTests
    {
        private IAcmeClientService AcmeClientService { get; set; }
        private ICertificateService CertificateService { get; set; }
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

            var container = ContainerRegistration.SetupLightInjectContainer(TargetApplication);
            var configuration = container.GetInstance<IConfiguration>();

            AcmeClientService = container.GetInstance<IAcmeClientService>();
            CertificateService = container.GetInstance<ICertificateService>();

            // Cleanup authorization files if they exist to make sure we start with a clean slate
            var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TargetApplication.Hostnames.First());
            var signerFile = Path.Combine(basePath, configuration.SignerFilename);
            var registrationFile = Path.Combine(basePath, configuration.RegistrationFilename);

            if (File.Exists(signerFile))
                File.Delete(signerFile);

            if (File.Exists(registrationFile))
                File.Delete(registrationFile);
        }

        [Test]
        public void Can_Get_Authorization_Results()
        {
            var authorizationStates = AcmeClientService.RequestVerificationChallenge(TargetApplication).ToList();

            Assert.IsNotEmpty(authorizationStates);

            foreach (var authorizationState in authorizationStates)
            {
                // Make sure the hostname in the authorization result is one that we actually sent in
                Assert.IsTrue(TargetApplication.Hostnames.Any(x => string.Equals(x, authorizationState.Identifier, StringComparison.InvariantCultureIgnoreCase)));

                // Since we can't authorize using HTTP from a unit test we'll be happy if all tests end in a pending status
                Assert.AreEqual(authorizationState.Status, AuthorizationState.STATUS_PENDING);
            }
        }

        [Test]
        public void Can_Handle_Authorization_Challenge()
        {
            var authorizationStates = AcmeClientService.RequestVerificationChallenge(TargetApplication).ToList();

            Assert.IsNotEmpty(authorizationStates);

            var hanledAuthorizationStates = new List<AuthorizationState>();
            foreach (var authorizationState in authorizationStates)
                hanledAuthorizationStates.Add(AcmeClientService.HandleVerificationChallenge(authorizationState));

            foreach (var authorizationState in hanledAuthorizationStates)
                Assert.AreEqual(authorizationState.Status, AuthorizationState.STATUS_VALID);
        }

        [Test]
        public void Can_Get_Certificate()
        {
            var authorizationStates = AcmeClientService.RequestVerificationChallenge(TargetApplication).ToList();

            Assert.IsNotEmpty(authorizationStates);

            var hanledAuthorizationStates = new List<AuthorizationState>();
            foreach (var authorizationState in authorizationStates)
                hanledAuthorizationStates.Add(AcmeClientService.HandleVerificationChallenge(authorizationState));

            foreach (var authorizationState in hanledAuthorizationStates)
                Assert.AreEqual(authorizationState.Status, AuthorizationState.STATUS_VALID);

            CertificateService.GetCertificate(TargetApplication);
        }
    }
}
