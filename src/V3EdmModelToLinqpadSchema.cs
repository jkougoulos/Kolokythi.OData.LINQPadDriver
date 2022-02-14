using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

using System.Linq;
using Microsoft.Data.Edm;

using LINQPad.Extensibility.DataContext;

namespace Kolokythi.OData.LINQPadDriver
{
    internal class V3EdmModelToLinqpadSchema
    {
        private readonly IEdmModel _model;
        private readonly bool _multiNS;
        private readonly bool _nativeSOC;
        private Stack<string> _stack;
        private readonly int _maxStackDepth;

        internal V3EdmModelToLinqpadSchema(IEdmModel model, bool multiNS, bool nativeSOC, int stackDepth)
        {
            _model = model;
            _multiNS = multiNS;
            _nativeSOC = nativeSOC;
            _stack = new Stack<string>();
            _maxStackDepth = stackDepth;

        }


        public List<ExplorerItem> GetRootSchema()
        {
            var items = new List<ExplorerItem>();
            items.AddRange(
                    _model.EntityContainers()
                    .OrderBy(c => c.Name)
                    .Select(c => new ExplorerItem(c.Name, ExplorerItemKind.Category, ExplorerIcon.Box)
                        {
                            Children= GetContainerSchema(c)
                        }
                    )

                );

            return items;
        }

        private List<ExplorerItem> GetContainerSchema(IEdmEntityContainer entityContainer)
        {
            List<ExplorerItem> containerSchema = new List<ExplorerItem>();

            if (entityContainer != null)
            {
                var elements = entityContainer
                                .Elements;

              
                containerSchema.AddRange(
                        elements
                            .Where(e => e.ContainerElementKind == EdmContainerElementKind.EntitySet)
                            .OrderBy(e => e.Name)
                            .Select(e => GetEntitySetSchema(e as IEdmEntitySet))
                            );



                var nonBindables = elements
                            .Where(e => e.ContainerElementKind == EdmContainerElementKind.FunctionImport)
                            .Where(e => (e as IEdmFunctionImport).IsBindable.Equals(false))
                            .OrderBy(e => (e as IEdmFunctionImport).IsSideEffecting)
                                .ThenBy(e => e.Name)
                            .Select(e => GetFunctionImportSchema(e as IEdmFunctionImport))
                            .ToList();

                nonBindables
                    .ForEach(e =>
                   {
                       (string feedType, string functionFullName, string dragTextSuffix) = (ValueTuple<string, string, string>)e.Tag;
                       e.DragText = GetDragTextForFunctionImport("this\n.Unbound()\n", dragTextSuffix);

                   });
                            
                containerSchema.AddRange(
                            nonBindables
                    );

                // here we get what we will display under entitysets-entitytype 
                var functionsSchema = _model
                                   .EntityContainers()
                                   .SelectMany( ec => ec.EntitySets() )
                                   .SelectMany( es =>  es
                                                    .Container
                                                    .FunctionImports()
                                                    .Where( f => 
                                                                f.IsBindable &&
                                                                f.Parameters.Any() &&
                                                                f.Parameters.FirstOrDefault().Type.IsEntity() 
                                                                &&
                                                                (f.Parameters.FirstOrDefault().Type.Definition as IEdmEntityType) == es.ElementType 

                                                           )
                                                    
                                                    
                                                    
                                        )
                                    .ToList();

                var functionsWithCollection = new List<IEdmFunctionImport>();

                _model
                   .EntityContainers()
                   .SelectMany(ec => ec.EntitySets())
                   .ToList()
                   .ForEach(es =>
                  {
                       var funs =   es
                                    .Container
                                    .FunctionImports()
                                    .Where(f => f.IsBindable)
                                    .Where(f => f.Parameters.Any())
                                    .Where(f => f.Parameters.FirstOrDefault().Type.IsCollection())
                                    .Where(f => (f.Parameters.FirstOrDefault().Type.Definition as IEdmCollectionType).ElementType.IsEntity())
                                    .Where(f =>
                                              ((f.Parameters.FirstOrDefault().Type.Definition as IEdmCollectionType).ElementType.Definition as IEdmEntityType) == es.ElementType

                                         );

                       if (funs.Any())
                       {
                           functionsWithCollection.AddRange(funs);
                       }
                   });



                functionsSchema.AddRange(functionsWithCollection);


                // we should use Dictionary instead of "Contains" for performance.. but ok, we will fix this. Sometime.

                // here we add bindables that do not appear under entitysets for whatever reason

                var bindables =
                                  elements
                                            .Where(e => e.ContainerElementKind == EdmContainerElementKind.FunctionImport)
                                            .Where(e => (e as IEdmFunctionImport).IsBindable)
                                            .Select(e => e as IEdmFunctionImport)
                                            .Where(f => !functionsSchema.Contains(f))
                                            .OrderBy(f => f.IsSideEffecting)
                                                .ThenBy(f => f.Name)
                                            .Select(f => GetFunctionImportSchema(f))
                                            .ToList();

                bindables
                    .ForEach(e =>
                    {
                        (string feedType, string functionFullName, string dragTextSuffix) = (ValueTuple<string, string, string>)e.Tag;
                        e.DragText = GetDragTextForFunctionImport("this\n.Unbound()\n", dragTextSuffix);

                    });


                containerSchema.AddRange( bindables );

            }

            return containerSchema;
        }

  

        private string GetDragTextForEntitySet(IEdmEntitySet entitySet)
        {
            var type = entitySet.ElementType;
            var typeName = _multiNS ? type.FullName() : type.Name;

            if (_nativeSOC)
            {
                return $"this\n.For<{typeName}>(\"{entitySet.Name}\")\n//.Expand( x => )\n//.Filter( x => )\n//.Top(10)\n.FindEntriesAsync() // ";
            }

            return entitySet.Name;
        }

        private ValueTuple<string, string, string> GetTagForEntitySet(IEdmEntitySet entitySet)
        {
            var type = entitySet.ElementType;

            return ("EntitySet", entitySet.Name, _multiNS ? type.FullName() : type.Name);
        }


        private ExplorerItem GetFunctionImportSchema(IEdmFunctionImport functionImport)
        {

            var returnType = functionImport.ReturnType;

            string tooltip = "";
            string tagText = "FunctionImport";
            string itemDescr = functionImport.Name;
            ExplorerIcon icon = functionImport.IsSideEffecting ? ExplorerIcon.StoredProc : ExplorerIcon.ScalarFunction;
            List<ExplorerItem> children = new List<ExplorerItem>();

            if (returnType != null)
            {
                var definition = returnType.Definition;

                tooltip = GetExplorerItemTooltip(definition);
                itemDescr = GetExplorerItemDescription(functionImport.Name, definition);
                children.Add(
                                GetReturnTypeSchema(returnType.Definition)
                    );

            }

            if (functionImport.Parameters.Count() > 0)
            {
                children.Add(
                    GetParametersSchema(functionImport.Parameters)
                );

                if ( 
                        functionImport.IsBindable &&
                        functionImport.Parameters.FirstOrDefault().Type.IsEntity()
                   )
                {
                    tagText = "FunctionImportBoundEntity";
                }
                else if (
                            functionImport.IsBindable &&
                            functionImport.Parameters.FirstOrDefault().Type.IsCollection() &&
                            (functionImport.Parameters.FirstOrDefault().Type.Definition as IEdmCollectionType).ElementType.IsEntity()
                    )
                {
                    tagText = "FunctionImportBoundCollectionEntity";
                }


            }
            else
            {
                tagText = "FunctionImportUnbound";
            }

            ExplorerItem item = CreateExplorerItem(itemDescr, ExplorerItemKind.QueryableObject, icon, tooltip);

            item.Children = children;
            item.Tag = GetTagForFunctionImport(functionImport, tagText );
          //  item.DragText = GetDragTextForFunctionImport(functionImport);
            item.IsEnumerable = true;

            return item;
        }

        private string GetDragTextForFunctionImport(string prefix, string suffix )
        {

            if (_nativeSOC)
            {
                return prefix + suffix;
            }
            else
            {
                return "";
            }
        }



        private ValueTuple<string, string, string> GetTagForFunctionImport(IEdmFunctionImport functionImport, string tagText)
        {
            // var type = navSource.EntityType();

            return ( tagText, functionImport.Name, GetDragTextSuffixForFunctionImport(functionImport));
        }

   
        private string GetCLRTypeFromEdmType(IEdmType edmType)
        {
         

            switch (edmType.TypeKind)
            {
                case EdmTypeKind.Primitive:
                    return (edmType as IEdmPrimitiveType).Name;
                case EdmTypeKind.Entity:
                    var entityType = edmType as IEdmEntityType;
                    return _multiNS ? entityType.FullName() : entityType.Name;
                case EdmTypeKind.Collection:
                    return GetCLRTypeFromEdmType((edmType as IEdmCollectionType).ElementType.Definition);
                case EdmTypeKind.Complex:
                    var complexType = edmType as IEdmComplexType;
                    return _multiNS ? complexType.FullName() : complexType.Name;
                default:
                    return "hmmmmm";
            }

        }

        private string GetDragTextSuffixForFunctionImport(IEdmFunctionImport functionImport)
        {
            string dragText = "";
            var retTypeRef = functionImport.ReturnType;
            string dragTextTrailer = "";

            if (
                   (functionImport.IsBindable && functionImport.Parameters.Count() > 1) ||
                   (!functionImport.IsBindable && functionImport.Parameters.Count() > 0)
                   )
            {
                string setText = ".Set( new {";
                var paramArray = functionImport.Parameters.ToArray();
                int i = functionImport.IsBindable ? 1 : 0; // if it is bound we don't add the parameter in Set

                for (; i < paramArray.Length; i++)
                {
                    setText += " " + paramArray[i].Name;
                    setText += " = ";
                    setText += GetCLRTypeFromEdmType(paramArray[i].Type.Definition);
                    if (i < paramArray.Length - 1)
                    {
                        setText += " ,";
                    }
                }
                setText += " })\n";
                dragText += setText;
            }

            var ann = _model.GetAnnotationValue(functionImport,
                        "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata",
                        "HttpMethod"
                        );
            
     //       Debugger.Launch();
            
            if ( (ann is Microsoft.Data.Edm.Library.Values.EdmStringConstant constant && 
                constant.Value == "GET" ) 
                || !functionImport.IsSideEffecting )
            {
                dragText += ".Function";
            }
            else
            {
                dragText += ".Action";
            }

                                                                               //        Debugger.Launch();
                                                                               //            dragText += functionImport.IsSideEffecting ? ".Action" : ".Function";
            
           
            if (retTypeRef != null)
            {
                var retType = retTypeRef.Definition;
                var typeName = GetCLRTypeFromEdmType(retType);
                string typeText = $"<{typeName}>";

                if (retTypeRef.IsCollection())
                {
                    dragTextTrailer = $".ExecuteAsEnumerableAsync";
                    dragText += typeText;
                }
                else if (retTypeRef.IsPrimitive())
                {
                    dragTextTrailer = $".ExecuteAsScalarAsync{typeText}";
                }
                else if (retTypeRef.IsEntity())
                {
                    dragTextTrailer = $".ExecuteAsSingleAsync{typeText}";
                }

            }
            else
            {
                dragTextTrailer = ".ExecuteAsync";
            }

            dragText += $"(\"{functionImport.Name}\")\n{dragTextTrailer}";

            return dragText;
        }

        private ExplorerItem GetParametersSchema(IEnumerable<IEdmFunctionParameter> parameters)
        {

            List<ExplorerItem> children = new List<ExplorerItem>();
            string itemDescr = "Parameters";
            var item = CreateExplorerItem(itemDescr, ExplorerItemKind.Category, ExplorerIcon.Schema);

            children.AddRange(parameters
                         //               .OrderBy(p => p.Name)
                                        .Select(p =>
                                                        GetParameterSchema(p)
                                )
                );

            item.Children = children;

            return item;
        }

        private ExplorerItem GetParameterSchema(IEdmFunctionParameter p)
        {
            var item = CreateExplorerItem(p.Name + " - " + GetTypeDescription(p.Type.Definition, false), ExplorerItemKind.Parameter, ExplorerIcon.Parameter );
            var children = new List<ExplorerItem>();

            children.AddRange( GetEdmTypeSchema(p.Type.Definition) );

            item.ToolTipText = GetTypeDescription(p.Type.Definition, true);

            item.Children = children;

            return item;
        }

        private ExplorerItem GetReturnTypeSchema(IEdmType definition)
        {

            string itemDescr = "Return: - " + GetTypeDescription(definition, false);
            var item = CreateExplorerItem( itemDescr, ExplorerItemKind.Category, ExplorerIcon.Schema);

            item.ToolTipText = GetTypeDescription(definition, _multiNS);

            item.Children = GetEdmTypeSchema(definition);

            return item;
        }



        private ExplorerItem GetEntitySetSchema(IEdmEntitySet entitySet)
        {
            string itemDescr = GetExplorerItemDescription(entitySet.Name, entitySet.ElementType );
            string tooltip = GetExplorerItemTooltip( entitySet.ElementType );
            List<ExplorerItem> children = new List<ExplorerItem>();

            ExplorerItem item = CreateExplorerItem(itemDescr, ExplorerItemKind.QueryableObject, ExplorerIcon.Table, tooltip);


            item.DragText = GetDragTextForEntitySet(entitySet);
            item.Tag = GetTagForEntitySet(entitySet);
            item.IsEnumerable = true;

            children.AddRange(GetEntityTypeSchema(entitySet.ElementType));

            var functionsSchema = children
                                    .Where(e =>
                                               e.Icon == ExplorerIcon.ScalarFunction ||
                                               e.Icon == ExplorerIcon.StoredProc
                                          )
                                    .ToList();

            functionsSchema
                .ForEach(e =>
                {
                    (string feedType, string functionFullName, string dragTextSuffix) = (ValueTuple<string, string, string>)e.Tag;
                    string typeName = _multiNS ? entitySet.ElementType.FullName() : entitySet.ElementType.Name;


                    if (feedType == "FunctionImportBoundCollectionEntity")
                    {
                        e.DragText = GetDragTextForFunctionImport(
                                                $"this\n.For<{typeName}>()\n",
                                                dragTextSuffix
                                            );
                    }
                    else
                    {
                        e.DragText = GetDragTextForFunctionImport(
                                                $"this\n.For<{typeName}>()\n.Key( xxx )\n",
                                                dragTextSuffix
                                            );

                    }
                });


            item.Children = children;

            return item;
        }


   

        private List<ExplorerItem> GetEntityTypeSchema(IEdmEntityType entityType)
        {
            List<ExplorerItem> children = new List<ExplorerItem>();

            string FullName = entityType.FullName();
            if (!_stack.Contains(FullName) && _stack.Count < _maxStackDepth )
            {
                _stack.Push(FullName);

                children.AddRange(
                     GetStructuredTypeSchema(entityType, entityType.DeclaredKey)
                );

                children.AddRange(
                    GetNavigationPropertiesSchema(entityType)
                );


                var functionsSchema = _model
                                   .EntityContainers()
                                   .SelectMany(e => e.FunctionImports() )
                                   .OrderBy(f => f.IsSideEffecting )
                                        .ThenBy(f => f.Name)
                                   .Where( f => f.Parameters.FirstOrDefault() != null && 
                                                f.Parameters.FirstOrDefault().Type.IsEntity() && 
                                                ( (f.Parameters.FirstOrDefault().Type.Definition as IEdmEntityType) == entityType )   
                                         )
                                    .Select(f => GetFunctionImportSchema(f))
                                    .ToList();

                functionsSchema.AddRange(
                            _model
                                   .EntityContainers()
                                   .SelectMany(e => e.FunctionImports())
                                   .OrderBy(f => f.IsSideEffecting)
                                        .ThenBy(f => f.Name)
                                   .Where(f => f.Parameters.FirstOrDefault() != null &&
                                               f.Parameters.FirstOrDefault().Type.IsCollection() &&
                                               ((f.Parameters.FirstOrDefault().Type.Definition as IEdmCollectionType).ElementType.Definition == entityType)
                                          )
                                    .Select(f => GetFunctionImportSchema(f))
                                    .ToList()
                    );


                if (functionsSchema.Any())
                    children.AddRange(
                            functionsSchema
                        );


                string lastInStack = "";
                _stack.TryPop(out lastInStack);
                if (lastInStack != FullName)
                {
                    throw new Exception($"Last in stack is {lastInStack}, expected {FullName}");
                }
            }

            return children ;
        }

        private List<ExplorerItem> GetComplexTypeSchema(IEdmComplexType complexType)
        {
  
            List<ExplorerItem> myschema = new List<ExplorerItem> ();
                

            string FullName = complexType.FullName();
            if (!_stack.Contains(FullName) && _stack.Count < _maxStackDepth)
            {
                _stack.Push(FullName);
                myschema = GetStructuredTypeSchema(complexType, null);

                string lastInStack = "";
                _stack.TryPop(out lastInStack);
                if (lastInStack != FullName)
                {
                    throw new Exception($"Last in stack is {lastInStack}, expected {FullName}");
                }

            }

            return myschema;
        }


        private List<ExplorerItem> GetStructuredTypeSchema(IEdmStructuredType structuredType, IEnumerable<IEdmStructuralProperty> keys)
        {
            List<ExplorerItem> children = new List<ExplorerItem>();

            children.AddRange(
                        GetStructuralPropertiesSchema(structuredType, keys)
                );

            if( structuredType.BaseType != null)
            {
                children.AddRange(
                            GetStructuredTypeSchema(structuredType.BaseType, null)
                        );
            }
            

            return children;
        }

        private List<ExplorerItem> GetCollectionTypeSchema(IEdmCollectionType collectionType)
        {
            IEdmType edmType = collectionType.ElementType.Definition;

            return GetEdmTypeSchema(edmType);
        }

        private List<ExplorerItem> GetEdmTypeSchema(IEdmType edmType)
        {
            EdmTypeKind typeKind = edmType.TypeKind;
            List<ExplorerItem> schema = new List<ExplorerItem> ();

            switch (typeKind)
            {
                case EdmTypeKind.Complex:
                    schema =  GetComplexTypeSchema(edmType as IEdmComplexType);
                    break;
                case EdmTypeKind.Entity:
                    schema = GetEntityTypeSchema(edmType as IEdmEntityType);
                    break;
                case EdmTypeKind.Collection:
                    schema = GetCollectionTypeSchema(edmType as IEdmCollectionType);
                    break;
            }

            return schema;
        }


        private List<ExplorerItem> GetStructuralPropertiesSchema(IEdmStructuredType structuredType, IEnumerable<IEdmStructuralProperty> keys)
        {

            if (keys == null)
            {
                //                return new List<ExplorerItem>();
                return structuredType
                    .DeclaredStructuralProperties()
                    .OrderBy(p => p.Name)
                    .Select
                        (p => GetStructuralPropertySchema(p, false)
                    )
                    .ToList();
            }
            else
            {
                return structuredType
                        .DeclaredStructuralProperties()
                        .OrderByDescending(p => keys.Any(key => key == p))
                            .ThenBy(p => p.Name)
                        .Select
                            (p => GetStructuralPropertySchema(p, keys.Any(key => key == p))
                        )
                        .ToList();
            }
        }


        private List<ExplorerItem> GetNavigationPropertiesSchema(IEdmEntityType entityType)
        {
            return entityType
                    .DeclaredNavigationProperties()
                    .OrderBy(p => p.Name)
                    .Select
                        (p => GetNavigationPropertySchema(p)
                    )
                    .ToList();
        }


        private ExplorerItem GetNavigationPropertySchema(IEdmNavigationProperty prop)
        {

            ExplorerIcon icon;
            ExplorerItemKind kind;
            List<ExplorerItem> children = new List<ExplorerItem>();

 
            switch ( prop.Partner.Multiplicity() )
            {
                case EdmMultiplicity.ZeroOrOne:
                case EdmMultiplicity.One:
                    if ( prop.Multiplicity() == EdmMultiplicity.Many )
                    {
                        icon = ExplorerIcon.ManyToOne;
                    }
                    else
                    {
                        icon = ExplorerIcon.OneToOne;
                    }

                    kind = ExplorerItemKind.ReferenceLink;
                    break;
                case EdmMultiplicity.Many:
                    if (prop.Multiplicity() == EdmMultiplicity.Many)
                    {
                        icon = ExplorerIcon.ManyToMany;
                    }
                    else
                    {
                        icon = ExplorerIcon.OneToMany;
                    }
                    kind = ExplorerItemKind.CollectionLink;
                    break;
                default:
                    icon = ExplorerIcon.Blank;
                    kind = ExplorerItemKind.Property;
                    break;
            }

            ExplorerItem item = GetPropertySchema(prop, kind, icon );

            children.AddRange( GetEdmTypeSchema(prop.Type.Definition) );

            item.Children = children;

            return item;
        }

        private ExplorerItem GetStructuralPropertySchema(IEdmStructuralProperty prop, bool isKey )
        {

            //      return new ExplorerItem(prop.Name, ExplorerItemKind.Property, ExplorerIcon.Column);
            
            ExplorerIcon icon = isKey ? ExplorerIcon.Key : ExplorerIcon.Column;
            ExplorerItemKind kind = ExplorerItemKind.Property;

            List<ExplorerItem> children = null;

            EdmTypeKind typeKind = prop.Type.Definition.TypeKind;

            switch (typeKind)
            {
                case EdmTypeKind.Collection:
                    children = GetCollectionTypeSchema(prop.Type.Definition as IEdmCollectionType);
                    icon = ExplorerIcon.OneToMany;
                    kind = ExplorerItemKind.CollectionLink;
                    break;
                case EdmTypeKind.Complex:
                    children = GetComplexTypeSchema(prop.Type.Definition as IEdmComplexType);
                    icon = ExplorerIcon.OneToOne;
                    break;
            }

            ExplorerItem item = GetPropertySchema(prop, kind, icon);
            
            item.Children = children;

            return item;
        }



        private ExplorerItem GetPropertySchema(IEdmProperty p, ExplorerItemKind kind, ExplorerIcon icon)
        {
            string tooltip = GetExplorerItemTooltip(p);
            string itemDescr = GetExplorerItemDescription(p);

            ExplorerItem item = CreateExplorerItem(itemDescr, kind, icon, tooltip);

            return item;
        }


        private string GetTypeDescription(IEdmType type, bool returnFull)
        {
            var typeKind = type.TypeKind;
            switch (typeKind)
            {
                case EdmTypeKind.Entity:
                    var typeName = returnFull ? (type as IEdmEntityType).FullName() : (type as IEdmEntityType).Name;
                    return $"Entity({typeName})";

                case EdmTypeKind.Primitive:
                    var primTypeName = returnFull ? (type as IEdmPrimitiveType).FullName() : (type as IEdmPrimitiveType).Name;
                    return $"{primTypeName}";

                case EdmTypeKind.Collection:
                    IEdmType edmType = (type as IEdmCollectionType).ElementType.Definition;
                    string collectionTypeName = GetTypeDescription(edmType, returnFull);
                    return $"Collection({collectionTypeName})";

                case EdmTypeKind.Complex:
                    var complexTypeName = returnFull ? (type as IEdmComplexType).FullName() : (type as IEdmComplexType).Name;
                    return $"Complex({complexTypeName})";

                case EdmTypeKind.Enum:
                    var enumTypeName = returnFull ? (type as IEdmEnumType).FullName() : (type as IEdmEnumType)?.Name;
                    return $"Enum({enumTypeName})";

            }

            return typeKind.ToString();
        }

        private string GetExplorerItemTooltip(IEdmProperty p)
        {

            return GetExplorerItemTooltip(p.Type.Definition);
        }

        private string GetExplorerItemTooltip(IEdmType definition)
        {
            return GetTypeDescription(definition, _multiNS);
        }

        private string GetExplorerItemTooltip(IEdmEntityType entityType)
        {
            return GetExplorerItemTooltip(entityType as IEdmType);
        }

        private string GetExplorerItemDescription(IEdmProperty p)
        {
            return p.Name + " - " + GetTypeDescription(p.Type.Definition, false);
        }

        private string GetExplorerItemDescription(string name, IEdmEntityType entityType)
        {
            return GetExplorerItemDescription(name, entityType as IEdmType);
        }

        private string GetExplorerItemDescription(string name, IEdmType edmType)
        {
            return name + " - " + GetTypeDescription(edmType, false);

        }


        private ExplorerItem CreateExplorerItem(string descr, ExplorerItemKind kind, ExplorerIcon explorerIcon, string tooltip)
        {
            ExplorerItem item = CreateExplorerItem(descr, kind, explorerIcon);

            item.ToolTipText = tooltip;

            return item;
        }


        private ExplorerItem CreateExplorerItem(string descr, ExplorerItemKind kind, ExplorerIcon explorerIcon)
        {
            return new ExplorerItem(descr, kind, explorerIcon);
        }



    }
}
