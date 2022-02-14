using LINQPad.Extensibility.DataContext;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Xml.Linq;
using System.Linq;

namespace Kolokythi.OData.LINQPadDriver
{
	/// <summary>
	/// Wrapper to read/write connection properties. This acts as our ViewModel - we will bind to it in ConnectionDialog.xaml.
	/// </summary>
	public class ConnectionProperties
	{
		public IConnectionInfo ConnectionInfo { get; private set; }
		private readonly XElement _driverData;

		public ConnectionProperties (IConnectionInfo cxInfo)
		{
			ConnectionInfo = cxInfo;
			_driverData = cxInfo.DriverData;
		}

		public string LastError {
			get => (string)_driverData.Element("LastError");
			set => _driverData.SetElementValue("LastError", value);

		}

		public string DisplayName
		{
			get => ConnectionInfo.DisplayName;
			set => ConnectionInfo.DisplayName = value;
		}

		public string Uri
        {
           	get => (string)_driverData.Element("Uri");
            set => _driverData.SetElementValue("Uri", value);
        }

		public string UserName
		{
			get => (string)_driverData.Element("UserName") == null ? "Default" : (string)_driverData.Element("UserName");
			set => _driverData.SetElementValue("UserName", value );
		}

		public string Password
		{
			get => ConnectionInfo.Decrypt((string)_driverData.Element("Password") );
			set => _driverData.SetElementValue("Password", ConnectionInfo.Encrypt(value) );
		}

		public string CertificateFileName
		{
			get => (string)_driverData.Element("CertificateFileName");
			set => _driverData.SetElementValue("CertificateFileName", value);
		}

		public string CertificatePassword
		{
			get => ConnectionInfo.Decrypt((string)_driverData.Element("CertificatePassword"));
			set => _driverData.SetElementValue("CertificatePassword", ConnectionInfo.Encrypt(value));
		}

		public bool MultiNSSupport
        {
			get => _driverData.Element("MultiNSSupport") == null ? false : (bool)_driverData.Element("MultiNSSupport");
			set => _driverData.SetElementValue("MultiNSSupport", value);
		}

		public bool ODataTrace
		{
			get => _driverData.Element("ODataTraceAll") == null ? false : (bool)_driverData.Element("ODataTraceAll");
			set => _driverData.SetElementValue("ODataTraceAll", value);
		}

		public bool NativeSOC
		{
			get => _driverData.Element("NativeSOC") == null ? true : (bool)_driverData.Element("NativeSOC");
			set => _driverData.SetElementValue("NativeSOC", value);
		}

		public bool IncludeAnnotations
		{
			get => _driverData.Element("IncludeAnnotations") == null ? true : (bool)_driverData.Element("IncludeAnnotations");
			set => _driverData.SetElementValue("IncludeAnnotations", value);
		}

		public bool ForceJson
        {
			get => _driverData.Element("ForceJson") == null ? true : (bool)_driverData.Element("ForceJson");
			set => _driverData.SetElementValue("ForceJson", value);
		}

		public bool Ignore404Exception
		{
			get => _driverData.Element("Ignore404Exception") == null ? true : (bool)_driverData.Element("Ignore404Exception");
			set => _driverData.SetElementValue("Ignore404Exception", value);
		}

		public int stackDepth
        {
			get => _driverData.Element("stackDepth") == null ? 2 : int.Parse((string)_driverData.Element("stackDepth"));
			set => _driverData.SetElementValue("stackDepth", value.ToString() );

		}

		public IEnumerable<KeyValuePair<string, string>> ConnCustomHeaders
		{
			get
			{
				var element = _driverData.Element("CustomHeaders");
				if (element == null)
					return new KeyValuePair<string, string>[0];

				return element.Elements("Header")
					.Select(x => new KeyValuePair<string, string>((string)x.Attribute("Name") ?? "",
						(string)x.Attribute("Value") ?? ""))
					.Where(x => !string.IsNullOrWhiteSpace(x.Key));
			}
			set
			{
				var headers = value?.Where(x => !string.IsNullOrWhiteSpace(x.Key))
					.Select(x => new XElement("Header",
						new XAttribute("Name", x.Key.Trim()),
						new XAttribute("Value", (x.Value ?? "").Trim())));

				_driverData.Elements("CustomHeaders").Remove();
				_driverData.Add(new XElement("CustomHeaders", headers));
			}
		}

		public NameValueCollection GetConnCustomHeaders()
		{
			var headers = new NameValueCollection();
			foreach (var header in ConnCustomHeaders)
			{
				headers.Add(header.Key, header.Value);
			}

			return headers;
		}

		// This is how to create custom connection properties.

		//public string SomeStringProperty
		//{
		//	get => (string)DriverData.Element ("SomeStringProperty") ?? "";
		//	set => DriverData.SetElementValue ("SomeStringProperty", value);
		//}

		//public int SomeIntProperty
		//{
		//	get => (int?)DriverData.Element ("SomeIntProperty") ?? 0;
		//	set => DriverData.SetElementValue ("SomeIntProperty", value);
		//}
	}
}