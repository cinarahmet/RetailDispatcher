using Dispatching.DataModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Dispatching.Reader
{
    public class CSVWriter
    {
        private String _filePath;

        private StreamWriter w;

        private List<OutputModel> _data;

        private List<Cargo> _cargos;

        private Int32 _numOfDepots;

        private Int32 _numOfCargos;

        private List<String> _depots = new List<String>();

        public CSVWriter(List<OutputModel> data, List<Cargo> cargos)
        {
            _data = data;
            _cargos = cargos;
            var fileName = $"AnalysisResults_{DateTime.Now.Year}{DateTime.Now.Month}{DateTime.Now.Day}{DateTime.Now.Hour}{DateTime.Now.Minute}{DateTime.Now.Second}"; ;

            DetermineDepots();

            _numOfDepots = _depots.Count;
            _numOfCargos = _cargos.Count;

            var header = ",";
            var tale = _numOfDepots * _numOfCargos;

            for(int i=0; i<tale; i++)
            {
                var r = i % _numOfCargos;
                if (r == 0)
                {
                    var k = i / _numOfCargos;
                    header = $"{header},{_depots[k]}";
                }
                else
                {
                    header = $"{header},{""}";
                }
            }
            
            header = $"{header},{"\nCity, Town"}";
            for(int i=0; i< _numOfDepots; i++)
            {
                foreach (var cargo in _cargos)
                {
                    header = $"{header},{cargo.GetID()}";
                }
            }
            

            _filePath = @"Output\" + fileName + ".csv";
            UTF8Encoding encoding = new UTF8Encoding();
            w = new StreamWriter(File.Open(_filePath, FileMode.OpenOrCreate), Encoding.GetEncoding("UTF-8"));
            w.WriteLine(header);

        }

        private void DetermineDepots()
        {
            foreach(var data in _data)
            {
                var depot = data.DepotID;
                if (!_depots.Contains(depot))
                    _depots.Add(depot);
            }
        }

        public void Write()
        {
            WriteResultsForAnalysis();
            WriteResultsForDeliveryPortal();
        }

        private void WriteResultsForAnalysis()
        {
            var dataLength = _data.Count;
            var limit = dataLength / _numOfDepots;

            for (int i = 0; i < limit; i++)
            {
                var data = _data[i];
                var town = data.TownID;
                var cityId = town.Split('~')[0];
                var townId = town.Split('~')[1];

                var result = $"{cityId},{townId}";
                for (int j = 0; j < _numOfDepots; j++)
                {
                    data = _data[i + j * limit];
                    var cargos = data.Cargos;
                    var ratios = data.Ratios;

                    foreach (var cargo in _cargos)
                    {
                        var id = cargo.GetID();
                        result = $"{result},{ratios[cargos.IndexOf(id)]}";
                    }
                }

                w.WriteLine(result);
                w.Flush();
            }
        }

        private void WriteResultsForDeliveryPortal()
        {
            var header = $"{"stID"},{"ftID"},{"cityId"},{"cityName"},{"districtId"},{"districtName"}," +
                $"{"warehouseId"},{"warehouseName"},{"cargoProviderId"},{"cargoProviderName"},{"stLimit"},{"ftLimit"}";
            
            var fileName = $"DeliveryPortal_{DateTime.Now.Year}{DateTime.Now.Month}{DateTime.Now.Day}{DateTime.Now.Hour}{DateTime.Now.Minute}{DateTime.Now.Second}";
            _filePath = @"Output\" + fileName + ".csv";
            UTF8Encoding encoding = new UTF8Encoding();
            w = new StreamWriter(File.Open(_filePath, FileMode.OpenOrCreate), Encoding.GetEncoding("UTF-8"));
            w.WriteLine(header);
            w.Flush();

            var dataLength = _data.Count;
            var limit = dataLength / _numOfDepots;

            for (int i = 0; i < limit; i++)
            {
                var data = _data[i];
                var town = data.TownID;
                var cityName = town.Split('~')[0];
                var districtName = town.Split('~')[1];

                for (int j = 0; j < _numOfDepots; j++)
                {
                    data = _data[i + j * limit];
                    var cargos = data.Cargos;
                    var ratios = data.Ratios;

                    foreach (var cargo in _cargos)
                    {
                        var id = cargo.GetID();
                        var result = $"{""},{""},{""},{cityName},{""},{districtName}";
                        result = $"{result},{""},{_depots[j]}";
                        result = $"{result},{""},{id},{ratios[cargos.IndexOf(id)]},{ratios[cargos.IndexOf(id)]}";
                        w.WriteLine(result);
                        w.Flush();
                    }
                }
            }
        }
    }
}
