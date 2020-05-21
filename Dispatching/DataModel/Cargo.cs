using System;
using System.Collections.Generic;
using System.Text;

namespace Dispatching.DataModel
{
    public class Cargo
    {
        private String CargoID;

        private Double Capacity;

        private Double MinimumRatio;

        private Double MaximumRatio;

        private Dictionary<String, Double> MinimumRatioForTownGroup;

        private Dictionary<String, Double> MaximumRatioForTownGroup;

        public Cargo(String cargoID, Double capacity, Double minimumRatio, Double maximumRatio)
        {
            CargoID = cargoID;
            Capacity = capacity;
            MinimumRatio = minimumRatio;
            MaximumRatio = maximumRatio;

            MinimumRatioForTownGroup = new Dictionary<String, Double>();
            MaximumRatioForTownGroup = new Dictionary<String, Double>();
        }

        public String GetID()
        {
            return CargoID;
        }

        public Double GetCapacity()
        {
            return Capacity;
        }

        public Double GetMinimumRatio()
        {
            return MinimumRatio;
        }

        public Double GetMaximumRatio()
        {
            return MaximumRatio;
        }

        public Double GetTownMinimumRatio(String townGroup)
        {
            return MinimumRatioForTownGroup[townGroup];
        }

        public Double GetTownMaximumRatio(String townGroup)
        {
            return MaximumRatioForTownGroup[townGroup];
        }

        public void AddMinimumRatioForTownGroup(String townGroup, Double ratio)
        {
            if(!MinimumRatioForTownGroup.ContainsKey(townGroup))
                MinimumRatioForTownGroup.Add(townGroup, ratio);
        }

        public void AddMaximumRatioForTownGroup(String townGroup, Double ratio)
        {
            if (!MaximumRatioForTownGroup.ContainsKey(townGroup))
                MaximumRatioForTownGroup.Add(townGroup, ratio);
        }
    }
}
