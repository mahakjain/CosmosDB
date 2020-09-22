using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AzureCosmosDB.Models;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

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

        private static readonly Dictionary<string, string> partitionKeyPairs = new Dictionary<string, string>()
        {
            ["association"] = "documentType",
            ["brokerage"] = "documentType",
            ["contact"] = "userIdOfOwner",
            ["document"] = "userIdOfOwner",
            ["invite"] = "documentType",
            ["leadrequest"] = "documentType",
            ["listings"] = "userId",
            ["market"] = "documentType",
            ["messages"] = "owner/userId",
            ["communication"] = "owner/userId",
            ["notification"] = "userId",
            ["offer"] = "documentType",
            ["portfolio"] = "documentType",
            ["regionaldirector"] = "documentType",
            ["searchhistory"] = "owner/userId",
            ["useraction"] = "actionType",
            ["portfolio1_new"] = "documentType",
            ["applicationUser"] = "documentType",
            ["identityServerAudit"] = "id",
            ["identityServerUser"] = "documentType",
            ["identityServerUserGrant"] = "documentType",
            ["messageaudit"] = "userIdOfOwner",
            ["office365Action"] = "userId"
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
                    RecordCount = recordCount,
                    NeedMigration = partitionKeyPairs.ContainsKey(collection.Id) ? partitionKeyPairs[collection.Id] == $"/{collection.PartitionKey.Paths.FirstOrDefault()}" : true
                });
            }
            return results.OrderBy(x => x.Id).ToList();
        }

        public async Task MigrateToTempCollection(string containerId)
        {
            var oldCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, containerId);

            var queryDefinition = new SqlQuerySpec($"SELECT * FROM d");
            var queryIterator = _client.CreateDocumentQuery<int>(oldCollectionUri, queryDefinition, DefaultFeedOptions).AsDocumentQuery();
            List<dynamic> results = new List<dynamic>();
            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ExecuteNextAsync();

                results.AddRange(response.ToList());
            }

            partitionKeyPairs.TryGetValue(containerId, out string partitionKey);

            if (!string.IsNullOrWhiteSpace(partitionKey))
            {
                var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);
                var newContainer = await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, new DocumentCollection()
                {
                    Id = $"{containerId}_new",
                    PartitionKey = new PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { $"/{partitionKey}" } }
                }).ConfigureAwait(false);

                var storedProcedures = _client.CreateStoredProcedureQuery(oldCollectionUri, DefaultFeedOptions).ToList();

                var triggers = _client.CreateTriggerQuery(oldCollectionUri, DefaultFeedOptions).ToList();

                var userDefinedFunctions = _client.CreateUserDefinedFunctionQuery(oldCollectionUri, DefaultFeedOptions).ToList();

                foreach (var item in results)
                {
                    await _client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), item, null, true).ConfigureAwait(false);
                }

                foreach (var storedProcedure in storedProcedures)
                {
                    await _client.CreateStoredProcedureAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), new StoredProcedure()
                    {
                        Id = storedProcedure.Id,
                        Body = storedProcedure.Body
                    }).ConfigureAwait(false);
                }

                foreach (var trigger in triggers)
                {
                    await _client.CreateTriggerAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), new Trigger()
                    {
                        Id = trigger.Id,
                        Body = trigger.Body
                    }).ConfigureAwait(false);
                }

                foreach (var userDefinedFunction in userDefinedFunctions)
                {
                    await _client.CreateUserDefinedFunctionAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainer.Resource.Id), new UserDefinedFunction()
                    {
                        Id = userDefinedFunction.Id,
                        Body = userDefinedFunction.Body
                    }).ConfigureAwait(false);
                }
            }
        }

        public async Task DeleteAndMigrateCollection(string containerId)
        {
            var newContainerId = $"{containerId}_new";

            var newCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseId, newContainerId);

            var queryDefinition = new SqlQuerySpec($"SELECT * FROM d");
            var queryIterator = _client.CreateDocumentQuery<int>(newCollectionUri, queryDefinition, DefaultFeedOptions).AsDocumentQuery();
            List<dynamic> results = new List<dynamic>();
            while (queryIterator.HasMoreResults)
            {
                var response = await queryIterator.ExecuteNextAsync();

                results.AddRange(response.ToList());
            }

            partitionKeyPairs.TryGetValue(containerId, out string partitionKey);

            if (!string.IsNullOrWhiteSpace(partitionKey))
            {
                var databaseUri = UriFactory.CreateDatabaseUri(_databaseId);

                //delete the old collection
                _ = await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, containerId)).ConfigureAwait(false);

                var storedProcedures = _client.CreateStoredProcedureQuery(newCollectionUri, DefaultFeedOptions).ToList();

                var triggers = _client.CreateTriggerQuery(newCollectionUri, DefaultFeedOptions).ToList();

                var userDefinedFunctions = _client.CreateUserDefinedFunctionQuery(newCollectionUri, DefaultFeedOptions).ToList();

                var partitionKeyContainer = await _client.CreateDocumentCollectionIfNotExistsAsync(databaseUri, new DocumentCollection()
                {
                    Id = $"{containerId}",
                    PartitionKey = new PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { $"/{partitionKey}" } }
                }).ConfigureAwait(false);

                foreach (var item in results)
                {
                    await _client.UpsertDocumentAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, partitionKeyContainer.Resource.Id), item, null, true).ConfigureAwait(false);
                }

                foreach (var storedProcedure in storedProcedures)
                {
                    await _client.CreateStoredProcedureAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, partitionKeyContainer.Resource.Id), new StoredProcedure()
                    {
                        Id = storedProcedure.Id,
                        Body = storedProcedure.Body
                    }).ConfigureAwait(false);
                }

                foreach (var trigger in triggers)
                {
                    await _client.CreateTriggerAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, partitionKeyContainer.Resource.Id), new Trigger()
                    {
                        Id = trigger.Id,
                        Body = trigger.Body
                    }).ConfigureAwait(false);
                }

                foreach (var userDefinedFunction in userDefinedFunctions)
                {
                    await _client.CreateUserDefinedFunctionAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, partitionKeyContainer.Resource.Id), new UserDefinedFunction()
                    {
                        Id = userDefinedFunction.Id,
                        Body = userDefinedFunction.Body
                    }).ConfigureAwait(false);
                }

                //Delete temporary collection
                _ = await _client.DeleteDocumentCollectionAsync(UriFactory.CreateDocumentCollectionUri(_databaseId, newContainerId)).ConfigureAwait(false);
            }
        }
    }
}
