namespace JADirect.Application.Interfaces;


/// <summary>
/// Contrato do serviço de geração de PDF do walkaround check.
/// Implementado por WalkaroundPdfService.
/// A interface isola o WalkaroundController da implementação concreta,
/// seguindo o mesmo padrão de IAmazonS3 no PhotoService.
/// </summary>
public interface IWalkaroundPdfService
{
    Task<byte[]> GenerateAsync(int walkaroundCheckId);
}