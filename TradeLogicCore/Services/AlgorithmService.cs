using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradeLogicCore.Algorithms;

namespace TradeLogicCore.Services;

public class AlgorithmService : IAlgorithmService
{
    public bool RegisterAlgorithm<IAlgorithm>(params object[] args)
    {
        throw new NotImplementedException();
    }

    public void Run<IAlgorithm>()
    {
        throw new NotImplementedException();
    }
}
