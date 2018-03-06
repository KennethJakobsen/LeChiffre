using System;
using System.Collections.Generic;
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

                _logger.Information("Setting up application");
                var container = ContainerRegistration.SetupLightInjectContainer(targetApplication, _logger);

                var plugins = container.GetAllInstances<IPlugin>();
                var selectedPlugin = plugins.FirstOrDefault(p => p.Name == targetApplication.Plugin);
                if (selectedPlugin != null)
                {
                    _logger.Information("Starting LeChiffre with the {pluginName} plugin", selectedPlugin.Name);

                    selectedPlugin.Setup();
                    var authorizationStates = selectedPlugin.RequestVerificationChallenge(targetApplication).ToList();
                    foreach (var authorizationState in authorizationStates)
                        selectedPlugin.HandleVerificationChallenge(authorizationState);
                    var allGood = authorizationStates.All(authorizationState => authorizationState.Status == AuthorizationState.STATUS_VALID);
                    if (allGood)
                    {
                        _logger.Information("All hostnames have been validated, generating certificates");
                        var certificatePath = selectedPlugin.GetCertificate(targetApplication);
                        selectedPlugin.ConfigureCertificate(certificatePath);
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
                else
                {
                    _logger.Information("Could not find plugin with name {pluginName}", targetApplication.Plugin);
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
