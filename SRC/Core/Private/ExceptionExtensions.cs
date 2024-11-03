/********************************************************************************
* ExceptionExtensions.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;

namespace Solti.Utils.Eventing.Internals
{
    internal static class ExceptionExtensions
    {
        public static TException WithData<TException>(this TException exc, params (string Key, object? Value)[] args) where TException : Exception
        {
            foreach ((string Key, object? Value) in args)
            {
                exc.Data[Key] = Value;
            }

            return exc;
        }
    }
}
