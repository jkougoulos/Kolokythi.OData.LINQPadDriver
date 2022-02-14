using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

using LINQPad.Extensibility.DataContext;
using System.Text.RegularExpressions;

namespace Kolokythi.OData.LINQPadDriver
{
	public partial class ConnectionDialog : Window
	{
		private readonly ConnectionProperties _connectionProperties;

		public ConnectionDialog (ConnectionProperties connectionProperties)
		{
			_connectionProperties = connectionProperties;

			// ConnectionProperties is your view-model.
			//			DataContext = new ConnectionProperties (cxInfo);
			InitializeComponent();

			DataContext = _connectionProperties;

			var customHeaders = _connectionProperties.ConnCustomHeaders;

			foreach (var item in customHeaders)
			{
				CustomHeaders.Add(new CustomHeader { Name = item.Key, Value = item.Value });
			}

			CHeaders.DataContext = this;

			
		}

		private void Remove_Click(object sender, RoutedEventArgs e)
		{
			if (SelectedCustomHeader != null && CustomHeaders.Contains(SelectedCustomHeader))
			{
				CustomHeaders.Remove(SelectedCustomHeader);
			}
		}

		private async void Test_Connection(object sender, RoutedEventArgs e)
		{
			//System.Diagnostics.Debugger.Launch();
			var res = DataContextDriver.TestConnection( _connectionProperties.ConnectionInfo, out _ );
			

			if (res != null)
			{

				MessageBox.Show(res, "Test Failed!");
			}
			else
            {
				var resOData = await DynamicDriver.ODataTestConnection( _connectionProperties.ConnectionInfo );
				if (resOData != null)
				{
					MessageBox.Show(resOData, "OData test failed!");
				}
				else
				{
					MessageBox.Show("OK!", "All tests Successful!");
				}
//				MessageBox.Show(_connectionProperties.ConnectionInfo.GetConnectionProperties().Uri, "Test Successful!");
			}

		}

		public ObservableCollection<CustomHeader> CustomHeaders { get; set; } = new ObservableCollection<CustomHeader>();

		public CustomHeader SelectedCustomHeader { get; set; }

		public class CustomHeader
		{
			public string Name { get; set; }

			public string Value { get; set; }
		}


		private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
		{
			_connectionProperties.Password = PasswordBox.Password;

		}

		private void CertPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
		{
			_connectionProperties.CertificatePassword = CertPasswordBox.Password;
		}

		private static readonly Regex _regex = new Regex("[^0-9]+"); //regex that matches disallowed text
		private void NumberValidationTextbox(object sender, TextCompositionEventArgs e)
		{
			e.Handled = _regex.IsMatch(e.Text);
		}

		void btnOK_Click (object sender, RoutedEventArgs e)
		{
			try
			{
					var customHeaders = new List<KeyValuePair<string, string>>();
					foreach (var item in CustomHeaders.Where(s => !string.IsNullOrEmpty(s.Name)))
					{
						customHeaders.Add(new KeyValuePair<string, string>(item.Name, item.Value));
					}
					_connectionProperties.ConnCustomHeaders = customHeaders;
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Something went wrong \n{ex.Message}. Please try again!");
			}

			DialogResult = true;
		}		
	}
}