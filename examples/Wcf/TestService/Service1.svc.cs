﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.Diagnostics;

namespace TestService
{
    // NOTE: If you change the class name "Service1" here, you must also update the reference to "Service1" in Web.config and in the associated .svc file.
    public class Service1 : IService1, IDisposable
    {
        private readonly ITest _test;

        public Service1(ITest test)
        {
            Debug.WriteLine("Service instance constructed.");
            _test = test;
        }

        public string GetData(int value)
        {
            return string.Format("You entered: {0}. This a {1}.", value, _test.Execute());
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public void Dispose()
        {
            Debug.WriteLine("Service instance disposed.");
        }
    }
}