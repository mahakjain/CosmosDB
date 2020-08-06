using AzureCosmosDB.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureCosmosDB.Services
{
    public class CosmosDBService : ICosmosDBService
    {
        private readonly Database _database;
        private Container _container;

        public CosmosDBService(
            CosmosClient dbClient,
            string databaseName)
        {
            _database = dbClient.GetDatabase(databaseName);
        }

        public void SetContainer(string containerName)
        {
            _container = _database.GetContainer(containerName);
        }

        public async Task AddItemAsync(Item item, bool hasPartitionKey)
        {
            if (hasPartitionKey)
                await _container.CreateItemAsync<Item>(item, new PartitionKey(item.Id));
            else
                await _container.CreateItemAsync<Item>(item);
        }

        public async Task DeleteItemAsync(string id, bool hasPartitionKey)
        {
            if (hasPartitionKey)
                await _container.DeleteItemAsync<Item>(id, new PartitionKey(id));
            else
                await _container.DeleteItemAsync<Item>(id, PartitionKey.None);
        }

        public async Task<Item> GetItemAsync(string id, bool hasPartitionKey)
        {
            try
            {
                Item response = null;
                if (hasPartitionKey)
                {
                    ItemResponse<Item> result = await _container.ReadItemAsync<Item>(id, new PartitionKey(id));
                    response = result.Resource;
                }
                else
                {
                    ItemResponse<Item> result = await _container.ReadItemAsync<Item>(id, PartitionKey.None);
                    response = result.Resource;
                }

                return response;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

        }

        public async Task<IEnumerable<Item>> GetItemsAsync(string queryString)
        {
            var query = _container.GetItemQueryIterator<Item>(new QueryDefinition(queryString));
            List<Item> results = new List<Item>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync();

                results.AddRange(response.ToList());
            }

            return results;
        }

        public async Task UpdateItemAsync(string id, Item item, bool hasPartitionKey)
        {
            if (hasPartitionKey)
                await _container.UpsertItemAsync<Item>(item, new PartitionKey(id));
            else
                await _container.UpsertItemAsync<Item>(item);
        }
    }
}