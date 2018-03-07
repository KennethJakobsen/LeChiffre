using System;
using System.Collections.Generic;
using LeChiffre.Core.Models;

namespace LeChiffre.Core.Interfaces
{
    public interface IConfiguration
    {
        string SignerEmail { get; set; }
        string SignerFilename { get; set; }
        string RegistrationFilename { get; set; }
        Dictionary<string, IPlugin> Plugins { get; set; }

        Uri GetAcmeServerBaseUri(TargetApplication targetApplication);
        string GetBaseOutPutPath(TargetApplication targetApplication);
    }
}
