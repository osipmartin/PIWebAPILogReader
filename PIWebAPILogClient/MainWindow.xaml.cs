using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PIWebAPI.LogReader;
using OSIsoft.AF;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.IO;
using OSIsoft.AF.EventFrame;
using OSIsoft.AF.Asset;
using System.Threading;

namespace PIWebAPILogClient
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public PISystem server;
		public AFDatabase db;

		private CancellationTokenSource ct;

		public enum SaveAsOptions { EventFrame, Text, Console };
		public enum LoadFromOptions { Static, Live };

		private SaveAsOptions SaveAsOption;
		private LoadFromOptions LoadFromOption;

		public MainWindow()
		{
			InitializeComponent();
		}

		private void SaveAs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			(SaveAs.SelectedItem as DropdownOption).OnSelect();
		}

		private void SaveAs_Loaded(object sender, RoutedEventArgs e)
		{
			SaveAs.ItemsSource = new [] {
				new DropdownOption(
					"Event Frame",
					() => {
						FileOutput.IsEnabled = false;
						FileOutputButton.IsEnabled = false;

						AFServer.IsEnabled = true;
						AFDatabase.IsEnabled = true;

						MinSeconds.IsEnabled = true;

						SaveAsOption = SaveAsOptions.EventFrame;
					}
				),
				new DropdownOption(
					"Text File",
					() => {
						FileOutput.IsEnabled = true;
						FileOutputButton.IsEnabled = true;

						AFServer.IsEnabled = false;
						AFDatabase.IsEnabled = false;

						MinSeconds.IsEnabled = true;

						SaveAsOption = SaveAsOptions.Text;
					}
				),
				new DropdownOption(
					"Console Only",
					() => {
						FileOutput.IsEnabled = false;
						FileOutputButton.IsEnabled = false;

						AFServer.IsEnabled = false;
						AFDatabase.IsEnabled = false;

						MinSeconds.IsEnabled = true;

						SaveAsOption = SaveAsOptions.Console;
					}
				),
			};
		}

		private void LoadFrom_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			(LoadFrom.SelectedItem as DropdownOption).OnSelect();
		}

		private void LoadFrom_Loaded(object sender, RoutedEventArgs e)
		{
			LoadFrom.ItemsSource = new[] {
				new DropdownOption(
					"Live Log",
					() => {
						LogFile.IsEnabled = false;
						LogFileButton.IsEnabled = false;
						
						MachineName.IsEnabled = true;
						StartWatchButton.IsEnabled = true;
						StopWatchButton.IsEnabled = true;

						ParseButton.IsEnabled = true;
						StartTime.IsEnabled = true;
						EndTime.IsEnabled = true;

						LoadFromOption = LoadFromOptions.Live;
					}
				),
				new DropdownOption(
					"Saved Log",
					() => {
						LogFile.IsEnabled = true;
						LogFileButton.IsEnabled = true;

						MachineName.IsEnabled = false;
						StartWatchButton.IsEnabled = false;
						StopWatchButton.IsEnabled = false;

						ParseButton.IsEnabled = true;
						StartTime.IsEnabled = true;
						EndTime.IsEnabled = true;

						LoadFromOption = LoadFromOptions.Static;
					}
				),
			};
		}

		private void AFServer_Loaded(object sender, RoutedEventArgs e)
		{
			PISystems ps = new PISystems();
			AFServer.ItemsSource = ps;
			AFServer.DisplayMemberPath = "Name";
			AFServer.SelectedItem = ps.DefaultPISystem;
		}

		private void AFServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			server = AFServer.SelectedItem as PISystem;

			AFDatabase.ItemsSource = server.Databases;
			AFDatabase.DisplayMemberPath = "Name";
			AFDatabase.SelectedItem = server.Databases.DefaultDatabase;

			db = server.Databases.DefaultDatabase;
		}

		private void AFDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			db = AFDatabase.SelectedItem as AFDatabase;
		}

		private void Parse(object sender, RoutedEventArgs e)
		{
			DateTime startTime;
			bool st = DateTime.TryParse(StartTime.Text,out startTime);
			if(!st) {
				startTime = DateTime.Now;
			}

			DateTime endTime;
			bool et = DateTime.TryParse(EndTime.Text, out endTime);
			if (!et)
			{
				endTime = DateTime.Now;
			}

			string s = LogQueryBuilder.Build(
				new List<int> { 4 },
				new List<int> { 11, 12 },
				startTime,
				endTime
				);

			Dictionary<string, Query> results;
			bool success = ReadLog(s, out results);
			if(!success)
				return;

			float mseconds;
			bool converted = float.TryParse(MinSeconds.Text,out mseconds);
			if(converted) {
				results = results.Values.Where( r => r.Duration.Seconds >= mseconds ).ToDictionary(r => r.id);
			}		

			SaveResults(results);
		}

		public bool ReadLog(string query, out Dictionary<string, Query> output) {
			LogReader lr;
			output = null;

			if (LoadFromOption == LoadFromOptions.Live)
			{
				lr = LogReaderFactory.CreateLiveLogReader(query: query, server: MachineName.Text);
			}
			else
			{
				if(File.Exists(LogFile.Text)) {
					lr = LogReaderFactory.CreateSavedLogReader(LogFile.Text, query);
				}
				else {
					MessageBoxResult result = MessageBox.Show("Invalid Log File", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}

			ct = new CancellationTokenSource();
			try {
				output = lr.ReadLog();
			}
			catch(Exception e) {
				MessageBoxResult result = MessageBox.Show(e.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		public void SaveResults(Dictionary<string, Query> results) {
			switch (SaveAsOption)
			{
				case SaveAsOptions.EventFrame:
					if(db != null) {
						AFElementTemplate eftemplate = db.ElementTemplates["PIWebAPI_QueryResults"];
						if(eftemplate == null) {
							eftemplate = new AFElementTemplate("PIWebAPI_QueryResults");
							eftemplate.AttributeTemplates.Add("ID");
							eftemplate.InstanceType = typeof(AFEventFrame);
							db.ElementTemplates.Add(eftemplate);
							db.CheckIn();
						}
						foreach(Query q in results.Values) {
							AFEventFrame ef = new AFEventFrame(db, $"{eftemplate.Name}_{q.StartTime.ToShortDateString()}", eftemplate);
							ef.SetStartTime(q.StartTime);
							ef.SetEndTime(q.EndTime);
							ef.Attributes["ID"].SetValue(new AFValue(q.id));
						}
						db.CheckIn();
						MessageBoxResult result = MessageBox.Show($"Created {results.Values.Count} Event Frames with name '{eftemplate.Name}_<Date>'", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);						
					}
					break;
				case SaveAsOptions.Text:
					if(File.Exists(FileOutput.Text)) {
						using (StreamWriter output = new StreamWriter(FileOutput.Text)) {
							foreach(Query q in results.Values) {
								output.WriteLine(q);
							}
						}
						System.Diagnostics.Process.Start(FileOutput.Text);
					}
					break;
				default:
				case SaveAsOptions.Console:
					Console c = new Console();
					foreach(Query q in results.Values) {
						c.listBox.Items.Add(q);
					}
					c.Show();
					break;
			}
		}

		private void StartWatch(object sender, RoutedEventArgs e)
		{

		}

		private void StopWatch(object sender, RoutedEventArgs e)
		{
			ct.Cancel();
		}

		private void FileOutput_Click(object sender, RoutedEventArgs e)
		{
			// Create OpenFileDialog 
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

			// Set filter for file extension and default file extension 
			dlg.DefaultExt = ".txt";
			dlg.Filter = "Text Files (*.txt)|*.txt";


			// Display OpenFileDialog by calling ShowDialog method 
			Nullable<bool> result = dlg.ShowDialog();


			// Get the selected file name and display in a TextBox 
			if (result == true)
			{
				// Open document 
				string filename = dlg.FileName;
				FileOutput.Text = filename;
			}
		}

		private void LogFile_Click(object sender, RoutedEventArgs e)
		{
			// Create OpenFileDialog 
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

			// Set filter for file extension and default file extension 
			dlg.DefaultExt = ".evtx";
			dlg.Filter = "Event Log Files (*.evtx)|*.evtx";


			// Display OpenFileDialog by calling ShowDialog method 
			Nullable<bool> result = dlg.ShowDialog();


			// Get the selected file name and display in a TextBox 
			if (result == true)
			{
				// Open document 
				string filename = dlg.FileName;
				LogFile.Text = filename;
			}
		}

		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex(@"[^0-9.]+");
			e.Handled = regex.IsMatch(e.Text);
		}
	}
}
