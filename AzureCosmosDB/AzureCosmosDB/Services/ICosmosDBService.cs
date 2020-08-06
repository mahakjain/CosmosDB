using AzureCosmosDB.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureCosmosDB.Services
{
    public interface ICosmosDBService
    {
        Task<IEnumerable<Item>> GetItemsAsync(string queryString);
        Task<Item> GetItemAsync(string id, bool hasPartitionKey);
        Task AddItemAsync(Item item, bool hasPartitionKey);
        Task UpdateItemAsync(string id, Item item, bool hasPartitionKey);
        Task DeleteItemAsync(string id, bool hasPartitionKey);
        void SetContainer(string containerName);
    }
}