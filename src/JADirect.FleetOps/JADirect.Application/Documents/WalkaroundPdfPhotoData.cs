namespace JADirect.Application.Documents;


/// <summary>
/// Dados de uma foto individual para composição do PDF.
/// Isolado da entidade WalkaroundPhoto para desacoplar o document
/// da camada de domínio. ImageBytes é preenchido pelo WalkaroundPdfService
/// após download paralelo do S3 com Task.WhenAll.
/// </summary>
public class WalkaroundPdfPhotoData
{
    public int ChecklistItemId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
}