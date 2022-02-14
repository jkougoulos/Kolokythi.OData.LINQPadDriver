using System;
using System.Collections.Generic;
using System.Text;

using System.Linq;
using Microsoft.OData.Edm;

using LINQPad.Extensibility.DataContext;

namespace Kolokythi.OData.LINQPadDriver
{
    internal class V4EdmModelToLinqpadSchema
    {
        private readonly IEdmModel _model;
        private readonly bool _multiNS;
        private readonly bool _nativeSOC;
        private Stack<string> _stack;
        private int _maxStackDepth;

        internal V4EdmModelToLinqpadSchema(IEdmModel model, bool multiNS, bool nativeSOC, int _stackDepth)
        {
            _model = model;
            _multiNS = multiNS;
            _nativeSOC = nativeSOC;
            _stack = new Stack<string>();
            _maxStackDepth = _stackDepth;

        }


        public List<ExplorerItem> GetRootSchema()
        {
            return GetContainerSchema(_model.EntityContainer);
        }

        private List<ExplorerItem> GetContainerSchema(IEdmEntityContainer entityContainer)
        {
            
            List<ExplorerItem> rootSchema = new List<ExplorerItem>();

            
            
            if (entityContainer != null)
            {

                ExplorerItem containerItem = CreateExplorerItem(entityContainer.Name, ExplorerItemKind.Category, ExplorerIcon.Box);

                List<ExplorerItem> containerSchema = new List<ExplorerItem>();

                containerSchema.AddRange(
                        entityContainer
                        .EntitySets()
                            .OrderBy(e => e.Name)
                            .Select(e => GetEntitySetSchema(e))
                            );

                containerSchema.AddRange(
                        entityContainer
                        .Singletons()
                            .OrderBy(s => s.Name)
                            .Select(s => GetSingletonSchema(s))
                            );

                var functionImports = entityContainer
                                        .OperationImports()
                                        .Where(oi => oi.IsFunctionImport())
                                        .Select( oi => oi as IEdmFunctionImport );

                var functionsOfImports = functionImports
                                            .Select(fi => fi.Function);

                containerSchema.AddRange(
                        functionImports
                        .OrderBy(fI => fI.Name)
                            .Select(fI => GetFunctionImportSchema(fI))
                            );

                containerSchema.AddRange(
                        entityContainer
                        .OperationImports()
                        .Where( oi => oi.IsActionImport() )
                        .OrderBy(oi => oi.Name)
                            .Select(e => GetActionImportSchema(e as IEdmActionImport))
                            );

                containerItem.Children = containerSchema;

                rootSchema.Add(containerItem);

                var notImportedUnboundFunctions =
                                    _model
                                        .SchemaElements
                                        .OfType<IEdmFunction>()
                                        .OrderBy(op => op.Name)
                                        .Where(op =>
                                                    op.IsBound.Equals(false) &&
                                                    !functionsOfImports.Contains(op)
                                            )
                                        .Select(op => GetFunctionSchema(op));

                if ( notImportedUnboundFunctions.Any())
                {
                    var functionsItem = CreateExplorerItem("UriOnlyFunctions", ExplorerItemKind.Category, ExplorerIcon.Box);
                    functionsItem.Children = notImportedUnboundFunctions.ToList();

                    rootSchema.Add( functionsItem );
                }

                

            }

            return rootSchema;
        }

        private ExplorerItem GetEntitySetSchema(IEdmEntitySet entitySet)
        {
            ExplorerItem item = GetNavigationSourcechema(entitySet);

            item.DragText = GetDragTextForEntitySet(entitySet);
            item.Tag = GetTagForEntitySet(entitySet);
            item.IsEnumerable = true;

            if ( item.Children.Any() && _nativeSOC )
            {
                item
                    .Children
                    .Where(child => child.Icon == ExplorerIcon.ScalarFunction || child.Icon == ExplorerIcon.StoredProc)
                    .ToList()
                    .ForEach(child =>
                   {
                       child.IsEnumerable = true;
                       if (child.Tag != null)
                       {
                           (string feedType, string functionFullName, string dragTextSuffix) = (ValueTuple<string, string, string>)child.Tag;
                           if (feedType == "OperationBoundCollectionEntity")
                           {
                               child.DragText = GetDragPrefixForEntitySetOperationCollection(entitySet) + dragTextSuffix;
                           }
                           else
                           {
                               child.DragText = GetDragPrefixForEntitySetOperation(entitySet) + dragTextSuffix;
                           }
                       }
                   });
            }

            return item;
        }

        private string GetDragTextForEntitySet(IEdmEntitySet entitySet)
        {
            var type = entitySet.EntityType();
            var typeName = _multiNS ? type.FullName() : type.Name;

            if (_nativeSOC)
            {
                return $"this\n.For<{typeName}>(\"{entitySet.Name}\")\n//.Expand( x => )\n//.Filter( x => )\n//.Top(10)\n.FindEntriesAsync() // ";
            }

            return entitySet.Name;
        }

        private string GetDragPrefixForEntitySetOperation(IEdmEntitySet entitySet)
        {
            var type = entitySet.EntityType();
            var typeName = _multiNS ? type.FullName() : type.Name;

            return $"this\n.For<{typeName}>(\"{entitySet.Name}\")\n.Key( xxxx )\n";
        }


        private string GetDragPrefixForEntitySetOperationCollection(IEdmEntitySet entitySet)
        {
            var type = entitySet.EntityType();
            var typeName = _multiNS ? type.FullName() : type.Name;

            return $"this\n.For<{typeName}>(\"{entitySet.Name}\")\n";
        }


        private ValueTuple<string, string, string> GetTagForEntitySet(IEdmEntitySet entitySet)
        {
            return GetTagForNavigationSource(entitySet);
        }

        private ValueTuple<string, string, string> GetTagForSingleton(IEdmSingleton singleton)
        {
            return GetTagForNavigationSource(singleton);
        }

        private ValueTuple<string, string, string> GetTagForNavigationSource(IEdmNavigationSource navSource)
        {
            var type = navSource.EntityType();

            return (navSource.NavigationSourceKind().ToString(), navSource.Name, _multiNS ? type.FullName() : type.Name);
        }
        private ValueTuple<string, string, string> GetTagForOperation(IEdmOperation operation, string feedType)
        {
           // var type = navSource.EntityType();

            return (feedType, operation.FullName(), GetDragTextForOperation(operation) );
        }


        private ExplorerItem GetSingletonSchema(IEdmSingleton singleton)
        {
            ExplorerItem item = GetNavigationSourcechema(singleton);

            item.DragText = GetDragTextForSingleton(singleton);
            item.Tag = GetTagForSingleton(singleton);

            item.IsEnumerable = true;

            return item;
        }

        private string GetDragTextForSingleton(IEdmSingleton singleton)
        {
            var type = singleton.EntityType();
            var typeName = _multiNS ? type.FullName() : type.Name;

            if (_nativeSOC)
            {
                return $"this\n.For<{typeName}>(\"{singleton.Name}\")\n//.Expand( x => )\n//.Filter( x => )\n//.Top(10)\n.FindEntryAsync() // ";
            }

            return singleton.Name + "\n// ";
        }

        private string GetCLRTypeFromEdmType(IEdmType edmType)
        {
            var actualType = edmType.AsElementType();

            switch (actualType.TypeKind)
            {
                case EdmTypeKind.Primitive:
                    return (actualType as IEdmPrimitiveType).ShortQualifiedName();
                case EdmTypeKind.Entity:
                    var entityType = actualType as IEdmEntityType;
                    return _multiNS ? entityType.FullName() : entityType.Name;

                case EdmTypeKind.Complex:
                    var complexType = actualType as IEdmComplexType;
                    return _multiNS ? complexType.FullName() : complexType.Name;
                default:
                    return "hmmmmm";
            }

        }

        private string GetDragTextForFunctionImport(IEdmFunctionImport functionImport)
        {

            if (_nativeSOC)
            {
                return "this\n.Unbound()\n" + GetDragTextForOperationImport(functionImport);
            }
            else
            {
                return "";
            }
        }

        private string GetDragTextForOperationImport(IEdmOperationImport operationImport)
        {
            return GetDragTextForOperation(operationImport.Operation);
        }

        private string GetDragTextForActionImport(IEdmActionImport actionImport)
        {

            if (_nativeSOC)
            {
                return "this\n.Unbound()\n" + GetDragTextForOperationImport(actionImport);
            }
            else
            {
                return "";
            }

        }

        private string GetDragTextForFunction(IEdmFunction function)
        {
            return GetDragTextForOperation(function);
        }

        private string GetDragTextForAction(IEdmAction action)
        {
            return GetDragTextForOperation(action);
        }

        private string GetDragTextForOperation(IEdmOperation operation)
        {
            string dragText = "";
            var retTypeRef = operation.ReturnType;
            string dragTextTrailer = "";

            if ( 
                   ( operation.IsBound && operation.Parameters.Count() > 1 ) ||
                   ( !operation.IsBound && operation.Parameters.Count() > 0 )
                   )
            {
                string setText = ".Set( new {";
                var paramArray = operation.Parameters.ToArray();
                int i = operation.IsBound ? 1 : 0; // if it is bound we don't add the parameter in Set

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

            dragText += operation.IsFunction() ? ".Function" : ".Action";
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

            dragText += $"(\"{operation.Name}\")\n{dragTextTrailer}";

            return dragText;
           
        }


        private ExplorerItem GetFunctionImportSchema(IEdmFunctionImport element)
        {
            var item = GetOperationImportSchema(element);
            
            item.DragText = GetDragTextForFunctionImport(element);
            item.IsEnumerable = true;

            return item;
        }

        private ExplorerItem GetActionImportSchema(IEdmActionImport element)
        {
            var item = GetOperationImportSchema(element);

            item.DragText = GetDragTextForActionImport(element);
            item.IsEnumerable = true;

            return item;
        }

        private ExplorerItem GetOperationImportSchema(IEdmOperationImport operationImport)
        {
            return GetOperationSchema(operationImport.Operation);
        }

        private ExplorerItem GetFunctionSchema(IEdmFunction function) // TODO diff type
        {
            return GetOperationSchema(function);
        }

        private ExplorerItem GetActionSchema(IEdmAction action) // TODO diff type
        {
            return GetOperationSchema(action);
        }

        private ExplorerItem GetOperationSchema(IEdmOperation operation)
        {

            var returnType = operation.ReturnType;
            List<ExplorerItem> children = new List<ExplorerItem>();

            string tooltip = "";
            string itemDescr = operation.Name;
            ExplorerIcon icon = operation.IsAction() ? ExplorerIcon.StoredProc : ExplorerIcon.ScalarFunction;
            string feedType = "Operation"; // operation.IsFunction() ? "Function" : "Action";

            feedType += operation.IsBound ? "Bound" : "Unbound";

            if (returnType != null)
            {
                var definition = returnType.Definition;

                tooltip = GetExplorerItemTooltip(definition);
                itemDescr = GetExplorerItemDescription(operation.Name, definition);

                children.Add(
                        GetReturnTypeSchema(returnType.Definition)
                );
            }

            if (operation.Parameters.Count() > 0)
            {
                children.Add(
                    GetParametersSchema(operation.Parameters)
                );

                if ( operation.IsBound )
                {

                }
                if ( operation.Parameters.FirstOrDefault().Type.IsEntity() )
                {
                    feedType += "Entity";
                }
                else if (
                        operation.Parameters.FirstOrDefault().Type.IsCollection() &&
                        operation.Parameters.FirstOrDefault().Type.Definition.AsElementType().TypeKind == EdmTypeKind.Entity
                    )
                {
                    feedType += "CollectionEntity";
                }

            }

            var item = CreateExplorerItem(itemDescr, ExplorerItemKind.QueryableObject, icon, tooltip);

            item.Children = children;
            item.Tag = GetTagForOperation(operation, feedType );

            return item;
        }

        private ExplorerItem GetReturnTypeSchema(IEdmType definition)
        {

            string itemDescr = "Return: - " + GetTypeDescription(definition, false);
            var item = CreateExplorerItem(itemDescr, ExplorerItemKind.Category, ExplorerIcon.Schema);

            item.ToolTipText = GetTypeDescription(definition, _multiNS);

            item.Children = GetEdmTypeSchema(definition);

            return item;
        }

        private ExplorerItem GetParametersSchema(IEnumerable<IEdmOperationParameter> parameters)
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

        private ExplorerItem GetParameterSchema(IEdmOperationParameter p)
        {
            var item = CreateExplorerItem(p.Name + " - " + GetTypeDescription(p.Type.Definition, false), ExplorerItemKind.Parameter, ExplorerIcon.Parameter);
            var children = new List<ExplorerItem>();

            children.AddRange(GetEdmTypeSchema(p.Type.Definition));

            item.ToolTipText = GetTypeDescription(p.Type.Definition, true);

            item.Children = children;

            return item;
        }

        private List<ExplorerItem> GetEdmTypeSchema(IEdmType edmType)
        {
            EdmTypeKind typeKind = edmType.TypeKind;
            List<ExplorerItem> schema = new List<ExplorerItem>();

            switch (typeKind)
            {
                case EdmTypeKind.Complex:
                    schema = GetComplexTypeSchema(edmType as IEdmComplexType);
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

        private ExplorerItem GetNavigationSourcechema(IEdmNavigationSource navSource)
        {
            string itemDescr = GetExplorerItemDescription(navSource.Name, navSource.EntityType());
            string tooltip = GetExplorerItemTooltip(navSource.EntityType());

            ExplorerItem item = CreateExplorerItem(itemDescr, ExplorerItemKind.QueryableObject, ExplorerIcon.Table, tooltip);


            item.Children = GetEntityTypeSchema(navSource.EntityType());

            return item;
        }

        private List<ExplorerItem> GetEntityTypeSchema(IEdmEntityType entityType)
        {
            return GetStructuredTypeSchema(entityType);
        }

        private List<ExplorerItem> GetStructuredTypeSchema(IEdmStructuredType structuredType)
        {
            List<ExplorerItem> children = new List<ExplorerItem>();

            string FullName = structuredType.FullTypeName();
            if (!_stack.Contains(FullName) && _stack.Count < _maxStackDepth)
            {
                _stack.Push(FullName);

                children.AddRange(
                     GetStructuralPropertiesSchema(structuredType)
                );

                children.AddRange(
                     GetNavigationPropertiesSchema(structuredType)
                );

                children.AddRange(
                        _model
                        .SchemaElements
                        .OfType<IEdmFunction>()
                        .OrderBy(f => f.Name)
                        .Where( f => f.IsBound &&
                                     f.Parameters.Any() &&
                                     f.Parameters.FirstOrDefault().Type.Definition.AsElementType().TypeKind == EdmTypeKind.Entity &&
                                     (f.Parameters.FirstOrDefault().Type.Definition.AsElementType() as IEdmStructuredType) == structuredType
                                     )
                        .Select(f => GetFunctionSchema(f) )
                    );

                children.AddRange(
                        _model
                        .SchemaElements
                        .OfType<IEdmAction>()
                        .OrderBy(f => f.Name)
                        .Where(f => f.IsBound &&
                                    f.Parameters.Any() &&
                                    f.Parameters.FirstOrDefault().Type.Definition.AsElementType().TypeKind == EdmTypeKind.Entity &&
                                    (f.Parameters.FirstOrDefault().Type.Definition.AsElementType() as IEdmStructuredType) == structuredType
                                     )
                        .Select(f => GetActionSchema(f))
                );

/*

                var operationsSchema = 
                            _model
                            .SchemaElements
                            .OfType<IEdmOperation>()
                           .OrderBy(op => op.IsAction())
                                .ThenBy(op => op.Name)
                           .Where(op => op.IsBound &&
                                        op.Parameters.FirstOrDefault() != null &&
                                        op.Parameters.FirstOrDefault().Type.IsEntity() &&
                                       ((op.Parameters.FirstOrDefault().Type.Definition as IEdmStructuredType) == structuredType)
                                  )
                            .Select(op => GetOperationSchema(op))
                            .ToList();

                operationsSchema.AddRange(
                                   _model
                                   .SchemaElements
                                   .OfType<IEdmOperation>()
                                   .OrderBy(f => f.IsAction())
                                        .ThenBy(f => f.Name)
                                   .Where(f => f.Parameters.FirstOrDefault() != null &&
                                               f.Parameters.FirstOrDefault().Type.IsCollection() &&
                                               ((f.Parameters.FirstOrDefault().Type.Definition as IEdmCollectionType).ElementType.Definition == structuredType)
                                          )
                                    .Select(f => GetOperationSchema(f))
                                    .ToList()
                    );


                if (operationsSchema.Any())
                    children.AddRange(
                            operationsSchema
                        );
*/

                string lastInStack = "";
                _stack.TryPop(out lastInStack);
                if (lastInStack != FullName)
                {
                    throw new Exception($"Last in stack is {lastInStack}, expected {FullName}");
                }
            }

            return children;
        }

        private List<ExplorerItem> GetCollectionTypeSchema(IEdmCollectionType collectionType)
        {
            IEdmType edmType = collectionType.ElementType.Definition;

            EdmTypeKind typeKind = edmType.TypeKind;

            switch (typeKind)
            {
                case (EdmTypeKind.Complex):
                    return GetComplexTypeSchema(edmType as IEdmComplexType);
                case (EdmTypeKind.Entity):
                    return GetEntityTypeSchema(edmType as IEdmEntityType);
                default:
                    return null;
            }
        }

        private List<ExplorerItem> GetComplexTypeSchema(IEdmComplexType complexType)
        {
            return GetStructuredTypeSchema(complexType);
        }

        private ExplorerItem GetPropertySchema(IEdmProperty p, ExplorerItemKind kind, ExplorerIcon icon)
        {
            string tooltip = GetExplorerItemTooltip(p);
            string itemDescr = GetExplorerItemDescription(p);

            ExplorerItem item = CreateExplorerItem(itemDescr, kind, icon, tooltip);

            return item;
        }


        private List<ExplorerItem> GetNavigationPropertiesSchema(IEdmStructuredType structuredType)
        {
            return structuredType
                    .DeclaredNavigationProperties()
                    .OrderBy(p => p.Name)
                    .Select
                        (p => GetNavigationPropertySchema(p)
                    )
                    .ToList();
        }

        private List<ExplorerItem> GetStructuralPropertiesSchema(IEdmStructuredType structuredType)
        {

            return structuredType
                    .DeclaredStructuralProperties()
                    .OrderByDescending(p => p.IsKey())
                        .ThenBy(p => p.Name)
                    .Select
                        (p => GetStructuralPropertySchema(p)
                    )
                    .ToList();
        }

        private ExplorerItem GetNavigationPropertySchema(IEdmNavigationProperty prop)
        {

            ExplorerIcon icon;
            ExplorerItemKind kind;

            //     var typesInEntitySets = _model.EntityContainer.EntitySets().Select(e => new { FullName = e.EntityType().FullName() } );
            var partnerType = prop.ToEntityType();
            var backReferenceType = partnerType.DeclaredNavigationProperties()
                             .FirstOrDefault(o => o.ToEntityType() == prop.DeclaringEntityType());

            switch (prop.TargetMultiplicity())
            {
                case EdmMultiplicity.ZeroOrOne:
                case EdmMultiplicity.One:
                    if (backReferenceType != null && backReferenceType.Type.IsCollection())
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
                    if (backReferenceType != null && backReferenceType.Type.IsCollection())
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

            ExplorerItem item = GetPropertySchema(prop, kind, icon);

            switch (prop.Type.TypeKind())
            {
                case EdmTypeKind.Collection:
                    item.Children = GetCollectionTypeSchema(prop.Type.Definition as IEdmCollectionType);
                    break;
                case EdmTypeKind.Complex:
                    IEdmComplexType complexType = prop.Type.Definition as IEdmComplexType;
                    item.Children = GetComplexTypeSchema(complexType);
                    break;
                case EdmTypeKind.Entity:
                    IEdmEntityType entityType = prop.Type.Definition as IEdmEntityType;
                    item.Children = GetEntityTypeSchema(entityType);
                    break;
                default:
                    break;

            }

            return item;
        }




        private ExplorerItem GetStructuralPropertySchema(IEdmStructuralProperty prop)
        {
            ExplorerIcon icon = prop.IsKey() ? ExplorerIcon.Key : ExplorerIcon.Column;
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

                case EdmTypeKind.TypeDefinition:
                    var typedefTypeName = returnFull ? (type as IEdmTypeDefinition).FullName() : (type as IEdmTypeDefinition).Name;
                    return $"TypDef({typedefTypeName})";

                case EdmTypeKind.Untyped:
                    var untypedTypeName = returnFull ? (type as IEdmUntypedType).FullName() : (type as IEdmUntypedType).Name;
                    return $"Untyped({untypedTypeName})";
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
