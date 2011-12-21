// The MIT License

// Portions Copyright (c) 2011 Jordan E. Terrell, licensed to 
// Microsoft Corporation under the MIT license (copied below).
// 
// Portions Copyright (c) 2011 Microsoft Corporation
// 
// Portions adapted from http://northhorizon.net/ under the 
// Creative Commons Attribution license 
// http://creativecommons.org/licenses/by/3.0/us/)

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
using System.Linq;

namespace Experimental.DotNetExtensions
{
    /// <summary>
    /// Provides extension methods for <see cref="IEnumerable{T}"/> and <see cref="IEnumerable"/>.
    /// </summary>
    public static class IEnumerable
    {
        /// <summary>
        /// The standard monadic "Return" operation.
        /// </summary>
        /// <typeparam name="T">The type of the thing to lift into the monad.</typeparam>
        /// <param name="singleValue">The sone value to lift into the monad.</param>
        /// <returns>An IEnumerable containing a single item of type T.</returns>
        public static IEnumerable<T> Return<T>(this T singleValue)
        {
            return new[] { singleValue } as IEnumerable<T>;
        }

        /// <summary>
        /// Constructs the outer product (outer join) of two IEnumerables, as an IEnumerable of Tuples.
        /// </summary>
        /// <param name="these">The first IEnumerable.</param>
        /// <param name="those">The second IEnumerable.</param>
        /// <returns>The IEnumerable of pairs (as Tuples) of the inputs.</returns>
        public static IEnumerable<Tuple<A, B>> Outer<A, B>(
            this IEnumerable<A> these,
            IEnumerable<B> those)
        {
            Contract.Requires(these != null);
            Contract.Requires(those != null);

            return from a in these
                   from b in those
                   select Tuple.Create(a, b);
        }

        /// <summary>
        /// Gets the input argument that produces the maximum value of the specified function.
        /// </summary>
        /// <typeparam name="A">The type of items in the collection.</typeparam>
        /// <typeparam name="T">The type of the value yielded from the specified function.</typeparam>
        /// <param name="collection">The target collection.</param>
        /// <param name="function">The function used to produce values.</param>
        /// <returns>The argument that produces the highest value.</returns>
        public static A ArgMax<A, V>(
            this IEnumerable<A> collection,
            Func<A, V> function)
            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);
            return ArgComp(collection, function, GreaterThan);
        }

        private static bool GreaterThan<A>(A first, A second) where A : IComparable<A>
        {
            return first.CompareTo(second) > 0;
        }

        /// <summary>
        /// Gets the intput argument that produces the minimum value of the specified function.
        /// </summary>
        /// <typeparam name="A">The type of items in the collection.</typeparam>
        /// <typeparam name="T">The type of the value yielded from the specified function.</typeparam>
        /// <param name="collection">The target collection.</param>
        /// <param name="function">The function used to produce values.</param>
        /// <returns>The argument that produces the least value.</returns>
        public static A ArgMin<A, V>(
            this IEnumerable<A> collection,
            Func<A, V> function)
            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);
            return ArgComp(collection, function, LessThan);
        }

        private static bool LessThan<A>(A first, A second) where A : IComparable<A>
        {
            return first.CompareTo(second) < 0;
        }

        private static A ArgComp<A, V>(
            IEnumerable<A> collection,
            Func<A, V> function,
            Func<V, V, bool> accept)

            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);
            Contract.Requires(accept != null);

            var isSet = false;
            var maxArg = default(A);
            var extremeValue = default(V);

            foreach (var item in collection)
            {
                var value = function(item);
                if (!isSet || accept(value, extremeValue))
                {
                    maxArg = item;
                    extremeValue = value;
                    isSet = true;
                }
            }

            return maxArg;
        }

        /// <summary>
        /// Encapsulates returned argument-and-value pairs from ArgAndMax and ArgAndMin.
        /// </summary>
        /// <typeparam name="A">The argument at which the IEnumerable has its extreme value.</typeparam>
        /// <typeparam name="T">The extreme value.</typeparam>
        public class ArgumentValuePair<A, V>
        {
            public A Argument { get; set; }
            public V Value { get; set; }
        }

        /// <summary>
        /// Gets the argument that produces the maximum value and the maximum value produced by the specified function.
        /// </summary>
        /// <typeparam name="A">The type of items in the collection.</typeparam>
        /// <typeparam name="T">The type of the value yielded from the specified function.</typeparam>
        /// <param name="collection">The target collection.</param>
        /// <param name="function">The function used to produce values.</param>
        /// <returns>Instance of an anonymous type with properties "Argument" and "Value" for the argument producing the maximum value and the minimum value.</returns>
        public static ArgumentValuePair<A, V> ArgAndMax<A, V>(
            this IEnumerable<A> collection,
            Func<A, V> function)
            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);

            return ArgAndComp(collection, function, GreaterThan);
        }

        /// <summary>
        /// Gets the argument that produces the minimum value and the minimum value produced by the specified function.
        /// </summary>
        /// <typeparam name="A">The type of items in the collection.</typeparam>
        /// <typeparam name="T">The type of the value yielded from the specified function.</typeparam>
        /// <param name="collection">The target collection.</param>
        /// <param name="function">The function used to produce values.</param>
        /// <returns>Instance of a dynamic type with properties "Argument" and "Value" for the argument producing the minimum value and the minimum value.</returns>
        public static ArgumentValuePair<A, V> ArgAndMin<A, V>(
            this IEnumerable<A> collection,
            Func<A, V> function)
            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);

            return ArgAndComp(collection, function, LessThan);
        }

        private static ArgumentValuePair<A, V> ArgAndComp<A, V>(
            IEnumerable<A> collection,
            Func<A, V> function,
            Func<V, V, bool> accept)
            where V : IComparable<V>
        {
            Contract.Requires(collection != null);
            Contract.Requires(function != null);
            Contract.Requires(accept != null);

            var isSet = false;
            var maxArg = default(A);
            var extremeValue = default(V);

            foreach (var item in collection)
            {
                var value = function(item);
                if (!isSet || accept(value, extremeValue))
                {
                    maxArg = item;
                    extremeValue = value;
                    isSet = true;
                }
            }

            return new ArgumentValuePair<A, V> { Argument = maxArg, Value = extremeValue };
        }

        /// <summary>
        /// Perform an action for side effect pairwise on elements of a pair of IEnumerables.
        /// </summary>
        /// <typeparam name="T">The type of elements in the first IEnumerable.</typeparam>
        /// <typeparam name="U">The type of elements in the second IEnumerable.</typeparam>
        /// <param name="first">The first IEnumerable.</param>
        /// <param name="second">The second IEnumerable.</param>
        /// <param name="action">The action to perform on pairs of elements.</param>
        public static void ZipDo<T, U>(
            this IEnumerable<T> first,
            IEnumerable<U> second,
            Action<T, U> action)
        {
            Contract.Requires(first != null);
            Contract.Requires(second != null);
            Contract.Requires(action != null);

            using (IEnumerator<T> firstEnumerator = first.GetEnumerator())
            using (IEnumerator<U> secondEnumerator = second.GetEnumerator())
                while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext())
                    action(firstEnumerator.Current, secondEnumerator.Current);
        }

        /// <summary>
        /// Perform an action for side effect pairwise on adjacent members of an IEnumerable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the input enumerable.</typeparam>
        /// <param name="enumerable">The input enumerable.</param>
        /// <param name="action">The action to perform on pairs of adjacent elements.</param>
        public static IEnumerable<T> PairwiseDo<T>(
            this IEnumerable<T> enumerable,
            Action<T, T> action)
        {
            Contract.Requires(enumerable != null);
            Contract.Requires(action != null);
            Contract.Requires(enumerable.First() != null);

            enumerable.ZipDo(enumerable.Skip(1), action);

            return enumerable;
        }

        /// <summary>
        /// Map a binary function over all adjacent pairs in the input enumerable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the input enumerable.</typeparam>
        /// <typeparam name="U">The type of element in the returned enumerable.</typeparam>
        /// <param name="enumerable">The input enumerable.</param>
        /// <param name="resultSelector">The function to map.</param>
        /// <returns>An enumerable of results of mapping the function over the input enumerable, two adjacent elements at a time.</returns>
        public static IEnumerable<U> Pairwise<T, U>(
            this IEnumerable<T> enumerable,
            Func<T, T, U> resultSelector)
        {
            Contract.Requires(enumerable != null);
            Contract.Requires(resultSelector != null);
            Contract.Requires(enumerable.First() != null);

            return enumerable.Zip(enumerable.Skip(1), resultSelector);
        }
    }
}
