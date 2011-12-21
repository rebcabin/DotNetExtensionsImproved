using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Experimental.DotNetExtensions;
using Experimental.MachineLearning.Viterbi;

namespace ViterbiTests
{
    [TestClass]
    public class ViterbiTest1
    {
        [TestMethod]
        public void WikipediaUnitTest()
        {
            var Rainy = "Rainy";
            var Sunny = "Sunny";

            var Walk = "Walk";
            var Shop = "Shop";
            var Clean = "Clean";

            var states = new[] { Rainy, Sunny };
            var observations = new[] { Walk, Shop, Clean };

            const double RainyProb = 0.6D;
            const double SunnyProb = 1D - RainyProb;

            const double RainyToRainyProb = 0.7D;
            const double RainyToSunnyProb = 1D - RainyToRainyProb;

            const double SunnyToRainyProb = 0.4D;
            const double SunnyToSunnyProb = 1D - SunnyToRainyProb;

            const double RainyWalkProb = 0.1D;
            const double RainyShopProb = 0.4D;
            const double RainyCleanProb = 1D - RainyWalkProb - RainyShopProb;

            const double SunnyWalkProb = 0.6D;
            const double SunnyShopProb = 0.3D;
            const double SunnyCleanProb = 1 - SunnyWalkProb - SunnyShopProb;

            var startingProbabilitiesDict = IDictionary
                .AddUnconditionally(Rainy, RainyProb)
                .AddUnconditionally(Sunny, SunnyProb);

            var transitionProbabilitiesDict = IDictionary
                .AddUnconditionally(Rainy, IDictionary
                    .AddUnconditionally(Rainy, RainyToRainyProb)
                    .AddUnconditionally(Sunny, RainyToSunnyProb))
                .AddUnconditionally(Sunny, IDictionary
                    .AddUnconditionally(Rainy, SunnyToRainyProb)
                    .AddUnconditionally(Sunny, SunnyToSunnyProb));

            var emissionProbabilitiesDict = IDictionary
                .AddUnconditionally(Rainy, IDictionary
                    .AddUnconditionally(Walk, RainyWalkProb)
                    .AddUnconditionally(Shop, RainyShopProb)
                    .AddUnconditionally(Clean, RainyCleanProb))
                .AddUnconditionally(Sunny, IDictionary
                    .AddUnconditionally(Walk, SunnyWalkProb)
                    .AddUnconditionally(Shop, SunnyShopProb)
                    .AddUnconditionally(Clean, SunnyCleanProb));

            try
            {
                var fbv = new FunctionBasedDoubleViterbi<string, string>
                    (startingProbabilities: (s => startingProbabilitiesDict[s])
                    , transitionProbabilities: ((sOut, sIn) => transitionProbabilitiesDict[sOut][sIn])
                    , emissionProbabilities: ((s, o) => emissionProbabilitiesDict[s][o])
                    );

                var obsStates = observations
                    .Zip(
                        new[] { states }.Repeat(),
                        (o, ss) => Tuple.Create(o, (IEnumerable<string>)ss))
                    .ToObservable()
                    ;

                obsStates.Subscribe(fbv);

                var finalProbabilities = fbv.V.Last();

                Assert.AreEqual(Math.Round(finalProbabilities[Rainy], 6), 0.013440d);
                Assert.AreEqual(Math.Round(finalProbabilities[Sunny], 6), 0.002592d);

                var mostProbableState = states.ArgMax(y => finalProbabilities[y]);

                Assert.AreEqual(mostProbableState, Rainy);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
