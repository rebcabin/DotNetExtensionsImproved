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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace Monza.DotNetExtensions.iSynaptic
{
    // Implementation of the Maybe monad. http://en.wikipedia.org/wiki/Monad_%28functional_programming%29#Maybe_monad
    // Thanks to Brian Beckman for his suggestions and assistance.
    // Don't Fear the Monad! http://channel9.msdn.com/shows/Going+Deep/Brian-Beckman-Dont-fear-the-Monads/
    public struct Maybe<T> : IMaybe<T>, IEquatable<Maybe<T>>, IEquatable<T>
    {
        public static readonly Maybe<T> NoValue = new Maybe<T>();
        public static readonly Maybe<T> Default = new Maybe<T>(default(T));

        private readonly T _Value;
        private readonly bool _HasValue;
        private readonly Exception _Exception;

        private readonly Func<Maybe<T>> _Computation;

        public Maybe(T value)
            : this()
        {
            _Value = value;
            _HasValue = true;
        }

        public Maybe(Func<T> computation)
            : this()
        {
            //Guard.NotNull(computation, "computation");
            _Computation = Default.Express(x => new Maybe<T>(computation()))._Computation;
        }

        public Maybe(Func<Maybe<T>> computation)
            : this(computation, true)
        {
        }

        private Maybe(Func<Maybe<T>> computation, bool wrapComputation)
            : this()
        {
            //Guard.NotNull(computation, "computation");

            _Computation = wrapComputation
                ? Default.Express(x => computation())._Computation
                : computation;
        }

        public Maybe(Exception exception)
            : this()
        {
            _Exception = exception;// Guard.NotNull(exception, "exception");
        }

        public T Value
        {
            get
            {
                if (_Computation == null)
                {
                    if (_Exception != null)
                        throw new InvalidOperationException("No value can be computed.", _Exception);

                    if (_HasValue != true)
                        throw new InvalidOperationException("No value can be computed.");

                    return _Value;
                }

                return _Computation().Value;
            }
        }

        object IMaybe.Value
        {
            get { return Value; }
        }

        public bool HasValue
        {
            get
            {
                if (_Computation == null)
                    return _HasValue;

                return _Computation().HasValue;
            }
        }
        public Exception Exception
        {
            get
            {
                if (_Computation == null)
                    return _Exception;

                return _Computation().Exception;
            }
        }

        public bool Equals(T other)
        {
            return Equals(new Maybe<T>(other));
        }

        public bool Equals(Maybe<T> other)
        {
            return Equals(other, EqualityComparer<T>.Default);
        }

        public bool Equals(Maybe<T> other, IEqualityComparer<T> comparer)
        {
            //Guard.NotNull(comparer, "comparer");

            if (Exception != null)
                return other.Exception != null && other.Exception == Exception;

            if (other.Exception != null)
                return false;

            if (!HasValue)
                return !other.HasValue;

            return other.HasValue && comparer.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
                return false;

            if (obj is Maybe<T>)
                return Equals((Maybe<T>)obj);

            return false;
        }

        public override int GetHashCode()
        {
            return GetHashCode(EqualityComparer<T>.Default);
        }

        public int GetHashCode(IEqualityComparer<T> comparer)
        {
            //Guard.NotNull(comparer, "comparer");

            if (Exception != null)
                return Exception.GetHashCode();

            if (HasValue != true)
                return -1;

            return comparer.GetHashCode(Value);
        }

        public static bool operator ==(Maybe<T> left, Maybe<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Maybe<T> left, Maybe<T> right)
        {
            return !(left == right);
        }

        public static bool operator ==(Maybe<T> left, T right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Maybe<T> left, T right)
        {
            return !(left == right);
        }

        public static bool operator ==(T left, Maybe<T> right)
        {
            return right.Equals(left);
        }

        public static bool operator !=(T left, Maybe<T> right)
        {
            return !(left == right);
        }

        public Maybe<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            //Guard.NotNull(selector, "selector");
            return Bind(x => selector(x).ToMaybe());
        }

        public Maybe<TResult> Bind<TResult>(Func<T, Maybe<TResult>> func)
        {
            //Guard.NotNull(func, "func");

            return Express(x =>
            {
                if (x.Exception != null)
                    return new Maybe<TResult>(x.Exception);

                if (x.HasValue != true)
                    return Maybe<TResult>.NoValue;

                return func(x.Value);
            });
        }

        public Maybe<TResult> Extend<TResult>(Func<Maybe<T>, TResult> func)
        {
            //Guard.NotNull(func, "func");
            return Express(x => new Maybe<TResult>(func(x)));
        }

        public Maybe<TResult> Express<TResult>(Func<Maybe<T>, Maybe<TResult>> func)
        {
            //Guard.NotNull(func, "func");

            var @this = this;
            Maybe<TResult>? memoizedResult = null;

            return new Maybe<TResult>(() =>
                memoizedResult.HasValue
                    ? memoizedResult.Value
                    : (memoizedResult = func(@this)).Value, false);
        }

        public static implicit operator Maybe<T>(T value)
        {
            return new Maybe<T>(value);
        }

        public static explicit operator T(Maybe<T> value)
        {
            return value.Value;
        }
    }

    public static class Maybe
    {
        #region Return Operator

        public static Maybe<T> Return<T>(T value)
        {
            return new Maybe<T>(value);
        }

        public static Maybe<T> Return<T>(Func<T> computation)
        {
            //Guard.NotNull(computation, "computation");
            return new Maybe<T>(computation);
        }

        #endregion

        #region NotNull Operator

        public static Maybe<T> NotNull<T>(T value) where T : class
        {
            return value.ToMaybe().NotNull();
        }

        public static Maybe<T> NotNull<T>(Func<T> computation) where T : class
        {
            return Return(computation).NotNull();
        }

        public static Maybe<T> NotNull<T>(T? value) where T : struct
        {
            return value.ToMaybe().NotNull();
        }

        public static Maybe<T> NotNull<T>(Func<T?> computation) where T : struct
        {
            return Return(computation).NotNull();
        }

        public static Maybe<T> NotNull<T>(this Maybe<T> self) where T : class
        {
            return self.NotNull(x => x);
        }

        public static Maybe<T> NotNull<T>(this Maybe<T?> self) where T : struct
        {
            return self.Where(x => x.HasValue).Select(x => x.Value);
        }

        public static Maybe<T> NotNull<T, TResult>(this Maybe<T> self, Func<T, TResult> selector) where TResult : class
        {
            //Guard.NotNull(selector, "selector");
            return self.Where(x => selector(x) != null);
        }

        public static Maybe<T> NotNull<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector) where TResult : struct
        {
            //Guard.NotNull(selector, "selector");
            return self.Where(x => selector(x).HasValue);
        }

        #endregion

        #region Using Operator

        public static Maybe<T> Using<T, TResource>(TResource resource, Func<TResource, Maybe<T>> selector) where TResource : IDisposable
        {
            //Guard.NotNull(resource, "resource");
            //Guard.NotNull(selector, "selector");

            return Using(() => resource, selector);
        }

        public static Maybe<T> Using<T, TResource>(Func<TResource> resourceFactory, Func<TResource, Maybe<T>> selector) where TResource : IDisposable
        {
            //Guard.NotNull(resourceFactory, "resourceFactory");
            //Guard.NotNull(selector, "selector");

            return Maybe<TResource>.Default
                .Using(x => resourceFactory(), selector);
        }

        public static Maybe<TResult> Using<T, TResource, TResult>(this Maybe<T> self, Func<T, TResource> resourceSelector, Func<TResource, Maybe<TResult>> selector) where TResource : IDisposable
        {
            //Guard.NotNull(resourceSelector, "resourceSelector");
            //Guard.NotNull(selector, "selector");

            return self.SelectMaybe(x =>
            {
                using (var resource = resourceSelector(x))
                    return selector(resource);
            });
        }

        #endregion

        #region Coalesce Operator

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult> selector) where TResult : class
        {
            //Guard.NotNull(selector, "selector");
            return self.Coalesce(selector, () => Maybe<TResult>.NoValue);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector) where TResult : struct
        {
            //Guard.NotNull(selector, "selector");
            return self.Coalesce(selector, () => Maybe<TResult>.NoValue);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult> selector, Func<Maybe<TResult>> valueIfNullFactory) where TResult : class
        {
            //Guard.NotNull(selector, "selector");
            //Guard.NotNull(valueIfNullFactory, "valueIfNullFactory");

            return self
                .Select(selector)
                .NotNull()
                .Or(valueIfNullFactory);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector, Func<Maybe<TResult>> valueIfNullFactory) where TResult : struct
        {
            //Guard.NotNull(selector, "selector");
            //Guard.NotNull(valueIfNullFactory, "valueIfNullFactory");

            return self
                .Select(selector)
                .NotNull()
                .Or(valueIfNullFactory);
        }

        #endregion

        #region Extract Operator

        public static T Extract<T>(this Maybe<T> self)
        {
            return self.Extract(default(T));
        }

        public static T Extract<T>(this Maybe<T> self, T @default)
        {
            return self.Extract(() => @default);
        }

        public static T Extract<T>(this Maybe<T> self, Func<T> @default)
        {
            return self.Or(@default)
                .Value;
        }

        #endregion

        #region Or Operator

        public static Maybe<T> Or<T>(this Maybe<T> self, T value)
        {
            return self.Or(() => value);
        }

        public static Maybe<T> Or<T>(this Maybe<T> self, Maybe<T> other)
        {
            return self.Or(() => other);
        }

        public static Maybe<T> Or<T>(this Maybe<T> self, Func<T> valueFactory)
        {
            //Guard.NotNull(valueFactory, "valueFactory");
            return self.Or(() => valueFactory().ToMaybe());
        }

        public static Maybe<T> Or<T>(this Maybe<T> self, Func<Maybe<T>> valueFactory)
        {
            //Guard.NotNull(valueFactory, "valueFactory");
            return self.Express(x => x.Exception == null && x.HasValue != true ? valueFactory() : x);
        }

        #endregion

        #region With Operator

        public static Maybe<T> With<T, TSelected>(this Maybe<T> self, Func<T, TSelected> selector, Action<TSelected> action)
        {
            //Guard.NotNull(selector, "selector");
            //Guard.NotNull(action, "action");

            return With(self, x => selector(x).ToMaybe(), action);
        }

        public static Maybe<T> With<T, TSelected>(this Maybe<T> self, Func<T, Maybe<TSelected>> selector, Action<TSelected> action)
        {
            //Guard.NotNull(selector, "selector");
            //Guard.NotNull(action, "action");

            return self
                .SelectMaybe(selector)
                .OnValue(action)
                .SelectMaybe(y => self)
                .Or(self);
        }

        #endregion

        #region When Operator

        public static Maybe<T> When<T>(this Maybe<T> self, T value, T newValue)
        {
            return self.When(value, x => newValue);
        }

        public static Maybe<T> When<T>(this Maybe<T> self, T value, Action<T> action)
        {
            //Guard.NotNull(action, "action");
            return self.When(x => x.Equals(value), x => { action(x); return x; });
        }

        public static Maybe<T> When<T>(this Maybe<T> self, T value, Func<T, Maybe<T>> computation)
        {
            //Guard.NotNull(computation, "computation");
            return self.When(x => x.Equals(value), computation);
        }

        public static Maybe<T> When<T>(this Maybe<T> self, Func<T, bool> predicate, T newValue)
        {
            //Guard.NotNull(predicate, "predicate");
            return self.When(predicate, x => newValue);
        }

        public static Maybe<T> When<T>(this Maybe<T> self, Func<T, bool> predicate, Action<T> action)
        {
            //Guard.NotNull(predicate, "predicate");
            //Guard.NotNull(action, "action");

            return self.When(predicate, x => { action(x); return self; });
        }

        public static Maybe<T> When<T>(this Maybe<T> self, Func<T, bool> predicate, Func<T, Maybe<T>> computation)
        {
            //Guard.NotNull(predicate, "predicate");
            //Guard.NotNull(computation, "computation");

            return self
                    .Where(predicate)
                    .SelectMaybe(computation)
                    .Or(self);
        }

        #endregion

        #region SuppressException Operator

        public static Maybe<T> SuppressException<T>(this Maybe<T> self, T value)
        {
            return self.SuppressException(ex => value);
        }

        public static Maybe<T> SuppressException<T>(this Maybe<T> self, Func<Exception, T> valueFactory)
        {
            return self.SuppressException(ex => valueFactory(ex).ToMaybe());
        }

        public static Maybe<T> SuppressException<T>(this Maybe<T> self, Func<Exception, Maybe<T>> handler)
        {
            //Guard.NotNull(handler, "handler");
            return self.Express(x => x.Exception != null ? handler(x.Exception) : x);
        }

        #endregion

        #region Join Operator

        public static Maybe<Tuple<T, U>> Join<T, U>(this Maybe<T> self, Maybe<U> other)
        {
            return self.Join(other, Tuple.Create);
        }

        public static Maybe<TResult> Join<T, U, TResult>(this Maybe<T> self, Maybe<U> other, Func<T, U, TResult> selector)
        {
            //Guard.NotNull(selector, "selector");
            return self.SelectMaybe(t => other.Select(r => selector(t, r)));
        }

        #endregion

        #region ThrowOnNoValue Operator

        public static Maybe<T> ThrowOnNoValue<T>(this Maybe<T> self, Exception exception)
        {
            //Guard.NotNull(exception, "exception");
            return self.ThrowOnNoValue(() => exception);
        }

        public static Maybe<T> ThrowOnNoValue<T>(this Maybe<T> self, Func<Exception> exceptionFactory)
        {
            //Guard.NotNull(exceptionFactory, "exceptionFactory");
            return self
                .ThrowOn(x => x.Exception == null && x.HasValue != true, x => exceptionFactory());
        }

        #endregion

        #region ThrowOnException Operator

        public static Maybe<T> ThrowOnException<T>(this Maybe<T> self)
        {
            return self.ThrowOnException(typeof(Exception));
        }

        public static Maybe<T> ThrowOnException<T>(this Maybe<T> self, Type exceptionType)
        {
            //Guard.NotNull(exceptionType, "exceptionType");
            return self.ThrowOnException(x => exceptionType.IsAssignableFrom(x.GetType()));
        }

        public static Maybe<T> ThrowOnException<T>(this Maybe<T> self, Func<Exception, bool> predicate)
        {
            //Guard.NotNull(predicate, "predicate");
            return self.ThrowOn(x => x.Exception != null, x => x.Exception);
        }

        #endregion

        #region ThrowOn Operator

        public static Maybe<T> ThrowOn<T>(this Maybe<T> self, T value, Exception exception)
        {
            //Guard.NotNull(exception, "exception");
            return self.ThrowOn(value, x => exception);
        }

        public static Maybe<T> ThrowOn<T>(this Maybe<T> self, T value, Func<Maybe<T>, Exception> exceptionFactory)
        {
            //Guard.NotNull(exceptionFactory, "exceptionFactory");
            return self.ThrowOn(x => x.Equals(value), exceptionFactory);
        }

        public static Maybe<T> ThrowOn<T>(this Maybe<T> self, Func<Maybe<T>, bool> predicate, Exception exception)
        {
            //Guard.NotNull(exception, "exception");
            //Guard.NotNull(predicate, "predicate");
            return self.ThrowOn(predicate, x => exception);
        }

        public static Maybe<T> ThrowOn<T>(this Maybe<T> self, Func<Maybe<T>, bool> predicate, Func<Maybe<T>, Exception> exceptionFactory)
        {
            //Guard.NotNull(exceptionFactory, "exceptionFactory");
            //Guard.NotNull(predicate, "predicate");

            return self.Express(x =>
            {
                if (predicate(x))
                    throw exceptionFactory(x);

                return x;
            });
        }

        #endregion

        #region SelectMany Operator

        // This is an alias of Bind, and exists only to satisfy C#'s LINQ comprehension syntax.
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Maybe<TResult> SelectMany<T, TResult>(this Maybe<T> self, Func<T, Maybe<TResult>> selector)
        {
            //Guard.NotNull(selector, "selector");
            return self.Bind(selector);
        }

        // This operator is implemented only to satisfy C#'s LINQ comprehension syntax. 
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Maybe<TResult> SelectMany<T, TIntermediate, TResult>(this Maybe<T> self, Func<T, Maybe<TIntermediate>> selector, Func<T, TIntermediate, TResult> combiner)
        {
            //Guard.NotNull(selector, "selector");
            //Guard.NotNull(combiner, "combiner");
            return self.Bind(x => selector(x).Bind(y => combiner(x, y).ToMaybe()));
        }

        #endregion

        // This is an alias of Bind and SelectMany.  Since SelectMany doesn't make sense (because there is at most one value),
        // the name SelectMaybe communicates better than Bind or SelectMany the semantics of the function.
        public static Maybe<TResult> SelectMaybe<T, TResult>(this Maybe<T> self, Func<T, Maybe<TResult>> selector)
        {
            //Guard.NotNull(selector, "selector");
            return self.Bind(selector);
        }

        public static Maybe<T> OnValue<T>(this Maybe<T> self, Action<T> action)
        {
            //Guard.NotNull(action, "action");
            return self.Select(x =>
            {
                action(x);
                return x;
            });
        }

        public static Maybe<T> OnNoValue<T>(this Maybe<T> self, Action action)
        {
            //Guard.NotNull(action, "action");

            return self.Express(x =>
            {
                if (x.Exception == null && x.HasValue != true)
                    action();

                return x;
            });
        }

        public static Maybe<T> OnException<T>(this Maybe<T> self, Action<Exception> handler)
        {
            //Guard.NotNull(handler, "handler");
            return self.Express(x =>
            {
                if (x.Exception != null)
                    handler(x.Exception);

                return x;
            });
        }

        public static Maybe<T> CatchExceptions<T>(this Maybe<T> self)
        {
            return self.Express(x =>
            {
                try
                {
                    return x.Run();
                }
                catch (Exception ex)
                {
                    return new Maybe<T>(ex);
                }
            });
        }

        public static Maybe<T> Where<T>(this Maybe<T> self, Func<T, bool> predicate)
        {
            //Guard.NotNull(predicate, "predicate");
            return self.SelectMaybe(x => predicate(x) ? x : Maybe<T>.NoValue);
        }

        public static Maybe<T> Unless<T>(this Maybe<T> self, Func<T, bool> predicate)
        {
            //Guard.NotNull(predicate, "predicate");
            return self.Where(x => !predicate(x));
        }

        public static Maybe<T> Assign<T>(this Maybe<T> self, ref T target)
        {
            if (self.HasValue)
                target = self.Value;

            return self;
        }

        public static Maybe<T> Run<T>(this Maybe<T> self, Action<T> action = null)
        {
            return self
                .When(x => action != null, x => self.OnValue(action))
                .HasValue ? self : self;
        }

        public static Maybe<T> RunAsync<T>(this Maybe<T> self, Action<T> action = null, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = TaskCreationOptions.None, TaskScheduler taskScheduler = default(TaskScheduler))
        {
            var task = Task.Factory.StartNew(() => self.Run(action), cancellationToken, taskCreationOptions,
                                             taskScheduler ?? TaskScheduler.Current);

            return self.Express(x =>
            {
                task.Wait(cancellationToken);
                return task.IsCanceled ? Maybe<T>.NoValue : task.Result;
            });
        }

        public static Maybe<T> Synchronize<T>(this Maybe<T> self)
        {
            return SynchronizeWith(self, new object());
        }

        public static Maybe<T> SynchronizeWith<T>(this Maybe<T> self, object lockObject)
        {
            //Guard.NotNull(lockObject, "lockObject");

            Func<Maybe<T>> synchronizedComputation = () =>
            {
                lock (lockObject)
                {
                    return self.Run();
                }
            };

            return new Maybe<T>(synchronizedComputation);
        }

        public static Maybe<TResult> Cast<TResult>(this IMaybe self)
        {
            //Guard.NotNull(self, "self");

            return new Maybe<TResult>(() =>
            {
                if (self.Exception != null)
                    return new Maybe<TResult>(self.Exception);

                if (self.HasValue != true)
                    return Maybe<TResult>.NoValue;

                return (TResult)self.Value;
            });
        }

        public static Maybe<TResult> OfType<TResult>(this IMaybe self)
        {
            //Guard.NotNull(self, "self");

            return new Maybe<TResult>(() =>
            {
                if (self.Exception != null)
                    return new Maybe<TResult>(self.Exception);

                if (self.HasValue != true)
                    return Maybe<TResult>.NoValue;

                if (self.Value is TResult)
                    return (TResult)self.Value;

                return Maybe<TResult>.NoValue;
            });
        }

        public static T? ToNullable<T>(this Maybe<T> self) where T : struct
        {
            return self.Select(x => (T?)x)
                .Or((T?)null)
                .Extract();
        }

        // Conventionally, in LINQ, the monadic "return" operator is written "To...,"
        // as in "ToList," "ToArray," etc. These are synonyms for Return.
        public static Maybe<T> ToMaybe<T>(this T value)
        {
            return Return(value);
        }
    }
}
