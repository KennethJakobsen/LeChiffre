using System.Collections.Generic;
using ACMESharp;
using LeChiffre.Core.Models;

namespace LeChiffre.Core.Interfaces
{
    public interface IAcmeClientService
    {
        IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication website);
        AuthorizationState RequestChallengeVerification(AuthorizationState authorizationState, AuthorizeChallenge challenge);
    }
}
