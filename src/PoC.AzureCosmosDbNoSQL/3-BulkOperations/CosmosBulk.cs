using Bogus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace PoC.AzureCosmosDbNoSQL._3_BulkOperations
{
    internal class BulkCosmos
    {
        public async Task BulkMode(
            Container container, 
            Faker<ProductSimple> ProductSimpleFaker,
            IConfigurationRoot configuration)
        {
            string? endpointServerless = configuration.GetSection("CosmosDBForNoSQL:Serverless:Endpoint").Value;
            string? keyServerless = configuration.GetSection("CosmosDBForNoSQL:Serverless:Key").Value;

            CosmosClient clientBO = new(endpointServerless, keyServerless, new CosmosClientOptions()
            {
                ApplicationPreferredRegions = new List<string> { "westus", "eastus" },
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Eventual,

                AllowBulkExecution = true // <-- 
            });

            {
                ProductSimple product = ProductSimpleFaker.Generate();

                PartitionKey partitionKey = new(product.Category);

                await container.CreateItemAsync<ProductSimple>(product, partitionKey);

                //Bulk
                List<Task> Tasks = new List<Task>();

                PartitionKey firstPartitionKey = new("canal-deploy");
                Task<ItemResponse<ProductSimple>> firstTask = container.CreateItemAsync<ProductSimple>(ProductSimpleFaker, firstPartitionKey);
                Tasks.Add(firstTask);

                PartitionKey secondPartitionKey = new("canal-deploy");
                Task<ItemResponse<ProductSimple>> secondTask = container.CreateItemAsync<ProductSimple>(ProductSimpleFaker, secondPartitionKey);
                Tasks.Add(secondTask);

                await Task.WhenAll(Tasks);
            }


            List<ProductSimple> productsToInsert = ProductSimpleFaker.Generate(300);
      
            productsToInsert.Select(itemToInsert =>
                container.CreateItemAsync(
                    itemToInsert,
                    new PartitionKey(itemToInsert.Category)))
                    .ToList();
        }
    }
}
