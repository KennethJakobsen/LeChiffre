using ACMESharp;
using LeChiffre.Core.Models;

namespace LeChiffre.Core.Interfaces
{
    public interface IAcmeClientConfigurationService
    {
        AcmeClient Configure(TargetApplication targetApplication);
    }
}
