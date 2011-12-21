using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Experimental.DotNetExtensions
{
    public static class CastExtensions
    {
        public static T MustBe<T>(this object self)
            where T : class
        {
            var that = self as T;
            if (that == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Expected object {0} to have type {1}",
                        self.ToString(),
                        typeof(T).ToString()));
            }
            return that;
        }
    }
}
