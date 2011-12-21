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

namespace Experimental.DotNetExtensions.iSynaptic
{
    public struct Maybe<T> : IMaybe<T>, IEquatable<Maybe<T>>, IEquatable<T>
    {
        private struct MaybeResult
        {
            public T Value;
            public bool HasValue;
            public Exception Exception;
        }

        public static readonly Maybe<T> NoValue = new Maybe<T>();
        public static readonly Maybe<T> Default = new Maybe<T>(default(T));

        private readonly MaybeResult? _Value;
        private readonly Func<MaybeResult> _Computation;

        public Maybe(T value)
            : this()
        {
            _Value = new MaybeResult { Value = value, HasValue = true };
        }

        public Maybe(Func<T> computation)
            : this()
        {
            //Contract.Requires(null != computation, "computation");
            _Computation = Default.Bind(x => computation().ToMaybe())._Computation;
        }

        public Maybe(Exception exception)
            : this()
        {
            //Contract.Requires(null != exception, "exception");
            _Value = new MaybeResult { Exception = exception };
        }

        private Maybe(Func<MaybeResult> computation)
            : this()
        {
            //Contract.Requires(null != computation, "computation");
            _Computation = computation;
        }

        private static MaybeResult ComputeResult(Maybe<T> value)
        {
            if (value._Value.HasValue)
                return value._Value.Value;

            if (value._Computation != null)
                return value._Computation();

            return default(MaybeResult);
        }

        public T Value
        {
            get
            {
                var result = ComputeResult(this);

                if (result.Exception != null)
                {
                    // [bbeckman: avoiding octopus-copy of entire ExceptionExtensions,
                    // Cloneable, ILGenerator, etc.]
                    //result.Exception.ThrowAsInnerExceptionIfNeeded();
                    var newEx = new InvalidOperationException(
                        "Inner exception recorded",
                        result.Exception);
                }

                if (result.HasValue != true)
                    throw new InvalidOperationException("No value can be provided.");

                return result.Value;
            }
        }

        object IMaybe.Value
        {
            get { return Value; }
        }

        public bool HasValue { get { return ComputeResult(this).HasValue; } }
        public Exception Exception { get { return ComputeResult(this).Exception; } }

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
            Contract.Requires(null != comparer, "comparer");

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
            if (Exception != null)
                return Exception.GetHashCode();

            if (HasValue != true)
                return -1;

            if (Value == null)
                return 0;

            return Value.GetHashCode();
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

        // [bbeckman] Prefer to use ToMaybe at calls that need this signature.
        //public Maybe<U> Bind<U>(Func<T, U> func)
        //{
        //    Contract.Requires(null != func, "func");
        //    return Bind(x => new Maybe<U>(func(x)));
        //}

        public Maybe<U> Bind<U>(Func<T, Maybe<U>> func)
        {
            Contract.Requires(null != func, "func");

            return Extend(x =>
            {
                if (x.Exception != null)
                    return new Maybe<U>(x.Exception);

                if (x.HasValue != true)
                    return Maybe<U>.NoValue;

                return func(x.Value);
            });
        }

        public Maybe<U> Extend<U>(Func<Maybe<T>, U> func)
        {
            Contract.Requires(null != func, "func");
            return Extend(x => new Maybe<U>(func(x)));
        }

        // [bbeckman: this one is creepy.]
        public Maybe<U> Extend<U>(Func<Maybe<T>, Maybe<U>> func)
        {
            Contract.Requires(null != func, "func");

            var self = this;

            Func<Maybe<U>.MaybeResult> boundComputation =
                () => Maybe<U>.ComputeResult(func(self));

            return new Maybe<U>(boundComputation.Memoize());
        }

        // [bbeckman: this makes the code more difficult to type-check visually]
        //public static implicit operator Maybe<T>(T value)
        //{
        //    return new Maybe<T>(value);
        //}

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
            Contract.Requires(null != computation, "computation");
            return new Maybe<T>(computation);
        }

        #endregion

        #region NotNull Operator

        public static Maybe<T> NotNull<T>(T value) where T : class
        {
            return Return(value).NotNull();
        }

        public static Maybe<T> NotNull<T>(Func<T> computation) where T : class
        {
            return Return(computation).NotNull();
        }

        public static Maybe<T> NotNull<T>(T? value) where T : struct
        {
            return Return(value).NotNull();
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
            Contract.Requires(null != selector, "selector");
            return self.Where(x => selector(x) != null);
        }

        public static Maybe<T> NotNull<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector) where TResult : struct
        {
            Contract.Requires(null != selector, "selector");
            return self.Where(x => selector(x).HasValue);
        }

        #endregion

        #region Using Operator

        public static Maybe<T> Using<T, TResource>(TResource resource, Func<TResource, Maybe<T>> selector) where TResource : IDisposable
        {
            Contract.Requires(null != resource, "resource");
            Contract.Requires(null != selector, "selector");

            return Using(() => resource, selector);
        }

        public static Maybe<T> Using<T, TResource>(Func<TResource> resourceFactory, Func<TResource, Maybe<T>> selector) where TResource : IDisposable
        {
            Contract.Requires(null != resourceFactory, "resourceFactory");
            Contract.Requires(null != selector, "selector");

            return Maybe<TResource>.Default
                .Using(x => resourceFactory(), selector);
        }

        public static Maybe<U> Using<T, TResource, U>(
            this Maybe<T> self, 
            Func<T, TResource> t2resource, 
            Func<TResource, Maybe<U>> resource2u) 
                where TResource : IDisposable
        {
            Contract.Requires(null != t2resource, "resourceSelector");
            Contract.Requires(null != resource2u, "selector");

            return self.Bind(x =>
            {
                using (var resource = t2resource(x))
                    return resource2u(resource);
            });
        }

        #endregion

        #region Select Operator

        public static Maybe<U> Select<T, U>(
            this Maybe<T> mt, 
            Func<T, U> t2u)
        {
            Contract.Requires(null != t2u, "t2u");
            return mt.Bind(t => t2u(t).ToMaybe());
        }

        // [bbeckman: this one just looks like a creepy overload of "Bind"]
        //public static Maybe<U> Select<T, U>(
        //    this Maybe<T> self, 
        //    Func<T, Maybe<U>> selector)
        //{
        //    Contract.Requires(null != selector, "selector");
        //    return self.Bind(selector);
        //}

        #endregion

        #region SelectMany Operator

        // This operator is implemented only to satisfy C#'s LINQ comprehension syntax.  
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Maybe<U> SelectMany<T, U>(
            this Maybe<T> mt,
            Func<T, Maybe<U>> t2mu)
        {
            Contract.Requires(null != t2mu, "t2mu");
            return mt.Bind(t2mu);
        }

        // This operator is implemented only to satisfy C#'s LINQ comprehension syntax.  
        // The name "SelectMany" is confusing as there is only one value to "select".
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static Maybe<V> SelectMany<T, U, V>(
            this Maybe<T> mt,
            Func<T, Maybe<U>> t2mu,
            Func<T, U, V> t2u2v)
        {
            Contract.Requires(null != t2mu, "t2mu");
            Contract.Requires(null != t2u2v, "t2u2v");
            return mt.Bind(x => t2mu(x).Bind(y => t2u2v(x, y).ToMaybe()));
        }

        #endregion

        #region ToMaybe Operator

        // Conventionally, in LINQ, the monadic "return" operator is written "To...,"
        // as in "ToList," "ToArray," etc. These are synonyms for Return.
        public static Maybe<T> ToMaybe<T>(this T value)
        {
            return Maybe.Return(value);
        }

        #endregion

        #region Coalesce Operator

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult> selector) where TResult : class
        {
            Contract.Requires(null != selector, "selector");
            return self.Coalesce(selector, () => Maybe<TResult>.NoValue);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector) where TResult : struct
        {
            Contract.Requires(null != selector, "selector");
            return self.Coalesce(selector, () => Maybe<TResult>.NoValue);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult> selector, Func<Maybe<TResult>> valueIfNullFactory) where TResult : class
        {
            Contract.Requires(null != selector, "selector");
            Contract.Requires(null != valueIfNullFactory, "valueIfNullFactory");

            return self
                .Select(selector)
                .NotNull()
                .Or(valueIfNullFactory);
        }

        public static Maybe<TResult> Coalesce<T, TResult>(this Maybe<T> self, Func<T, TResult?> selector, Func<Maybe<TResult>> valueIfNullFactory) where TResult : struct
        {
            Contract.Requires(null != selector, "selector");
            Contract.Requires(null != valueIfNullFactory, "valueIfNullFactory");

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
            return self
                .Or(@default)
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
            Contract.Requires(null != valueFactory, "valueFactory");
            return self.Or(() => Return(valueFactory));
        }

        public static Maybe<T> Or<T>(this Maybe<T> self, Func<Maybe<T>> valueFactory)
        {
            Contract.Requires(null != valueFactory, "valueFactory");
            return self.Extend(x => x.Exception == null && x.HasValue != true ? valueFactory() : x);
        }

        #endregion

        #region With Operator

        public static Maybe<T> With<T, TSelected>(
            this Maybe<T> self, 
            Func<T, TSelected> selector, 
            Action<TSelected> action)
        {
            Contract.Requires(null != selector, "selector");
            Contract.Requires(null != action, "action");

            return With(self, x => selector(x).ToMaybe(), action);
        }

        public static Maybe<T> With<T, TSelected>(
            this Maybe<T> mt, 
            Func<T, Maybe<TSelected>> selector, 
            Action<TSelected> action)
        {
            Contract.Requires(null != selector, "selector");
            Contract.Requires(null != action, "action");

            return mt.Bind(x =>
            {
                selector(x)
                    .OnValue(action)
                    .ThrowOnException()
                    .Run();

                return x.ToMaybe();
            });
        }

        #endregion

        #region When Operator

        public static Maybe<T> When<T>(
            this Maybe<T> self, 
            T value, 
            T newValue)
        {
            return self.When(value, x => newValue.ToMaybe());
        }

        public static Maybe<T> When<T>(
            this Maybe<T> self, 
            T value, 
            Action<T> action)
        {
            Contract.Requires(null != action, "action");
            return self.When(x => x.Equals(value), x => { action(x); return x.ToMaybe(); });
        }

        public static Maybe<T> When<T>(
            this Maybe<T> self, 
            T value, 
            Func<T, Maybe<T>> computation)
        {
            Contract.Requires(null != computation, "computation");
            return self.When(x => x.Equals(value), computation);
        }

        public static Maybe<T> When<T>(
            this Maybe<T> self, 
            Func<T, bool> predicate, 
            T newValue)
        {
            Contract.Requires(null != predicate, "predicate");
            return self.When(predicate, x => newValue.ToMaybe());
        }

        public static Maybe<T> When<T>(this Maybe<T> self, Func<T, bool> predicate, Action<T> action)
        {
            Contract.Requires(null != predicate, "predicate");
            Contract.Requires(null != action, "action");

            return self.When(predicate, x => { action(x); return self; });
        }

        public static Maybe<T> When<T>(
            this Maybe<T> self, 
            Func<T, bool> predicate, 
            Func<T, Maybe<T>> computation)
        {
            Contract.Requires(null != predicate, "predicate");
            Contract.Requires(null != computation, "computation");

            return self
                    .Where(predicate)
                    .Bind(computation)
                    .Or(self);
        }

        #endregion

        #region Join Operator

        public static Maybe<Tuple<T, U>> Join<T, U>(
            this Maybe<T> self, 
            Maybe<U> other)
        {
            return self.Join(other, Tuple.Create);
        }

        public static Maybe<TResult> Join<T, U, TResult>(
            this Maybe<T> self, 
            Maybe<U> other, 
            Func<T, U, TResult> selector)
        {
            Contract.Requires(null != selector, "selector");
            return self.Bind(t => other.Select(r => selector(t, r)));
        }

        #endregion

        #region ThrowOnNoValue Operator

        public static Maybe<T> ThrowOnNoValue<T>(
            this Maybe<T> self, 
            Exception exception)
        {
            Contract.Requires(null != exception, "exception");
            return self.ThrowOnNoValue(() => exception);
        }

        public static Maybe<T> ThrowOnNoValue<T>(
            this Maybe<T> self, 
            Func<Exception> exceptionFactory)
        {
            Contract.Requires(null != exceptionFactory, "exceptionFactory");
            return self
                .ThrowOn(x => x.Exception == null && x.HasValue != true, x => exceptionFactory());
        }

        #endregion

        #region ThrowOnException Operator

        public static Maybe<T> ThrowOnException<T>(this Maybe<T> self)
        {
            return self.ThrowOnException(typeof(Exception));
        }

        public static Maybe<T> ThrowOnException<T>(
            this Maybe<T> self, 
            Type exceptionType)
        {
            Contract.Requires(null != exceptionType, "exceptionType");
            return self.ThrowOnException(x => exceptionType.IsAssignableFrom(x.GetType()));
        }

        public static Maybe<T> ThrowOnException<T>(
            this Maybe<T> self, 
            Func<Exception, bool> predicate)
        {
            Contract.Requires(null != predicate, "predicate");
            return self.ThrowOn(x => x.Exception != null, x => x.Exception);
        }

        #endregion

        #region ThrowOn Operator

        public static Maybe<T> ThrowOn<T>(
            this Maybe<T> self, 
            T value, 
            Exception exception)
        {
            Contract.Requires(null != exception, "exception");
            return self.ThrowOn(value, x => exception);
        }

        public static Maybe<T> ThrowOn<T>(
            this Maybe<T> self, 
            T value, 
            Func<Maybe<T>, Exception> exceptionFactory)
        {
            Contract.Requires(null != exceptionFactory, "exceptionFactory");
            return self.ThrowOn(x => x.Equals(value), exceptionFactory);
        }

        public static Maybe<T> ThrowOn<T>(
            this Maybe<T> self, 
            Func<Maybe<T>, bool> predicate, 
            Exception exception)
        {
            Contract.Requires(null != exception, "exception");
            Contract.Requires(null != predicate, "predicate");
            return self.ThrowOn(predicate, x => exception);
        }

        public static Maybe<T> ThrowOn<T>(
            this Maybe<T> self,
            Func<Maybe<T>, bool> predicate,
            Func<Maybe<T>, Exception> exceptionFactory)
        {
            Contract.Requires(null != exceptionFactory, "exceptionFactory");
            Contract.Requires(null != predicate, "predicate");

            return self.Extend(x =>
            {
                if (predicate(x))
                {
                    // [bbeckman: avoiding octopus-copy of entire ExceptionExtensions,
                    // Cloneable, ILGenerator, etc.]
                    //result.Exception.ThrowAsInnerExceptionIfNeeded();
                    var newEx = new InvalidOperationException(
                        "Inner exception recorded",
                        exceptionFactory(x));
                }

                return x;
            });
        }

        #endregion

        #region OnException Operator

        public static Maybe<T> OnException<T>(
            this Maybe<T> self, 
            T value)
        {
            return self.OnException(() => value);
        }

        public static Maybe<T> OnException<T>(
            this Maybe<T> self, 
            Func<T> valueFactory)
        {
            return self.OnException(x => Return(valueFactory()));
        }

        public static Maybe<T> OnException<T>(this Maybe<T> self, Action<Exception> handler)
        {
            Contract.Requires(null != handler, "handler");
            return self.OnException(x => { handler(x); return new Maybe<T>(x); });
        }

        public static Maybe<T> OnException<T>(this Maybe<T> self, Func<Exception, Maybe<T>> handler)
        {
            Contract.Requires(null != handler, "handler");
            return self.Extend(x => x.Exception != null ? handler(x.Exception) : x);
        }

        #endregion

        public static Maybe<T> OnValue<T>(
            this Maybe<T> self, 
            Action<T> action)
        {
            Contract.Requires(null != action, "action");
            return self.Bind(x =>
            {
                action(x);
                return x.ToMaybe();
            });
        }

        public static Maybe<T> OnNoValue<T>(this Maybe<T> self, Action action)
        {
            Contract.Requires(null != action, "action");

            return self.Extend(x =>
            {
                if (x.Exception == null && x.HasValue != true)
                    action();

                return x;
            });
        }

        public static Maybe<T> CatchExceptions<T>(this Maybe<T> self)
        {
            return self.Extend(x =>
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

        public static Maybe<T> Where<T>(
            this Maybe<T> self, 
            Func<T, bool> predicate)
        {
            Contract.Requires(null != predicate, "predicate");
            return self.Bind(x => predicate(x) ? x.ToMaybe() : Maybe<T>.NoValue);
        }

        public static Maybe<T> Unless<T>(
            this Maybe<T> self, 
            Func<T, bool> predicate)
        {
            Contract.Requires(null != predicate, "predicate");
            return self.Where(x => !predicate(x));
        }

        public static Maybe<T> Assign<T>(
            this Maybe<T> self, 
            ref T target)
        {
            if (self.HasValue)
                target = self.Value;

            return self;
        }

        public static Maybe<T> Run<T>(
            this Maybe<T> self, 
            Action<T> action = null)
        {
            return self
                .When(x => action != null, x => self.OnValue(action))
                // [bbeckman: what does the following accomplish? Looks like a no-op.]
                .HasValue ? self : self;
        }

        public static Maybe<T> RunAsync<T>(
            this Maybe<T> self, 
            Action<T> action = null, 
            CancellationToken cancellationToken = default(CancellationToken), 
            TaskCreationOptions taskCreationOptions = TaskCreationOptions.None, 
            TaskScheduler taskScheduler = default(TaskScheduler))
        {
            var task = Task.Factory.StartNew(
                () => self.Run(action), 
                cancellationToken, 
                taskCreationOptions,
                taskScheduler ?? TaskScheduler.Default);

            return self.Extend(x =>
            {
                task.Wait(cancellationToken);
                return task.IsCanceled ? Maybe<T>.NoValue : task.Result;
            });
        }

        #region Synchronize operator

        public static Maybe<T> Synchronize<T>(this Maybe<T> mt)
        {
            return SynchronizeWith(mt, new object());
        }

        public static Maybe<T> SynchronizeWith<T>(
            this Maybe<T> mt, 
            object lockObject)
        {
            Contract.Requires(null != lockObject, "lockObject");

            Func<Maybe<T>> synchronizedComputation = () => mt.Run();

            // [bbeckman: this calls an overload in FuncExtensions.]
            synchronizedComputation = synchronizedComputation.SynchronizeWith(() => true, lockObject);

            return Return(synchronizedComputation)
                // [bbeckman: strip off one level of monad:]
                .Bind(x => x);
        }

        #endregion

        public static Maybe<T> Cast<T>(this IMaybe self)
        {
            Contract.Requires(null != self, "self");

            return Return(() =>
            {
                if (self.Exception != null)
                    return new Maybe<T>(self.Exception);

                if (self.HasValue != true)
                    return Maybe<T>.NoValue;

                return ((T)self.Value).ToMaybe();
            })
            // [bbeckman: here's a good reason to get rid of the overload of bind and 
            // the implicit operator that promotes an x to a Maybe<x>... the meaning 
            // of code like the following is highly context dependent.
            .Bind(x => x);
        }

        public static Maybe<T> OfType<T>(this IMaybe self)
        {
            Contract.Requires(null != self, "self");

            return Return(() =>
            {
                if (self.Exception != null)
                    return new Maybe<T>(self.Exception);

                if (self.HasValue != true)
                    return Maybe<T>.NoValue;

                if (self.Value is T)
                    return ((T)self.Value).ToMaybe();

                return Maybe<T>.NoValue;
            })
            .Bind(x => x);
        }

        public static T? ToNullable<T>(this Maybe<T> self) where T : struct
        {
            return self.Select(x => (T?)x)
                .Or((T?)null)
                .Extract();
        }
    }
}
