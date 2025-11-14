// Services/AzureStorageService.cs
using ABCRetailers.Models;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Files.Shares;
using System.Text.Json;

namespace ABCRetailers.Services
{
    public class AzureStorageService : IAzureStorageService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly QueueServiceClient _queueServiceClient;
        private readonly ShareServiceClient? _shareServiceClient; // Make nullable
        private readonly ILogger<AzureStorageService> _logger;
        private readonly bool _isDevelopmentStorage;

        public AzureStorageService(
            IConfiguration configuration,
            ILogger<AzureStorageService> logger)
        {
            _logger = logger;

            try
            {
                string connectionString = configuration.GetConnectionString("AzureStorage")
                    ?? "UseDevelopmentStorage=true";

                // Check if we're using development storage
                _isDevelopmentStorage = connectionString.Contains("UseDevelopmentStorage=true");

                if (_isDevelopmentStorage)
                {
                    _logger.LogInformation("🔧 Using Azure Storage Emulator (Development Storage)");

                    // Development storage doesn't support File Shares, so we'll skip it
                    _shareServiceClient = null;
                }
                else
                {
                    _logger.LogInformation("☁️ Using Azure Cloud Storage");
                    _shareServiceClient = new ShareServiceClient(connectionString);
                }

                _tableServiceClient = new TableServiceClient(connectionString);
                _blobServiceClient = new BlobServiceClient(connectionString);
                _queueServiceClient = new QueueServiceClient(connectionString);

                // Initialize storage asynchronously without blocking
                _ = InitializeStorageAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Azure Storage services");
                // Don't throw - we want the app to start even if storage fails
            }
        }
        private void TestDevelopmentStorage(string connectionString)
        {
            try
            {
                var blobService = new BlobServiceClient(connectionString);
                var containers = blobService.GetBlobContainers();
                _logger.LogInformation("✅ Development storage is accessible");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Development storage may not be running. Please start Azure Storage Emulator.");
            }
        }

        private async Task InitializeStorageAsync()
        {
            try
            {
                _logger.LogInformation("Starting Azure Storage initialization...");

                // Create tables
                await CreateTableIfNotExistsAsync("Customers");
                await CreateTableIfNotExistsAsync("Products");
                await CreateTableIfNotExistsAsync("Orders");
                _logger.LogInformation("✅ Tables created successfully");

                // Create blob containers
                await CreateBlobContainerIfNotExistsAsync("product-images");
                await CreateBlobContainerIfNotExistsAsync("payment-proofs");
                _logger.LogInformation("✅ Blob containers created successfully");

                // Create queues
                await CreateQueueIfNotExistsAsync("order-notifications");
                await CreateQueueIfNotExistsAsync("stock-updates");
                _logger.LogInformation("✅ Queues created successfully");

                // Create file share only if not using development storage
                if (!_isDevelopmentStorage && _shareServiceClient != null)
                {
                    await CreateFileShareIfNotExistsAsync("contracts");
                    _logger.LogInformation("✅ File shares created successfully");
                }
                else
                {
                    _logger.LogInformation("ℹ️ File shares skipped (not supported in development storage)");
                }

                _logger.LogInformation("🎉 Azure Storage initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Storage initialization completed with warnings - some features may not work");
            }
        }

        private async Task CreateTableIfNotExistsAsync(string tableName)
        {
            try
            {
                await _tableServiceClient.CreateTableIfNotExistsAsync(tableName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create table: {TableName}", tableName);
            }
        }

        private async Task CreateBlobContainerIfNotExistsAsync(string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create blob container: {ContainerName}", containerName);
            }
        }

        private async Task CreateQueueIfNotExistsAsync(string queueName)
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient(queueName);
                await queueClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create queue: {QueueName}", queueName);
            }
        }

        private async Task CreateFileShareIfNotExistsAsync(string shareName)
        {
            if (_shareServiceClient == null) return;

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(shareName);
                await shareClient.CreateIfNotExistsAsync();

                var directoryClient = shareClient.GetDirectoryClient("payments");
                await directoryClient.CreateIfNotExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create file share: {ShareName}", shareName);
            }
        }

        // Table Operations
        public async Task<List<T>> GetAllEntitiesAsync<T>() where T : class, ITableEntity, new()
        {
            try
            {
                var tableName = GetTableName<T>();
                var tableClient = _tableServiceClient.GetTableClient(tableName);
                var entities = new List<T>();
                await foreach (var entity in tableClient.QueryAsync<T>())
                {
                    entities.Add(entity);
                }
                return entities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all entities for type {Type}", typeof(T).Name);
                return new List<T>();
            }
        }

        public async Task<T?> GetEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            try
            {
                var tableClient = _tableServiceClient.GetTableClient(GetTableName<T>());
                var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
                return response.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity {PartitionKey}/{RowKey}", partitionKey, rowKey);
                return null;
            }
        }

        public async Task<T> AddEntityAsync<T>(T entity) where T : class, ITableEntity
        {
            var tableClient = _tableServiceClient.GetTableClient(GetTableName<T>());
            await tableClient.AddEntityAsync(entity);
            return entity;
        }

        public async Task<T> UpdateEntityAsync<T>(T entity) where T : class, ITableEntity
        {
            var tableClient = _tableServiceClient.GetTableClient(GetTableName<T>());
            await tableClient.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace);
            return entity;
        }

        public async Task DeleteEntityAsync<T>(string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            var tableClient = _tableServiceClient.GetTableClient(GetTableName<T>());
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }

        public async Task<string> UploadImageAsync(IFormFile file, string containerName)
        {
            try
            {
                _logger.LogInformation("Starting image upload to Azurite...");

                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

                // Create container if it doesn't exist - use public access for Azurite
                await containerClient.CreateIfNotExistsAsync(Azure.Storage.Blobs.Models.PublicAccessType.Blob);

                // Generate unique file name
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var blobClient = containerClient.GetBlobClient(fileName);

                _logger.LogInformation("Uploading to Azurite: {FileName}", fileName);

                using var stream = file.OpenReadStream();
                var response = await blobClient.UploadAsync(stream, overwrite: true);

                var imageUrl = blobClient.Uri.ToString();
                _logger.LogInformation("✅ Image uploaded to Azurite: {ImageUrl}", imageUrl);

                return imageUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error uploading to Azurite");

                // Fallback to placeholder
                var placeholderText = Uri.EscapeDataString(Path.GetFileNameWithoutExtension(file.FileName));
                return $"https://via.placeholder.com/600x400/007bff/ffffff?text={placeholderText}";
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string containerName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync();

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
                var blobClient = containerClient.GetBlobClient(fileName);

                using var stream = file.OpenReadStream();
                await blobClient.UploadAsync(stream, overwrite: true);

                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file to container {ContainerName}", containerName);
                throw;
            }
        }

        public async Task DeleteBlobAsync(string blobName, string containerName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        // Queue Operations
        public async Task SendMessageAsync(string queueName, string message)
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient(queueName);
                await queueClient.CreateIfNotExistsAsync();
                await queueClient.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message to queue {QueueName}", queueName);
            }
        }

        public async Task<string?> ReceiveMessageAsync(string queueName)
        {
            try
            {
                var queueClient = _queueServiceClient.GetQueueClient(queueName);
                var response = await queueClient.ReceiveMessageAsync();

                if (response.Value != null)
                {
                    await queueClient.DeleteMessageAsync(response.Value.MessageId, response.Value.PopReceipt);
                    return response.Value.MessageText;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving message from queue {QueueName}", queueName);
                return null;
            }
        }

        // File Share Operations - Only work in cloud storage
        public async Task<string> UploadToFileShareAsync(IFormFile file, string shareName, string directoryName = "")
        {
            if (_shareServiceClient == null || _isDevelopmentStorage)
            {
                _logger.LogWarning("File share operations are not available in development storage");
                return string.Empty;
            }

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(shareName);
                var directoryClient = string.IsNullOrEmpty(directoryName)
                    ? shareClient.GetRootDirectoryClient()
                    : shareClient.GetDirectoryClient(directoryName);

                await directoryClient.CreateIfNotExistsAsync();

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{file.FileName}";
                var fileClient = directoryClient.GetFileClient(fileName);

                using var stream = file.OpenReadStream();
                await fileClient.CreateAsync(stream.Length);
                await fileClient.UploadAsync(stream);

                return fileName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to file share {ShareName}", shareName);
                return string.Empty;
            }
        }

        public async Task<byte[]> DownloadFromFileShareAsync(string shareName, string fileName, string directoryName = "")
        {
            if (_shareServiceClient == null || _isDevelopmentStorage)
            {
                _logger.LogWarning("File share operations are not available in development storage");
                return Array.Empty<byte>();
            }

            try
            {
                var shareClient = _shareServiceClient.GetShareClient(shareName);
                var directoryClient = string.IsNullOrEmpty(directoryName)
                    ? shareClient.GetRootDirectoryClient()
                    : shareClient.GetDirectoryClient(directoryName);

                var fileClient = directoryClient.GetFileClient(fileName);
                var response = await fileClient.DownloadAsync();

                using var memoryStream = new MemoryStream();
                await response.Value.Content.CopyToAsync(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading from file share {ShareName}", shareName);
                return Array.Empty<byte>();
            }
        }

        private static string GetTableName<T>()
        {
            return typeof(T).Name switch
            {
                nameof(Customer) => "Customers",
                nameof(Product) => "Products",
                nameof(Order) => "Orders",
                _ => typeof(T).Name + "s"
            };
        }
    }
}