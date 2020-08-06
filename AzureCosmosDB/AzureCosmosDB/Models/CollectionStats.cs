using Newtonsoft.Json;

namespace AzureCosmosDB.Models
{
    public class CollectionStats
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "partitionKey")]
        public string PartitionKey { get; set; }

        [JsonProperty(PropertyName = "records")]
        public int RecordCount { get; set; }
    }
}
