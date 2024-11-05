using Microsoft.Azure.Cosmos;

namespace PoC.AzureCosmosDbNoSQL._4_CustomQuery
{
    internal class CosmosCustomQuery
    {
        public CosmosCustomQuery()
        {
            Console.WriteLine();
            Console.WriteLine(nameof(CosmosCustomQuery));
            Console.WriteLine();
        }

        public async Task ExecuteQuery(Container container)
        {
            QueryDefinition query = new("SELECT * FROM simpleProducts p");

            using (FeedIterator<ProductSimple> feedIterator = container.GetItemQueryIterator<ProductSimple>(
                query,
                null,
                new QueryRequestOptions() {  }))
            {
                while (feedIterator.HasMoreResults)
                {
                    foreach (var item in await feedIterator.ReadNextAsync())
                    {
                        Console.WriteLine($"[{item.Id}]\t{item.Name,35} \t {item.Price,15:C}");
                    }
                }
            }
        }

        public async Task ExecuteQueryWithFilter(Container container)
        {
            QueryDefinition query = new QueryDefinition("SELECT * FROM simpleProducts p WHERE p.price < @price")
                .WithParameter("@price", Decimal.Parse("10"));

            using (FeedIterator<ProductSimple> feedIterator = container.GetItemQueryIterator<ProductSimple>(
                query,
                null,
                new QueryRequestOptions() 
                { 
                    MaxItemCount = -1, 
                    ExcludeRegions = new List<string> { "eastus" }
                }))

            {
                Console.WriteLine("Presenting products with a price less than 10");
                Console.WriteLine();

                while (feedIterator.HasMoreResults)
                {
                    foreach (var item in await feedIterator.ReadNextAsync())
                    {
                        Console.WriteLine($"   [{item.Id}]\t{item.Name,35}\t{item.Price,15:C}");
                    }
                }
            }

            //string sql = "SELECT p.name, t.name AS tag " +
            //    "FROM simpleProducts p" +
            //    "JOIN t IN p.tags " +
            //    "WHERE p.price >= @lower AND p.price <= @upper";

            string sql = "SELECT p.name, p.price " +
                "FROM simpleProducts p " +
                "WHERE p.price >= @lower AND p.price <= @upper " +
                " ORDER BY p.price";

            QueryDefinition query2 = new QueryDefinition(sql)
                .WithParameter("@lower", Decimal.Parse("72"))
                .WithParameter("@upper", Decimal.Parse("73"));

            using (FeedIterator<ProductSimple> feedIterator = container.GetItemQueryIterator<ProductSimple>(
                query2,
                null,
                new QueryRequestOptions() { MaxItemCount = 3})) //item importante
            {
                while (feedIterator.HasMoreResults)
                {
                    foreach (var item in await feedIterator.ReadNextAsync())
                    {
                        Console.WriteLine($"[{item.Id}]\t{item.Name,35} \t {item.Price,15:C}");
                    }
                }
            }
        }
    }
}
