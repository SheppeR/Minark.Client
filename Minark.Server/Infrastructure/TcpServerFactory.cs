#pragma warning disable SYSLIB0057

using System.Security.Cryptography.X509Certificates;
using WatsonTcp;

namespace Minark.Server.Infrastructure;

/// <summary>
///     Construit un <see cref="WatsonTcpServer" /> avec TLS strict.
///     - TLS est obligatoire : si <c>Tls:Enabled</c> est à <c>false</c>, le démarrage échoue.
///     - Le certificat doit être fourni via <c>Tls:CertPath</c> ; aucun fallback auto-signé.
///     - Les certificats invalides sont toujours rejetés (pas de contournement possible en config).
/// </summary>
public class TcpServerFactory(IConfiguration config, ILogger<TcpServerFactory> logger)
{
    public WatsonTcpServer Create()
    {
        var host = config["Server:Host"] ?? "0.0.0.0";
        var port = int.Parse(config["Server:Port"] ?? "9000");
        var tlsEnabled = config.GetValue<bool>("Tls:Enabled");

        if (!tlsEnabled)
        {
            throw new InvalidOperationException(
                "TLS est obligatoire. Configurez 'Tls:Enabled=true' et fournissez un certificat valide via 'Tls:CertPath'.");
        }

        var cert = LoadCertificate();
        var server = new WatsonTcpServer(host, port, cert);

        // Pas de mutual TLS : le serveur ne demande pas de certificat client.
        // AcceptInvalidCertificates doit être true dans ce mode, sinon WatsonTcp rejette
        // toute connexion avec "RemoteCertificateNotAvailable". Ce flag concerne uniquement
        // la validation du certificat CLIENT (pas du serveur).
        // La sécurité TLS reste pleinement assurée côté client, qui lui valide strictement
        // le certificat du serveur contre les CA reconnues (Let's Encrypt en prod).
        server.Settings.MutuallyAuthenticate = false;
        server.Settings.AcceptInvalidCertificates = true;

        // TCP keepalives : indispensable pour détecter rapidement les clients déconnectés
        // brutalement (câble débranché, crash, OS kill). Sans ça, le serveur garde des GUID
        // "zombies" en mémoire indéfiniment, ce qui casse PushStatusToFriendsAsync et autres.
        // 15s sans réponse → 3 probes de 5s → déconnexion effective après ~30s max.
        server.Keepalive.EnableTcpKeepAlives = true;
        server.Keepalive.TcpKeepAliveTime = 15; // délai avant 1er probe (secondes)
        server.Keepalive.TcpKeepAliveInterval = 5; // délai entre probes
        server.Keepalive.TcpKeepAliveRetryCount = 3; // nb de probes avant déconnexion

        server.Settings.Logger = (_, msg) => logger.LogDebug("[WatsonTcp] {Msg}", msg);
        return server;
    }

    private X509Certificate2 LoadCertificate()
    {
        var certPath = config["Tls:CertPath"];
        var certPassword = config["Tls:CertPassword"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(certPath))
        {
            throw new InvalidOperationException(
                "TLS est activé mais 'Tls:CertPath' n'est pas configuré. Fournissez un certificat valide (.pfx).");
        }

        if (!File.Exists(certPath))
        {
            throw new FileNotFoundException(
                $"Certificat TLS introuvable à l'emplacement '{certPath}'. Vérifiez 'Tls:CertPath'.", certPath);
        }

        var cert = new X509Certificate2(
            certPath,
            certPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        if (cert.NotAfter < DateTime.UtcNow)
        {
            throw new InvalidOperationException(
                $"Le certificat TLS '{certPath}' a expiré le {cert.NotAfter:yyyy-MM-dd}. Renouvelez-le.");
        }

        if (cert.NotAfter < DateTime.UtcNow.AddDays(30))
        {
            logger.LogWarning(
                "Le certificat TLS expire bientôt ({Exp:yyyy-MM-dd}) — prévoyez un renouvellement.",
                cert.NotAfter);
        }

        logger.LogInformation(
            "TLS certificate loaded from {Path} (subject={Subject}, expires {Exp:yyyy-MM-dd})",
            certPath, cert.Subject, cert.NotAfter);

        return cert;
    }
}