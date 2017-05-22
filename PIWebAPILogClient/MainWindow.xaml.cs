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
using System.Threading;
using System.Windows.Threading;
using System.IO;

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

		//how are we saving the results
		private SaveAsOptions SaveAsOption;
		//where is the log being read from
		private LoadFromOptions LoadFromOption;

		//Time to manages the "Processing..." text that appears while query is running
		private DispatcherTimer dispatcherTimer;

		//used only when watching a log
		private LogReader watchingLogReader;
		//used only when watching a log - handles how the results are output
		private IOutputWriter outputWriter;

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
			//set the code execution that occurs when a particular dropdown option is selected in the SaveAs dropdown
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
			//set the code execution that occurs when a particular dropdown option is selected in the LoadFrom dropdown
			LoadFrom.ItemsSource = new[] {
				new DropdownOption(
					"Live Log",
					() => {
						LogFile.IsEnabled = false;
						LogFileButton.IsEnabled = false;
						
						MachineName.IsEnabled = true;
						//StartWatchButton.IsEnabled = true;

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

			//set the items in the box to display the available servers
			AFServer.ItemsSource = ps;
			AFServer.DisplayMemberPath = "Name";
			AFServer.SelectedItem = ps.DefaultPISystem;
		}

		private void AFServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			server = AFServer.SelectedItem as PISystem;

			//set the items in the database box to be the available databases for the selected server
			AFDatabase.ItemsSource = server.Databases;
			AFDatabase.DisplayMemberPath = "Name";
			AFDatabase.SelectedItem = server.Databases.DefaultDatabase;

			db = server.Databases.DefaultDatabase;
		}

		private void AFDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			db = AFDatabase.SelectedItem as AFDatabase;
		}

		public void ShowProcessing(object sender, EventArgs e) {
			//Callback for dispatcherTimer
			//Sets the text that says "Processing" while querying to oscillate between 0 and 3 periods
			string pc = Processing.Content.ToString();
			pc += ".";
			if(pc.Length <= 13)
				Processing.Content = pc;
			else
				Processing.Content = "Processing";
		}

		private void Parse(object sender, RoutedEventArgs e)
		{
			//Gets all the entries from the requested log that fit the filter criteria requested

			//can only update the ui from this context
			var ui = TaskScheduler.FromCurrentSynchronizationContext();
			//initialize token
			ct = new CancellationTokenSource();
			//completed queries will be stored here
			Dictionary<string, Query> results = new Dictionary<string, Query>();

			//read textbox values (since you can't easily do it in the task)
			string stringStart = StartTime.Text;
			string stringEnd = EndTime.Text;
			string machineName = MachineName.Text;
			string logFile = LogFile.Text;

			//update UI with available options
			StartWatchButton.IsEnabled = false;
			ParseButton.IsEnabled = false;
			CancelButton.IsEnabled = true;
			Processing.Content = "Processing";

			dispatcherTimer.Start();

			var task = Task.Factory.StartNew(() =>
			{
				//try to parse start and endtime
				DateTime startTime;
				bool st = DateTime.TryParse(stringStart, out startTime);
				if (!st)
				{
					startTime = DateTime.MinValue;
				}

				DateTime endTime;
				bool et = DateTime.TryParse(stringEnd, out endTime);
				if (!et)
				{
					endTime = DateTime.Now;
				}

				//create query
				//4 = Information Log Level (required)
				//11,12 - EventIDs of the start/end query processing (required)
				string s = LogQueryBuilder.Build(
					new List<int> { 4 },
					new List<int> { 11, 12 },
					startTime,
					endTime
					);

				ReadLog(s, results, logFile, machineName);
			}, ct.Token);

			//once task has been completed or cancelled
			task.ContinueWith( (tresult) => 
				{
					//remove events that were shorter than the minimum time specified
					float mseconds;
					bool converted = float.TryParse(MinSeconds.Text,out mseconds);
					if(converted) {
						results = results.Values.Where( r => r.Duration.Seconds >= mseconds ).ToDictionary(r => r.id);
					}

					System.Console.WriteLine($"{results.Count} results");

					//save results to whatever medium was selected
					SaveResults(results);
				},
				CancellationToken.None,
				TaskContinuationOptions.NotOnFaulted,
				ui
			);

			//If task has been completed, cancelled, or faulted
			task.ContinueWith(
				(tresult) => {
					//update UI with available options
					CancelButton.IsEnabled = false;
					//StartWatchButton.IsEnabled = true;
					ParseButton.IsEnabled = true;
					Processing.Content = "";

					//Stop text from saying "Processing"
					dispatcherTimer.Stop();
				},
				CancellationToken.None,
				TaskContinuationOptions.None,
				ui
			);
		}

		/// <summary>
		/// Create a log reader based on the selected UI inputs
		/// </summary>
		/// <param name="query"></param>
		/// <param name="output"></param>
		/// <param name="logfile"></param>
		/// <param name="machineName"></param>
		/// <returns></returns>
		public bool ReadLog(string query, Dictionary<string, Query> output, string logfile = "", string machineName = "") {
			LogReader lr;
 
			if (LoadFromOption == LoadFromOptions.Live)
			{
				lr = LogReaderFactory.CreateLiveLogReader(query: query, server: machineName);
			}
			else
			{
				if(File.Exists(logfile)) {
					lr = LogReaderFactory.CreateSavedLogReader(logfile, query);
				}
				else {
					MessageBoxResult result = MessageBox.Show("Invalid Log File", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
					return false;
				}
			}

			try {
				lr.ReadLog(output, ct);
			}
			catch(Exception e) {
				MessageBoxResult result = MessageBox.Show(e.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
			}
			return true;
		}

		/// <summary>
		/// Save results to whatever medium was specified in the UI
		/// </summary>
		/// <param name="results"></param>
		public void SaveResults(Dictionary<string, Query> results) {
			switch (SaveAsOption)
			{
				case SaveAsOptions.EventFrame:
					AFWriter ow = new AFWriter(db);
					ow.WriteAllQuery(results);
					MessageBoxResult result = MessageBox.Show($"Created {results.Values.Count} Event Frames with name '{ow.eftemplate.Name}_<Date>'", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);	
					break;
				case SaveAsOptions.Text:
					TextFileWriter tw = new TextFileWriter(FileOutput.Text);
					tw.WriteAllQuery(results);
					//open file that was just written to
					System.Diagnostics.Process.Start(FileOutput.Text);
					break;
				default:
				case SaveAsOptions.Console:
					ConsoleWriter cw = new ConsoleWriter();
					cw.WriteAllQuery(results);
					break;
			}
		}

		private void StartWatch(object sender, RoutedEventArgs e)
		{
			//read textbox values
			string machineName = MachineName.Text;
			string logFile = LogFile.Text;

			//create query
			//4 = Information Log Level (required)
			//11,12 - EventIDs of the start/end query processing (required)
			string s = LogQueryBuilder.Build(
				new List<int> { 4 },
				new List<int> { 11, 12 }
				);

			//Create logreader -> sign up for new logreader events -> Begin watching
			watchingLogReader = LogReaderFactory.CreateLiveLogReader(query: s, server: machineName);
			watchingLogReader.CompleteQueryWrittenEvent += WriteWatchResults;
			bool watchStartSuccessful = watchingLogReader.StartWatch();

			if(watchStartSuccessful) {
				//update UI with available options
				StartWatchButton.IsEnabled = false;
				ParseButton.IsEnabled = false;
				CancelButton.IsEnabled = true;
				Processing.Content = "Processing";


				dispatcherTimer.Start();

				//set outputwriter to whatever output option was chosen in the ui
				switch (SaveAsOption)
				{
					default:
					case SaveAsOptions.EventFrame:
						outputWriter = new AFWriter(db);
						break;
					case SaveAsOptions.Text:
						outputWriter = new TextFileWriter(FileOutput.Text);
						break;
					case SaveAsOptions.Console:
						outputWriter = new ConsoleWriter();
						break;
				}
			}
			else {
				MessageBoxResult result = MessageBox.Show("Error creating log watcher - this feature is not available with this version", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}			
		}

		public void WriteWatchResults(object sender, CompleteQueryWrittenEventArgs e) {
			outputWriter.WriteQuery(e.query);
		}

		private void Cancel(object sender, RoutedEventArgs e)
		{
			//if we are currently watching a log, stop it
			if(watchingLogReader != null) {
				watchingLogReader.CompleteQueryWrittenEvent -= WriteWatchResults;
				watchingLogReader = null;
				dispatcherTimer.Stop();
			}
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
			bool? result = dlg.ShowDialog();

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
			bool? result = dlg.ShowDialog();

			// Get the selected file name and display in a TextBox 
			if (result == true)
			{
				// Open document 
				string filename = dlg.FileName;
				LogFile.Text = filename;
			}
		}

		/// <summary>
		/// Verify that only numbers were entered in the "Only Capture events longer than" textbox
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex(@"[^0-9.]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		//Create dispatcherTimer to fire every 0.5s
		//This timer will be started whenever user presses  the Parse/Watch Log buttons
		private void AppLoaded(object sender, RoutedEventArgs e)
		{
			dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
			dispatcherTimer.Tick += new EventHandler(ShowProcessing);
			dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 500);
		}
	}
}
