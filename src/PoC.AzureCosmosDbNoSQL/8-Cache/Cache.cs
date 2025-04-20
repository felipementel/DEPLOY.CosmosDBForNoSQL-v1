using Bogus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace PoC.AzureCosmosDbNoSQL._7_Performance
{
    internal class Cache
    {
        public async Task SelectUsingCacheConnection(IConfiguration configuration)
        {
            var containerThroughput = await CreateDatabase_Throughput(configuration);
            var containerCache = await CreateDatabase_Cache(configuration);

            List<Task> tasks = new List<Task>();
            tasks.Add(LoadInfo(containerCache.Container));
            tasks.Add(LoadInfo(containerThroughput.Container));

            await Task.WhenAll(tasks);

            Console.WriteLine("** Count items");
            Console.Write("Cache ");
            CountItems(containerCache);

            Console.WriteLine();
            Console.Write("Throughput ");
            CountItems(containerThroughput);

            Console.WriteLine();
            Console.WriteLine("** Searchs");
            Console.WriteLine();
            Stopwatch stopwatch = Stopwatch.StartNew();

            var itemThroughput = Query_One(containerThroughput.Container);

            stopwatch.Stop();
            Console.WriteLine($"* Throughput Elapsed time: {stopwatch.ElapsedMilliseconds} ms and find {itemThroughput.Count()} itens");
            Console.WriteLine($"   RUs:\t{containerThroughput.RequestCharge:0.00}");
            Console.WriteLine();


            //Cache
            stopwatch.Start();

            var itemCache = Query_One_Cache(containerCache.Container);

            stopwatch.Stop();
            Console.WriteLine($"* Cache Elapsed time: {stopwatch.ElapsedMilliseconds} ms and find {itemCache.Count()} itens");
            Console.WriteLine($"   RUs:\t{containerCache.RequestCharge:0.00}");
            Console.WriteLine();




            //Query_Two(containerCache.r);


            ////Nível do item
            //ItemRequestOptions operationOptions = new()
            //{
            //    ConsistencyLevel = ConsistencyLevel.Eventual,
            //    DedicatedGatewayRequestOptions = new()
            //    {
            //        MaxIntegratedCacheStaleness = TimeSpan.FromMinutes(15)
            //    },
            //    IfMatchEtag = "etag"
            //};

            //var item2 = new Product
            //{
            //    Id = Guid.NewGuid().ToString(),
            //    Name = "Product 1",
            //    Price = 100
            //};

            //Microsoft.Azure.Cosmos.PartitionKey partitionKey = new("category");

            //await container.UpsertItemAsync<Product>(item2, partitionKey, requestOptions: operationOptions);


            ////Nível da Pesquisa
            //QueryRequestOptions queryOptions = new()
            //{
            //    ConsistencyLevel = ConsistencyLevel.Eventual,
            //    DedicatedGatewayRequestOptions = new()
            //    {
            //        MaxIntegratedCacheStaleness = TimeSpan.FromSeconds(120)
            //    }
            //};

            //string sql = "SELECT * FROM products p";

            //QueryDefinition query = new(sql);

            //FeedIterator<Product> iterator5 = container.GetItemQueryIterator<Product>(query, requestOptions: queryOptions);
        }

        private IQueryable<dynamic> Query_One(Microsoft.Azure.Cosmos.Container container)
        {
            return container.GetItemLinqQueryable<Product>(
                allowSynchronousQueryExecution: true)
                .Where(p => p.Price > 10 && p.Price < 13)
                .OrderBy(p => p.Price)
                .Select(p => new { p.Name, p.Category });
        }

        private IQueryable<dynamic> Query_One_Cache(Microsoft.Azure.Cosmos.Container container)
        {
            return container.GetItemLinqQueryable<Product>(
                allowSynchronousQueryExecution: true,
                null,
                new QueryRequestOptions()
                {
                    MaxItemCount = -1,
                    DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions()
                    {
                        MaxIntegratedCacheStaleness = TimeSpan.FromSeconds(120)
                    }
                })
                .Where(p => p.Price > 10 && p.Price < 13)
                .OrderBy(p => p.Price)
                .Select(p => new { p.Name, p.Category });
        }

        private void CountItems(ContainerResponse container)
        {
            var countTotal = container.Container
                .GetItemLinqQueryable<Product>(allowSynchronousQueryExecution: true,
                continuationToken: null,
                new QueryRequestOptions()
                {
                    MaxItemCount = -1
                })
                .Count();

            Console.WriteLine($"  Total items: {countTotal}");
        }

        private async Task LoadInfo(
            Microsoft.Azure.Cosmos.Container container)
        {
            Console.WriteLine();
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

            for (int i = 0; i < 10; i++)
            {
                var productsToInsert = ProductFaker.Generate(200);

                List<Task> taskThroughputAndCache = new();

                foreach (Product itemToInsert in productsToInsert)
                {
                    taskThroughputAndCache.Add(container.CreateItemAsync(
                        itemToInsert,
                        new Microsoft.Azure.Cosmos.PartitionKey(itemToInsert.CategoryId)));
                }

                await Task.WhenAll(taskThroughputAndCache);

                Console.WriteLine($"Products inserted in {container.Database.Id} database, {i.ToString()} times");
            }
        }

        private async Task<ContainerResponse> CreateDatabase_Cache(IConfiguration configuration)
        {
            string? endpointCache = configuration
                .GetSection("CosmosDBForNoSQL:Cache:Endpoint").Value;

            string? keyCache = configuration
                .GetSection("CosmosDBForNoSQL:Cache:Key").Value;

            var client = new CosmosClientBuilder(endpointCache, keyCache)
                .Build();

            //mode 1 - SERVERLESS
            client = new(endpointCache, keyCache, new CosmosClientOptions()
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

            DatabaseResponse databaseResponse = await client
                .CreateDatabaseIfNotExistsAsync("CanalDEPLOYCache");

            Microsoft.Azure.Cosmos.Database databaseCanalDEPLOY = client
                .GetDatabase(databaseResponse.Database.Id);

            Console.WriteLine("Created Database at Cache Cosmos Server: {0}\n",
                databaseResponse.Database.Id);

            ContainerProperties containerProperties = new("products", "/categoryId");
            containerProperties.IndexingPolicy.Automatic = true;
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            containerProperties.ConflictResolutionPolicy = new ConflictResolutionPolicy
            {
                Mode = ConflictResolutionMode.LastWriterWins,
                ResolutionPath = "/_ts"
            };

            return await databaseCanalDEPLOY!
                .CreateContainerIfNotExistsAsync(containerProperties);
        }

        private async Task<ContainerResponse> CreateDatabase_Throughput(IConfiguration configuration)
        {
            string? endpointThroughput = configuration
                .GetSection("CosmosDBForNoSQL:Throughput:Endpoint").Value;

            string? keyThroughput = configuration
                .GetSection("CosmosDBForNoSQL:Throughput:Key").Value;

            var client = new CosmosClientBuilder(endpointThroughput, keyThroughput)
                .Build();

            client = new(
                endpointThroughput,
                keyThroughput,
                new CosmosClientOptions()
                {
                    PortReuseMode = PortReuseMode.PrivatePortPool, // This property allows the SDK to use a small pool of ephemeral ports for various Azure Cosmos DB destination endpoints.
                    IdleTcpConnectionTimeout = TimeSpan.FromMinutes(20), // This property allows the SDK to close idle connections after a specified time.
                    //ApplicationRegion = Regions.BrazilSouth,
                    SerializerOptions = new CosmosSerializationOptions()
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                        IgnoreNullValues = true
                    },
                    ConnectionMode = ConnectionMode.Direct, // <-- Direct não utiliza cache
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationName = "Canal DEPLOY - Azure Cosmos DB NoSQL"
                });

            DatabaseResponse databaseResponse = await client
                .CreateDatabaseIfNotExistsAsync("CanalDEPLOYCache");

            Microsoft.Azure.Cosmos.Database databaseCanalDEPLOY = client
                .GetDatabase(databaseResponse.Database.Id);

            Console.WriteLine("Created Database at Throughout Cosmos Server: {0}\n",
                databaseResponse.Database.Id);

            ContainerProperties containerProperties = new("products", "/categoryId");
            containerProperties.IndexingPolicy.Automatic = true;
            containerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            containerProperties.ConflictResolutionPolicy = new ConflictResolutionPolicy
            {
                Mode = ConflictResolutionMode.LastWriterWins,
                ResolutionPath = "/_ts"
            };

            return await databaseCanalDEPLOY!
                .CreateContainerIfNotExistsAsync(containerProperties);
        }
    }
}
