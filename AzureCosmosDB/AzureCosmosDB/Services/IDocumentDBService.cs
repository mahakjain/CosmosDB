using AzureCosmosDB.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureCosmosDB.Services
{
    public interface IDocumentDBService
    {
        void SetDatabase(string endPoint, string authorizationKey);
        Task<List<CollectionStats>> GetDatabaseCollections(string databaseId);
        Task MigrateCollection(string containerId);
    }
}
