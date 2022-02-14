using System;
using System.Collections.Generic;
using System.Text;
using LINQPad.Extensibility.DataContext;


namespace Kolokythi.OData.LINQPadDriver
{
    internal static class Extensions
    {
        public static ConnectionProperties GetConnectionProperties(this IConnectionInfo connectionInfo)
        {
            return new ConnectionProperties(connectionInfo);
        }

    }
}
