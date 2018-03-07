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

        [Option(HelpText = "Any string at all can be added here. Useful for passing in extra information to your plugin")]
        public string AdditionalInformation { get; set; }

        [Option(HelpText = "Ask for a keypress so you have time to attach a debugger")]
        public bool Debug { get; set; }

        [Option(HelpText = "Which AppSetting to use for the Acme Server base Url", Required = true)]
        public string BaseUrlConfigKey { get; internal set; }
    }
}
