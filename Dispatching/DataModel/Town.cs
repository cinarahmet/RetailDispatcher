using System;
using System.Collections.Generic;
using System.Text;

namespace Dispatching.DataModel
{
    public class Town
    {
        private String TownID;

        private String TownGroup;

        private Dictionary<Cargo, Double> NPS;

        private Double Demand;

        public Town(String townID, String townGroup, Double demand)
        {
            TownID = townID;
            TownGroup = townGroup;
            Demand = demand;
            NPS = new Dictionary<Cargo, double>();
        }

        public String GetID()
        {
            return TownID;
        }
        
        public void AddNPS(Cargo cargo, Double nps)
        {
            NPS.Add(cargo, nps);
        }
        
        public Double GetDemand()
        {
            return Demand;
        }

        public Double GetNPS(Cargo cargo)
        {
            return NPS[cargo];
        }
        
        public Double GetCargoMinRatio(Cargo cargo)
        {
            var ratio = cargo.GetTownMinimumRatio(this.TownGroup);
            return ratio;
        }

        public Double GetCargoMaxRatio(Cargo cargo)
        {
            var ratio = cargo.GetTownMaximumRatio(this.TownGroup);
            return ratio;
        }
        
    }
}
