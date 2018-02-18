using LeChiffre.Core.Models;

namespace LeChiffre.Core.Interfaces
{
    public interface ICertificateService
    {
        string GetCertificate(TargetApplication application);
    }
}
