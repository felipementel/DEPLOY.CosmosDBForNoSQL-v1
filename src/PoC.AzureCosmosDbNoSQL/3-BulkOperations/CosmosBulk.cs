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
                //Para fins de contexto, como geralmente executamos uma só operação de "Criar item"?
                //Aqui, invocamos o método CreateItemAsync, que retorna uma Task que, por sua vez,
                //é invocada imediatamente usando a palavra-chave await, de modo que nunca tratamos realmente do objeto Task.
                //Trata-se apenas de um atalho de sintaxe para facilitar a leitura do nosso código.

                //Bulk
                List<Task> Tasks = new List<Task>();

                PartitionKey firstPartitionKey = new("some-value");
                Task<ItemResponse<ProductSimple>> firstTask = container.CreateItemAsync<ProductSimple>(ProductSimpleFaker, firstPartitionKey);
                Tasks.Add(firstTask);

                PartitionKey secondPartitionKey = new("some-value");
                Task<ItemResponse<ProductSimple>> secondTask = container.CreateItemAsync<ProductSimple>(ProductSimpleFaker, secondPartitionKey);
                Tasks.Add(secondTask);
            }

            List<ProductSimple> productsToInsert = ProductSimpleFaker.Generate(30);
            List<Task> concurrentTasks = new List<Task>();

            foreach (ProductSimple productItem in productsToInsert)
            {
                concurrentTasks.Add(
                    container.CreateItemAsync<ProductSimple>(
                        productItem,
                        new PartitionKey(productItem.Category))
                );
            }

            await Task.WhenAll(concurrentTasks);
        }
    }
}
