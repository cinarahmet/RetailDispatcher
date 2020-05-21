using Dispatching.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dispatching.Reader
{
    public class CSVWriter
    {
        private String FilePath;

        private StreamWriter w;

        private List<OutputModel> Data;

        private List<Cargo> Cargos;

        private String InventoryName;

        public CSVWriter(List<OutputModel> data, List<Cargo> cargos, String fileName)
        {
            Data = data;
            Cargos = cargos;
            InventoryName = fileName;

            var header = "Location";
            foreach (var cargo in Cargos)
            {
                header = String.Format("{0},{1}", header, cargo.GetID());
            }

            FilePath = @"Output\" + fileName + ".csv";
            UTF8Encoding encoding = new UTF8Encoding();
            w = new StreamWriter(File.Open(FilePath, FileMode.OpenOrCreate), Encoding.GetEncoding("UTF-8"));
            w.WriteLine(header);

        }

        public void Write()
        {
            foreach (var data in Data)
            {
                var town = data.TownID;
                var cargos = data.Cargos;
                var ratios = data.Ratios;

                var result = String.Format("{0}", town);
                foreach (var cargo in Cargos)
                {
                    var id = cargo.GetID();
                    result = String.Format("{0},{1}", result, ratios[cargos.IndexOf(id)]);
                }

                w.WriteLine(result);

                w.Flush();
            }
        }
    }
}
