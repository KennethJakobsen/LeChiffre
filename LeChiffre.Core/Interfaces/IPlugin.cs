using ACMESharp;
using LeChiffre.Core.Models;
using System.Collections.Generic;

namespace LeChiffre.Core.Interfaces
{
    public interface IPlugin
    {
        // A unique name for this plugin
        string Name { get; }
        void Setup();
        IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication application);
        AuthorizationState HandleVerificationChallenge(AuthorizationState authorizationState);
        string GetCertificate(TargetApplication application);
        void ConfigureCertificate(string certificatePath);
    }
}
