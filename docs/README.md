# LeChiffre

![NuGet](https://img.shields.io/nuget/v/LeChiffre.Core.svg) [![Join the chat at https://gitter.im/LeChiffreProject/Lobby](https://badges.gitter.im/LeChiffreProject/Lobby.svg)](https://gitter.im/LeChiffreProject/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge) 

A Let's Encrypt CLI implementation for Windows

## What is LeChiffre?

While working with an ACME client for Windows ([Windows ACME Simple (WACS)](https://github.com/PKISharp/win-acme) - previously known as Let's Encrypt Win Simple, LEWS) it turned out that I wanted it to do a little more than it could. After trying to contribute a full rewrite (and failing at that) I tried to make it at least pluggable but LEWS is just not really meant for that at the moment.

So LeChiffre is what I wish WinACME would be: a small wrapper around the [ACMESharp library](https://github.com/ebekker/ACMESharp) which does nothing much on it's own, but plugins make it possible to do anything you want.

By default you get a console app, with one plugin where you have to manually create a verifcation file in your site and you will get a nice certificate generated.

The power is in the plugins for this. With a plugin you can automate the two most important things: making sure that the verification file needed for Let's Encrypt exists and installing the certificate where you need it to.

### That name though...?

Ah yes, the name. While looking for a name that didn't include "Let's Encrypt", the "LE" abbreviation seemed nice. Of course anything that starts with "Le" has to be French. Talking to [my colleague Stéphane](github.com/zpqrtbnk/) to whom I suggested "Le Cryptage" - he said Le Chiffre would make more sense.

Sounded pretty cool! Not only that, here in Denmark we happen to have a famous actor named [Mads Mikkelsen](https://en.wikipedia.org/wiki/Mads_Mikkelsen) who played ["Le Chiffre" in the Bond movie Casino Royale](https://en.wikipedia.org/wiki/Le_Chiffre). Perfect fit.

## Getting started

You can use LeChiffre with the following command with the required parameters:

`LeChiffre.exe --hostnames www.test.com,test.com --certificatepassword abcd1234 --baseurlconfigkey baseUrlStaging`

The parameters used here are:

  - `--hostnames` - a comma separated list of hostnames for which you want to generate a single certificate (you can not mix multiple domains)
  - `-- certificatepassword` - once you generate the `pfx` file, it will have this password
  - `--baseurlconfigkey` - the choices here are `baseUrlStaging` and `baseUrlLive`, these choices are the appSettings you can find in `LeChiffre.exe.config` to choose between using the Let's Encrypt staging server or live server (make sure to alway do all testing on the staging server as you have very strict rate limits on the live server)

  Speaking of the `LeChiffre.exe.config` file: it is advisable to update the `signerEmail` address before you start.

## Plugin authoring

Writing a plugin is pretty simple, here's the steps:

  1. Start a new Class Library project in Visual Studio (.NET 4.5.2 and up)
  2. Important: your project should output a dll that end in `LeChiffre.Plugin.dll` - when loading plugins [we look for `*.LeChiffre.Plugin.dll`](https://github.com/nul800sebastiaan/LeChiffre/blob/master/src/LeChiffre.Core/ContainerRegistration.cs)
  3. Install the LeChiffre.Core NuGet package: `Install-Package LeChiffre.Core` 
  4. Create a class that inherits from `LeChiffre.Core.Interfaces.IPlugin`
  5. Implement the `IPlugin` interface

That's it!

A sample plugin could look something like this:

```
using ACMESharp;
using ACMESharp.ACME;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using Serilog;
using System.Collections.Generic;

namespace Test.LeChiffre.Plugin
{
    public class TestPlugin : IPlugin
    {
        public string Name => "Test";

        private readonly ILogger _logger;
        private readonly IAcmeClientService _acmeClientService;
        private readonly AcmeClient _acmeClient;
        private readonly ICertificateService _certificateService;

        public TestPlugin(ILogger logger, IAcmeClientService acmeClientService,
            AcmeClient acmeClient, ICertificateService certificateService)
        {
            _logger = logger;
            _acmeClientService = acmeClientService;
            _acmeClient = acmeClient;
            _certificateService = certificateService;
        }

        public void Setup(TargetApplication application)
        {
            // Do any work you need here to prepare for getting and installing a certificate
            _logger.Information("> {method} doesn't need to do anything for this plugin", "Setup");
        }

        public IEnumerable<AuthorizationState> RequestVerificationChallenge(TargetApplication application)
        {
            // Let LeChiffre.Core deal with requesting the challenge verification
            return _acmeClientService.RequestVerificationChallenge(application);
        }

        public AuthorizationState HandleVerificationChallenge(TargetApplication application, AuthorizationState authorizationState)
        {
            var challenge = _acmeClient.DecodeChallenge(authorizationState, AcmeProtocol.CHALLENGE_TYPE_HTTP);
            var httpChallenge = (HttpChallenge)challenge.Challenge;

            if (httpChallenge == null)
            {
                _logger.Error("Could not decode challenge for {hostname} using the {protocol} protocol",
                    authorizationState.Identifier, AcmeProtocol.CHALLENGE_TYPE_HTTP);
                return null;
            }

            _logger.Information("Expecting response on URL {responseUrl}", httpChallenge.FileUrl);

            // Create the response that Let's Encrypt Expects
            
            // Then ask Let's Encrypt to check it
            return _acmeClientService.RequestChallengeVerification(authorizationState, challenge);
        }
        
        public string GetCertificate(TargetApplication application)
        {
            // Let LeChiffre.Core deal with creating the certificate
            return _certificateService.GetCertificate(application);
        }

        public void ConfigureCertificate(TargetApplication application, string certificatePath)
        {
            // Install your certificate here
            _logger.Information("> {method} doesn't need to do anything for this plugin", "ConfigureCertificate");
        }
    }
}

```

## Contributing

Make sure to refer to [the contributing documentation](CONTRIBUTING.md) to learn how to contribute to this project.

## Contact 

[Join the chat on Gitter](https://gitter.im/LeChiffreProject/Lobby)

## Licens Licensee
Copyright © 2018-Present - Sebastiaan Janssen

Licensed under the [MIT License](../LICENSE)