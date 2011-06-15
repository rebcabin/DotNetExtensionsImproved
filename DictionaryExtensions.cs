﻿// The MIT License

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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using Monza.DotNetExtensions.iSynaptic;

namespace Monza.DotNetExtensions
{
    /// <summary>
    /// Extensions to the IDictionary interface, supporting chainable dictionary combinators.
    /// </summary>
    public static class IDictionary
    {
        /// <summary>
        /// Creation through explicitly supplied first values.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="key">A value of type K.</param>
        /// <param name="value">A value of type V.</param>
        /// <returns>The IDictionary interface of a new dictionary.</returns>
        public static IDictionary<K, V> AddUnconditionally<K, V>(K key, V value)
        {
            return new Dictionary<K, V>()
                .AddUnconditionally(key, value);
        }

        /// <summary>
        /// Creation through explicitly supplied first values.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="key">A value of type K.</param>
        /// <param name="value">A value of type V.</param>
        /// <returns>The IDictionary interface of a new dictionary.</returns>
        public static IDictionary<K, V> AddConditionally<K, V>(K key, V value)
        {
            return new Dictionary<K, V>()
                .AddConditionally(key, value);
        }

        /// <summary>
        /// Creation based on types of ignored values. Recommended usage IDictionary.Create(default(K), default(V)).
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="key">A value of type K to assist type inference; ignored.</param>
        /// <param name="value">A value of type V to assist type inference; ignored.</param>
        /// <returns>The IDictionary interface of a new dictionary.</returns>
        public static IDictionary<K, V> Create<K, V>(K key, V value)
        {
            return new Dictionary<K, V>();
        }

        /// <summary>
        /// Creation based on explicitly supplied type arguments.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <returns>The IDictionary interface of a new dictionary.</returns>
        public static IDictionary<K, V> Create<K, V>()
        {
            return new Dictionary<K, V>();
        }

        /// <summary>
        /// Returns a Maybe instance encapsulating a potentially absent value.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="theDictionary">The input dictionary.</param>
        /// <param name="key">The key to try.</param>
        /// <returns>The potentially absent value encapsulated in a Maybe.</returns>
        public static Maybe<V> TryGetValue<K, V>(
            this IDictionary<K, V> theDictionary,
            K key)
        {
            Contract.Requires(null != theDictionary, "theDictionary");

            V retreived = default(V);

            return theDictionary
                .ToMaybe()
                .Where(d => d.TryGetValue(key, out retreived))
                .Select(_ => retreived);
        }

        /// <summary>
        /// Add whether the key is present or not.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="theDictionary">The input dictionary.</param>
        /// <param name="keyValuePair">The input key-value pair.</param>
        /// <returns>The modified dictionary.</returns>
        public static IDictionary<K, V> AddUnconditionally<K, V>(
            this IDictionary<K, V> theDictionary,
            KeyValuePair<K, V> keyValuePair)
        {
            Contract.Requires(theDictionary != null);

            // Take the kvp . . .
            return keyValuePair
                // Bring it into the state monad with the IDictionary as state
                // and the default propagator that does nothing to the state . . .
                .ToState<IDictionary<K, V>, KeyValuePair<K, V>>()
                // Shove this through a transformer that produces a bool and state . . .
                .Bind<IDictionary<K, V>, KeyValuePair<K, V>, bool>(
                // From the provided kvp . . .
                    kvp => new State<IDictionary<K, V>, bool>(
                        // with a propagator that UNCONDITIONALLY puts the kvp in 
                        // the dictionary, and reports whether the key was already
                        // present . . .
                        propagator: dict => Tuple.Create(dict
                            // via the Maybe monad . . .
                            .TryGetValue(kvp.Key)
                            // if the value was not in the dictionary, add it . . .
                            .OnNoValue(() => dict.Add(key: kvp.Key, value: kvp.Value))
                            // if the value was in the dictionary, replace it . . .
                            .OnValue(_ => dict[kvp.Key] = kvp.Value)
                            // be thread-safe . . .
                            .SynchronizeWith(dict)
                            // provide the bool . . .
                            .HasValue,
                            // and the original dictionary.
                            dict)))
                // Apply the newly bound state to the input dictionary . . .
                .Propagator(theDictionary)
                // Extract the dictionary from the tuple and return it:
                .Item2
                ;

            // Sadly, the bool info about whether the key was in the dictionary
            // is lost, the price we pay to thread the dictionary out. 
        }

        /// <summary>
        /// Add whether the key is present or not.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="theDictionary">The input dictionary.</param>
        /// <param name="key">The key to install.</param>
        /// <param name="value">The value to install.</param>
        /// <returns>The modified dictionary.</returns>
        public static IDictionary<K, V> AddUnconditionally<K, V>(
            this IDictionary<K, V> theDictionary,
            K key,
            V value)
        {
            return theDictionary
                .AddUnconditionally(
                    new KeyValuePair<K, V>(key: key, value: value));
        }

        /// <summary>
        /// Add only if key is NOT already present.
        /// </summary>
        /// <typeparam name="K">The type of keys in the dictionary.</typeparam>
        /// <typeparam name="V">The type of values in the dictionary.</typeparam>
        /// <param name="theDictionary">The input dictionary.</param>
        /// <param name="keyValuePair">The input key-value pair.</param>
        /// <returns>The modified dictionary.</returns>
        public static IDictionary<K, V> AddConditionally<K, V>(
            this IDictionary<K, V> theDictionary,
            KeyValuePair<K, V> kvpInput)
        {
            Contract.Requires(theDictionary != null);

            // Take the kvp . . .
            return kvpInput
                // Bring it into the state monad with the IDictionary as state
                // and the default propagator that does nothing to the state . . .
                .ToState<IDictionary<K, V>, KeyValuePair<K, V>>()
                // Shove this through a transformer that produces a bool and state . . .
                .Bind<IDictionary<K, V>, KeyValuePair<K, V>, bool>(
                // From the provided kvp . . .
                    kvp => new State<IDictionary<K, V>, bool>(
                        // with a propagator that CONDITIONALLY puts the kvp in 
                        // the dictionary, and reports whether the key was already
                        // present . . .
                        propagator: dict => Tuple.Create(dict
                            // via the Maybe monad . . .
                            .TryGetValue(kvp.Key)
                            // if the value was not in the dictionary, add it . . .
                            .OnNoValue(() => dict.Add(key: kvp.Key, value: kvp.Value))
                            // be thread safe . . .
                            .SynchronizeWith(dict)
                            // provide the bool . . .
                            .HasValue, 
                            // and the original dictionary.
                            dict)))
                // Apply the newly bound state to the input dictionary . . .
                .Propagator(theDictionary)
                // Extract the dictionary from the tuple and return it:
                .Item2
                ;

            // Sadly, the info about whether the key was in the dictionary
            // is lost, the price we pay to thread the dictionary through. 
        }

        public static IDictionary<K, V> AddConditionally<K, V>(
            this IDictionary<K, V> theDictionary,
            K key,
            V value)
        {
            return theDictionary
                .AddConditionally(
                    new KeyValuePair<K, V>(key: key, value: value));
        }

        /// <summary>
        /// Look up the value, and return the default of type 'Value' if the key is not present.
        /// </summary>
        /// <typeparam name="K">The type of keys.</typeparam>
        /// <typeparam name="T">The type of values.</typeparam>
        /// <param name="theDictionary">The dictionary in which to look up values.</param>
        /// <param name="key">They key to look up.</param>
        /// <returns>The value of type T corresponding to the key if the key is present in the dictionary, otherwise, the defined default value for the type T of all values.</returns>
        public static V GetValueOrDefault<K, V>(
            this IDictionary<K, V> theDictionary,
            K key)
        {
            V result;
            bool foundP = theDictionary.TryGetValue(key, out result);
            Contract.Assert(foundP == true || result.Equals(default(V)));
            return result; // lapses automatically to default(T) if TryGetValue returns false
        }

        /// <summary>
        /// Increases the tally count for the given key in the dictionary..
        /// </summary>
        /// <typeparam name="K">The type of keys.</typeparam>
        /// <param name="theDictionary">The dictionary in which to tally keys.</param>
        /// <param name="key">The key to tally.</param>
        public static IDictionary<K,long> AccumulateTally<K>(
            this IDictionary<K, long> theDictionary,
            K key)
        {
            long currentCount = theDictionary.GetValueOrDefault(key) + 1;
            return theDictionary.AddUnconditionally(key, currentCount);
        }
    }
}