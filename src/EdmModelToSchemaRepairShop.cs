using System;
using System.Collections.Generic;
using System.Text;
using LINQPad.Extensibility.DataContext;

namespace Kolokythi.OData.LINQPadDriver
{
    internal class EdmModelToSchemaRepairShop
    {
  
        protected EdmModelToSchemaRepairShop()
        {

        }
  
        public static List<ExplorerItem> GetRootSchema(object _model, bool _multiNS, bool _nativeSOC, int _stackDepth)
        {
            //if (_model.GetType().FullName.StartsWith("Microsoft.Data.Edm"))
            switch (_model)
            {
                case  Microsoft.Data.Edm.IEdmModel modv3:
                    var model2schemav3 = new V3EdmModelToLinqpadSchema(modv3, _multiNS, _nativeSOC, _stackDepth );
                    return model2schemav3.GetRootSchema();
                case Microsoft.OData.Edm.IEdmModel modv4:
                    var model2schemav4 = new V4EdmModelToLinqpadSchema(modv4, _multiNS, _nativeSOC, _stackDepth);
                    return model2schemav4.GetRootSchema();
                default:
                    throw new Exception("Cannot handle model type " + _model.GetType().ToString());
            }

        }
    }
}
