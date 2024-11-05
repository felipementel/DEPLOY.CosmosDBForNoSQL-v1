using Bogus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Diagnostics;

namespace PoC.AzureCosmosDbNoSQL._7_Performance
{
    internal class Cache
    {
        public async Task SelectUsingCacheConnection(IConfiguration configuration)
        {
            string? endpointServerless = configuration.GetSection("CosmosDBForNoSQL:Serverless:Endpoint").Value;
            string? keyServerless = configuration.GetSection("CosmosDBForNoSQL:Serverless:Key").Value;

            var client = new CosmosClientBuilder(endpointServerless, keyServerless).Build();

            //mode 1 - SERVERLESS
            client = new(endpointServerless, keyServerless, new CosmosClientOptions()
            {
                //ApplicationRegion = Regions.BrazilSouth,
                SerializerOptions = new CosmosSerializationOptions()
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                    IgnoreNullValues = true
                },
                ConnectionMode = ConnectionMode.Gateway, // <-- Para usar o Cache, precisa ser Gateway
                ConsistencyLevel = ConsistencyLevel.Session,
                ApplicationName = "Canal DEPLOY - Azure Cosmos DB NoSQL"
            });

            Microsoft.Azure.Cosmos.Database databaseCanalDEPLOY = client.GetDatabase("CanalDEPLOY-Cache");
            
            if (databaseCanalDEPLOY != null)
            {
                await databaseCanalDEPLOY.DeleteAsync();
            }


            Microsoft.Azure.Cosmos.Container container;

            ContainerProperties containerProperties = new("products", "/categoryId");
            containerProperties.IndexingPolicy.Automatic = true;
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            containerProperties.ConflictResolutionPolicy = new ConflictResolutionPolicy
            {
                Mode = ConflictResolutionMode.LastWriterWins,
                ResolutionPath = "/_ts"
            };

            container = await databaseCanalDEPLOY
                .CreateContainerIfNotExistsAsync(containerProperties);

            var ProductFaker = new Faker<Product>("pt_BR")
                .RuleFor(p => p.Id, f => Guid.NewGuid().ToString())
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.CategoryId, f => Guid.NewGuid().ToString())
                .RuleFor(p => p.Category, f => f.Commerce.Categories(1)[0])
                .RuleFor(p => p.Price, f => f.Random.Double(1, 100))
                .RuleFor(p => p.Tags, f => f.Commerce.Categories(3).ToArray())
                .FinishWith((f, u) =>
                {
                    //Console.WriteLine("Product Created Id={0}", u.Id);
                });

            var product = ProductFaker.Generate(300);


            Stopwatch stopwatch = Stopwatch.StartNew();

            var item = container.GetItemLinqQueryable<Product>(allowSynchronousQueryExecution: true)
                .Where(p => p.Price > 10)
                .OrderBy(p => p.Price)
                .Select(p => new { p.Id, p.Name, p.Price });

            stopwatch.Stop();

            Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");

            //Cache

            //Nível do item
            ItemRequestOptions operationOptions = new()
            {
                ConsistencyLevel = ConsistencyLevel.Eventual,
                DedicatedGatewayRequestOptions = new()
                {
                    MaxIntegratedCacheStaleness = TimeSpan.FromMinutes(15)
                },
                IfMatchEtag = "etag"
            };

            var item2 = new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Product 1",
                Price = 100
            };

            Microsoft.Azure.Cosmos.PartitionKey partitionKey = new("category");

            await container.UpsertItemAsync<Product>(item2, partitionKey, requestOptions: operationOptions);


            //Nível da Pesquisa
            QueryRequestOptions queryOptions = new()
            {
                ConsistencyLevel = ConsistencyLevel.Eventual,
                DedicatedGatewayRequestOptions = new()
                {
                    MaxIntegratedCacheStaleness = TimeSpan.FromSeconds(120)
                }
            };

            string sql = "SELECT * FROM products p";

            QueryDefinition query = new(sql);

            FeedIterator<Product> iterator5 = container.GetItemQueryIterator<Product>(query, requestOptions: queryOptions);
        }   
    }
}
