using System;
using System.Collections.Generic;
using System.Linq;
using Dispatching.DataModel;
using Google.OrTools.LinearSolver;

namespace Dispatching.Algorithm
{
    public class Model
    {
        /// <summary>
        /// Solver instance: in dispatcher problem we use Google'e ORTools.
        /// If the problem turns out to ne computationally challenging for
        /// ORTools to provide a solution within an allowable run time, then
        /// we may switch to commercial solvers such as CPLEX, GUROBI, XPress
        /// </summary>
        private Solver Solver;

        /// <summary>
        /// a[i] € N: Total amount of order assigned to cargo i 
        /// </summary>
        private List<Variable> TotalOrderDeliveredByCargo;

        /// <summary>
        /// x[d][i][k] € N: Amount of orders delivered to town k by cargo firm i from depot d
        /// during the same day
        /// </summary>
        private List<List<List<Variable>>> SameDayDelivery;

        /// <summary>
        /// y[i][k] € N: Amount of orders not delivered the same day but carried
        /// over to town k by cargo firm i from depot d
        /// </summary>
        private List<List<List<Variable>>> CarryOver;

        /// <summary>
        /// z[i][k] € N: Amount of orders not delivered to town k by cargo firm i from depot d
        /// </summary>
        private List<List<List<Variable>>> NonDelivery;

        /// <summary>
        /// List of towns by inventories
        /// </summary>
        private Dictionary<String, List<Town>> InventoryTowns;

        /// <summary>
        /// List of cargos by inventories
        /// </summary>
        private Dictionary<String, List<Cargo>> InventoryCargos;

        /// <summary>
        /// Cost charged for exceeding the capacity
        /// </summary>
        private Double ExceedCost = 100.0;

        /// <summary>
        /// List of depots
        /// </summary>
        private List<Depot> Depots;

        /// <summary>
        /// Total amount of orders, achieved by summing over the 
        /// demands for each town
        /// </summary>
        private Double OrderAmount;

        /// <summary>
        /// Number of cargos
        /// </summary>
        private Int32 NumOfCargos;

        /// <summary>
        /// Number of towns
        /// </summary>
        private Int32 NumOfTowns;

        /// <summary>
        /// Number of depots
        /// </summary>
        private Int32 NumOfDepots;

        /// <summary>
        /// Total cost in terms of actual TL based on NPS values
        /// </summary>
        private Double TotalCost;

        /// <summary>
        /// Objective function
        /// </summary>
        private Objective Objective;

        /// <summary>
        /// Output model
        /// </summary>
        private List<OutputModel> Output;

        /// <summary>
        /// Solution status
        /// </summary>
        private Int32 Status;
        
        /// <summary>
        /// Construction
        /// </summary>
        /// <param name="invTowns"></param>
        /// <param name="invCargos"></param>
        /// <param name="depots"></param>
        public Model(Dictionary<String, List<Town>> invTowns, Dictionary<String, List<Cargo>> invCargos, List<Depot> depots)
        {
            Solver = Solver.CreateSolver("Dispatcher", "CBC_MIXED_INTEGER_PROGRAMMING");
            Objective = Solver.Objective();

            TotalOrderDeliveredByCargo = new List<Variable>();
            SameDayDelivery = new List<List<List<Variable>>>();
            CarryOver = new List<List<List<Variable>>>();
            NonDelivery = new List<List<List<Variable>>>();

            InventoryTowns = invTowns;
            InventoryCargos = invCargos;
            Depots = depots;

            NumOfDepots = InventoryTowns.Keys.Count;
            NumOfTowns = InventoryTowns[InventoryTowns.Keys.ToList()[0]].Count;
            NumOfCargos = InventoryCargos[InventoryCargos.Keys.ToList()[0]].Count;

            foreach(var invTownPair in InventoryTowns)
            {
                var towns = invTownPair.Value;
                OrderAmount += towns.Sum(x => x.GetDemand());
            }

            Output = new List<OutputModel>();
        }

        /// <summary>
        /// Run method where the underlying engine is triggered
        /// </summary>
        public void Run()
        {
            //DataAnalysis();
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
            return Output;
        }

        /// <summary>
        /// Print some statistics concerning the solution
        /// </summary>
        private void PrintResults()
        {
            var ratios = new List<Double>();
            var cargoNames = new List<String>();

            var Cargos = InventoryCargos[Depots[0].GetDepotID()];
            var Towns = InventoryTowns[Depots[0].GetDepotID()];
            CalculateCost();

            Console.WriteLine("Total Order Quantity: {0}", OrderAmount);
            Console.WriteLine("Objective Value: {0}", Math.Round(Solver.Objective().Value(), 2));
            Console.WriteLine("Total Cost: {0} TL", Math.Round(TotalCost, 2));
            Console.WriteLine("Solution Time: {0} milliseconds\n ", Solver.WallTime());
            for (int i = 0; i < NumOfCargos; i++)
            {
                cargoNames.Add(Cargos[i].GetID());
                var x_i = TotalOrderDeliveredByCargo[i].SolutionValue();
                ratios.Add(x_i / OrderAmount);
                Console.WriteLine("r[{0}] = {1}\t x[{0}] = {2}", Cargos[i].GetID(), Math.Round(ratios[i], 3), TotalOrderDeliveredByCargo[i].SolutionValue());
            }

        }

        private Double CalculateCost()
        {
            for (int d = 0; d < NumOfDepots; d++)
            {
                var depot = Depots[d];

                var cargos = InventoryCargos[depot.GetDepotID()];
                var towns = InventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < NumOfCargos; i++)
                {
                    for (int k = 0; k < NumOfTowns; k++)
                    {
                        var town = towns[k];
                        var cargo = cargos[i];

                        var sameDayCost = town.GetSameDayDeliveryCost(cargo);
                        var nonDeliveryCost = town.GetNonDeliveryCost(cargo);
                        var nps = town.GetNPS(cargo);

                        var sameDay = SameDayDelivery[d][i][k].SolutionValue();
                        var nonDelivery = NonDelivery[d][i][k].SolutionValue();

                        TotalCost += (sameDay * (sameDayCost + nps)) + (nonDelivery * (nonDeliveryCost + nps));
                    }
                }
            }

            return TotalCost;
        }

        /// <summary>
        /// Prepare the output data
        /// </summary>
        private void PrepareOutput()
        {
            var ratios = new List<Double>();
            var cargoNames = new List<String>();

            var Cargos = InventoryCargos[Depots[0].GetDepotID()];
            var Towns = InventoryTowns[Depots[0].GetDepotID()];

            for (int i = 0; i < NumOfCargos; i++)
            {
                cargoNames.Add(Cargos[i].GetID());
                var x_i = TotalOrderDeliveredByCargo[i].SolutionValue();
                ratios.Add(x_i / OrderAmount);
            }

            var globalOutput = new OutputModel(ratios, "TURKEY", "ALL", cargoNames);
            Output.Add(globalOutput);

            //for (int d = 0; d < NumOfDepots; d++)
            //{
            //    var depot = Depots[d];
            //    var towns = InventoryTowns[depot.GetDepotID()];
            //    var demand = towns.Sum(town => town.GetDemand());

            //    var dRatios = new List<Double>();

            //    for (int i = 0; i < NumOfCargos; i++)
            //    {
            //        var assigned = 0.0;
            //        for (int k = 0; k < NumOfTowns; k++)
            //        {
            //            assigned += SameDayDelivery[d][i][k].SolutionValue();
            //            assigned += CarryOver[d][i][k].SolutionValue();
            //            assigned += NonDelivery[d][i][k].SolutionValue();
            //        }

            //        dRatios.Add(assigned / demand);
            //    }

            //    var outputModel = new OutputModel(dRatios, depot.GetDepotID(), depot.GetDepotID(), cargoNames);
            //    Output.Add(outputModel);
            //}


            for (int d = 0; d < NumOfDepots; d++)
            {
                Towns = InventoryTowns[Depots[d].GetDepotID()];
                Cargos = InventoryCargos[Depots[d].GetDepotID()];

                for (int k = 0; k < NumOfTowns; k++)
                {
                    var town = Towns[k].GetID();
                    var cargoRatios = new List<Double>();
                    var cargos = new List<String>();

                    for (int i = 0; i < NumOfCargos; i++)
                    {
                        var sd = SameDayDelivery[d][i][k].SolutionValue();
                        var co = CarryOver[d][i][k].SolutionValue();
                        var nd = NonDelivery[d][i][k].SolutionValue();

                        //var ratio = (sd + co + nd) / Towns[k].GetDemand();
                        var ratio = (sd + co + nd);
                        cargoRatios.Add(ratio);
                        cargos.Add(Cargos[i].GetID());
                    }
                    var outputModel = new OutputModel(cargoRatios, Towns[k].GetID(), Depots[d].GetDepotID(), cargos);
                    Output.Add(outputModel);
                }
            }
        }

        /// <summary>
        /// Create decision variables for the underlying mathematical model
        /// a[i]    € N: TotalOrderDeliveredByCargo
        /// x[d][i][k] € N: SameDayDelivery
        /// y[d][i][k] € N: CarryOver
        /// z[d][i][k] € N: NonDelivery
        /// </summary>
        private void CreateDecisionVariables()
        {
            // TotalOrderDeliveredByCargo variables
            for (int i = 0; i < NumOfCargos; i++)
            {
                var name = String.Format("a[{0}]", (i + 1));
                var ub = OrderAmount;

                var a_i = Solver.MakeIntVar(0, ub, name);
                TotalOrderDeliveredByCargo.Add(a_i);
            }

            // SameDayDelivery variables
            for(int d=0; d < NumOfDepots; d++)
            {
                var x_d = new List<List<Variable>>();
                var depot = Depots[d];
                var cargos = InventoryCargos[depot.GetDepotID()];
                var towns = InventoryTowns[depot.GetDepotID()];

                for(int i=0; i < NumOfCargos; i++)
                {
                    var x_di = new List<Variable>();
                    var cargo = cargos[i];

                    for(int k=0; k < NumOfTowns; k++)
                    {
                        var name = String.Format("x[{0}][{1}{2}]", (i + 1), (k + 1), (d + 1));
                        var town = towns[k];
                        var ub = town.GetSameDayCapacity(cargo);
                        var x_dik = Solver.MakeIntVar(0, ub, name);

                        x_di.Add(x_dik);
                    }
                    x_d.Add(x_di);
                }
                SameDayDelivery.Add(x_d);
            }

            // CarryOver variables
            for (int d = 0; d < NumOfDepots; d++)
            {
                var y_d = new List<List<Variable>>();
                var depot = Depots[d];
                var cargos = InventoryCargos[depot.GetDepotID()];
                var towns = InventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < NumOfCargos; i++)
                {
                    var y_di = new List<Variable>();
                    var cargo = cargos[i];

                    for (int k = 0; k < NumOfTowns; k++)
                    {
                        var name = String.Format("y[{0}][{1}{2}]", (i + 1), (k + 1), (d + 1));
                        var town = towns[k];
                        var ub = town.GetCarryOverCapacity(cargo);
                        var y_dik = Solver.MakeIntVar(0, ub, name);

                        y_di.Add(y_dik);
                    }
                    y_d.Add(y_di);
                }
                CarryOver.Add(y_d);
            }

            // NonDelivery variables
            for (int d = 0; d < NumOfDepots; d++)
            {
                var z_d = new List<List<Variable>>();
                var depot = Depots[d];
                var cargos = InventoryCargos[depot.GetDepotID()];
                var towns = InventoryTowns[depot.GetDepotID()];

                for (int i = 0; i < NumOfCargos; i++)
                {
                    var z_di = new List<Variable>();
                    var cargo = cargos[i];

                    for (int k = 0; k < NumOfTowns; k++)
                    {
                        var name = String.Format("z[{0}][{1}{2}]", (i + 1), (k + 1), (d + 1));
                        var town = towns[k];
                        var ub = Int32.MaxValue;
                        var z_dik = Solver.MakeIntVar(0, ub, name);

                        z_di.Add(z_dik);
                    }
                    z_d.Add(z_di);
                }
                NonDelivery.Add(z_d);
            }
        }

        /// <summary>
        /// Create objective function for the underlying mathematical model
        /// sum(i,k) SDC[i][k] x[i][k] + COC[i][k] x[i][k] + NDC[i][k] x[i][k]
        /// where objective sense is minimization and the costs are:
        /// SDC[d][i][k]: SameDay cost for cargo i in town k
        /// COC[d][i][k]: CarryOver cost for cargo i in town k
        /// NDC[d][i][k]: NonDelivery cost for cargo i in town k
        /// </summary>
        private void CreateObjective()
        {
            for(int d=0; d < NumOfDepots; d++)
            {
                var depot = Depots[d];

                var cargos = InventoryCargos[depot.GetDepotID()];
                var towns = InventoryTowns[depot.GetDepotID()];

                for(int i = 0; i < NumOfCargos; i++)
                {
                    for(int k = 0; k < NumOfTowns; k++)
                    {
                        var town = towns[k];
                        var cargo = cargos[i];

                        var sameDayCost = town.GetSameDayDeliveryCost(cargo);
                        var carryOverCost = town.GetCarryOverCost(cargo);
                        var nonDeliveryCost = town.GetNonDeliveryCost(cargo);
                        var nps = town.GetNPS(cargo);

                        Objective.SetCoefficient(SameDayDelivery[d][i][k], sameDayCost + nps);
                        //Objective.SetCoefficient(CarryOver[d][i][k], carryOverCost + nps);
                        Objective.SetCoefficient(NonDelivery[d][i][k], nonDeliveryCost + ExceedCost);
                    }
                }
            }
            Objective.SetMinimization();
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
            Status = Solver.Solve();

            // Status: 0 --> Optimal; 1 --> Feasible; 2 --> Infeasible
            //if(Status == 2)
            //{
            //    Console.WriteLine("Infeasibility is detected!");
            //    Console.WriteLine("Minimum ratio limit constraints are relaxed!\n");
            //    ReRun();
            //}
        }

        /// <summary>
        /// This constraints make sure that the sum of orders assigned to
        /// cargo firms is equal to the total order amount.
        /// sum(i) a[i] = OrderAmount
        /// </summary>
        private void OrdersAssignedToCargos()
        {
            var constraint = Solver.MakeConstraint(OrderAmount, OrderAmount);

            for (int i = 0; i < NumOfCargos; i++)
                constraint.SetCoefficient(TotalOrderDeliveredByCargo[i], 1);
        }

        /// <summary>
        /// This constraints make sure that the sum of orders assigned to 
        /// cargos in towns must be eqaul to the demand in that town.
        /// sum(i) x[d][i][k] + y[d][i][k] + z[d][i][k] = Demand[d][k] 
        /// for all k € K for all d € D 
        /// </summary>
        private void OrdersInTowns()
        {
            for(int d=0; d < NumOfDepots; d++)
            {
                var depot = Depots[d];
                var towns = InventoryTowns[depot.GetDepotID()];

                for (int k = 0; k < NumOfTowns; k++)
                {
                    var demand = towns[k].GetDemand();
                    var constraint = Solver.MakeConstraint(demand, demand);

                    for (int i = 0; i < NumOfCargos; i++)
                    {
                        constraint.SetCoefficient(SameDayDelivery[d][i][k], 1);
                        //constraint.SetCoefficient(CarryOver[d][i][k], 1);
                        constraint.SetCoefficient(NonDelivery[d][i][k], 1);
                    }
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
            for (int i = 0; i < NumOfCargos; i++)
            {
                var constraint = Solver.MakeConstraint(0, 0);
                for (int d = 0; d < NumOfDepots; d++)
                {
                    for (int k = 0; k < NumOfTowns; k++)
                    {
                        constraint.SetCoefficient(SameDayDelivery[d][i][k], 1);
                        //constraint.SetCoefficient(CarryOver[d][i][k], 1);
                        constraint.SetCoefficient(NonDelivery[d][i][k], 1);
                    }
                }
                constraint.SetCoefficient(TotalOrderDeliveredByCargo[i], -1);
            }
        }

        /// <summary>
        /// The amount orders assigned to each cargo in Turkey must be greater than
        /// a certain percentage of the total orders.
        /// a[i] >= r[i] * OrderAmount for all i € N
        /// </summary>
        private void MinimumRatioLimitForEachCargo()
        {
            var cargos = InventoryCargos[Depots[0].GetDepotID()];
            for (int i = 0; i < NumOfCargos; i++)
            {
                var ratio = cargos[i].GetMinimumRatio();
                var constraint = Solver.MakeConstraint(OrderAmount * ratio, Double.PositiveInfinity);
                constraint.SetCoefficient(TotalOrderDeliveredByCargo[i], 1);
            }
        }

        private void MaximumRatioLimitForEachCargo()
        {
            var cargos = InventoryCargos[Depots[0].GetDepotID()];
            for (int i = 0; i < NumOfCargos; i++)
            {
                var ratio = cargos[i].GetMaximumRatio();
                var constraint = Solver.MakeConstraint(0, OrderAmount * ratio);
                constraint.SetCoefficient(TotalOrderDeliveredByCargo[i], 1);
            }

        }

        /// <summary>
        /// The amount orders assigned to each cargo in a town must be greater than
        /// a certain percentage
        /// sum(d) x[d][i][k] + y[d][i][k] + z[d][i][k] >= Demand[d][k] * ratio[i] for all i € N; k € K
        /// </summary>
        private void MinimumRatioLimitForEachCargoInTown()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                for (int k = 0; k < NumOfTowns; k++)
                {
                    var ratio = 0.0;
                    var demand = 0.0;

                    for (int d = 0; d < NumOfDepots; d++)
                    {
                        var depot = Depots[d];

                        var towns = InventoryTowns[depot.GetDepotID()];
                        var cargos = InventoryCargos[depot.GetDepotID()];

                        var town = towns[k];
                        var cargo = cargos[i];

                        demand += town.GetDemand();
                        ratio = town.GetCargoMinRatio(cargo);
                    }
                    

                    var constraint = Solver.MakeConstraint(demand * ratio, Double.PositiveInfinity);
                    for (int d = 0; d < NumOfDepots; d++)
                    {
                        constraint.SetCoefficient(SameDayDelivery[d][i][k], 1);
                        //constraint.SetCoefficient(CarryOver[d][i][k], 1);
                        constraint.SetCoefficient(NonDelivery[d][i][k], 1);
                    }
                }
            }
        }

        /// <summary>
        /// The amount orders assigned to each cargo in a town must be less than
        /// a certain percentage
        /// sum(d) x[d][i][k] + y[d][i][k] + z[d][i][k] LE Demand[d][k] * ratio[i] for all i € N; k € K
        /// </summary>
        private void MaximumRatioLimitForEachCargoInTown()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                for (int k = 0; k < NumOfTowns; k++)
                {
                    var ratio = 0.0;
                    var demand = 0.0;

                    for (int d = 0; d < NumOfDepots; d++)
                    {
                        var depot = Depots[d];

                        var towns = InventoryTowns[depot.GetDepotID()];
                        var cargos = InventoryCargos[depot.GetDepotID()];

                        var town = towns[k];
                        var cargo = cargos[i];

                        demand += town.GetDemand();
                        ratio = town.GetCargoMaxRatio(cargo);
                    }


                    var constraint = Solver.MakeConstraint(0, demand * ratio);
                    for (int d = 0; d < NumOfDepots; d++)
                    {
                        constraint.SetCoefficient(SameDayDelivery[d][i][k], 1);
                        //constraint.SetCoefficient(CarryOver[d][i][k], 1);
                        //constraint.SetCoefficient(NonDelivery[d][i][k], 1);
                    }
                }
            }
        }

        /// <summary>
        /// sum(k) x[d][i][k] + y[d][i][k] + z[d][i][k] <= Capacity[d][k] for all d € D; k € K
        /// </summary>
        private void CargoCapacitiesByInventories()
        {
            for(int i=0; i<NumOfCargos; i++)
            {
                for(int d=0; d<NumOfDepots; d++)
                {
                    var depot = Depots[d];
                    var cargos = InventoryCargos[depot.GetDepotID()];
                    var cargo = cargos[i];

                    var capacity = cargo.GetCapacity();
                    var constraint = Solver.MakeConstraint(0, capacity);

                    for(int k=0; k<NumOfTowns; k++)
                    {
                        constraint.SetCoefficient(SameDayDelivery[d][i][k], 1);
                        //constraint.SetCoefficient(CarryOver[d][i][k], 1);
                        //constraint.SetCoefficient(NonDelivery[d][i][k], 1);
                    }
                }
            }
        }

        /// <summary>
        /// Input Data Analyis
        /// </summary>
        private void DataAnalysis()
        {
            foreach (var depot in Depots)
            {
                var cargos = InventoryCargos[depot.GetDepotID()];
                Console.WriteLine("Depot ID: {0}", depot.GetDepotID());

                foreach (var cargo in cargos)
                {
                    Console.WriteLine("Cargo: {0}  \tCapacity: {1}", cargo.GetID(), cargo.GetCapacity());
                }
            }

            var towns1 = InventoryTowns[Depots[0].GetDepotID()];
            var towns2 = InventoryTowns[Depots[1].GetDepotID()];
            var towns3 = InventoryTowns[Depots[2].GetDepotID()];
            var minAssigned = 0.0;

            for(int k=0; k<NumOfTowns; k++)
            {
                var town = towns1[k];
                Console.WriteLine("Town: {0} \tD1: {1}  \tD2: {2}  \tD3: {3}   \tMin: {4}", 
                    (k+1), towns1[k].GetDemand(), towns2[k].GetDemand(), towns3[k].GetDemand(),
                    (towns1[k].GetDemand() + towns2[k].GetDemand() + towns3[k].GetDemand()) * 0.1);

                minAssigned += (towns1[k].GetDemand() + towns2[k].GetDemand() + towns3[k].GetDemand()) * 0.1;
            }

            Console.WriteLine("Min assigned: {0}", minAssigned);
        }

        private void ReRun()
        {
            Clear();
            CreateDecisionVariables();
            OrdersAssignedToCargos();
            OrdersInTowns();
            OrderMatch();
            //MinimumRatioLimitForEachCargo();
            MinimumRatioLimitForEachCargoInTown();
            CargoCapacitiesByInventories();
            CreateObjective();
            Solve();
        }

        private void Clear()
        {
            Solver = Solver.CreateSolver("Dispatcher", "CBC_MIXED_INTEGER_PROGRAMMING");
            Objective = Solver.Objective();
            SameDayDelivery = new List<List<List<Variable>>>();
            CarryOver = new List<List<List<Variable>>>();
            NonDelivery = new List<List<List<Variable>>>();
            TotalOrderDeliveredByCargo = new List<Variable>();
        }
    }
}
