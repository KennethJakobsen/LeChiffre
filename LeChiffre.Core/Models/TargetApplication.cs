using System.Collections.Generic;
using CommandLine;

namespace LeChiffre.Core.Models
{
    public class TargetApplication
    {
        [Option(HelpText = "Comma separated list of hostnames to get a certificate for", Required = true, Separator = ',')]
        public IEnumerable<string> Hostnames { get; set; }

        [Option(HelpText = "The PFX password to set for the certificates", Required = true)]
        public string CertificatePassword { get; set; }

        [Option(HelpText = "The name of a plugin to use")]
        public string Plugin { get; set; }
    }
}
