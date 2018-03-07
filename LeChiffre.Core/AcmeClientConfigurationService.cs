using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ACMESharp;
using ACMESharp.JOSE;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using Serilog;

namespace LeChiffre.Core
{
    public class AcmeClientConfigurationService : IAcmeClientConfigurationService
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public AcmeClientConfigurationService(ILogger logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public AcmeClient Configure(TargetApplication targetApplication)
        {
            var contacts = NormalizeContacts();
            var signer = new RS256Signer();

            var basePath = _configuration.GetBaseOutPutPath(targetApplication);
            ConfigureSigner(signer, basePath);

            var acmeServerBaseUri = _configuration.GetAcmeServerBaseUri(targetApplication);
            var acmeClient = new AcmeClient(acmeServerBaseUri, new AcmeServerDirectory(), signer);
            acmeClient.Init();
            acmeClient.GetDirectory(saveRelative: true);

            ProcessRegistration(acmeClient, contacts.ToArray(), basePath);

            return acmeClient;
        }

        private IEnumerable<string> NormalizeContacts()
        {
            var normalizedContacts = new List<string>();
            var email = _configuration.SignerEmail;
            _logger.Debug("Adding registration email: {contact}", email);
            if (email.StartsWith("mailto:", StringComparison.InvariantCultureIgnoreCase) == false)
                email = "mailto:" + email;
            normalizedContacts.Add(email);

            return normalizedContacts;
        }

        private void ConfigureSigner(ISigner signer, string basePath)
        {
            signer.Init();
            var signerPath = Path.Combine(basePath, _configuration.RegistrationFilename);
            if (File.Exists(signerPath))
            {
                _logger.Information("Loading signer from file {signerPath}", signerPath);
                using (var signerStream = File.OpenRead(signerPath))
                    signer.Load(signerStream);
            }
            else
            {
                _logger.Information("Saving signer to {signerPath}", signerPath);
                using (var signerStream = File.OpenWrite(signerPath))
                    signer.Save(signerStream);
            }
        }

        private void ProcessRegistration(AcmeClient acmeClient, string[] contacts, string basePath)
        {
            var registrationPath = Path.Combine(basePath, _configuration.SignerFilename);
            if (File.Exists(registrationPath))
            {
                _logger.Information("Loading Registration from {registrationPath}", registrationPath);
                using (var registrationStream = File.OpenRead(registrationPath))
                    acmeClient.Registration = AcmeRegistration.Load(registrationStream);
            }
            else
            {
                _logger.Information("Registering AcmeClient with contacts {@contacts}", contacts);
                acmeClient.Register(contacts);
                acmeClient.UpdateRegistration(useRootUrl: true, agreeToTos: true);

                _logger.Information("Saving Registration to {registrationPath}", registrationPath);
                using (var registrationStream = File.OpenWrite(registrationPath))
                    acmeClient.Registration.Save(registrationStream);
            }
        }
    }
}
