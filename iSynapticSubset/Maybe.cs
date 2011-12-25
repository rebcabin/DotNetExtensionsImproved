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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Experimental.DotNetExtensions.iSynaptic
{
    // Implementation of the Maybe monad. http://en.wikipedia.org/wiki/Monad_%28functional_programming%29#Maybe_monad
    // Thanks to Brian Beckman for his suggestions and assistance.
    // Don't Fear the Monad! http://channel9.msdn.com/shows/Going+Deep/Brian-Beckman-Dont-fear-the-Monads/
    public struct Maybe<T> : IMaybe<T>, IEquatable<Maybe<T>>, IEquatable<T>
    {
        public static readonly Maybe<T> NoValue;

        private readonly T _Value;
        private readonly bool _HasValue;

        private readonly Func<Maybe<T>> _Computation;

        public Maybe(T value)
            : this()
        {
            _Value = value;
            _HasValue = value != null;
        }

        public Maybe(Func<Maybe<T>> computation)
            : this()
        {
            var cachedComputation = Guard.NotNull(computation, "computation");
            var memoizedResult = default(Maybe<T>);
            var resultComputed = false;

            _Computation = () =>
            {
                if (resultComputed)
                    return memoizedResult;

                memoizedResult = cachedComputation();
                resultComputed = true;
                cachedComputation = null;

                return memoizedResult;
            };
        }

        public T Value
        {
            get
            {
                if (_Computation != null)
                    return _Computation().Value;

                if (typeof(T) == typeof(Unit) || _HasValue)
                    return _Value;

                throw new InvalidOperationException("No value can be computed.");
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
                if (_Computation != null)
                    return _Computation().HasValue;

                return typeof(T) == typeof(Unit) ||
                       _HasValue;
            }
        }

        public bool Equals(T other)
        {
            return Equals(other, EqualityComparer<T>.Default);
        }

        public bool Equals(T other, IEqualityComparer<T> comparer)
        {
            return Equals(new Maybe<T>(other), comparer);
        }

        public bool Equals(Maybe<T> other)
        {
            return Equals(other, EqualityComparer<T>.Default);
        }

        public bool Equals(Maybe<T> other, IEqualityComparer<T> comparer)
        {
            Guard.NotNull(comparer, "comparer");

            if (!HasValue)
                return !other.HasValue;

            return other.HasValue && comparer.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is Maybe<T>)
                return Equals((Maybe<T>)obj);

            if (obj is T)
                return Equals(new Maybe<T>((T)obj));

            return false;
        }

        public override int GetHashCode()
        {
            return GetHashCode(EqualityComparer<T>.Default);
        }

        public int GetHashCode(IEqualityComparer<T> comparer)
        {
            Guard.NotNull(comparer, "comparer");

            return HasValue
                ? comparer.GetHashCode(Value)
                : 0;
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

        public static explicit operator T(Maybe<T> value)
        {
            return value.Value;
        }

        public static implicit operator Maybe<T>(Maybe<Unit> value)
        {
            return new Maybe<T>();
        }
    }

    public static class Maybe
    {
        #region Defer Operator

        public static Maybe<T> Defer<T>(Func<T> computation)
        {
            Guard.NotNull(computation, "computation");

            return new Maybe<T>(() => new Maybe<T>(computation()));
        }

        public static Maybe<T> Defer<T>(Func<T?> computation) where T : struct
        {
            Guard.NotNull(computation, "computation");

            return new Maybe<T>(() => Return(computation()));
        }

        public static Maybe<T> Defer<T>(Func<Maybe<T>> computation)
        {
            return new Maybe<T>(computation);
        }

        #endregion

        #region If Operator

        public static Maybe<T> If<T>(bool predicate, Maybe<T> thenValue)
        {
            return predicate ? thenValue : NoValue;
        }

        public static Maybe<T> If<T>(bool predicate, Maybe<T> thenValue, Maybe<T> elseValue)
        {
            return predicate ? thenValue : elseValue;
        }

        public static Maybe<T> If<T>(Func<bool> predicate, Maybe<T> thenValue)
        {
            Guard.NotNull(predicate, "predicate");
            return new Maybe<T>(() => predicate() ? thenValue : NoValue);
        }

        public static Maybe<T> If<T>(Func<bool> predicate, Maybe<T> thenValue, Maybe<T> elseValue)
        {
            Guard.NotNull(predicate, "predicate");
            return new Maybe<T>(() => predicate() ? thenValue : elseValue);
        }

        #endregion

        #region Using Operator

        public static Maybe<T> Using<T, TResource>(Func<TResource> resourceFactory, Func<TResource, Maybe<T>> selector) where TResource : IDisposable
        {
            Guard.NotNull(resourceFactory, "resourceFactory");
            Guard.NotNull(selector, "selector");

            return new Maybe<T>(() =>
            {
                using (var resource = resourceFactory())
                    return selector(resource);
            });
        }

        public static Maybe<TResult> Using<T, TResource, TResult>(this Maybe<T> @this, Func<T, TResource> resourceSelector, Func<TResource, Maybe<TResult>> selector) where TResource : IDisposable
        {
            Guard.NotNull(resourceSelector, "resourceSelector");
            Guard.NotNull(selector, "selector");

            return @this.SelectMaybe(x =>
            {
                using (var resource = resourceSelector(x))
                    return selector(resource);
            });
        }

        #endregion

        #region ValueOrDefault Operator

        public static T ValueOrDefault<T>(this Maybe<T> @this, Func<T> @default)
        {
            return @this.HasValue
                ? @this.Value
                : @default();
        }

        public static T ValueOrDefault<T>(this Maybe<T> @this, T @default)
        {
            return @this.HasValue
                ? @this.Value
                : @default;
        }

        public static T ValueOrDefault<T>(this Maybe<T> @this)
        {
            return @this.ValueOrDefault(default(T));
        }

        #endregion

        #region Or Operator

        public static Maybe<T> Or<T>(this Maybe<T> @this, T value)
        {
            var self = @this;
            return new Maybe<T>(() => self.HasValue ? self : new Maybe<T>(value));
        }

        public static Maybe<T> Or<T>(this Maybe<T> @this, Func<T> valueFactory)
        {
            Guard.NotNull(valueFactory, "valueFactory");

            var self = @this;
            return new Maybe<T>(() => self.HasValue ? self : new Maybe<T>(valueFactory()));
        }

        public static Maybe<T> Or<T>(this Maybe<T> @this, Func<Maybe<T>> valueFactory)
        {
            Guard.NotNull(valueFactory, "valueFactory");

            var self = @this;
            return new Maybe<T>(() => self.HasValue ? self : valueFactory());
        }

        public static Maybe<T> Or<T>(this Maybe<T> @this, Maybe<T> other)
        {
            var self = @this;
            return new Maybe<T>(() => self.HasValue ? self : other);
        }

        #endregion

        #region With Operator

        public static Maybe<T> With<T, TSelected>(this Maybe<T> @this, Func<T, TSelected> selector, Action<TSelected> action)
        {
            Guard.NotNull(selector, "selector");
            Guard.NotNull(action, "action");

            var self = @this;

            return new Maybe<T>(() =>
            {
                if (self.HasValue)
                    action(selector(self.Value));

                return self;
            });
        }

        public static Maybe<T> With<T, TSelected>(this Maybe<T> @this, Func<T, Maybe<TSelected>> selector, Action<TSelected> action)
        {
            Guard.NotNull(selector, "selector");
            Guard.NotNull(action, "action");

            var self = @this;

            return new Maybe<T>(() =>
            {
                if (self.HasValue)
                {
                    var selected = selector(self.Value);
                    if (selected.HasValue)
                        action(selected.Value);
                }

                return self;
            });
        }

        #endregion

        #region When Operator

        public static Maybe<T> When<T>(this Maybe<T> @this, T value, Action<T> action)
        {
            Guard.NotNull(action, "action");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && EqualityComparer<T>.Default.Equals(self.Value, value))
                    action(self.Value);

                return self;
            });
        }

        public static Maybe<T> When<T>(this Maybe<T> @this, T value, Maybe<T> newValue)
        {
            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && EqualityComparer<T>.Default.Equals(self.Value, value))
                    return newValue;

                return self;
            });
        }

        public static Maybe<T> When<T>(this Maybe<T> @this, Func<T, bool> predicate, Action<T> action)
        {
            Guard.NotNull(predicate, "predicate");
            Guard.NotNull(action, "action");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && predicate(self.Value))
                    action(self.Value);

                return self;
            });
        }

        public static Maybe<T> When<T>(this Maybe<T> @this, Func<T, bool> predicate, Maybe<T> newValue)
        {
            Guard.NotNull(predicate, "predicate");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && predicate(self.Value))
                    return newValue;

                return self;
            });
        }

        public static Maybe<T> When<T>(this Maybe<T> @this, Func<T, bool> predicate, Func<T, Maybe<T>> selector)
        {
            Guard.NotNull(predicate, "predicate");
            Guard.NotNull(selector, "selector");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && predicate(self.Value))
                    return selector(self.Value);

                return self;
            });
        }

        #endregion

        #region Suppress Operator

        public static Maybe<T> Suppress<T>(this Maybe<T> @this, Action<Exception> action = null)
        {
            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch (Exception ex)
                {
                    if (action != null)
                        action(ex);

                    return NoValue;
                }
            });
        }

        public static Maybe<T> Suppress<T>(this Maybe<T> @this, Func<Exception, bool> predicate)
        {
            Guard.NotNull(predicate, "predicate");

            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch (Exception ex)
                {
                    if (predicate(ex))
                        return NoValue;

                    throw;
                }
            });
        }

        public static Maybe<T> Suppress<T>(this Maybe<T> @this, T value)
        {
            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch
                {
                    return new Maybe<T>(value);
                }
            });
        }

        public static Maybe<T> Suppress<T>(this Maybe<T> @this, Func<Exception, bool> predicate, T value)
        {
            Guard.NotNull(predicate, "predicate");

            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch (Exception ex)
                {
                    if (predicate(ex))
                        return new Maybe<T>(value);

                    throw;
                }
            });
        }

        public static Maybe<T> Suppress<T>(this Maybe<T> @this, Func<Exception, bool> predicate, Func<Exception, Maybe<T>> selector)
        {
            Guard.NotNull(predicate, "predicate");
            Guard.NotNull(selector, "selector");

            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch (Exception ex)
                {
                    if (predicate(ex))
                        return selector(ex);

                    throw;
                }
            });
        }


        #endregion

        #region Join Operator

        public static Maybe<Tuple<T, U>> Join<T, U>(this Maybe<T> @this, Maybe<U> other)
        {
            var self = @this;
            return new Maybe<Tuple<T, U>>(() => self.HasValue && other.HasValue
                ? new Maybe<Tuple<T, U>>(Tuple.Create(self.Value, other.Value))
                : NoValue);
        }

        public static Maybe<TResult> Join<T, U, TResult>(this Maybe<T> @this, Maybe<U> other, Func<T, U, TResult> selector)
        {
            Guard.NotNull(selector, "selector");

            var self = @this;
            return new Maybe<TResult>(() => self.HasValue && other.HasValue
                ? new Maybe<TResult>(selector(self.Value, other.Value))
                : NoValue);
        }

        public static Maybe<TResult> Join<T, U, TResult>(this Maybe<T> @this, Maybe<U> other, Func<T, U, TResult?> selector) where TResult : struct
        {
            Guard.NotNull(selector, "selector");

            var self = @this;
            return new Maybe<TResult>(() => self.HasValue && other.HasValue
                ? Return(selector(self.Value, other.Value))
                : NoValue);
        }

        public static Maybe<TResult> Join<T, U, TResult>(this Maybe<T> @this, Maybe<U> other, Func<T, U, Maybe<TResult>> selector)
        {
            Guard.NotNull(selector, "selector");

            var self = @this;
            return new Maybe<TResult>(() => self.HasValue && other.HasValue
                ? selector(self.Value, other.Value)
                : NoValue);
        }

        #endregion

        #region ThrowOnNoValue Operator

        public static Maybe<T> ThrowOnNoValue<T>(this Maybe<T> @this, Exception exception)
        {
            Guard.NotNull(exception, "exception");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue != true)
                    throw exception;

                return self;
            });
        }

        public static Maybe<T> ThrowOnNoValue<T>(this Maybe<T> @this, Func<Exception> exceptionFactory)
        {
            Guard.NotNull(exceptionFactory, "exceptionFactory");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue != true)
                    throw exceptionFactory();

                return self;
            });
        }

        #endregion

        #region ThrowOn Operator

        public static Maybe<T> ThrowOn<T>(this Maybe<T> @this, T value, Exception exception)
        {
            Guard.NotNull(exception, "exception");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue && EqualityComparer<T>.Default.Equals(self.Value, value))
                    throw exception;

                return self;
            });
        }

        public static Maybe<T> ThrowOn<T>(this Maybe<T> @this, Func<Maybe<T>, Exception> exceptionSelector)
        {
            Guard.NotNull(exceptionSelector, "exceptionSelector");

            var self = @this;
            return new Maybe<T>(() =>
            {
                var ex = exceptionSelector(self);
                if (ex != null)
                    throw ex;

                return self;
            });
        }

        #endregion

        #region ToEnumerable Operator

        public static IEnumerable<T> ToEnumerable<T>(this Maybe<T> @this)
        {
            if (@this.HasValue)
                yield return @this.Value;
        }

        public static IEnumerable<T> ToEnumerable<T>(params Maybe<T>[] values)
        {
            Guard.NotNull(values, "values");
            return values
                .Where(x => x.HasValue)
                .Select(x => x.Value);
        }

        public static IEnumerable<T> ToEnumerable<T>(this Maybe<T> @this, IEnumerable<Maybe<T>> others)
        {
            Guard.NotNull(others, "others");

            return new[] { @this }.Concat(others)
                .Where(x => x.HasValue)
                .Select(x => x.Value);
        }

        #endregion

        #region Squash Operator

        public static Maybe<T> Squash<T>(this IMaybe<IMaybe<T>> @this)
        {
            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self == null)
                    return NoValue;

                return self.HasValue
                    ? self.Value.Cast<T>()
                    : NoValue;
            });
        }

        public static IEnumerable<T> Squash<T>(this IMaybe<IEnumerable<T>> @this)
        {
            if (@this == null || @this.HasValue != true || @this.Value == null)
                yield break;

            foreach (var item in @this.Value)
                yield return item;
        }

        public static Maybe<T> Squash<T>(this Maybe<Maybe<T>> @this)
        {
            var self = @this;

            return new Maybe<T>(() => self.HasValue
                ? self.Value
                : NoValue);
        }

        public static IEnumerable<T> Squash<T>(this Maybe<IEnumerable<T>> @this)
        {
            foreach (var item in @this.ValueOrDefault(Enumerable.Empty<T>()))
                yield return item;
        }

        #endregion

        public static Maybe<Unit> NoValue
        {
            get { return new Maybe<Unit>(); }
        }

        public static Maybe<T> Return<T>(T value)
        {
            return new Maybe<T>(value);
        }

        public static Maybe<T> Return<T>(T? value) where T : struct
        {
            return value.HasValue
                ? new Maybe<T>(value.Value)
                : NoValue;
        }

        public static Maybe<TResult> Bind<T, TResult>(this Maybe<T> @this, Func<T, Maybe<TResult>> selector)
        {
            return SelectMaybe(@this, selector);
        }

        public static Maybe<TResult> Let<T, TResult>(this Maybe<T> @this, Func<Maybe<T>, Maybe<TResult>> func)
        {
            Guard.NotNull(func, "func");

            var self = @this;
            return new Maybe<TResult>(() => func(self));
        }

        public static Maybe<TResult> Select<T, TResult>(this Maybe<T> @this, Func<T, TResult> selector)
        {
            Guard.NotNull(selector, "selector");

            var self = @this;

            return new Maybe<TResult>(() => self.HasValue ? new Maybe<TResult>(selector(self.Value)) : NoValue);
        }

        public static Maybe<TResult> Select<T, TResult>(this Maybe<T> @this, Func<T, TResult?> selector) where TResult : struct
        {
            Guard.NotNull(selector, "selector");

            var self = @this;

            return new Maybe<TResult>(() => self.HasValue ? Return(selector(self.Value)) : NoValue);
        }

        public static Maybe<TResult> TrySelect<TResult>(TrySelector<TResult> selector)
        {
            Guard.NotNull(selector, "selector");

            return new Maybe<TResult>(() =>
            {
                TResult result = default(TResult);

                return selector(out result)
                    ? new Maybe<TResult>(result)
                    : NoValue;
            });
        }

        public static Maybe<TResult> TrySelect<T, TResult>(this Maybe<T> @this, TrySelector<T, TResult> selector)
        {
            Guard.NotNull(selector, "selector");

            var self = @this;

            return new Maybe<TResult>(() =>
            {
                if (!self.HasValue)
                    return NoValue;

                TResult result = default(TResult);

                return selector(self.Value, out result)
                    ? new Maybe<TResult>(result)
                    : NoValue;
            });
        }

        // This functionally is the same as Bind and SelectMany.  Since SelectMany doesn't make sense (because there is at most one value),
        // the name SelectMaybe communicates better than Bind or SelectMany the semantics of the function.
        public static Maybe<TResult> SelectMaybe<T, TResult>(this Maybe<T> @this, Func<T, Maybe<TResult>> selector)
        {
            Guard.NotNull(selector, "selector");

            var self = @this;

            return new Maybe<TResult>(() => self.HasValue ? selector(self.Value) : NoValue);
        }

        public static Maybe<Unit> Throw(Exception exception)
        {
            Guard.NotNull(exception, "exception");
            return new Maybe<Unit>(() => { throw exception; });
        }

        public static Maybe<T> Throw<T>(Exception exception)
        {
            Guard.NotNull(exception, "exception");
            return new Maybe<T>(() => { throw exception; });
        }

        public static Maybe<T> Finally<T>(this Maybe<T> @this, Action finallyAction)
        {
            Guard.NotNull(finallyAction, "finallyAction");

            var self = @this;
            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue ? self : self;
                }
                finally
                {
                    finallyAction();
                }
            });
        }

        public static Maybe<T> OnValue<T>(this Maybe<T> @this, Action<T> action)
        {
            Guard.NotNull(action, "action");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue)
                    action(self.Value);

                return self;
            });
        }

        public static Maybe<T> OnNoValue<T>(this Maybe<T> @this, Action action)
        {
            Guard.NotNull(action, "action");

            var self = @this;
            return new Maybe<T>(() =>
            {
                if (self.HasValue != true)
                    action();

                return self;
            });
        }

        public static Maybe<T> OnException<T>(this Maybe<T> @this, Action<Exception> handler)
        {
            Guard.NotNull(handler, "handler");
            var self = @this;

            return new Maybe<T>(() =>
            {
                try
                {
                    return self.HasValue
                        ? self
                        : self;
                }
                catch (Exception ex)
                {
                    handler(ex);
                    throw;
                }
            });
        }

        public static Maybe<T> Where<T>(this Maybe<T> @this, Func<T, bool> predicate)
        {
            Guard.NotNull(predicate, "predicate");
            var self = @this;

            return new Maybe<T>(() =>
            {
                if (self.HasValue)
                {
                    var value = self.Value;

                    if (predicate(value))
                        return self;
                }

                return NoValue;
            });
        }

        public static Maybe<T> Unless<T>(this Maybe<T> @this, Func<T, bool> predicate)
        {
            Guard.NotNull(predicate, "predicate");
            var self = @this;

            return new Maybe<T>(() =>
            {
                if (self.HasValue)
                {
                    var value = self.Value;

                    if (!predicate(value))
                        return self;
                }

                return NoValue;
            });
        }

        public static Maybe<T> Assign<T>(this Maybe<T> @this, ref T target)
        {
            if (@this.HasValue)
                target = @this.Value;

            return @this;
        }

        public static Maybe<T> Run<T>(this Maybe<T> @this, Action<T> action = null)
        {
            // Getting HasValue forces evaluation
            if (@this.HasValue && action != null)
                action(@this.Value);

            return @this;
        }

        public static Maybe<T> RunAsync<T>(this Maybe<T> @this, Action<T> action = null, CancellationToken cancellationToken = default(CancellationToken), TaskCreationOptions taskCreationOptions = TaskCreationOptions.None, TaskScheduler taskScheduler = default(TaskScheduler))
        {
            var self = @this;
            var task = Task.Factory.StartNew(() => self.Run(action), cancellationToken, taskCreationOptions, taskScheduler ?? TaskScheduler.Current);

            return new Maybe<T>(() =>
            {
                task.Wait(cancellationToken);
                return task.IsCanceled ? NoValue : self;
            });
        }

        public static Maybe<T> Synchronize<T>(this Maybe<T> @this)
        {
            return Synchronize(@this, new object());
        }

        public static Maybe<T> Synchronize<T>(this Maybe<T> @this, object gate)
        {
            Guard.NotNull(gate, "gate");

            var self = @this;
            return new Maybe<T>(() =>
            {
                lock (gate)
                {
                    return self.Run();
                }
            });
        }

        public static Maybe<TResult> Cast<TResult>(this IMaybe @this)
        {
            if (@this == null)
                return NoValue;

            if (@this is Maybe<TResult>)
                return (Maybe<TResult>)@this;

            return new Maybe<TResult>(() => @this.HasValue
                ? new Maybe<TResult>((TResult)@this.Value)
                : NoValue);
        }

        public static Maybe<TResult> OfType<TResult>(this IMaybe @this)
        {
            if (@this == null)
                return NoValue;

            if (@this is Maybe<TResult>)
                return (Maybe<TResult>)@this;

            return new Maybe<TResult>(() =>
            {
                if (@this.HasValue != true)
                    return NoValue;

                return @this.Value is TResult
                    ? new Maybe<TResult>((TResult)@this.Value)
                    : NoValue;
            });
        }

        public static T? ToNullable<T>(this Maybe<T> @this) where T : struct
        {
            return @this.HasValue ? (T?)@this.Value : null;
        }

        public static T? ToNullable<T>(this Maybe<T?> @this) where T : struct
        {
            return @this.HasValue ? @this.Value : null;
        }

        // Conventionally, in LINQ, the monadic "return" operator is written "To...,"
        // as in "ToList," "ToArray," etc. These are synonyms for Return.
        public static Maybe<T> ToMaybe<T>(this T @this)
        {
            return new Maybe<T>(@this);
        }

        public static Maybe<T> ToMaybe<T>(this T? @this) where T : struct
        {
            return @this.HasValue
                ? new Maybe<T>(@this.Value)
                : NoValue;
        }

        public static Maybe<T> ToMaybe<T>(this object @this)
        {
            if (@this is T)
                return new Maybe<T>((T)@this);

            if (@this == null && typeof(T).IsValueType != true)
                return new Maybe<T>(default(T));

            return NoValue;
        }

        public static Maybe<T> AsMaybe<T>(this IMaybe<T> value)
        {
            return value.OfType<T>();
        }

        public static Maybe<object> AsMaybe(this IMaybe value)
        {
            return value.OfType<object>();
        }
    }
}
