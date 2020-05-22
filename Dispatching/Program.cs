using Dispatching.Algorithm;
using Dispatching.Reader;
using System;

namespace Dispatching
{
    class Program
    {
        static void Main(string[] args)
        {
            var reader = new CSVReader("Input/Test_Cargo.csv", "Input/Test_Town.csv", "Input/Test_Depot.csv");
            reader.Read();

            var cargos = reader.GetCargosByInventory();
            var inventories = reader.GetTownsByInventory();
            var depots = reader.GetDepots();

            var model = new Model(inventories, cargos, depots);
            model.Run();

            var output = model.GetOutput();
            var outputFileName = $"Result_{DateTime.Now.Year}{DateTime.Now.Month}{DateTime.Now.Day}{DateTime.Now.Hour}{DateTime.Now.Minute}{DateTime.Now.Second}";
            var writer = new CSVWriter(output, cargos[depots[0].GetDepotID()], outputFileName);
            writer.Write();
            
            Console.WriteLine("\nPress any key to exit!");
            Console.ReadKey();
        }
    }
}