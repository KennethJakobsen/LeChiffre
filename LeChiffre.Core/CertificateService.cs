using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using ACMESharp;
using ACMESharp.HTTP;
using ACMESharp.JOSE;
using ACMESharp.PKI;
using ACMESharp.PKI.RSA;
using LeChiffre.Core.Interfaces;
using LeChiffre.Core.Models;
using Serilog;

namespace LeChiffre.Core
{
    public class CertificateService : ICertificateService
    {
        private readonly ILogger _logger;
        private readonly AcmeClient _acmeClient;
        private readonly IConfiguration _configuration;

        public CertificateService(ILogger logger, AcmeClient acmeClient, IConfiguration configuration)
        {
            _logger = logger;
            _acmeClient = acmeClient;
            _configuration = configuration;
        }

        public string GetCertificate(TargetApplication targetApplication)
        {
            using (var pkiTool = PkiToolExtManager.GetPkiTool("BouncyCastle"))
            {
                var rsaKeyParams = new RsaPrivateKeyParams();
                var rsaKeys = pkiTool.GeneratePrivateKey(rsaKeyParams);

                var certificateName = targetApplication.Hostnames.First();
                var baseOutPutPath = _configuration.GetBaseOutPutPath(targetApplication);
                var certificateFolder = Path.Combine(baseOutPutPath, $"{DateTime.Now:yyyyMMdd_HHmmss}");

                if (Directory.Exists(certificateFolder) == false)
                    Directory.CreateDirectory(certificateFolder);

                var csrDetails = new CsrDetails
                {
                    CommonName = certificateName,
                    AlternativeNames = targetApplication.Hostnames,
                };

                var certificateSigningRequest = pkiTool.GenerateCsr(
                    new CsrParams { Details = csrDetails }, rsaKeys, Crt.MessageDigest.SHA256);

                byte[] certificateSigningRequestBytes;
                using (var bs = new MemoryStream())
                {
                    pkiTool.ExportCsr(certificateSigningRequest, EncodingFormat.DER, bs);
                    certificateSigningRequestBytes = bs.ToArray();
                }

                var certificateSigningRequestBytesBase64 = JwsHelper.Base64UrlEncode(certificateSigningRequestBytes);

                _logger.Information("Requesting a certificate");
                var certificateRequest = _acmeClient.RequestCertificate(certificateSigningRequestBytesBase64);

                _logger.Debug("Result of certificate request {certificateRequest}", certificateRequest);
                _logger.Information("Certificate request status is {statusCode}", certificateRequest.StatusCode);

                if (certificateRequest.StatusCode == HttpStatusCode.Created)
                {
                    var keyGenFile = Path.Combine(certificateFolder, $"{certificateName}-gen-key.json");
                    var keyPemFile = Path.Combine(certificateFolder, $"{certificateName}-key.pem");
                    var csrGenFile = Path.Combine(certificateFolder, $"{certificateName}-gen-csr.json");
                    var csrPemFile = Path.Combine(certificateFolder, $"{certificateName}-csr.pem");
                    var crtDerFile = Path.Combine(certificateFolder, $"{certificateName}-crt.der");
                    var crtPemFile = Path.Combine(certificateFolder, $"{certificateName}-crt.pem");
                    var chainPemFile = Path.Combine(certificateFolder, $"{certificateName}-chain.pem");

                    using (var fileStream = new FileStream(keyGenFile, FileMode.Create))
                        pkiTool.SavePrivateKey(rsaKeys, fileStream);
                    using (var fileStream = new FileStream(keyPemFile, FileMode.Create))
                        pkiTool.ExportPrivateKey(rsaKeys, EncodingFormat.PEM, fileStream);
                    using (var fileStream = new FileStream(csrGenFile, FileMode.Create))
                        pkiTool.SaveCsr(certificateSigningRequest, fileStream);
                    using (var fileStream = new FileStream(csrPemFile, FileMode.Create))
                        pkiTool.ExportCsr(certificateSigningRequest, EncodingFormat.PEM, fileStream);

                    _logger.Information("Saving certificate to {crtDerFile}", crtDerFile);
                    using (var file = File.Create(crtDerFile))
                        certificateRequest.SaveCertificate(file);

                    Crt crt;
                    using (FileStream source = new FileStream(crtDerFile, FileMode.Open), target = new FileStream(crtPemFile, FileMode.Create))
                    {
                        crt = pkiTool.ImportCertificate(EncodingFormat.DER, source);
                        pkiTool.ExportCertificate(crt, EncodingFormat.PEM, target);
                    }

                    var issuerCertificate = GetIssuerCertificate(certificateRequest, pkiTool, certificateFolder);
                    using (FileStream intermediate = new FileStream(issuerCertificate, FileMode.Open),
                        certificate = new FileStream(crtPemFile, FileMode.Open), chain = new FileStream(chainPemFile, FileMode.Create))
                    {
                        certificate.CopyTo(chain);
                        intermediate.CopyTo(chain);
                    }

                    var crtPfxFile = Path.Combine(certificateFolder, $"{targetApplication.Hostnames.First()}.pfx");

                    _logger.Information("Saving certificate for hostname {host} and alternative hostnames {@altHostNames} to {crtPfxFile}",
                        targetApplication.Hostnames.First(), targetApplication.Hostnames.Skip(1), crtPfxFile);

                    using (FileStream source = new FileStream(issuerCertificate, FileMode.Open),
                        target = new FileStream(crtPfxFile, FileMode.Create))
                    {
                        try
                        {
                            var importCertificate = pkiTool.ImportCertificate(EncodingFormat.PEM, source);
                            pkiTool.ExportArchive(rsaKeys, new[] { crt, importCertificate }, ArchiveFormat.PKCS12, target,
                                targetApplication.CertificatePassword);
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Error exporting archive {@ex}", ex);
                        }
                    }

                    pkiTool.Dispose();

                    return crtPfxFile;
                }

                _logger.Error("Request status is {statusCode}", certificateRequest.StatusCode);
                throw new Exception($"Request status = {certificateRequest.StatusCode}");
            }
        }

        private string GetIssuerCertificate(CertificateRequest certificate, IPkiTool cp, string certificateFolder)
        {
            var linksEnum = certificate.Links;
            if (linksEnum == null)
                return null;

            var links = new LinkCollection(linksEnum);
            var upLink = links.GetFirstOrDefault("up");
            if (upLink == null)
                return null;

            var temporaryFileName = Path.GetTempFileName();
            try
            {
                using (var web = new WebClient())
                {
                    var uri = new Uri(_configuration.AcmeServerBaseUri, upLink.Uri);
                    web.DownloadFile(uri, temporaryFileName);
                }

                var cacert = new X509Certificate2(temporaryFileName);
                var sernum = cacert.GetSerialNumberString();

                var cacertDerFile = Path.Combine(certificateFolder, $"ca-{sernum}-crt.der");
                var cacertPemFile = Path.Combine(certificateFolder, $"ca-{sernum}-crt.pem");

                if (!File.Exists(cacertDerFile))
                    File.Copy(temporaryFileName, cacertDerFile, true);

                _logger.Information("Saving issuer certificate to {cacertPemFile}", cacertPemFile);
                if (File.Exists(cacertPemFile))
                    return cacertPemFile;

                using (FileStream source = new FileStream(cacertDerFile, FileMode.Open),
                    target = new FileStream(cacertPemFile, FileMode.Create))
                {
                    var caCrt = cp.ImportCertificate(EncodingFormat.DER, source);
                    cp.ExportCertificate(caCrt, EncodingFormat.PEM, target);
                }

                return cacertPemFile;
            }
            finally
            {
                if (File.Exists(temporaryFileName))
                    File.Delete(temporaryFileName);
            }
        }
    }
}
