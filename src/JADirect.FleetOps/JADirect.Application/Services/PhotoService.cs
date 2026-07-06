using Amazon.S3;
using Amazon.S3.Model;
using JADirect.Data.Repositories;
using JADirect.Domain.Entities;

namespace JADirect.Application.Services;


/// <summary>
/// Serviço responsável por orquestrar o upload de fotografias ao Railway Bucket
/// e a persistência dos metadados na tabela walkaround_photos.
/// Segue o mesmo padrão de estrutura do WalkaroundService.
/// </summary>
public class PhotoService
{
    private readonly PhotoRepository _photoRepository;
    private readonly IAmazonS3 _amazonS3Client;
    private readonly string _bucketName;

    
    /// <summary>
    /// Inicializa o serviço com as dependências necessárias via Injeção de Dependência.
    /// </summary>
    /// <param name="photoRepository">Repositório de metadados de fotos.</param>
    /// <param name="amazonS3Client">Cliente S3 configurado com endpoint do Railway Bucket.</param>
    /// <param name="bucketName">Nome do bucket lido da variável de ambiente RAILWAY_BUCKET_NAME.</param>
    public PhotoService(
        PhotoRepository photoRepository,
        IAmazonS3 amazonS3Client,
        string bucketName)
    {
        _photoRepository = photoRepository;
        _amazonS3Client = amazonS3Client;
        _bucketName = bucketName;
    }

    /// <summary>
    /// Recebe a entidade WalkaroundPhoto parcialmente preenchida pelo controller,
    /// gera os campos internos (StorageKey, FileSizeKb, TakenAt), faz o upload
    /// do arquivo para o Railway Bucket e grava os metadados no banco.
    /// Se o upload ao bucket falhar, o registro no banco não é gravado.
    /// </summary>
    /// <param name="photo">
    /// Entidade com WalkaroundId, ChecklistItemId, DriverId e VehicleId
    /// já preenchidos pelo controller. StorageKey, FileSizeKb e TakenAt
    /// são preenchidos por este método.
    /// </param>
    /// <param name="fileStream">Stream do arquivo já comprimido pelo client-side.</param>
    /// <param name="contentType">Tipo MIME do arquivo. Apenas image/jpeg e image/png são aceitos.</param>
    /// <param name="fileSizeBytes">Tamanho do arquivo em bytes para cálculo do file_size_kb.</param>
    /// <returns>O storage key gerado, usado pelo controller para retornar ao frontend.</returns>
    public string UploadAndSave(
        WalkaroundPhoto photo,
        Stream fileStream,
        string contentType,
        long fileSizeBytes)
    {
        string storageKey = GenerateStorageKey(photo.WalkaroundId, photo.ChecklistItemId);

        var uploadRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey,
            InputStream = fileStream,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning =  true,
        };
        _amazonS3Client.PutObjectAsync(uploadRequest).GetAwaiter().GetResult();
        
        photo.StorageKey = storageKey;
        photo.FileSizeKb = (int)Math.Ceiling(fileSizeBytes / 1024.0);
        photo.TakenAt = DateTime.UtcNow;
        
        _photoRepository.InsertPhoto(photo);

        return storageKey;
    }

    /// <summary>
    /// Recupera o stream de uma foto armazenada no Railway Bucket.
    /// Verifica a existência do registro no banco antes de acessar o bucket,
    /// evitando chamadas desnecessárias ao S3 para chaves inexistentes.
    /// </summary>
    /// <param name="storageKey">Chave do arquivo no Railway Bucket.</param>
    /// <returns>Stream do arquivo para ser retornado como FileResult pelo controller.</returns>
    /// <exception cref="FileNotFoundException">
    /// Lançada quando o storage key não existe na tabela walkaround_photos.
    /// O controller deve converter esta exceção em HTTP 404.
    /// </exception>
    public Stream GetPhotoStream(string storageKey)
    {
        bool recordExists = _photoRepository.ExistsByStorageKey(storageKey);

        if (!recordExists)
        {
            throw new FileNotFoundException($"Photo record not found for storage key: {storageKey}");

        }

        var getRequest = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        };
        
        var response = _amazonS3Client.GetObjectAsync(getRequest).GetAwaiter().GetResult();
        return response.ResponseStream;
    }


    /// <summary>
    /// Versão assíncrona de GetPhotoStream.
    /// Necessária para download paralelo com Task.WhenAll no WalkaroundPdfService,
    /// evitando bloqueio de thread durante I/O com o Railway Bucket.
    /// O método síncrono é mantido para uso no WalkaroundController.Photo.
    /// </summary>
    /// <param name="storageKey">Chave do arquivo no Railway Bucket.</param>
    /// <returns>Stream do arquivo para leitura dos bytes.</returns>
    /// <exception cref="FileNotFoundException">
    /// Lançada quando o storage key não existe na tabela walkaround_photos.
    /// </exception>
    public async Task<Stream> GetPhotoStreamAsync(string storageKey)
    {
        bool recordExists = _photoRepository.ExistsByStorageKey(storageKey);

        if (!recordExists)
        {
            throw new FileNotFoundException(
                $"Photo record not found for storage key: {{storageKey}}");
        }

        var getRequest = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = storageKey
        };
        
        var response = await _amazonS3Client.GetObjectAsync(getRequest);
        return response.ResponseStream;
    }


    /// <summary>
    /// Gera o storage key seguindo a convenção definida na arquitetura:
    /// photos/{ano}/{mes}/{dia}/{walkaroundId}_{checklistItemId}_{uuidCurto}.jpg
    /// O UUID curto garante unicidade mesmo se o mesmo item for fotografado mais de uma vez.
    /// </summary>
    /// <param name="walkaroundId">ID do walkaround check.</param>
    /// <param name="checklistItemId">ID do item do checklist.</param>
    /// <returns>Storage key gerado.</returns>
    private string GenerateStorageKey(int walkaroundId, int checklistItemId)
    {
        var now =  DateTime.UtcNow;
        string shortUuid = Guid.NewGuid().ToString("N")[..8];
        
        return string.Format("photos/{0}/{1:D2}/{2:D2}/{3}_{4}_{5}.jpg",
            now.Year,
            now.Month,
            now.Day,
            walkaroundId,
            checklistItemId,
            shortUuid);
    }
    
}