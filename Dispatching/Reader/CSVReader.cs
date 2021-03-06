﻿using Dispatching.DataModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Dispatching.Reader
{
    public class CSVReader
    {
        private readonly String CargoFile;

        private readonly String TownFile;

        private readonly String DepotFile;

        private Dictionary<String, List<Town>> TownByInventories;

        private Dictionary<String, List<Cargo>> CargosByInventories;

        private List<Depot> Depots;

        private List<String> DepotNames;

        private Dictionary<String, Double> DepotCapacity;

        private Dictionary<String, Dictionary<String, Double>> CargoBasedDepotCapacity;

        public CSVReader(String cargoFile, String townFile, String depotFile)
        {
            CargoFile = cargoFile;
            TownFile = townFile;
            DepotFile = depotFile;

            TownByInventories = new Dictionary<String, List<Town>>();
            CargosByInventories = new Dictionary<String, List<Cargo>>();

            CargoBasedDepotCapacity = new Dictionary<string, Dictionary<string, double>>();
            DepotCapacity = new Dictionary<string, double>();
            DepotNames = new List<string>();
            Depots = new List<Depot>();
        }

        public void Read()
        {
            ReadDepots();
            ReadInventoryCargos();
            ReadInventoryTowns();
            ProcessData();
        }

        private void ReadDepots()
        {
            using (var sr = File.OpenText(DepotFile))
            {
                String s = sr.ReadLine();
                var header = s.Split(',');
                for (int i = 1; i < header.Length; i++)
                    DepotNames.Add(header[i]);
                
                while ((s = sr.ReadLine()) != null)
                {
                    var line = s.Split(',');
                    var cargoID = line[0];

                    if(!CargoBasedDepotCapacity.ContainsKey(cargoID))
                    {
                        var depotCapacities = new Dictionary<String, Double>();
                        for (int i = 0; i < DepotNames.Count; i++)
                            depotCapacities.Add(DepotNames[i], Convert.ToDouble(line[i + 1], CultureInfo.InvariantCulture));
                        CargoBasedDepotCapacity.Add(cargoID, depotCapacities);
                    }
                }
            }

            foreach(var cargo in CargoBasedDepotCapacity)
            {
                var depotCapacities = cargo.Value;
                foreach(var depot in DepotNames)
                {
                    var capacity = depotCapacities[depot];
                    if(!DepotCapacity.ContainsKey(depot))
                    {
                        DepotCapacity.Add(depot, capacity);
                    }
                    else
                    {
                        DepotCapacity[depot] += capacity;
                    }
                }
            }
        }

        private void ReadInventoryCargos()
        {
            using (var sr = File.OpenText(CargoFile))
            {
                String s = sr.ReadLine();
                while ((s = sr.ReadLine()) != null)
                {
                    var line = s.Split(',');

                    var cargoID = line[0];
                    var globalMinRatio = Convert.ToDouble(line[1], CultureInfo.InvariantCulture);
                    var minForTownGroupA = Convert.ToDouble(line[2], CultureInfo.InvariantCulture);
                    var minForTownGroupB = Convert.ToDouble(line[3], CultureInfo.InvariantCulture);
                    var minForTownGroupC = Convert.ToDouble(line[4], CultureInfo.InvariantCulture);

                    var globalMaxRatio = Convert.ToDouble(line[5], CultureInfo.InvariantCulture);
                    var maxForTownGroupA = Convert.ToDouble(line[6], CultureInfo.InvariantCulture);
                    var maxForTownGroupB = Convert.ToDouble(line[7], CultureInfo.InvariantCulture);
                    var maxForTownGroupC = Convert.ToDouble(line[8], CultureInfo.InvariantCulture);

                    var depotCapacities = CargoBasedDepotCapacity[cargoID];
                    foreach(var depot in depotCapacities)
                    {
                        var inventoryID = depot.Key;
                        var capacity = depot.Value;

                        if (CargosByInventories.ContainsKey(inventoryID))
                        {
                            var cargos = CargosByInventories[inventoryID];
                            var cargo = new Cargo(cargoID, capacity, globalMinRatio, globalMaxRatio);
                            if (!cargos.Contains(cargo))
                            {
                                cargos.Add(cargo);

                                cargo.AddMinimumRatioForTownGroup("A", minForTownGroupA);
                                cargo.AddMinimumRatioForTownGroup("B", minForTownGroupB);
                                cargo.AddMinimumRatioForTownGroup("C", minForTownGroupC);

                                cargo.AddMaximumRatioForTownGroup("A", maxForTownGroupA);
                                cargo.AddMaximumRatioForTownGroup("B", maxForTownGroupB);
                                cargo.AddMaximumRatioForTownGroup("C", maxForTownGroupC);

                            }
                        }
                        else
                        {
                            var cargo = new Cargo(cargoID, capacity, globalMinRatio, globalMaxRatio);

                            cargo.AddMinimumRatioForTownGroup("A", minForTownGroupA);
                            cargo.AddMinimumRatioForTownGroup("B", minForTownGroupB);
                            cargo.AddMinimumRatioForTownGroup("C", minForTownGroupC);

                            cargo.AddMaximumRatioForTownGroup("A", maxForTownGroupA);
                            cargo.AddMaximumRatioForTownGroup("B", maxForTownGroupB);
                            cargo.AddMaximumRatioForTownGroup("C", maxForTownGroupC);

                            var cargos = new List<Cargo>();
                            cargos.Add(cargo);

                            CargosByInventories.Add(inventoryID, cargos);
                        }
                    }
                }
            }
        }

        private void ReadInventoryTowns()
        {
            using (var sr = File.OpenText(TownFile))
            {
                String s = sr.ReadLine();
                var headerLine = sr.ReadLine().Split(',');
                var length = headerLine.Count();
                var listOfCargos = new List<String>();

                for (int i = 4; i < length; i++)
                {
                    listOfCargos.Add(headerLine[i]);
                }

                while ((s = sr.ReadLine()) != null)
                {
                    var line = s.Split(',');
                    
                    foreach(var invCap in DepotCapacity)
                    {
                        var inventoryID = invCap.Key;
                        var invCapacity = invCap.Value;

                        var cityID = line[0];
                        var townID = line[1];
                        var townGroup = line[2];
                        var demandPercentage = Convert.ToDouble(line[3], CultureInfo.InvariantCulture);
                        var demand = Math.Round(invCapacity * demandPercentage / 100, 0);

                        if (demand == 0)
                            demand = 1.0;

                        var listOfNps = new List<Double>();
                        for (int i = 4; i < length; i++)
                        {
                            listOfNps.Add(Convert.ToDouble(line[i], CultureInfo.InvariantCulture));
                        }

                        var inventoryIsContained = TownByInventories.ContainsKey(inventoryID);

                        townID = cityID + "~" + townID;

                        if(inventoryIsContained)
                        {
                            var towns = TownByInventories[inventoryID];
                            var town = new Town(townID, townGroup, demand);

                            for (int i = 0; i < listOfCargos.Count; i++)
                            {
                                var cargoID = listOfCargos[i];
                                var nps = listOfNps[i];

                                var cargo = CargosByInventories[inventoryID].Find(x => x.GetID() == cargoID);
                                town.AddNPS(cargo, nps);
                            }

                            towns.Add(town);
                        }
                        else
                        {
                            var town = new Town(townID, townGroup, demand);

                            for (int i = 0; i < listOfCargos.Count; i++)
                            {
                                var cargoID = listOfCargos[i];
                                var nps = listOfNps[i];

                                var cargo = CargosByInventories[inventoryID].Find(x => x.GetID() == cargoID);

                                town.AddNPS(cargo, nps);
                            }

                            var towns = new List<Town>();
                            towns.Add(town);
                            TownByInventories.Add(inventoryID, towns);
                        }
                    }
                }
            }
        }

        public Dictionary<String, List<Town>> GetTownsByInventory()
        {
            return TownByInventories;
        }

        public Dictionary<String, List<Cargo>> GetCargosByInventory()
        {
            return CargosByInventories;
        }

        public List<Depot> GetDepots()
        {
            return Depots;
        }

        private void ProcessData()
        {
            foreach(var cargoDepot in CargosByInventories)
            {
                var depotName = cargoDepot.Key;
                var depot = new Depot(depotName);
                Depots.Add(depot);

                var cargos = cargoDepot.Value.ToList();
                foreach(var cargo in cargos)
                {
                    depot.AddCargoCapacity(cargo.GetID(), cargo.GetCapacity());
                }
            }

            foreach(var depot in Depots)
            {
                var towns = TownByInventories[depot.GetDepotID()];

                foreach (var town in towns)
                    depot.AddTownDemand(town.GetID(), town.GetDemand());
            }
        }
    }
}
