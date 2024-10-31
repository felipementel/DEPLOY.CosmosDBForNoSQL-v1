using Bogus;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using System.ClientModel;
using System.Diagnostics;

namespace PoC.AzureCosmosDbNoSQL.CRUD;

internal class BulkInsert
{
    public BulkInsert()
    {
        Console.WriteLine();
        Console.WriteLine(nameof(BulkInsert));
        Console.WriteLine();
    }
    public async Task<Container> BulkInsertModel(IConfiguration configuration)
    {
        string? endpointServerless = configuration
            .GetSection("CosmosDBForNoSQL:Serverless:Endpoint")
            .Value;

        string? keyServerless = configuration
            .GetSection("CosmosDBForNoSQL:Serverless:Key")
            .Value;

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


            AllowBulkExecution = true, // <--- Bulk support
            RequestTimeout = TimeSpan.FromMinutes(3),


            ApplicationPreferredRegions = new List<string> { "brazilsouth" },
            ConnectionMode = ConnectionMode.Direct,
            ConsistencyLevel = ConsistencyLevel.Session,
            ApplicationName = "Canal DEPLOY - Azure Cosmos DB NoSQL"
        });

        Microsoft.Azure.Cosmos.Database databaseCanalDEPLOY = client
            .GetDatabase("CanalDEPLOY-Bulk");
        if (databaseCanalDEPLOY != null)
        {
            await databaseCanalDEPLOY.DeleteAsync();
        }


        DatabaseResponse databaseResponse = await client
            .CreateDatabaseIfNotExistsAsync("CanalDEPLOY-Bulk");

        databaseCanalDEPLOY = client.GetDatabase(databaseResponse.Database.Id);
        Console.WriteLine("Created Database: {0}\n", databaseResponse.Database.Id);

        Container container;
        ContainerProperties containerProperties = new(
            "products", 
            "/categoryId");

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

        var products_Linq = ProductFaker.Generate(10000);
        var products_foreach = ProductFaker.Generate(10000);
        var products_Parallel_ForEach = ProductFaker.Generate(10000);

        Stopwatch stopwatch = Stopwatch.StartNew();
        stopwatch.Start();



        // Insert 1
        products_Linq.Select(itemToInsert =>
        container.CreateItemAsync(
            itemToInsert, 
            new PartitionKey(itemToInsert.CategoryId)))
            .ToList();

        stopwatch.Stop();
        Console.WriteLine("LINQ -\t\t Elapsed time: {0} ms", 
            stopwatch.ElapsedMilliseconds);




        // Insert 2
        List<Task> concurrentTasks = null;

        stopwatch.Restart();
        concurrentTasks = new List<Task>();

        foreach (Product itemToInsert in products_foreach)
        {
            concurrentTasks.Add(container.CreateItemAsync(
                itemToInsert,
                new PartitionKey(itemToInsert.CategoryId)));
        }

        await Task.WhenAll(concurrentTasks);

        stopwatch.Stop();
        Console.WriteLine("foreach -\t Elapsed time: {0} ms",
            stopwatch.ElapsedMilliseconds);




        // Insert 3
        stopwatch.Restart();
        concurrentTasks = new List<Task>();

        Parallel.ForEach(products_Parallel_ForEach, itemToInsert =>
        {
            concurrentTasks.Add(container.CreateItemAsync(
                itemToInsert, 
                new PartitionKey(itemToInsert.CategoryId)));
        });

        await Task.WhenAll(concurrentTasks);

        stopwatch.Stop();
        Console.WriteLine("Parallel.ForEach -\t Elapsed time: {0} ms",
            stopwatch.ElapsedMilliseconds);

        // FIM

        var countTotal = container
            .GetItemLinqQueryable<Product>(allowSynchronousQueryExecution: true,
            continuationToken: null,
            new QueryRequestOptions() { MaxItemCount = -1 }) // -1 unlimited
            .Count();



        //foreach (Product itemToInsert in products)
        //{
        //    await Task.Delay(500);
        //    await container.CreateItemAsync(itemToInsert, new PartitionKey(itemToInsert.CategoryId));
        //}

        //RU's

        return container;
    }
}