using JADirect.Application.Documents;
using JADirect.Application.Interfaces;
using JADirect.Data.Repositories;
using JADirect.Domain.Models;

namespace JADirect.Application.Services;

/// <summary>
/// Orquestra a geração do PDF do walkaround check em quatro etapas:
/// 1. Busca os dados do walkaround via InspectionRepository.
/// 2. Mapeia WalkaroundDetailViewModel para WalkaroundPdfData.
/// 3. Faz download paralelo das fotos do S3 com Task.WhenAll.
/// 4. Entrega o WalkaroundPdfData completo ao WalkaroundPdfDocument.
/// </summary>
public class WalkaroundPdfService : IWalkaroundPdfService
{
    private readonly InspectionRepository _inspectionRepository;
    private readonly PhotoRepository _photoRepository;
    private readonly PhotoService _photoService;

    public WalkaroundPdfService(
        InspectionRepository inspectionRepository,
        PhotoRepository photoRepository,
        PhotoService photoService)
    {
        _inspectionRepository = inspectionRepository;
        _photoRepository = photoRepository;
        _photoService= photoService;
    }

    /// <summary>
    /// Gera o PDF completo do walkaround check identificado pelo ID informado.
    /// As fotos são baixadas do S3 em paralelo para minimizar a latência total.
    /// Falhas individuais de download não abortam o PDF — a foto é ignorada
    /// e as demais são incluídas normalmente.
    /// </summary>
    /// <param name="walkaroundCheckId">ID do walkaround check.</param>
    /// <returns>Bytes do PDF gerado.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Lançada quando o walkaround não existe ou não está com status Completed.
    /// </exception>
    public async Task<byte[]> GenerateAsync(int walkaroundCheckId)
    {
        // Etapa 1: buscar dados do walkaround (Card 3)
        WalkaroundDetailViewModel? detail =
            _inspectionRepository.GetWalkaroundById(walkaroundCheckId);

        if (detail == null)
        {
            throw new KeyNotFoundException(
                string.Format(
                    "Walkaround check {0} not found or not completed.",
                    walkaroundCheckId));
        }

        // Etapa 2: mapear WalkaroundDetailViewModel → WalkaroundPdfData
        // O mapeamento é explícito e campo a campo para evitar acoplamento
        // entre o contrato do repositório e o contrato do document.
        var pdfData = new WalkaroundPdfData
        {
            WalkaroundId = detail.WalkaroundId,
            CheckDate = detail.CheckDate,
            DriverName = detail.DriverName,
            VehicleRegistration = detail.VehicleRegistration,
            VehicleMake = detail.VehicleMake,
            VehicleModel = detail.VehicleModel,
            VehicleType = detail.VehicleType,
            Odometer = detail.Odometer,
            HasDefect = detail.HasDefect,
            Items = detail.Items,
            Photos = new List<WalkaroundPdfPhotoData>()
        };

        // Etapa 3: download paralelo das fotos (Card 4)
        var photos = _photoRepository.GetPhotosByWalkaroundId(walkaroundCheckId);

        if (photos.Count > 0)
        {
            var downloadTasks = photos
                .Select(photo => DownloadPhotoAsync(photo.StorageKey, photo.ChecklistItemId));

            WalkaroundPdfPhotoData[] downloadedPhotos = await Task.WhenAll(downloadTasks);

            pdfData.Photos = downloadedPhotos
                .Where(photo => photo.ImageBytes.Length > 0)
                .ToList();
        }

        // Etapa 4: gerar o PDF (layout implementado no Card 6)
        return WalkaroundPdfDocument.GeneratePdf(pdfData);
    }

    /// <summary>
    /// Baixa os bytes de uma foto do Railway Bucket de forma assíncrona.
    /// Falhas individuais são capturadas e retornam WalkaroundPdfPhotoData com
    /// ImageBytes vazio. O GenerateAsync filtra os vazios antes de passar ao document,
    /// garantindo que uma foto com problema não aborte o PDF inteiro.
    /// </summary>
    private async Task<WalkaroundPdfPhotoData> DownloadPhotoAsync(
        string storageKey,
        int checklistItemId)
    {
        try
        {
            using var stream = await _photoService.GetPhotoStreamAsync(storageKey);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            return new WalkaroundPdfPhotoData
            {
                ChecklistItemId = checklistItemId,
                StorageKey = storageKey,
                ImageBytes = memoryStream.ToArray()
            };
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                string.Format(
                    "WalkaroundPdfService: photo download failed for key {0}. {1}: {2}",
                    storageKey,
                    exception.GetType().Name,
                    exception.Message));

            return new WalkaroundPdfPhotoData
            {
                ChecklistItemId = checklistItemId,
                StorageKey = storageKey,
                ImageBytes = Array.Empty<byte>()
            };
        }
    }
}