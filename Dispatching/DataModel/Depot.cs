using System;
using System.Collections.Generic;
using System.Text;

namespace Dispatching.DataModel
{
    public class Depot
    {
        private String DepotID;
        
        private Dictionary<String, Double> CargoCapacity;

        private Dictionary<String, Double> TownDemand;

        public Depot(String depotID)
        {
            DepotID = depotID;
            CargoCapacity = new Dictionary<String, Double>();
            TownDemand = new Dictionary<String, Double>();
        }

        public void AddCargoCapacity(String cargoID, Double capacity)
        {
            if(!CargoCapacity.ContainsKey(cargoID))
                CargoCapacity.Add(cargoID, capacity);
        }

        public void AddTownDemand(String townID, Double demand)
        {
            if (!TownDemand.ContainsKey(townID))
                TownDemand.Add(townID, demand);
        }

        public Double GetCargoCapacity(String cargoID)
        {
            return CargoCapacity[cargoID];
        }

        public Double GetTownDemand(String townID)
        {
            return TownDemand[townID];
        }

        public String GetDepotID()
        {
            return DepotID;
        }
    }
}
