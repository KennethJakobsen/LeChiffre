using System;
using System.Collections.Generic;
using LeChiffre.Core.Models;

namespace LeChiffre.Core.Interfaces
{
    public interface IConfiguration
    {
        Uri AcmeServerBaseUri { get; set; }
        string SignerEmail { get; set; }
        string GetBaseOutPutPath(TargetApplication targetApplication);
        string SignerFilename { get; set; }
        string RegistrationFilename { get; set; }
        Dictionary<string, IPlugin> Plugins { get; set; }
    }
}
