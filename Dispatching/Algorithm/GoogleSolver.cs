using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dispatching.DataModel;
using Google.OrTools.LinearSolver;

namespace Dispatching.Algorithm
{
    public class GoogleSolver
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
        /// x[i][k] € N: Amount of orders delivered in town k by cargo firm i
        /// during the same day
        /// </summary>
        private List<List<Variable>> SameDayDelivery;

        /// <summary>
        /// y[i][k] € N: Amount of orders not delivered the same day but carried
        /// over
        /// </summary>
        private List<List<Variable>> CarryOver;

        /// <summary>
        /// z[i][k] € N: Amount of orders not delivered
        /// </summary>
        private List<List<Variable>> NonDelivery;

        /// <summary>
        /// List of cargos
        /// </summary>
        private List<Cargo> Cargos;

        /// <summary>
        /// List of towns
        /// </summary>
        private List<Town> Towns;

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
        /// Objective function
        /// </summary>
        private Objective Objective;

        /// <summary>
        /// Output model
        /// </summary>
        private List<OutputModel> Output;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="cargos"></param>
        /// <param name="towns"></param>
        public GoogleSolver(List<Cargo> cargos, List<Town> towns)
        {
            Solver = Solver.CreateSolver("Dispatcher", "CBC_MIXED_INTEGER_PROGRAMMING");
            Objective = Solver.Objective();

            TotalOrderDeliveredByCargo = new List<Variable>();
            SameDayDelivery = new List<List<Variable>>();
            CarryOver = new List<List<Variable>>();
            NonDelivery = new List<List<Variable>>();

            Cargos = cargos;
            Towns = towns;

            NumOfCargos = Cargos.Count;
            NumOfTowns = Towns.Count;

            OrderAmount = Towns.Sum(x => x.GetDemand());

            Output = new List<OutputModel>();
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
        /// Create decision variables for the underlying mathematical model
        /// a[i]    € N: TotalOrderDeliveredByCargo
        /// x[i][k] € N: SameDayDelivery
        /// y[i][k] € N: CarryOver
        /// z[i][k] € N: NonDelivery
        /// </summary>
        private void CreateDecisionVariables()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                var name = String.Format("a[{0}]", (i + 1));
                var ub = Cargos[i].GetCapacity();

                var a_i = Solver.MakeIntVar(0, ub, name);
                TotalOrderDeliveredByCargo.Add(a_i);
            }

            //SameDayDelivery variables
            for (int i = 0; i < NumOfCargos; i++)
            {
                var x_i = new List<Variable>();
                for (int k = 0; k < NumOfTowns; k++)
                {
                    var name = String.Format("x[{0}][{1}]", (i + 1), (k + 1));

                    var town = Towns[k];
                    var cargo = Cargos[i];

                    var ub = town.GetSameDayCapacity(cargo);

                    var x_ij = Solver.MakeIntVar(0, ub, name);

                    x_i.Add(x_ij);
                }
                SameDayDelivery.Add(x_i);
            }

            //CarryOver variables
            for (int i = 0; i < NumOfCargos; i++)
            {
                var y_i = new List<Variable>();

                for (int k = 0; k < NumOfTowns; k++)
                {
                    var name = String.Format("y[{0}][{1}]", (i + 1), (k + 1));

                    var town = Towns[k];
                    var cargo = Cargos[i];

                    var ub = town.GetCarryOverCapacity(cargo);

                    var y_ij = Solver.MakeIntVar(0, ub, name);

                    y_i.Add(y_ij);
                }
                CarryOver.Add(y_i);
            }

            //NonDelivery variables
            for (int i = 0; i < NumOfCargos; i++)
            {
                var z_i = new List<Variable>();

                for (int k = 0; k < NumOfTowns; k++)
                {
                    var name = String.Format("z[{0}][{1}]", (i + 1), (k + 1));

                    var town = Towns[k];
                    var cargo = Cargos[i];

                    var ub = Int32.MaxValue;

                    var z_ij = Solver.MakeIntVar(0, ub, name);

                    z_i.Add(z_ij);
                }
                NonDelivery.Add(z_i);
            }
        }

        /// <summary>
        /// Create objective function for the underlying mathematical model
        /// sum(i,k) SDC[i][k] x[i][k] + COC[i][k] x[i][k] + NDC[i][k] x[i][k]
        /// where objective sense is minimization and the costs are:
        /// SDC[i][k]: SameDay cost for cargo i in town k
        /// COC[i][k]: CarryOver cost for cargo i in town k
        /// NDC[i][k]: NonDelivery cost for cargo i in town k
        /// </summary>
        private void CreateObjective()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                for (int k = 0; k < NumOfTowns; k++)
                {
                    var town = Towns[k];
                    var cargo = Cargos[i];

                    var sameDayCost = town.GetSameDayDeliveryCost(cargo);
                    var carryOvercost = town.GetCarryOverCost(cargo);
                    var nonDeliveryCost = town.GetNonDeliveryCost(cargo);
                    var nps = town.GetNPS(cargo);

                    Objective.SetCoefficient(SameDayDelivery[i][k], sameDayCost + nps);
                    Objective.SetCoefficient(CarryOver[i][k], carryOvercost + nps);
                    Objective.SetCoefficient(NonDelivery[i][k], nonDeliveryCost + nps);
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
            MinimumRatioLimitForEachCargoInTown();
        }

        /// <summary>
        /// Solve the mathematical model by calling solvers' solve
        /// </summary>
        private void Solve()
        {
            var status = Solver.Solve();
        }

        /// <summary>
        /// Print some statistics concerning the solution
        /// </summary>
        private void PrintResults()
        {
            var ratios = new List<Double>();
            var cargoNames = new List<String>();

            Console.WriteLine("Total Order Quantity: {0}", OrderAmount);
            Console.WriteLine("Total Cost: {0} TL", Solver.Objective().Value());
            Console.WriteLine("Solution Time: {0} milliseconds\n ", Solver.WallTime());
            for (int i = 0; i < NumOfCargos; i++)
            {
                cargoNames.Add(Cargos[i].GetID());
                var x_i = TotalOrderDeliveredByCargo[i].SolutionValue();
                ratios.Add(x_i / OrderAmount);
                Console.WriteLine("r[{0}] = {1}\t x[{0}] = {2}", Cargos[i].GetID(), Math.Round(ratios[i], 3), TotalOrderDeliveredByCargo[i].SolutionValue());
            }

            var globalOutput = new OutputModel(ratios, "TURKEY", "" ,cargoNames);
            Output.Add(globalOutput);

            for (int k = 0; k < NumOfTowns; k++)
            {
                var town = Towns[k].GetID();
                var cargoRatios = new List<Double>();
                var cargos = new List<String>();

                for (int i = 0; i < NumOfCargos; i++)
                {
                    var sd = SameDayDelivery[i][k].SolutionValue();
                    var co = CarryOver[i][k].SolutionValue();
                    var nd = NonDelivery[i][k].SolutionValue();

                    var ratio = (sd + co + nd) / Towns[k].GetDemand();
                    cargoRatios.Add(ratio);
                    cargos.Add(Cargos[i].GetID());
                }
                var outputModel = new OutputModel(cargoRatios, Towns[k].GetID(), "", cargos);
                Output.Add(outputModel);
            }

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
        /// sum(i) x[i][k] + y[i][k] + z[i][k] = d[k] for all k € K 
        /// </summary>
        private void OrdersInTowns()
        {
            for (int k = 0; k < NumOfTowns; k++)
            {
                var demand = Towns[k].GetDemand();
                var constraint = Solver.MakeConstraint(demand, demand);

                for (int i = 0; i < NumOfCargos; i++)
                {
                    constraint.SetCoefficient(SameDayDelivery[i][k], 1);
                    constraint.SetCoefficient(CarryOver[i][k], 1);
                    constraint.SetCoefficient(NonDelivery[i][k], 1);
                }
            }
        }

        /// <summary>
        /// This constraints make sure that the sum of the orders assigned to a cargo
        /// in different towns must be equal to the total orders assigned.
        /// sum(k) x[i][k] + y[i][k] + z[i][k] = a[i] for all i € N
        /// </summary>
        private void OrderMatch()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                var constraint = Solver.MakeConstraint(0, 0);

                for (int k = 0; k < NumOfTowns; k++)
                {
                    constraint.SetCoefficient(SameDayDelivery[i][k], 1);
                    constraint.SetCoefficient(CarryOver[i][k], 1);
                    constraint.SetCoefficient(NonDelivery[i][k], 1);
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
            for (int i = 0; i < NumOfCargos; i++)
            {
                var ratio = Cargos[i].GetMinimumRatio();
                var constraint = Solver.MakeConstraint(OrderAmount * ratio, Double.PositiveInfinity);
                constraint.SetCoefficient(TotalOrderDeliveredByCargo[i], 1);
            }
        }

        /// <summary>
        /// The amount orders assigned to each cargo in a town must be greater than
        /// a certain percentage
        /// x[i][k] + y[i][k] + z[i][k] >= d[k] * ratio[i] for all i € N; k € K
        /// </summary>
        private void MinimumRatioLimitForEachCargoInTown()
        {
            for (int i = 0; i < NumOfCargos; i++)
            {
                for (int k = 0; k < NumOfTowns; k++)
                {
                    var town = Towns[k];
                    var cargo = Cargos[i];

                    var ratio = town.GetCargoMinRatio(cargo);
                    var constraint = Solver.MakeConstraint(town.GetDemand() * ratio, Double.PositiveInfinity);

                    constraint.SetCoefficient(SameDayDelivery[i][k], 1);
                    constraint.SetCoefficient(CarryOver[i][k], 1);
                    constraint.SetCoefficient(NonDelivery[i][k], 1);
                }
            }
        }

    }
}
