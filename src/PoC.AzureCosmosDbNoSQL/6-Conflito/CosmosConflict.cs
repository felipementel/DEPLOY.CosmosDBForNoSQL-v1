using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Scripts;

namespace PoC.AzureCosmosDbNoSQL._6_Conflito
{
    internal class CosmosConflict
    {
        public async Task CreateNewContainer(Database database)
        {
            ContainerProperties properties_simple = new("productsConflitsSimple", "/categoryId")
            {
                ConflictResolutionPolicy = new ConflictResolutionPolicy()
                {
                    Mode = ConflictResolutionMode.LastWriterWins,
                    ResolutionPath = "/sortableTimestamp",
                }
            };

            Container container = await database.CreateContainerIfNotExistsAsync(properties_simple);

            {
                ContainerProperties properties_FULL = new("productsConflitsFULL", "/categoryId")
                {
                    ConflictResolutionPolicy = new ConflictResolutionPolicy()
                    {
                        //O padrão é _ts

                        Mode = ConflictResolutionMode.Custom,
                        ResolutionProcedure = $"dbs/CanalDEPLOY/colls/products/sprocs/SPcanalDEPLOY"
                    }
                };

                Container container_Custom = await database.CreateContainerIfNotExistsAsync(properties_FULL);

                StoredProcedureProperties spproperties = new("SPcanalDEPLOY", File.ReadAllText(@"6-Conflito\code.js"));

                await container_Custom.Scripts.CreateStoredProcedureAsync(spproperties);
            }
        }
    }
}
