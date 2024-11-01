using Bogus;
using Microsoft.Azure.Cosmos;
using PoC.AzureCosmosDbNoSQL.CRUD;

namespace PoC.AzureCosmosDbNoSQL._2_Transacao_e_Concorrencia;

internal class CosmosConcorrencia
{
    public CosmosConcorrencia()
    {
        Console.WriteLine();
        Console.WriteLine(nameof(CosmosConcorrencia));
        Console.WriteLine();
    }
    public async Task ControleDeConcorrencia(Container container, Faker<ProductSimple> productTransactionFaker)
    {
        PartitionKey partitionKey = new("canal-deploy");

        var products = productTransactionFaker.Generate(2);

        await container.CreateItemAsync<ProductSimple>(
            products[0]);

        ItemResponse<ProductSimple> responseRU = await container.ReadItemAsync<ProductSimple>(products[0].Id, partitionKey);
        ProductSimple product1 = responseRU.Resource;
        string eTag = responseRU.ETag;

        ItemRequestOptions itemRequestOption = new ItemRequestOptions
        {
            IfMatchEtag = eTag // <-- SEMPRE utilizar ETag para controle de concorrência
        };

        await container.UpsertItemAsync<ProductSimple>(product1, requestOptions: itemRequestOption);


        // Client 1
        {
            ItemResponse<ProductSimple> Product_ORIGINAL = await container.ReadItemAsync<ProductSimple>(product1.Id, partitionKey);
            Product_ORIGINAL.Resource.Name = "Sorvete";

            { //Intervalo de tempo para simular concorrência
                // Client 2: Outro processo alterando o mesmo dado
                ItemResponse<ProductSimple> Product_CONCORRENTE = await container.ReadItemAsync<ProductSimple>(product1.Id, partitionKey);
                Product_CONCORRENTE.Resource.Name = "Picolé";

                await container.UpsertItemAsync<ProductSimple>(
                    Product_CONCORRENTE,
                    partitionKey,
                    requestOptions: new ItemRequestOptions
                    {
                        IfMatchEtag = Product_CONCORRENTE.ETag
                    });
            } //Fim do intervalo



            try
            {
                //Cliente 1: Realiza a tentativa de Update
                await container.UpsertItemAsync<ProductSimple>(
                    Product_ORIGINAL,
                    partitionKey,
                    requestOptions: new ItemRequestOptions
                    {
                        IfMatchEtag = Product_ORIGINAL.ETag
                    });
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"Erro: {ex.Message}");

                await container.UpsertItemAsync<ProductSimple>(
                    Product_ORIGINAL,
                    partitionKey);
            }
        }
    }
}
