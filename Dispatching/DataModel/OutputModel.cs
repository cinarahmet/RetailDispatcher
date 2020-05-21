using System;
using System.Collections.Generic;
using System.Text;

namespace Dispatching.DataModel
{
    public class OutputModel
    {
        public List<Double> Ratios;

        public String TownID;

        public String DepotID;

        public List<String> Cargos;
        
        public OutputModel(List<Double> ratios, String townID, String depotID, List<String> cargos)
        {
            Ratios = ratios;
            TownID = townID;
            DepotID = depotID;
            Cargos = cargos;
        }
    }
}
