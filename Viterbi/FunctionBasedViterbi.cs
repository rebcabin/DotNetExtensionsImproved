using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Experimental.DotNetExtensions;

namespace Experimental.MachineLearning.Viterbi
{
    // TODO: write contract reference assemblies

    // TODO: optimize to only call probability functions for domain
    // values guaranteed to return non-zero probabilities

    /// <summary>
    /// A reactive type of Viterbi algorithm (
    /// http://en.wikipedia.org/wiki/Viterbi_algorithm ), suitable for
    /// on-line or off-line application.  Requires user-supplied
    /// functions to express the model (state-to-state transition
    /// probabilities and state-to-observation probabilities)
    /// </summary>
    /// <typeparam name="S">The type of states.</typeparam>
    /// <typeparam name="O">The type of observations.</typeparam>
    public class FunctionBasedDoubleViterbi<O, S> : IObserver<Tuple<O, IEnumerable<S>>>
    {
        /// <summary>
        /// The TargetStates are the states the system might transition TO in response to an observation.
        /// </summary>
        private IEnumerable<S> TargetStates { get; set; }

        /// <summary>
        /// The SourceStates are the states the system might transition FROM to EMIT an observation.
        /// </summary>
        private IEnumerable<S> SourceStates { get; set; }

        /// <summary>
        /// Function that computes probabilities of occurrence of the initial states; for bootstrapping the model.
        /// </summary>
        public Func<S, double> StartingProbabilities { get; private set; }

        /// <summary>
        /// Transition probabilities from state-to-state; part of the Hidden Markov Model.
        /// </summary>
        public Func<S, S, double> TransitionProbabilities { get; private set; }

        /// <summary>
        /// Probabilities of observing outcome (observation) O given
        /// that the system is in state S; part of the Hidden Markov
        /// Model.
        /// </summary>
        public Func<S, O, double> EmissionProbabilities { get; private set; }

        /// <summary>
        /// After the current observation is processed, _V.Last()
        /// contains the probabilities of the most probable paths
        /// leading to each state.  Each element of _V is a map from
        /// state to probability of most-probable-path.  _V keeps the
        /// entire history of such probability maps.
        /// </summary>
        private List<Dictionary<S, double>> _V;

        /// <summary>
        /// At any time t and for any state s, V[t][s] is the
        /// probability of the most probable path ending in state s.
        /// The times are implicitly 0, 1, ...; that is, the indices
        /// of the IEnumerable.
        /// </summary>
        public IEnumerable<Dictionary<S, double>> V { get { return _V; } }

        /// <summary>
        /// For any state, the most probable path through all prior
        /// states leading to that state, given all the observations
        /// seen so far.  "Path" only keeps the LAST map from state to
        /// list of states, unlike V, which keeps the entire history
        /// of maps from states to probabilities of
        /// most-probable-paths.
        /// </summary>
        public Dictionary<S, List<S>> Path { get; private set; }

        /// <summary>
        /// The constructor requires functions for computing probabilities. 
        /// </summary>
        /// <param name="startingProbabilities">Initial probabilities, one for each state.</param>
        /// <param name="transitionProbabilities">Probabilities for transition from a state to any other state.</param>
        /// <param name="emissionProbabilities">Probabilities of seeing observation O given state S.</param>
        public FunctionBasedDoubleViterbi
        (
            Func<S, double> startingProbabilities,
            Func<S, S, double> transitionProbabilities,
            Func<S, O, double> emissionProbabilities)
        {
            Contract.Requires(startingProbabilities != null);
            Contract.Requires(transitionProbabilities != null);
            Contract.Requires(emissionProbabilities != null);

            // TODO: Require that probabilities sum APPROXIMATELY to one. 

            StartingProbabilities = startingProbabilities;
            TransitionProbabilities = transitionProbabilities;
            EmissionProbabilities = emissionProbabilities;
        }

        /// <summary>
        ///   Do nothing on completed.
        /// </summary>
        public void OnCompleted()
        {
        }

        /// <summary>
        ///   Re-throw on error.
        /// </summary>
        public void OnError(Exception error)
        {
            throw error;
        }

        /// <summary>
        ///   Static helper function.
        /// </summary>
        private static bool Implies(bool A, bool B)
        {
            return ((!A) || B);
        }

        /// <summary>
        ///   Static helper function.
        /// </summary>
        private static bool Iff(bool A, bool B)
        {
            return Implies(A, B) && Implies(B, A);
        }

        /// <summary>
        ///   LastV contains the mapping from state to probability
        ///   of most-probable-paths-to-the-state that was produced
        ///   by the prior observation.
        /// </summary>
        private Dictionary<S, double> LastV { get; set; }

        /// <summary>
        ///   OnNext takes a tuple of an observation and a sequence of
        ///   states that might have emitted this observation (the
        ///   FROM states).  The name of the tuple is "value".  The
        ///   observation is in "value.Item1" and the sequence of
        ///   states is in "value.Item2".
        /// </summary>
        public void OnNext(Tuple<O, IEnumerable<S>> value)
        {
            Contract.Requires(value.Item2 != null);
            // Contract.Requires fails here due to visibility of the properties.
            Contract.Assert(Iff(TargetStates == null, LastV == null));

            // True only first time around -- at bootstrapping time.
            if (TargetStates == null)
            {
                // Initialize the HISTORY LIST of maps from state to
                // probability-of-most-probable-path.
                _V = new List<Dictionary<S, double>>();

                // Initialize the map from state to
                // most-probable-path-leading-to-state.
                Path = new Dictionary<S, List<S>>();

                // Initialize the FIRST map from state to probability
                // of most-probable-path leading to the state.
                var firstV = new Dictionary<S, double>();

                // value.Item2 contains the FROM states: the states
                // that might have emitted this observation.  Now
                // compute the probabilities for transition to all the
                // TO states.  In this bootstrapping case, the
                // probabilities are simply the emission
                // probabilities -- the probabilities that the state
                // produced this observation -- times the probability
                // that the system was in that state.
                foreach (var state in value.Item2)
                {
                    // The probability of the most probable path
                    // leading to "state" given observation is the
                    // a-priori probability of state times the
                    // conditional probability of observation given
                    // the state.  Observation is in the slot
                    // "value.Item1."
                    firstV[state] =
                        StartingProbabilities(state) *
                        EmissionProbabilities(state, value.Item1);

                    // The state that yielded the transition to this
                    // most probable path is just "state" itself.
                    Path[state] = new List<S>();
                    Path[state].Add(state);
                }
                // The possible targets for the next transition are in
                // the states, in-turn in slot "value.Item2."
                TargetStates = value.Item2;
                _V.Add(firstV);
                LastV = firstV;
                return;
            }

            // The source states for this observation are the target
            // states from the last observation (Viterbi, strictly
            // speaking, has a memory of ONE transition).  In case of
            // the first observation -- the bootstrapping case, this
            // is also true, it just so happens that the source and
            // target states are the same during bootstrapping.
            SourceStates = TargetStates;

            // The target states for this observation are in the input
            // to OnNext.
            TargetStates = value.Item2;

            // Space for the new probabilities of the
            // most-probable-paths leading to each state.
            var nextV = new Dictionary<S, double>();

            // and for each candidate target state . . .
            foreach (var target in TargetStates)
            {
                // . . . find the SOURCE state that leads to the most
                // probable path to the target.
                //
                // Maximize over the prior states: the transition
                // probabilities from the source state, times the
                // conditional probabilities of seeing the actual
                // observation given the candidate source state, times
                // the probability of the most-probable-path leading
                // to the source state.  Use the LINQ non-standard
                // query operator "ArgAndMax," which, for any
                // IEnumerable, finds the input (the arg or "source")
                // that produces the maximum value of its given
                // function (lambda expression), and also that maximum
                // value, returning both the argument and the maximum
                // in an instance of class ArgumentValuePair.
                //
                // In this lambda expression, "target" is fixed
                // (closed over).
                var maxStateAndProb = SourceStates.ArgAndMax(source =>
                {
                    // probability of the most probable path that led
                    // to source
                    var a = LastV[source];

                    // transition probability from source to target
                    var b = TransitionProbabilities(source, target);

                    // probability of seeing actual observation if we
                    // are in the target state; observation is stored
                    // in value.Item1.
                    var c = EmissionProbabilities(target, value.Item1);

                    return a * b * c;
                });

                // After this point, we have found the SOURCE state
                // that produces the most probable path to the given
                // target state and yielding the given observation.
                // Save the probability of that most-probable-path.
                nextV[target] = maxStateAndProb.Value;

                // Copy the most probable path that led to the source
                // state that yields the maximum probability.  I must 
                // copy it because this path might be the same for 
                // multiple targets, and each target will need its own
                // copy of the prior path to append itself to.
                var newpath = new List<S>(Path[maxStateAndProb.Argument]);

                // Append the current state.
                newpath.Add(target);

                // Replace the path to the current target with the
                // refreshed one.
                Path[target] = newpath;
            }
            _V.Add(nextV);
            LastV = nextV;
        }
    }
}
