using AzureCosmosDB.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AzureCosmosDB.Services
{
    public class DocumentDBService : IDocumentDBService
    {
        private DocumentClient _client;
        private string _databaseId;
        private static readonly FeedOptions DefaultFeedOptions = new FeedOptions
        {
            EnableCrossPartitionQuery = true
        };

        public void SetDatabase(string endPoint, string authorizationKey)
        {
            var clientUri = new Uri(endPoint);
            _client = new DocumentClient(clientUri, authorizationKey);
        }

        public async Task<List<CollectionStats>> GetDatabaseCollections(string databaseId)
        {
            _databaseId = databaseId;
            var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);

            List<DocumentCollection> collections = _client.CreateDocumentCollectionQuery(databaseUri).ToList();

            return await GetNumberOfRecords(collections);
        }

        private async Task<List<CollectionStats>> GetNumberOfRecords(List<DocumentCollection> collections)
        {
            List<CollectionStats> results = new List<CollectionStats>();
            foreach (var collection in collections)
            {
                var collectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, collection.Id);
                var queryDefinition = new SqlQuerySpec($"SELECT VALUE COUNT(1) FROM d");
                var queryIterator = _client.CreateDocumentQuery<int>(collectionUri, queryDefinition, DefaultFeedOptions).AsDocumentQuery();
                int recordCount = 0;

                while (queryIterator.HasMoreResults)
                {
                    var response = await queryIterator.ExecuteNextAsync();
                    recordCount = response.FirstOrDefault();
                }
                results.Add(new CollectionStats()
                {
                    Id = collection.Id,
                    PartitionKey = collection.PartitionKey.Paths.FirstOrDefault(),
                    RecordCount = recordCount
                });
            }
            return results.OrderBy(x => x.Id).ToList();
        }

        public async Task MigrateCollection(string containerId)
        {
            var collectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, containerId);

            var queryDefinition = new SqlQuerySpec($"SELECT * FROM d");
            var queryIterator = _client.CreateDocumentQuery<int>(collectionUri, queryDefinition, DefaultFeedOptions).AsDocumentQuery();
            List<dynamic> results = new List<dynamic>();
            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ExecuteNextAsync();

                results.AddRange(response.ToList());
            }
            var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);
            var newContainer = await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, new DocumentCollection()
            {
                Id = $"{containerId}_new",
                PartitionKey = new PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/documentType" } }
            });

            foreach (var item in results)
            {
                await _client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), item, null, true);
            }
        }


        //public async Task MigrateCollection(string containerId)
        //{
        //    var collectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, containerId);

        //    var queryDefinition = new SqlQuerySpec($"SELECT * FROM d");
        //    var queryIterator = _client.CreateDocumentQuery<int>(collectionUri, queryDefinition, DefaultFeedOptions).AsDocumentQuery();
        //    List<dynamic> results = new List<dynamic>();
        //    while (queryIterator.HasMoreResults)
        //    {
        //        var response = await queryIterator.ExecuteNextAsync();

        //        results.AddRange(response.ToList());
        //    }
        //    var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);
        //    var newContainer = await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, new DocumentCollection()
        //    {
        //        Id = $"{containerId}_new",
        //        PartitionKey = new PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/documentType" } }
        //    });

        //    foreach (var item in results)
        //    {
        //        await _client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), item, null, true);
        //    }
        //}
    }
}
