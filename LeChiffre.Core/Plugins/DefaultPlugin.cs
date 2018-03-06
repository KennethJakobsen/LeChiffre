using ACMESharp;
using ACMESharp.ACME;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LeChiffre.Core.Extensions;
using Serilog;
using System;
using System.Collections.Generic;
using System.Net;

namespace LeChiffre.Core.Plugins
{
    public class DefaultPlugin : IPlugin
    {
        public string Name => "Default";

        private ILogger _logger;
        private IAcmeClientService _acmeClientService;
        private AcmeClient _acmeClient;
        private ICertificateService _certificateService;

        public DefaultPlugin(ILogger logger, IAcmeClientService acmeClientService,
            AcmeClient acmeClient, ICertificateService certificateService)
        {
            _logger = logger;
            _acmeClientService = acmeClientService;
            _acmeClient = acmeClient;
            _certificateService = certificateService;
        }

        public void Setup()
        { }

        public IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication website)
        {
            return _acmeClientService.RequestVerificationChallenge(website);
        }

        public AuthorizationState HandleVerificationChallenge(AuthorizationState authorizationState)
        {
            var challenge = _acmeClient.DecodeChallenge(authorizationState, AcmeProtocol.CHALLENGE_TYPE_HTTP);
            var httpChallenge = (HttpChallenge)challenge.Challenge;

            if (httpChallenge == null)
            {
                _logger.Error("Could not decode challenge for {hostname} using the {protocol} protocol",
                    authorizationState.Identifier, AcmeProtocol.CHALLENGE_TYPE_HTTP);
                return null;
            }

            _logger.Information("Expecting response on URL {responseUrl}", httpChallenge.FileUrl);

            CreateChallengeResponse(httpChallenge);
            CheckChallengeResponse(httpChallenge);

            return _acmeClientService.RequestChallengeVerification(authorizationState, challenge);
        }

        private void CreateChallengeResponse(HttpChallenge httpChallenge)
        {
            _logger.Information("Make sure the URL {responseUrl} responds with {response}", httpChallenge.FileUrl, httpChallenge.FileContent);
            _logger.Information("Press a key to continue when the response is available...", httpChallenge.FileUrl, httpChallenge.FileContent);
            Console.ReadKey();
        }

        private void CheckChallengeResponse(HttpChallenge httpChallenge)
        {
            var response = string.Empty;
            var uri = new Uri(httpChallenge.FileUrl);

            try
            {
                using (var webClient = new WebClient())
                {
                    response = webClient.DownloadString(uri);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error getting valid response from URL {url}: {@ex}", uri.ToString(), ex);

                // We'll continue in the hope that maybe Let's Encrypt can read from the URL
            }

            if (response == httpChallenge.FileContent)
                return;

            // TODO: might want to suppress in production
            _logger.Information("Response from URL {responseUrl} does not match the challenge response.", httpChallenge.FileUrl);
            _logger.Information("Response from URL {url} is {response}", httpChallenge.FileUrl, response.Truncate(200));
            _logger.Information("Expected response from URL {url} was {response}", httpChallenge.FileUrl, httpChallenge.FileContent);

            // We'll continue in the hope that maybe Let's Encrypt can read the requested data from the URL
        }

        public string GetCertificate(TargetApplication application)
        {
            return _certificateService.GetCertificate(application);
        }

        public void ConfigureCertificate(string certificatePath)
        { }
    }
}
