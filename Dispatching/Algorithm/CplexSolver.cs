using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dispatching.DataModel;
using ILOG.Concert;
using ILOG.CPLEX;

namespace Dispatching.Algorithm
{
    public class CplexSolver
    {
        /// <summary>
        /// Solver instance: in dispatcher problem we use Google'e ORTools.
        /// If the problem turns out to ne computationally challenging for
        /// ORTools to provide a solution within an allowable run time, then
        /// we may switch to commercial solvers such as CPLEX, GUROBI, XPress
        /// </summary>
        private Cplex _solver;

        /// <summary>
        /// a[i] € N: Total amount of order assigned to cargo i 
        /// </summary>
        private List<INumVar> _totalOrderDeliveredByCargo;

        /// <summary>
        /// x[d][i][k] € N: Amount of orders delivered to town k by cargo firm i from depot d
        /// during the same day
        /// </summary>
        private List<List<List<INumVar>>> _deliveryAmount;
        
        /// <summary>
        /// z[i][k] € N: Amount of orders not delivered to town k by cargo firm i from depot d
        /// </summary>
        private List<List<List<INumVar>>> _excessAmount;

        /// <summary>
        /// List of towns by inventories
        /// </summary>
        private Dictionary<String, List<Town>> _inventoryTowns;

        /// <summary>
        /// List of cargos by inventories
        /// </summary>
        private Dictionary<String, List<Cargo>> _inventoryCargos;

        /// <summary>
        /// Cost charged for exceeding the capacity
        /// </summary>
        private Double ExceedCost = 100.0;

        /// <summary>
        /// List of depots
        /// </summary>
        private List<Depot> _depots;

        /// <summary>
        /// Total amount of orders, achieved by summing over the 
        /// demands for each town
        /// </summary>
        private Double _orderAmount;

        /// <summary>
        /// Number of cargos
        /// </summary>
        private Int32 _numOfCargos;

        /// <summary>
        /// Number of towns
        /// </summary>
        private Int32 _numOfTowns;

        /// <summary>
        /// Number of depots
        /// </summary>
        private Int32 _numOfDepots;

        /// <summary>
        /// Total cost in terms of actual TL based on NPS values
        /// </summary>
        private Double _totalCost;

        /// <summary>
        /// Objective function
        /// </summary>
        private ILinearNumExpr _objective;

        /// <summary>
        /// Output model
        /// </summary>
        private List<OutputModel> _output;

        /// <summary>
        /// Solution status
        /// </summary>
        private Cplex.Status _status;

        /// <summary>
        /// Construction
        /// </summary>
        /// <param name="invTowns"></param>
        /// <param name="invCargos"></param>
        /// <param name="depots"></param>
        public CplexSolver(Dictionary<String, List<Town>> invTowns, Dictionary<String, List<Cargo>> invCargos, List<Depot> depots)
        {
            _solver = new Cplex();
            _solver.SetOut(null);
            _objective = _solver.LinearNumExpr();

            _totalOrderDeliveredByCargo = new List<INumVar>();
            _deliveryAmount = new List<List<List<INumVar>>>();
            _excessAmount = new List<List<List<INumVar>>>();

            _inventoryTowns = invTowns;
            _inventoryCargos = invCargos;
            _depots = depots;

            _numOfDepots = _inventoryTowns.Keys.Count;
            _numOfTowns = _inventoryTowns[_inventoryTowns.Keys.ToList()[0]].Count;
            _numOfCargos = _inventoryCargos[_inventoryCargos.Keys.ToList()[0]].Count;

            foreach (var invTownPair in _inventoryTowns)
            {
                var towns = invTownPair.Value;
                _orderAmount += towns.Sum(x => x.GetDemand());
            }

            _output = new List<OutputModel>();
        }

        /// <summary>
        /// Run method where the underlying engine is triggered
        /// </summary>
        public void Run()
        {
            CreateDecisionVariables();
            CreateConstraints();
            CreateObjective();
            Solve();
            PrepareOutput();
            PrintResults();
        }

        /// <summary>
        /// Get the result
        /// </summary>
        /// <returns></returns>
        public List<OutputModel> GetOutput()
        {
            return _output;
        }

        /// <summary>
        /// Print some statistics concerning the solution
        /// </summary>
        private void PrintResults()
        {
            // Do not print if status is infeasible
            if (_status == Cplex.Status.Infeasible)
                return;

            var ratios = new List<Double>();
            var cargoNames = new List<String>();

            var Cargos = _inventoryCargos[_depots[0].GetDepotID()];
            var Towns = _inventoryTowns[_depots[0].GetDepotID()];
            CalculateCost();

            Console.WriteLine("Total Order Quantity: {0}", _orderAmount);
            Console.WriteLine("Objective Value: {0}", Math.Round(_solver.GetObjValue(), 2));
            Console.WriteLine("Total Cost: {0} TL", Math.Round(_totalCost, 2));
            Console.WriteLine("Solution Time: {0} seconds\n ", Math.Round(_solver.GetDetTime() / 10000.0, 2));
            for (int i = 0; i < _numOfCargos; i++)
            {
                cargoNames.Add(Cargos[i].GetID());
                var x_i = Math.Round(_solver.GetValue(_totalOrderDeliveredByCargo[i]), 0);
                ratios.Add(x_i / _orderAmount);
                Console.WriteLine("r[{0}] = {1}\t x[{0}] = {2}", Cargos[i].GetID(), Math.Round(ratios[i], 3), x_i);
            }

        }

        private Double CalculateCost()
        {
            for (int d = 0; d < _numOfDepots; d++)
            {
                var depot = _depots[d];

                var cargos = _inventoryCargos[depot.GetDepotID()];
                var towns = _inventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < _numOfCargos; i++)
                {
                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        var town = towns[k];
                        var cargo = cargos[i];

                        var nps = town.GetNPS(cargo);

                        var sameDay = _solver.GetValue(_deliveryAmount[d][i][k]);
                        var nonDelivery = _solver.GetValue(_excessAmount[d][i][k]);

                        _totalCost += (sameDay * nps) + (nonDelivery * nps);
                    }
                }
            }

            return _totalCost;
        }

        /// <summary>
        /// Prepare the output data
        /// </summary>
        private void PrepareOutput()
        {
            var ratios = new List<Double>();
            var cargoNames = new List<String>();

            var Cargos = _inventoryCargos[_depots[0].GetDepotID()];
            var Towns = _inventoryTowns[_depots[0].GetDepotID()];

            for (int i = 0; i < _numOfCargos; i++)
            {
                cargoNames.Add(Cargos[i].GetID());
                var x_i = _solver.GetValue(_totalOrderDeliveredByCargo[i]);
                ratios.Add(x_i / _orderAmount);
            }

            //var globalOutput = new OutputModel(ratios, "TURKEY", "ALL", cargoNames);
            //_output.Add(globalOutput);

            for (int d = 0; d < _numOfDepots; d++)
            {
                Towns = _inventoryTowns[_depots[d].GetDepotID()];
                Cargos = _inventoryCargos[_depots[d].GetDepotID()];

                for (int k = 0; k < _numOfTowns; k++)
                {
                    var town = Towns[k];
                    var cargoRatios = new List<Double>();
                    var cargos = new List<String>();

                    for (int i = 0; i < _numOfCargos; i++)
                    {
                        var sd = _solver.GetValue(_deliveryAmount[d][i][k]);
                        var nd = _solver.GetValue(_excessAmount[d][i][k]);

                        var denominator = town.GetDemand();
                        if (denominator == 0)
                            denominator = 1.0;
                        var ratio = (sd + nd) / denominator;
                        //var ratio = (sd +  nd);
                        cargoRatios.Add(ratio);
                        cargos.Add(Cargos[i].GetID());
                    }
                    var outputModel = new OutputModel(cargoRatios, town.GetID(), _depots[d].GetDepotID(), cargos);
                    _output.Add(outputModel);
                }
            }
        }

        /// <summary>
        /// Create decision variables for the underlying mathematical model
        /// a[i]    € N: TotalOrderDeliveredByCargo
        /// x[d][i][k] € N: _deliveryAmount
        /// z[d][i][k] € N: _excessAmount
        /// </summary>
        private void CreateDecisionVariables()
        {
            // TotalOrderDeliveredByCargo variables
            for (int i = 0; i < _numOfCargos; i++)
            {
                var name = $"a[{(i + 1)}]";
                var ub = _orderAmount;

                var a_i = _solver.NumVar(0, ub, NumVarType.Float, name);
                _totalOrderDeliveredByCargo.Add(a_i);
            }

            // _deliveryAmount variables
            for (int d = 0; d < _numOfDepots; d++)
            {
                var x_d = new List<List<INumVar>>();
                var depot = _depots[d];
                var cargos = _inventoryCargos[depot.GetDepotID()];
                var towns = _inventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < _numOfCargos; i++)
                {
                    var x_di = new List<INumVar>();
                    var cargo = cargos[i];

                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        var name = $"x[{(i + 1)}][{(k + 1)}{(d + 1)}]";
                        var town = towns[k];
                        var x_dik = _solver.NumVar(0, double.MaxValue, NumVarType.Float, name);

                        x_di.Add(x_dik);
                    }
                    x_d.Add(x_di);
                }
                _deliveryAmount.Add(x_d);
            }


            // _excessAmount variables
            for (int d = 0; d < _numOfDepots; d++)
            {
                var z_d = new List<List<INumVar>>();
                var depot = _depots[d];
                var cargos = _inventoryCargos[depot.GetDepotID()];
                var towns = _inventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < _numOfCargos; i++)
                {
                    var z_di = new List<INumVar>();
                    var cargo = cargos[i];

                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        var name = $"z[{(i + 1)}][{(k + 1)}{(d + 1)}]";
                        var town = towns[k];
                        var ub = Int32.MaxValue;
                        var z_dik = _solver.NumVar(0, ub, NumVarType.Float, name);

                        z_di.Add(z_dik);
                    }
                    z_d.Add(z_di);
                }
                _excessAmount.Add(z_d);
            }
        }

        /// <summary>
        /// Create objective function for the underlying mathematical model
        /// sum(i,k) SDC[i][k] x[i][k] + COC[i][k] x[i][k] + NDC[i][k] x[i][k]
        /// where objective sense is minimization and the costs are:
        /// SDC[d][i][k]: SameDay cost for cargo i in town k
        /// COC[d][i][k]: CarryOver cost for cargo i in town k
        /// NDC[d][i][k]: _excessAmount cost for cargo i in town k
        /// </summary>
        private void CreateObjective()
        {
            for (int d = 0; d < _numOfDepots; d++)
            {
                var depot = _depots[d];

                var cargos = _inventoryCargos[depot.GetDepotID()];
                var towns = _inventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < _numOfCargos; i++)
                {
                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        var town = towns[k];
                        var cargo = cargos[i];

                        var nps = town.GetNPS(cargo);

                        _objective.AddTerm(_deliveryAmount[d][i][k], nps);
                        _objective.AddTerm(_excessAmount[d][i][k], ExceedCost);
                    }
                }
            }
            _solver.AddMinimize(_objective);
        }

        /// <summary>
        /// Create the set of constraints
        /// </summary>
        private void CreateConstraints()
        {
            OrdersAssignedToCargos();
            OrdersInTowns();
            OrderMatch();
            MinimumRatioLimitForEachCargo();
            MaximumRatioLimitForEachCargo();
            MinimumRatioLimitForEachCargoInTown();
            MaximumRatioLimitForEachCargoInTown();
            CargoCapacitiesByInventories();
        }

        /// <summary>
        /// Solve the mathematical model by calling solvers' solve
        /// </summary>
        private void Solve()
        {
            _solver.Solve();
            
            _status = _solver.GetStatus();
            
            if (_status == Cplex.Status.Infeasible)
            {
                Console.WriteLine("Infeasibility is detected!");
            }
        }

        /// <summary>
        /// This constraints make sure that the sum of orders assigned to
        /// cargo firms is equal to the total order amount.
        /// sum(i) a[i] = OrderAmount
        /// </summary>
        private void OrdersAssignedToCargos()
        {
            //var constraint = _solver.MakeConstraint(_orderAmount, _orderAmount);
            var constraint = _solver.LinearNumExpr();

            for (int i = 0; i < _numOfCargos; i++)
                constraint.AddTerm(_totalOrderDeliveredByCargo[i], 1);

            _solver.AddEq(constraint, _orderAmount);
        }

        /// <summary>
        /// This constraints make sure that the sum of orders assigned to 
        /// cargos in towns must be eqaul to the demand in that town.
        /// sum(i) x[d][i][k] + y[d][i][k] + z[d][i][k] = Demand[d][k] 
        /// for all k € K for all d € D 
        /// </summary>
        private void OrdersInTowns()
        {
            for (int d = 0; d < _numOfDepots; d++)
            {
                var depot = _depots[d];
                var towns = _inventoryTowns[depot.GetDepotID()];

                for (int k = 0; k < _numOfTowns; k++)
                {
                    var demand = towns[k].GetDemand();
                    //var constraint = _solver.MakeConstraint(demand, demand);

                    var constraint = _solver.LinearNumExpr();

                    for (int i = 0; i < _numOfCargos; i++)
                    {
                        constraint.AddTerm(_deliveryAmount[d][i][k], 1);
                        constraint.AddTerm(_excessAmount[d][i][k], 1);
                    }

                    _solver.AddEq(constraint, demand);
                }
            }
        }

        /// <summary>
        /// This constraints make sure that the sum of the orders assigned to a cargo
        /// in different towns must be equal to the total orders assigned.
        /// sum(k, d) x[d][i][k] + y[d][i][k] + z[d][i][k] = a[i] for all i € N
        /// </summary>
        private void OrderMatch()
        {
            for (int i = 0; i < _numOfCargos; i++)
            {
                //var constraint = _solver.MakeConstraint(0, 0);
                var constraint = _solver.LinearNumExpr();
                for (int d = 0; d < _numOfDepots; d++)
                {
                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        constraint.AddTerm(_deliveryAmount[d][i][k], 1);
                        constraint.AddTerm(_excessAmount[d][i][k], 1);
                    }
                }
                constraint.AddTerm(_totalOrderDeliveredByCargo[i], -1);
                _solver.AddEq(constraint, 0);
            }
        }

        /// <summary>
        /// The amount orders assigned to each cargo in Turkey must be greater than
        /// a certain percentage of the total orders.
        /// a[i] >= r[i] * OrderAmount for all i € N
        /// </summary>
        private void MinimumRatioLimitForEachCargo()
        {
            var cargos = _inventoryCargos[_depots[0].GetDepotID()];
            for (int i = 0; i < _numOfCargos; i++)
            {
                var ratio = cargos[i].GetMinimumRatio();
                //var constraint = _solver.MakeConstraint(_orderAmount * ratio, double.PositiveInfinity);
                var constraint = _solver.LinearNumExpr();
                constraint.AddTerm(_totalOrderDeliveredByCargo[i], 1);
                _solver.AddGe(constraint, _orderAmount * ratio);
            }
        }

        private void MaximumRatioLimitForEachCargo()
        {
            var cargos = _inventoryCargos[_depots[0].GetDepotID()];
            for (int i = 0; i < _numOfCargos; i++)
            {
                var ratio = cargos[i].GetMaximumRatio();
                //var constraint = _solver.MakeConstraint(0, _orderAmount * ratio);
                var constraint = _solver.LinearNumExpr();
                constraint.AddTerm(_totalOrderDeliveredByCargo[i], 1);
                _solver.AddLe(constraint, _orderAmount * ratio);
            }

        }

        /// <summary>
        /// The amount orders assigned to each cargo in a town must be greater than
        /// a certain percentage
        /// sum(d) x[d][i][k] + y[d][i][k] + z[d][i][k] >= Demand[d][k] * ratio[i] for all i € N; k € K
        /// </summary>
        private void MinimumRatioLimitForEachCargoInTown()
        {
            for (int i = 0; i < _numOfCargos; i++)
            {
                for (int k = 0; k < _numOfTowns; k++)
                {
                    var ratio = 0.0;
                    var demand = 0.0;

                    for (int d = 0; d < _numOfDepots; d++)
                    {
                        var depot = _depots[d];

                        var towns = _inventoryTowns[depot.GetDepotID()];
                        var cargos = _inventoryCargos[depot.GetDepotID()];

                        var town = towns[k];
                        var cargo = cargos[i];

                        demand += town.GetDemand();
                        ratio = town.GetCargoMinRatio(cargo);
                    }


                    //var constraint = _solver.MakeConstraint(demand * ratio, double.PositiveInfinity);
                    var constraint = _solver.LinearNumExpr();
                    for (int d = 0; d < _numOfDepots; d++)
                    {
                        constraint.AddTerm(_deliveryAmount[d][i][k], 1);
                        constraint.AddTerm(_excessAmount[d][i][k], 1);
                    }
                    _solver.AddGe(constraint, demand * ratio);
                }
            }
        }

        /// <summary>
        /// The amount orders assigned to each cargo in a town must be less than
        /// a certain percentage
        /// sum(d) x[d][i][k] + y[d][i][k] + z[d][i][k] le Demand[d][k] * ratio[i] for all i € N; k € K
        /// </summary>
        private void MaximumRatioLimitForEachCargoInTown()
        {
            for (int i = 0; i < _numOfCargos; i++)
            {
                for (int k = 0; k < _numOfTowns; k++)
                {
                    var ratio = 0.0;
                    var demand = 0.0;

                    for (int d = 0; d < _numOfDepots; d++)
                    {
                        var depot = _depots[d];

                        var towns = _inventoryTowns[depot.GetDepotID()];
                        var cargos = _inventoryCargos[depot.GetDepotID()];

                        var town = towns[k];
                        var cargo = cargos[i];

                        demand += town.GetDemand();
                        ratio = town.GetCargoMaxRatio(cargo);
                    }


                    //var constraint = _solver.MakeConstraint(0, demand * ratio);
                    var constraint = _solver.LinearNumExpr();
                    for (int d = 0; d < _numOfDepots; d++)
                    {
                        constraint.AddTerm(_deliveryAmount[d][i][k], 1);
                    }
                    _solver.AddLe(constraint, demand * ratio);
                }
            }
        }

        /// <summary>
        /// sum(k) x[d][i][k] + y[d][i][k] + z[d][i][k] le Capacity[d][k] for all d € D; k € K
        /// </summary>
        private void CargoCapacitiesByInventories()
        {
            for (int i = 0; i < _numOfCargos; i++)
            {
                for (int d = 0; d < _numOfDepots; d++)
                {
                    var depot = _depots[d];
                    var cargos = _inventoryCargos[depot.GetDepotID()];
                    var cargo = cargos[i];

                    var capacity = cargo.GetCapacity();
                    //var constraint = _solver.MakeConstraint(0, capacity);
                    var constraint = _solver.LinearNumExpr();

                    for (int k = 0; k < _numOfTowns; k++)
                    {
                        constraint.AddTerm(_deliveryAmount[d][i][k], 1);
                    }

                    _solver.AddLe(constraint, capacity);
                }
            }
        }

        private void Clear()
        {
            _solver = new Cplex();
            _objective = _solver.LinearNumExpr();
            _deliveryAmount = new List<List<List<INumVar>>>();
            _excessAmount = new List<List<List<INumVar>>>();
            _totalOrderDeliveredByCargo = new List<INumVar>();
        }
    }
}
