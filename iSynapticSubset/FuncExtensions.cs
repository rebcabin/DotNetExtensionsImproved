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
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Monza.DotNetExtensions.iSynaptic
{
    public static partial class FuncExtensions
    {
        public static Action ToAction<TRet>(this Func<TRet> self)
        {
            Contract.Requires(null != self, "self");
            return () => self();
        }

        public static IComparer<T> ToComparer<T>(this Func<T, T, int> self)
        {
            Contract.Requires(null != self, "self");
            return new FuncComparer<T>(self);
        }

        public static IEqualityComparer<T> ToEqualityComparer<T, TResult>(this Func<T, TResult> selector)
        {
            Contract.Requires(null != selector, "selector");

            return ToEqualityComparer<T>((x, y) => EqualityComparer<TResult>.Default.Equals(selector(x), selector(y)),
                                          x => EqualityComparer<TResult>.Default.GetHashCode((selector(x))));
        }

        public static IEqualityComparer<T> ToEqualityComparer<T>(this Func<T, T, bool> equalsStrategy, Func<T, int> hashCodeStrategy)
        {
            Contract.Requires(null != equalsStrategy, "equalsStrategy");
            Contract.Requires(null != hashCodeStrategy, "hashCodeStrategy");

            return new FuncEqualityComparer<T>(equalsStrategy, hashCodeStrategy);
        }

        public static Func<TResult> Memoize<TResult>(this Func<TResult> self)
        {
            Contract.Requires(null != self, "self");

            TResult result = default(TResult);
            Exception exception = null;
            bool executed = false;

            return () =>
            {
                if (!executed)
                {
                    try
                    {
                        result = self();
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                        throw;
                    }
                    finally
                    {
                        self = null;
                        executed = true;
                    }
                }

                if (exception != null)
                    throw exception;

                return result;
            };
        }

        public static Func<TResult> Synchronize<TResult>(this Func<TResult> self)
        {
            return self.Synchronize(() => true);
        }

        public static Func<TResult> Synchronize<TResult>(
            this Func<TResult> self, 
            Func<bool> needsSynchronizationPredicate)
        {
            Contract.Requires(null != self, "self");
            Contract.Requires(null != needsSynchronizationPredicate, "needsSynchronizationPredicate");

            var lockObject = new object();
            return SynchronizeWith(self, needsSynchronizationPredicate, lockObject);
        }

        public static Func<TResult> SynchronizeWith<TResult>(
            this Func<TResult> self, 
            Func<bool> needsSynchronizationPredicate, 
            object lockObject)
        {
            Contract.Requires(null != self, "self");
            Contract.Requires(null != needsSynchronizationPredicate, "needsSynchronizationPredicate");
            Contract.Requires(null != lockObject, "lockObject");

            return () =>
            {
                if (needsSynchronizationPredicate())
                {
                    lock (lockObject)
                    {
                        return self();
                    }
                }

                return self();
            };
        }

        public static Func<Maybe<TResult>> Or<TResult>(this Func<Maybe<TResult>> self, Func<Maybe<TResult>> orFunc)
        {
            if (self == null || orFunc == null)
                return self ?? orFunc;

            return () =>
            {
                var results = self();

                if (results.HasValue != true && results.Exception == null)
                    return orFunc();

                return results;
            };
        }

        private class FuncComparer<T> : IComparer<T>
        {
            private readonly Func<T, T, int> _Strategy;

            public FuncComparer(Func<T, T, int> strategy)
            {
                _Strategy = strategy;
            }

            public int Compare(T x, T y)
            {
                return _Strategy(x, y);
            }
        }

        private class FuncEqualityComparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> _Strategy;
            private readonly Func<T, int> _HashCodeStrategy;

            public FuncEqualityComparer(Func<T, T, bool> strategy, Func<T, int> hashCodeStategy)
            {
                _Strategy = strategy;
                _HashCodeStrategy = hashCodeStategy;
            }

            public bool Equals(T x, T y)
            {
                return _Strategy(x, y);
            }

            public int GetHashCode(T obj)
            {
                return _HashCodeStrategy(obj);
            }
        }
    }
}
