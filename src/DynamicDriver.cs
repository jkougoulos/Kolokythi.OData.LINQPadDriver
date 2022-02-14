using LINQPad;
using LINQPad.Extensibility.DataContext;


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

using System.Security.Cryptography.X509Certificates;

using Simple.OData.Client;

using System.Text.Json;
using System.Text.Json.Serialization;


using System.Threading.Tasks;

using OData2Poco.Api;
using System.IO;
using System.Net.Http;


using Uncapsulator;
using static Uncapsulator.TypeUncapsulator;


namespace Kolokythi.OData.LINQPadDriver
{
    public class DynamicDriver : DynamicDataContextDriver
    {

        private string _metadata;
//        private List<string> _namespaces; 
        private bool _multiNS;
        private bool _nativeSOC;

        static DynamicDriver()
        {
            //Debugger.Launch();
            // Uncomment the following code to attach to Visual Studio's debugger when an exception is thrown:
          
//            AppDomain.CurrentDomain.FirstChanceException += (sender, args) =>
//            			{
//            				if (args.Exception.StackTrace.Contains ("Kolokythi.OData.LINQPadDriver"))	Debugger.Launch ();
//            			};
   
        }

        public override string Name => "Kolokythi OData v1-v4 Driver";

        public override string Author => "John Kougoulos";

        public override string GetConnectionDescription(IConnectionInfo cxInfo)
            => "Connection description";

        public async static Task<string> ODataTestConnection(IConnectionInfo cxInfo)
        {
//            ODataClientSettings _settings;
            try
            {
                ODataClientSettings _settings = GetODataClientSettings(cxInfo);
                ODataClient _client = new ODataClient(_settings);
                _ = await _client.GetMetadataAsStringAsync();
            }
            catch (Exception ex)
            {
                return ex.ToString(); // ex.Message;
            }

            return null;

        }

        public override bool ShowConnectionDialog(IConnectionInfo cxInfo, ConnectionDialogOptions dialogOptions)
        {
            var connProps = cxInfo.GetConnectionProperties();

            if( dialogOptions.IsNewConnection)
                connProps.Uri = "https://services.odata.org/TripPinRESTierService/";

            return new ConnectionDialog(connProps).ShowDialog() == true;
        }


        public override void InitializeContext(IConnectionInfo cxInfo, object context,
                                                QueryExecutionManager executionManager)
        {
            var oDataClient = (ODataClient)context;

            ODataClientSettings _settings = oDataClient.Uncapsulate()._settings;
       
            _settings.OnTrace = (x, y) => executionManager.SqlTranslationWriter.WriteLine(string.Format(x, y));

            if ( cxInfo.GetConnectionProperties().ODataTrace )
            {
                _settings.BeforeRequest += delegate (HttpRequestMessage message)
                {
                    executionManager.SqlTranslationWriter.Write($"\nRequest Headers:\n{message.Headers.ToString()}");
                };

                _settings.AfterResponse += delegate (HttpResponseMessage message)
                {
                    executionManager.SqlTranslationWriter.Write($"\nResponse Headers:\n{message.Headers.ToString()}");
                };

            }

        }
        

        private static ODataClientSettings GetODataClientSettings(IConnectionInfo connectionInfo)
        {
            ConnectionProperties connectionProperties = connectionInfo.GetConnectionProperties();

            ODataClientSettings settings = new ODataClientSettings(new Uri(connectionProperties.Uri));

            settings.IgnoreResourceNotFoundException = connectionProperties.Ignore404Exception;
            if ( connectionProperties.ForceJson)  settings.PayloadFormat = ODataPayloadFormat.Json;   
            if ( connectionProperties.ODataTrace)  settings.TraceFilter = ODataTrace.All;            
            settings.IncludeAnnotationsInResults = connectionProperties.IncludeAnnotations;



            if (!string.IsNullOrEmpty(connectionProperties.UserName))
            {
                if( connectionProperties.UserName == "Default")
                {
                    settings.Credentials = System.Net.CredentialCache.DefaultCredentials;
                }
                else
                {
                    settings.Credentials = new System.Net.NetworkCredential(connectionProperties.UserName, connectionProperties.Password);
                }
            }

            if ( connectionProperties.ConnCustomHeaders.Any() )
            {
                settings.BeforeRequest += delegate (HttpRequestMessage message)
                {
                    foreach ( var header in connectionProperties.ConnCustomHeaders )
                    {
                        message.Headers.Add(header.Key, header.Value);
                    }
                };
            }

            if ( !string.IsNullOrEmpty(connectionProperties.CertificateFileName) && !string.IsNullOrEmpty( connectionProperties.CertificatePassword) )
            {
                if ( File.Exists( connectionProperties.CertificateFileName ) )
                {
                    var certbytes = File.ReadAllBytes(connectionProperties.CertificateFileName);
                    X509Certificate2 cert = new X509Certificate2(certbytes, connectionProperties.CertificatePassword);
                    settings.OnCreateMessageHandler = () => GetClientHandler(cert);
                }
                else
                {
                    throw new FileNotFoundException("Certificate file not found", connectionProperties.CertificateFileName);
                }
            }

            return settings;
        }

        private static HttpClientHandler GetClientHandler(X509Certificate2 certificate = null)
        {
            HttpClientHandler clientHandler = new HttpClientHandler();

            if (certificate != null)
            {
                clientHandler.ClientCertificates.Add(certificate);
                clientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
            }

            return clientHandler;
        }

        private string GetHelperFromExplorerItem(ExplorerItem explorerItem)
        {
            if (explorerItem.Tag != null)
            {
                (string feedType, string entityName, string entityType) = (ValueTuple<string, string, string>)explorerItem.Tag;

                switch (feedType)
                {
                    case "EntitySet":
                        return $"       public IEnumerable<{entityType}> {entityName} => this.For<{entityType}>(\"{entityName}\").FindEntriesAsync().GetAwaiter().GetResult();" + '\n';
                    case "Singleton":
                        return $"       public {entityType} {entityName} => this.For<{entityType}>(\"{entityName}\").FindEntryAsync().GetAwaiter().GetResult();" + '\n';
                }
            }

            return "";
        }

        public override List<ExplorerItem> GetSchemaAndBuildAssembly(
            IConnectionInfo cxInfo, AssemblyName assemblyToBuild, ref string nameSpace, ref string typeName)
        {

            _multiNS = cxInfo.GetConnectionProperties().MultiNSSupport;
            _nativeSOC = cxInfo.GetConnectionProperties().NativeSOC;
            
            var visSchema = GetMySchema( cxInfo ).GetAwaiter().GetResult();
            

            Type baseType = typeof(ODataClient); 

            // Start create types using Odata2Poco
            string codeForTypes = GenerateTypes();

            string helpers = GetHelpersFromSchema(visSchema);
            //            helpers = "";   // for testing

            string sourceMain = GetMainSource(nameSpace, typeName, baseType, helpers);

            string typeDefinitions;

            if (!_multiNS)
            {
                typeDefinitions = $@"
namespace {nameSpace}
{{
{codeForTypes}
}}
";
            }
            else
            {
                typeDefinitions = $@"
{codeForTypes}
";
            
            }


            string selfSource = GetThisSource( nameSpace, typeName, baseType, sourceMain + typeDefinitions );

            string[] refPaths = GetCoreFxReferenceAssemblies().Union(new[] {
                            typeof(Microsoft.Spatial.GeographyPoint).Assembly.Location,
                            baseType.Assembly.Location
                        }).ToArray();

       
            string[] finalCodeArr = new[] {
                sourceMain,
                typeDefinitions,
                selfSource,
//                "\n/* refPaths\n",
//                string.Join(Environment.NewLine, refPaths),
//                "\n*/\n"
            };

            string finalCode = string.Join("", finalCodeArr);


/*         // for debugging 
 *         
            string tmpfile = Path.GetTempPath() + Guid.NewGuid() + ".cs";
            File.WriteAllText(tmpfile, finalCode);
*/

            var result = CompileSource(new CompilationInput
            {
                SourceCode = new[] { finalCode },
                FilePathsToReference = refPaths,
                OutputPath = assemblyToBuild.CodeBase,
            });

            if (result.Errors.Any())
            {
                visSchema.AddRange(
                        result.Errors.Select(error => 
                                    new ExplorerItem("Error:" + error, ExplorerItemKind.Parameter, ExplorerIcon.Blank)
                                )
                    );
                
            }
       
            return visSchema;
        }

        private string GetMainSource(string nameSpace, string typeName, Type baseType, string helpers)
        {
            string source = $@"
using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Spatial;
using Simple.OData.Client;
using System.Xml.Linq;


namespace {nameSpace}
{{
	public partial class {typeName} : {baseType.FullName}
	{{

        public readonly string MetaData;
        
{helpers}

        public {typeName}( ODataClientSettings settings ) : base(settings)
        {{

            string meta = this.GetMetadataDocumentAsync().GetAwaiter().GetResult();
            MetaData = XDocument.Parse(meta).ToString();

        }}
		
	}}
}}
";
            return source;
        }

        private string GetHelpersFromSchema(List<ExplorerItem> visSchema)
        {
            string helpers = "";
            
            
            foreach (var explorerItem in visSchema)
            {

                if (explorerItem.Icon == ExplorerIcon.Box)
                {
                    foreach (var boxItem in explorerItem.Children)
                    {
                        helpers += GetHelperFromExplorerItem(boxItem);
                    }

                }
                else
                {
                    helpers += GetHelperFromExplorerItem(explorerItem);
                }

            }
            return helpers;
        }

        private string GetThisSource(string nameSpace, string typeName, Type baseType, string sourceCode)
        {

            string thissource = $@"
namespace {nameSpace}
{{
    public partial class {typeName} : {baseType.FullName}
	{{
        public string SourceCode = @""
{sourceCode.Replace("\"", "\"\"")}
""; 

    }}
}}

";
            return thissource;
        }


        public override IEnumerable<string> GetNamespacesToAdd(IConnectionInfo connectionInfo)
        {
            return new[] { "Simple.OData.Client" };
        }
        
        
                
        public override IEnumerable<string> GetAssembliesToAdd(IConnectionInfo connectionInfo)
        {

            string[] Assemblies = new[]
            {
                "Simple.OData.Client.Core.dll"
            };
            // We need the following assembly for compilation and auto-completion:
            return Assemblies;
        }
       
        public override ParameterDescriptor[] GetContextConstructorParameters(IConnectionInfo cxInfo)
              => new[]
                      {
                        new ParameterDescriptor ( "settings" , "Simple.OData.Client.ODataClientSettings")
                      };

        public override object[] GetContextConstructorArguments(IConnectionInfo cxInfo)
        {
  
            return new object[]
                              {
                                 GetODataClientSettings(cxInfo)
                              };
        }


        private string GenerateTypes()
        { 

            // ugly hack, o2p expects a path to a file or url, no metadata in string
            string tmpfile = Path.GetTempPath() + Guid.NewGuid() + ".xml";
            File.WriteAllText(tmpfile, _metadata);

            var o2pconnstring = new OData2Poco.OdataConnectionString
            {
                ServiceUrl = tmpfile
            };

            var o2psetting = new OData2Poco.PocoSetting
            {
                AddNavigation = true,
                AddNullableDataType = true

            };

            var o2p = new O2P(o2psetting);

            string theCode = o2p.GenerateAsync(o2pconnstring).GetAwaiter().GetResult();
            
            File.Delete(tmpfile);

            // here is an ugly hack to remove service namespaces if needed and "using xxx" from o2p
            string[] theCodeInLines = theCode
                                        .Split(new string[] { Environment.NewLine }, StringSplitOptions.None)
                                        .Where(x => !x.StartsWith("using"))
                                        .Where(x => !(x.StartsWith("namespace") && !_multiNS))
                                        .Where(x => !(x.StartsWith("{") && !_multiNS))
                                        .Where(x => !(x.StartsWith("}") && !_multiNS))
                                        .ToArray();

            string theCodeFinal = string.Join(Environment.NewLine, theCodeInLines);
            theCodeFinal = theCodeFinal.Replace("{get;}", "{get;set;}"); // workaround for o2p

            return theCodeFinal;
        }


        private async Task<List<ExplorerItem>> GetMySchema(IConnectionInfo cxInfo)
        {
            ODataClient _client = new ODataClient(GetODataClientSettings(cxInfo));

            _metadata = await _client.GetMetadataAsStringAsync();
            var model = await _client.GetMetadataAsync();
            

            var schema = EdmModelToSchemaRepairShop.GetRootSchema(model, _multiNS, _nativeSOC, cxInfo.GetConnectionProperties().stackDepth) ;

            return schema;
        }
    }
}