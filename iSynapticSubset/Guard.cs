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
using System.Linq;

namespace Experimental.DotNetExtensions.iSynaptic
{
    public static class Guard
    {
        public static T NotNull<T>(T value, string name, string message = null)
        {
            if (null == value)
                throw new ArgumentNullException(name, message);

            return value;
        }

        public static string NotEmpty(string value, string name, string message = "Must not be empty.")
        {
            if (string.Empty.Equals(value))
                throw new ArgumentException(message, name);

            return value;
        }

        public static Guid NotEmpty(Guid value, string name, string message = "Must not be empty.")
        {
            if (value.Equals(Guid.Empty))
                throw new ArgumentException(message, name);

            return value;
        }

        public static IEnumerable<T> NotEmpty<T>(IEnumerable<T> value, string name, string message = "Must not be empty.")
        {
            if (null != value && value.Any() != true)
                throw new ArgumentException(message, name);

            return value;
        }

        public static string NotNullOrEmpty(string value, string name, string message = "Must not be empty.")
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException(message, name);

            return value;
        }

        public static IEnumerable<T> NotNullOrEmpty<T>(IEnumerable<T> value, string name, string message = "Must not be null or empty.")
        {
            if (null == value || value.Any() != true)
                throw new ArgumentException(message, name);

            return value;
        }

        public static string NotNullOrWhiteSpace(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(string.Format("{0} must not be whitespace only.", name), name);

            return value;
        }

        public static T MustBeDefined<T>(T value, string name, string message = null)
        {
            if (Enum.IsDefined(typeof(T), value) != true)
                throw new ArgumentOutOfRangeException(name, message);

            return value;
        }

        public static T IsOfType<T>(object value, string name, string message = null)
        {
            if (!typeof (T).IsAssignableFrom(value.GetType()))
                throw new ArgumentException(name, message ?? string.Format("{0} must be of type '{1}'", name, typeof (T).FullName));

            return (T) value;
        }

        public static void Ensure(bool expectation, string name, string message)
        {
            if (!expectation)
                throw new ArgumentException(name, message);
        }
    }
}
