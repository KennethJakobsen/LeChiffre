using ACMESharp;
using LeChiffre.Core.Models;
using System.Collections.Generic;

namespace LeChiffre.Core.Interfaces
{
    public interface IPlugin
    {
        // A unique name for this plugin
        string Name { get; }
        void Setup(TargetApplication application);
        IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication application);
        AuthorizationState HandleVerificationChallenge(TargetApplication application, AuthorizationState authorizationState);
        string GetCertificate(TargetApplication application);
        void ConfigureCertificate(TargetApplication application, string certificatePath);
    }
}
