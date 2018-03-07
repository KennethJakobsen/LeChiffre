using System;
using System.Collections.Generic;
using System.Threading;
using ACMESharp;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
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

        public AuthorizationState RequestChallengeVerification(AuthorizationState authorizationState, AuthorizeChallenge challenge)
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

                    if(refreshedAuthorization.Status != authorizationState.Status) 
                        _logger.Information("Authorization has updated status from {previousStatus} to {newStatus}",
                            authorizationState.Status, refreshedAuthorization.Status);

                    if (refreshedAuthorization.Status != AuthorizationState.STATUS_VALID)
                    {
                        // Update the state for the next retry
                        authorizationState = refreshedAuthorization;

                        // We're not at valid yet, retry
                        continue;
                    }

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
    }
}
