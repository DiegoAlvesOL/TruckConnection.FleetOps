using System.Security.Cryptography;
using System.Text;
using JADirect.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JADirect.Web.Controllers;

/// <summary>
/// Controller público responsável por receber e validar os eventos da Meta WhatsApp API.
/// Não requer autenticação pois a Meta não conhece nosso sistema de login.
/// A autenticidade das requisições é garantida pela validação do X-Hub-Signature-256.
/// </summary>
[AllowAnonymous]
[Route("webhook/whatsapp")]
public class WhatsAppWebhookController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        ILogger<WhatsAppWebhookController> logger)
    {
        _configuration = configuration;
        _scopeFactory  = scopeFactory;
        _logger        = logger;
    }

    /// <summary>
    /// Endpoint de verificação do webhook.
    /// A Meta faz um GET com hub.mode, hub.challenge e hub.verify_token.
    /// Responde com hub.challenge apenas se o token bater.
    /// </summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")]         string mode,
        [FromQuery(Name = "hub.challenge")]    string challenge,
        [FromQuery(Name = "hub.verify_token")] string verifyToken)
    {
        string expectedToken = _configuration["MetaWhatsApp:WebhookVerifyToken"]
            ?? string.Empty;

        if (mode == "subscribe" && verifyToken == expectedToken)
        {
            _logger.LogInformation("WhatsApp webhook verified successfully.");
            return Ok(challenge);
        }

        _logger.LogWarning(
            "WhatsApp webhook verification failed. Token mismatch or invalid mode.");

        return Forbid();
    }

    /// <summary>
    /// Endpoint que recebe os eventos da Meta (status de mensagens, mensagens recebidas).
    /// Valida a assinatura SHA256 antes de qualquer processamento.
    /// Retorna 200 imediatamente e processa o payload em background.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Receive()
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync();
        }

        string signature = Request.Headers["X-Hub-Signature-256"]
            .FirstOrDefault() ?? string.Empty;

        if (!IsSignatureValid(rawBody, signature))
        {
            _logger.LogWarning(
                "WhatsApp webhook: invalid signature. Request rejected.");

            return Forbid();
        }

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var webhookService = scope.ServiceProvider
                .GetRequiredService<WhatsAppWebhookService>();

            await webhookService.ProcessAsync(rawBody);
        });

        return Ok();
    }

    /// <summary>
    /// Valida o cabeçalho X-Hub-Signature-256 usando o AppSecret como chave HMAC.
    /// Rejeita qualquer requisição cujo hash não corresponda ao payload recebido.
    /// </summary>
    private bool IsSignatureValid(string rawBody, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader))
        {
            return false;
        }

        string appSecret = _configuration["MetaWhatsApp:AppSecret"]
            ?? Environment.GetEnvironmentVariable("META_WHATSAPP_APP_SECRET")
            ?? string.Empty;

        if (string.IsNullOrEmpty(appSecret))
        {
            _logger.LogError(
                "WhatsApp webhook: AppSecret not configured. Cannot validate signature.");

            return false;
        }

        using var hmac     = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        byte[] hash        = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        string expectedSig = "sha256=" + Convert.ToHexString(hash).ToLower();

        return string.Equals(signatureHeader, expectedSig, StringComparison.OrdinalIgnoreCase);
    }
}