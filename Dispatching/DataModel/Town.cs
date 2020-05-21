using System;
using System.Collections.Generic;
using System.Text;

namespace Dispatching.DataModel
{
    public class Town
    {
        private String TownID;

        private String TownGroup;

        private Dictionary<Cargo, Double> SameDayCapacity;

        private Dictionary<Cargo, Double> CarryOverCapacity;

        private Dictionary<Cargo, Double> MinRatios;

        private Dictionary<Cargo, Double> SameDayDeliveryCost;

        private Dictionary<Cargo, Double> CarryOverCost;

        private Dictionary<Cargo, Double> NonDeliveryCost;

        private Dictionary<Cargo, Double> NPS;

        private Double Demand;

        public Town(String townID, String townGroup, Double demand)
        {
            TownID = townID;
            TownGroup = townGroup;
            Demand = demand;

            SameDayCapacity = new Dictionary<Cargo, double>();
            CarryOverCapacity = new Dictionary<Cargo, double>();

            SameDayDeliveryCost = new Dictionary<Cargo, double>();
            CarryOverCost = new Dictionary<Cargo, double>();
            NonDeliveryCost = new Dictionary<Cargo, double>();

            MinRatios = new Dictionary<Cargo, double>();
            NPS = new Dictionary<Cargo, double>();
        }

        public String GetID()
        {
            return TownID;
        }

        public void AddCargoCapacity(Cargo cargo, Double sameDayCapacity, Double carryOverCapacity)
        {
            SameDayCapacity.Add(cargo, sameDayCapacity);
            CarryOverCapacity.Add(cargo, carryOverCapacity);
        }

        public void AddCargoMinRatio(Cargo cargo, Double ratio)
        {
            MinRatios.Add(cargo, ratio);
        }

        public void AddNPS(Cargo cargo, Double nps)
        {
            NPS.Add(cargo, nps);
        }

        public void AddCargoCost(Cargo cargo, Double sameDayCost, Double carryOverCost, Double nonDeliveryCost)
        {
            SameDayDeliveryCost.Add(cargo, sameDayCost);
            CarryOverCost.Add(cargo, carryOverCost);
            NonDeliveryCost.Add(cargo, nonDeliveryCost);
        }

        public Double GetDemand()
        {
            return Demand;
        }

        public Double GetNPS(Cargo cargo)
        {
            return NPS[cargo];
        }

        public Double GetSameDayCapacity(Cargo cargo)
        {
            return SameDayCapacity[cargo];
        }

        public Double GetCarryOverCapacity(Cargo cargo)
        {
            return CarryOverCapacity[cargo];
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

        public Double GetSameDayDeliveryCost(Cargo cargo)
        {
            return SameDayDeliveryCost[cargo];
        }

        public Double GetCarryOverCost(Cargo cargo)
        {
            return CarryOverCost[cargo];
        }

        public Double GetNonDeliveryCost(Cargo cargo)
        {
            return NonDeliveryCost[cargo];
        }
    }
}
