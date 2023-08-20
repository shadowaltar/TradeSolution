using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeLogicCore.Algorithms
{
    internal class Algorithms
    {

        public IAlgorithm<RumiVariables> CreateRumi()
        {
            return new Rumi();
        }
    }
}
