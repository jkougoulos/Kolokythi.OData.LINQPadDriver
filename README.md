# Kolokythi.OData.LINQPadDriver
An OData v1-v4 Driver for LINQPad based on Simple.OData.Client

## Disclaimer
I do not know C#/.NET, but I can write bits and pieces in C#/.NET. There are probably a lot of bugs, probably more than [250](http://www.ganssle.com/tem/tem299.html#article2). Hopefully I will learn a bit better the various [patterns](https://en.wikipedia.org/wiki/Design_Patterns) and the features of the language, resulting into less code -> less bugs!

I have used several parts from another ODataV4 LINQPad driver (https://github.com/meancrazy/LINQPadOData4), especially the part of handling the connection dialogue.

## Description

TL;DR  
This driver will show you the structure in LINQPad of the OData service and the associated types, functions & actions and create the POCOs to consume the OData feeds using the typed syntax of Simple.OData.Client

I have been using LINQPad for several years, but I was disappointed a bit because I could access OData v2-v3 services only using LINQPad 5, but there were also some issues there. ODataV4 support was feasible using LINQPad 7 with the above mentioned (unmaintained) driver.
Therefore I decided to spend some time and learn how to write a driver myself.

Now comes the important part. This driver uses [Simple.OData.Client](https://github.com/simple-odata-client/Simple.OData.Client) (SOC) to run the queries, custom code to build the schema in LINQPad left pane and [odata2poco](https://github.com/moh-hassan/odata2poco) to create the types.

SOC has async methods and does not return IQueryable. (https://github.com/simple-odata-client/Simple.OData.Client/issues/271) 
As such, it is preferable to use the native way to access the data (see https://www.odata.org/blog/advanced-odata-tutorial-with-simple-odata-client/), instead of using the LINQPad way of eg providing only the EntitySet name, as this will translate into getting the full dataset and filtering/sorting locally.

In the SQL tab you can monitor the OData requests (thanks to [Uncapsulator](https://github.com/albahari/uncapsulator))

## Other Features

### DataContext source code
```
this.SourceCode
```
will provide the source code used by the driver when accessing the specific OData service. The code includes the POCO definitions, so you can use it as a class in your project by providing only the ODataClientSettings in the constructor.  
Another nice feature is that you can copy paste the code into a new LINQPad query, add via NuGet the "Simple.OData.Client" and now you don't even need the driver.  
For the moment you will have to create the ODataClientSettings to pass to the constructor.
TODO: provide example

### Service Metadata
```
this.MetaData
```
will provide you with the metadata of the service for troubleshooting or taking a closer look in the service definition

## TODO
- Test more services
- Fix the translation between OData primitive types and C# types
- Test ODataV4 bound functions with Collection of EntityType parameter
- Add a setting for proxy
- Test if some static methods introduce any issues 

## Options

### Friendly Name
Connection Name to display in LINQPad left pane

### Uri for service
The URI of the OData service

### Username
Username to use for the authenticating to the service.
When empty it will not try to authenticate
When "Default" is specified, Default windows credentials will be used
When anything else, it will be the username for Basic authentication (maybe it works also for NTLM, to be tested)

### Password
The password to use in combination with the above defined username. When Default username is used, password can be left empty.
Note: Password will be encrypted with WindowsDPAPI, therefore if the .linq file is transferred to a different host, password needs to be defined again. As a workaround, one may specify username empty and provide "Authorization" as additional header with based64 encoded the string "username:password" 

### Certificate file
Path to a .pfx file containing the certificate and private key

### Certificate Password
Password used to create the .pfx file. This attribute is also encrypted using DPAPI in .linq file, no workaround available

### Remember this connection
TOCHECK

### Use service namespaces
If checked, the driver will use the namespaces defined in the OData service for type names. Otherwise, all types will be created under the (default) LINQPad.User namespace.
This option is useful in cases where the OData type definition is a bit complicated and there are conflicts in the short names of the types. A side effect is that whenever you need to use a specific type, you will need to provide the fully qualified name

### Native Simple OData Client in menu
Right click on an entityset will provide the SOC code instead of just the EntitySet name. Function & Actions menu shortcuts are available in this format for now. 

### Include OData Annotations
This option is required if you plan to use OData annotations (eg for paging)

### Full OData Trace
It will display in the SQL tab also the exchanged headers and content

### Use Json
Prefer JSON payload format

### Ignore resource not found exception
Choose if a 404 error, for example in case you request by key an entity and it does not exist, will trigger an exception

### Discovery levels
How many levels deep will be the display of entity types, complex types. 

### Additional headers
Add headers that should be included in each request. Examples include "sap-client" header for choosing the SAP client, "DataServiceVersion" to request from the remote service to use a specific OData version, API keys etc

### Test connection
It will do driver checks (initialize the Datacontext) and fetch the metadata file from the service as a verification of the connectivity
