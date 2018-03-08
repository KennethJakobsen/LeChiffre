using System;
using System.Linq;
using System.Net;
using ACMESharp;
using CommandLine;
using LeChiffre.Core;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using LightInject;
using Serilog;

namespace LeChiffre
{
    internal class Program
    {
        private static ILogger _logger;

        private static void Main(string[] args)
        {
            // Need to specify allowed protocols to communicate over, else .NET might try to use old protocols
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            _logger = Configuration.SetupLogger();

#if DEBUG
            _logger.Information("Application was built in debug mode. Now would be a good time to attach a debugger.");
            _logger.Information("Press any key to continue...");
            Console.ReadKey();
#endif

            try
            {
                var targetApplication = ParseCommandlineInput(args);
                if (targetApplication == null)
                    return;

                if (targetApplication.Debug)
                {
                    _logger.Information("Now would be a good time to attach a debugger.");
                    _logger.Information("Press any key to continue...");
                    Console.ReadKey();
                }

                _logger.Information("Setting up application");
                var container = ContainerRegistration.SetupLightInjectContainer(targetApplication, _logger);

                var configuration = container.GetInstance<IConfiguration>();
                var plugins = container.GetAllInstances<IPlugin>().ToList();
                // Get the plugin from the command line args, if it can't be found, use the Default one
                var selectedPlugin = plugins.FirstOrDefault(p => string.Equals(p.Name, targetApplication.Plugin, StringComparison.InvariantCultureIgnoreCase)) ??
                                     plugins.FirstOrDefault(p => p.Name == "Default");

                var acmeServerBaseUri = configuration.GetAcmeServerBaseUri(targetApplication);
                _logger.Information("Starting LeChiffre with the {pluginName} plugin, using Acme Server {acmeServer}", selectedPlugin.Name, acmeServerBaseUri);

                _logger.Information("Calling plugin's {method} method", "Setup");
                selectedPlugin.Setup(targetApplication);

                _logger.Information("Calling plugin's {method} method", "RequestVerificationChallenge");
                var authorizationStates = selectedPlugin.RequestVerificationChallenge(targetApplication).ToList();

                foreach (var authorizationState in authorizationStates)
                {
                    _logger.Information("Calling plugin's {method} method", "HandleVerificationChallenge");
                    selectedPlugin.HandleVerificationChallenge(targetApplication, authorizationState);
                }

                var allGood = authorizationStates.All(authorizationState => authorizationState.Status == AuthorizationState.STATUS_VALID);
                if (allGood)
                {
                    _logger.Information("All hostnames have been validated, generating certificates");
                    _logger.Information("Calling plugin's {method} method", "GetCertificate");
                    var certificatePath = selectedPlugin.GetCertificate(targetApplication);

                    _logger.Information("Calling plugin's {method} method", "ConfigureCertificate");
                    selectedPlugin.ConfigureCertificate(targetApplication, certificatePath);
                }
                else
                {
                    var badStates = authorizationStates.Where(authorizationState => authorizationState.Status != AuthorizationState.STATUS_VALID);
                    foreach (var state in badStates)
                    {
                        _logger.Warning("Can't get certificate, the authorization state for Id {identifier} is {authorizationState}", state.Identifier, state.Status);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("Exception thrown: {@exception}", e);
            }

#if DEBUG
            _logger.Information("Quitting. Press any key to continue...");
            Console.ReadKey();
#endif
        }


        public static TargetApplication ParseCommandlineInput(string[] args)
        {
            try
            {
                var commandLineParseResult = Parser.Default.ParseArguments<TargetApplication>(args);
                if (!(commandLineParseResult is Parsed<TargetApplication> parsed))
                    return null;

                var targetApplication = parsed.Value;
                _logger.Information("Parsed input: {@options}", targetApplication);
                return targetApplication;
            }
            catch (Exception e)
            {
                _logger.Error("Failed parsing command line input. {@e}", e);
                throw;
            }
        }
    }
}
