// The MIT License

// Portions Copyright (c) 2011 Jordan E. Terrell, licensed to 
// Microsoft Corporation under the MIT license (copied below).
// 
// Portions Copyright (c) 2011 Microsoft Corporation

// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:

// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;

namespace Experimental.DotNetExtensions
{

    /// <summary>
    /// Factory class for ValueStatePair<T, S> objects
    /// </summary>
    public static class ValueStatePair
    {
        /// <summary>
        /// Creates a value-state pair
        /// </summary>
        /// <typeparam name="T">The type of the value object.</typeparam>
        /// <typeparam name="S">The type of the state object.</typeparam>
        /// <param name="value">The value object to store in the pair.</param>
        /// <param name="state">The state object to store in the pair.</param>
        /// <returns>The newly created value-state pair.</returns>
        public static ValueStatePair<T, S> Create<T, S>(T value, S state)
        {
            return new ValueStatePair<T, S>(value, state);
        }
    }
    /// <summary>
    /// Represents a tuple of a value and state.
    /// </summary>
    /// <typeparam name="T">The type of the value object.</typeparam>
    /// <typeparam name="S">The type of the state object.</typeparam>
    public struct ValueStatePair<T, S>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">The value object to store in the pair.</param>
        /// <param name="state">The state object to store in the pair.</param>
        public ValueStatePair(T value, S state)
            : this()
        {
            Value = value;
            State = state;
        }

        public T Value { get; private set; }
        public S State { get; private set; }
    }

    /// <summary>
    /// Encapsulates a propagator transform that combines a mutable state object 
    /// with a value, usually via closure.
    /// </summary>
    /// <typeparam name="S">The type of state object to propagate.</typeparam>
    /// <typeparam name="T">The type of values included with propagated state.</typeparam>
    public struct State<S, T>
    {
        // Haskell does not require a name for the function "inside" the 
        // state monad that moves the state along, but we do. Let's call it
        // a Propagator:

        /// <summary>
        /// The content of a State is the propagator function that combines a
        /// mutable state object with a value.
        /// </summary>
        public Func<S, ValueStatePair<T, S>> Propagator { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="propagator">The input propagator.</param>
        public State(Func<S, ValueStatePair<T, S>> propagator)
            : this()
        {
            Contract.Requires(null != propagator, "propagator");
            Contract.Ensures(Propagator != null, "Propagator");
            Propagator = propagator;
        }
    }

    /// <summary>
    /// Encapsulates a propagator transform that combines a mutable state object 
    /// with a value, usually via closure.
    /// </summary>
    public static class State
    {
        /// <summary>
        /// Produces a State with a trivial propagator: a closure over the given value.
        /// </summary>
        /// <typeparam name="S">The type of state object to propagate.</typeparam>
        /// <typeparam name="T">The type of values included with propagated state.</typeparam>
        /// <param name="value">The value to include with the propagated state.</param>
        /// <returns>A State encapsulating the propagator closure.</returns>
        public static State<S, T> Return<S, T>(T value)
        {
            // value is allowed to be null, but it's better if it's a Maybe (TODO)!
            return new State<S, T>(propagator: s => ValueStatePair.Create(value, s));
        }

        // Conventionally, in LINQ, the monadic "return" operator is written "To...,"
        // as in "ToList," "ToArray," etc. These are synonyms for Return.

        /// <summary>
        /// Converts a value int a State with a trivial propagator: a closure over the given value.
        /// </summary>
        /// <typeparam name="S">The type of state object to propagate.</typeparam>
        /// <typeparam name="T">The type of values included with propagated state.</typeparam>
        /// <param name="value">The value to include with the propagated state.</param>
        /// <returns>A State encapsulating the propagator closure.</returns>
        public static State<S, T> ToState<S, T>(
            this T value)
        {
            // value is allowed to be null, but it's better if it's a Maybe (TODO)!
            return State.Return<S, T>(value);
        }
        
        /// <summary>
        /// Produces a State that chains an input propagator through a transform
        /// that produces a second propagator.
        /// </summary>
        /// <typeparam name="S">The type of state object to propagate through the chain.</typeparam>
        /// <typeparam name="T">The type of value included with propagated state in the first link of the chain.</typeparam>
        /// <typeparam name="U">The type of value included with propagated state in the second link of the chain.</typeparam>
        /// <param name="st">A State whose propagator includes values of type T.</param>
        /// <param name="t2su">A function that converts values of type T to States whose propagators include values of type U.</param>
        /// <returns>A State whose propagator includes values of type U.</returns>
        public static State<S, U> Bind<S, T, U>(
            this State<S, T> st,
            Func<T, State<S, U>> t2su)
        {
            Contract.Requires(null != st.Propagator, "mt.Propagator");
            Contract.Requires(null != t2su, "t2mu");

            return new State<S, U>(
                propagator: s =>
                {
                    var intermediate = st.Propagator(s);
                    var tNuValue = intermediate.Value;
                    var sNuState = intermediate.State;

                    return t2su(tNuValue).Propagator(sNuState);
                });
        }

        #region SelectMany Operator

        // This operator is implemented only to satisfy C#'s LINQ comprehension syntax.  
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static State<S, U> SelectMany<S, T, U>(
            this State<S, T> mt,
            Func<T, State<S, U>> t2mu)
        {
            Contract.Requires(null != mt.Propagator, "mt.Propagator");
            Contract.Requires(null != t2mu, "t2mu");

            return mt.Bind(t2mu);
        }

        // This operator is implemented only to satisfy C#'s LINQ comprehension syntax.  
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static State<S, V> SelectMany<S, T, U, V>(
            this State<S, T> mt,
            Func<T, State<S, U>> t2mu,
            Func<T, U, V> t2u2v)
        {
            Contract.Requires(null != t2mu, "t2mu");
            Contract.Requires(null != t2u2v, "t2u2v");

            return mt.Bind(t => t2mu(t).Bind(u => t2u2v(t, u).ToState<S, V>()));
        }

        #endregion
    }
}