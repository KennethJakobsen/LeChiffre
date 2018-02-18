using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using ACMESharp;
using ACMESharp.ACME;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using Newtonsoft.Json;
using Serilog;

namespace LeChiffre.Core
{
    public class AcmeClientService : IAcmeClientService
    {
        private readonly ILogger _logger;
        private readonly AcmeClient _acmeClient;

        public AcmeClientService(ILogger logger, AcmeClient acmeClient)
        {
            _logger = logger;
            _acmeClient = acmeClient;
        }

        public IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication website)
        {
            var authorizationStates = new List<AuthorizationState>();
            foreach (var hostname in website.Hostnames)
            {
                _logger.Information("Authorizing hostname {hostname} using challenge type {CHALLENGE_TYPE_HTTP}",
                    hostname, AcmeProtocol.CHALLENGE_TYPE_HTTP);

                var authorizationState = _acmeClient.AuthorizeIdentifier(hostname);

                _logger.Information("Authorization status for {hostname} is now {state}",
                    hostname, authorizationState.Status);

                authorizationStates.Add(authorizationState);
            }

            return authorizationStates;
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
            
            return RequestChallengeVerification(authorizationState, challenge);
        }

        private void CreateChallengeResponse(HttpChallenge httpChallenge)
        {
            try
            {
                // TODO: Implement response - this should be done by each plugin
            }
            catch (Exception ex)
            {
                _logger.Error("Error uploading validation {@ex}", ex);

                // We'll continue in the hope that maybe Let's Encrypt can read from the URL
            }
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
            _logger.Information("Response from URL {url} is {response}", httpChallenge.FileUrl, response.Substring(0, 200));
            _logger.Information("Expected response from URL {url} was {response}", httpChallenge.FileUrl, httpChallenge.FileContent);

            // We'll continue in the hope that maybe Let's Encrypt can read the requested data from the URL
        }

        private AuthorizationState RequestChallengeVerification(AuthorizationState authorizationState, AuthorizeChallenge challenge)
        {
            try
            {
                _logger.Information("Submitting answer to authorization server, asking for verification using {challengeType}",
                    AcmeProtocol.CHALLENGE_TYPE_HTTP);

                authorizationState.Challenges = new[] { challenge };
                _acmeClient.SubmitChallengeAnswer(authorizationState, AcmeProtocol.CHALLENGE_TYPE_HTTP, true);

                var retries = 0;
                const int retryTime = 3000;
                while (authorizationState.Status == AuthorizationState.STATUS_PENDING)
                {
                    retries += 1;
                    if (retries > 5)
                        break;

                    _logger.Information("Authorization in progress, attempt {retries}", retries);

                    if (retries > 1)
                        // Give it some time before doing a retry
                        Thread.Sleep(retryTime);

                    var refreshedAuthorization = _acmeClient.RefreshIdentifierAuthorization(authorizationState);
                    if (refreshedAuthorization.Status == AuthorizationState.STATUS_PENDING)
                        continue;

                    _logger.Information("Authorization has updated status from {previousStatus} to {newStatus}",
                        authorizationState.Status, refreshedAuthorization.Status);

                    authorizationState = refreshedAuthorization;

                    // If we've successfully validated then return this new status
                    if (authorizationState.Status == AuthorizationState.STATUS_VALID)
                        break;
                }
            }
            catch (Exception ex)
            {
                if (authorizationState.Status == AuthorizationState.STATUS_INVALID)
                    _logger.Error("Authorization failed with status {status}", authorizationState.Status);

                _logger.Error("Exception: {@ex}", ex);
            }

            return authorizationState;
        }

        public class ValidationFile
        {
            public string DirectoryName { get; set; }
            public string FileContent { get; set; }
            public string Token { get; set; }
        }
    }
}
