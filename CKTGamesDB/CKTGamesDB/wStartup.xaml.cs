using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using System.Windows.Threading;

namespace CKTGamesDB
{
	/// <summary>
	/// Interaktionslogik für MainWindow.xaml
	/// </summary>
	public partial class wStartup : Window
	{
		enum eSplashState
		{
			Loading, SplashFin, Starting
		}

		// gets a list of files
		// returns lists of itmes for each file
		BackgroundWorker mDataLoader = null;

		DispatcherTimer mTimer;
		double mAngleHeight = 0.0;
		int mPointToMove = 0;
		bool mLoadingDone = false, mLoadingError = false;
		eSplashState mState = eSplashState.Loading;

		static readonly int POINT_OFFSET = 17;

		struct BGWResult
		{
			public bool Error;
			public string Msg;

			public BGWResult(bool err, string msg)
			{
				Error = err;
				Msg = msg;
			}
		}

		public wStartup()
		{
			Classes.Util.ShowCallInfo();
			InitializeComponent();

			// load the main ini file
			Common.FileReader iniReader = new Common.FileReader();
			if (iniReader.init("../../data/CKTGamesDB.ini") != Common.Util.CKResult.CK_OK ||
				iniReader.read() != Common.Util.CKResult.CK_OK)
			{
				Debug.WriteLine("Error loading " + iniReader.DataFile.FullName);
			}
			
			Dictionary<string, string> iniFiles = iniReader.getEntryDic("FILE");
			string[] files = iniFiles.Values.ToArray<string>();

			// init and start the loading process
			mDataLoader = new BackgroundWorker();
			mDataLoader.WorkerReportsProgress = true;
			mDataLoader.WorkerSupportsCancellation = true;
			// callbacks for data loader
			mDataLoader.ProgressChanged += DataLoader_ProgressChanged;
			mDataLoader.DoWork += DataLoader_DoWork;
			mDataLoader.RunWorkerCompleted += DataLoader_RunWorkerCompleted;
			// start dataloader
			mDataLoader.RunWorkerAsync(files);

			// init the moving wait points
			Ellipse[] point = new Ellipse[3];
			for (int i = 0; i < point.Length; i++)
			{
				point[i] = new Ellipse();
				point[i].Width = point[i].Height = 20;
				point[i].Fill = Brushes.Black;

				int offsetMul = 2 - i;
				Canvas.SetLeft(point[i], 80 * Math.Cos((Math.PI / 180.0) * (mAngleHeight - POINT_OFFSET * offsetMul)) + 190);
				Canvas.SetTop(point[i], 80 * Math.Sin((Math.PI / 180.0) * (mAngleHeight - POINT_OFFSET * offsetMul)) + 110);

				w_Canvas.Children.Add(point[i]);
			}

			// init the timer for the updating in state Loading
			mTimer = new DispatcherTimer();
			mTimer.Interval = TimeSpan.FromMilliseconds(250);
			mTimer.IsEnabled = true;
			mTimer.Tag = point;
			mTimer.Tick += Wait_Tick;

		}

		// window related events
		private void Window_KeyUp(object sender, KeyEventArgs e)
		{
			Classes.Util.ShowCallInfo();
			if (e.Key == Key.Escape)
			{
				mDataLoader.CancelAsync();
				Close();
			}
		}
		// window related events

		// timer tick (update states)
		private void Wait_Tick(object sender, EventArgs e)
		{
			double x = 0.0, y = 0.0;

			switch (mState)
			{
				case eSplashState.Loading:
					{
						Ellipse[] point = (Ellipse[])((DispatcherTimer)sender).Tag;

						if (point[mPointToMove].Visibility == Visibility.Visible)
						{
							point[mPointToMove].Visibility = Visibility.Hidden;
							return;
						}

						mAngleHeight += POINT_OFFSET;
						if (mAngleHeight >= 360.0)
							mAngleHeight -= 360.0;

						x = 80 * Math.Cos((Math.PI / 180.0) * (mAngleHeight));
						y = 80 * Math.Sin((Math.PI / 180.0) * (mAngleHeight));

						point[mPointToMove].Visibility = Visibility.Visible;
						Canvas.SetLeft(point[mPointToMove], x + 190);
						Canvas.SetTop(point[mPointToMove], y + 110);

						mPointToMove++;
						mPointToMove %= 3;

						if (mLoadingDone || mLoadingError)
						{
							point[0].Visibility = point[1].Visibility = point[2].Visibility = Visibility.Hidden;
							mAngleHeight = 0.0;
							((DispatcherTimer)sender).Interval = TimeSpan.FromMilliseconds(16);
							mState = eSplashState.SplashFin;
						}

						break;
					}
				case eSplashState.SplashFin:
					{
						mAngleHeight += 300 * ((DispatcherTimer)sender).Interval.TotalSeconds;

						if (mLoadingError)
						{
							if (mAngleHeight > 170)
								mAngleHeight = 170;

							Canvas.SetTop(w_CancelImg, 120 - mAngleHeight / 2);
							w_CancelImg.Height = mAngleHeight;
						}
						else
						{
							Canvas.SetTop(w_OkImg, 130 - mAngleHeight / 2);
							w_OkImg.Height = mAngleHeight;
						}

						if (mAngleHeight >= 170 && !mLoadingError)
						{
							((DispatcherTimer)sender).Interval = TimeSpan.FromMilliseconds(500);
							mState = eSplashState.Starting;
						}

						break;
					}
				case eSplashState.Starting:
					{
						((DispatcherTimer)sender).Stop();

						// TODO: if loading is done
						// open the main app window
						Close();
						break;
					}
			}
		}
		// timer tick (update states)

		// dataloader related events
		private void DataLoader_ProgressChanged(object sender, ProgressChangedEventArgs e)
		{
			if (e.UserState != null)
			{
				BGWResult result = (BGWResult)e.UserState;
				Debug.WriteLine("Progress: " + e.ProgressPercentage + " | " + result.Msg);
				
				w_InfoLbl.Content = e.ProgressPercentage + "% : " + result.Msg;
			}
		}

		private void DataLoader_DoWork(object sender, DoWorkEventArgs e)
		{
			Classes.Util.ShowCallInfo();
			BackgroundWorker worker = sender as BackgroundWorker;
			string[] files = e.Argument as string[];

			FileInfo[] fi = new FileInfo[files.Length];
			long fileSizeSum = 0;
			for(int i = 0; i < files.Length; i++)
			{
				fi[i] = new FileInfo(files[i]);
				if (fi[i].Exists)
					fileSizeSum += fi[i].Length;
			}
			
			for(int i = 0; i < fi.Length; i++)
			{
				// check cancellation
				if (worker.CancellationPending)
				{
					Debug.WriteLine("Dataloader cancel!");
					e.Cancel = true;
					return;
				}

				if (fi[i].Exists && fi[i].Length > 0)
				{
					// TODO: process file data
					for (int j = 0; j < 1000000; j++)
					{
						for (int k = 0; k < 500; k++)
						{
							double a = Math.Sqrt(25);
						}
					}
				}
				
				string fileDone = fi[i].Name + " " + (fi[i].Exists ? "is loaded" : "not found");
				BGWResult report = new BGWResult(!fi[i].Exists, fileDone);
				worker.ReportProgress((i + 1) * 100 / fi.Length, report);

				if (!fi[i].Exists || fi[i].Length <= 0)
				{
					// set error when file is not found
					Debug.WriteLine("Dataloader cancel because error!");
					//e.Cancel = true;
					e.Result = new BGWResult(true, "File not Found: " + fi[i].FullName);
                    return;
				}
			}
			
			//const int OUTER_LOOP = 250;
			//for (int i = 0; i < OUTER_LOOP; i++)
			//{
			//	for(int j = 0; j < 10000000; j++)
			//	{
			//		if (worker.CancellationPending)
			//		{
			//			Debug.WriteLine("Dataloader cancel!");
			//			e.Cancel = true;
			//			return;
			//		}

			//		double a = Math.Sqrt(25);
			//	}

			//	worker.ReportProgress(i * 100 / OUTER_LOOP);
			//}

			//worker.ReportProgress(OUTER_LOOP * 100 / OUTER_LOOP);
		}

		private void DataLoader_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			Classes.Util.ShowCallInfo();
			if (e.Cancelled || (e.Result != null && ((BGWResult)e.Result).Error))
			{
				Debug.WriteLine("Dataloader was cancelled!");

				if (e.Result != null)
				{
					BGWResult result = (BGWResult)e.Result;
					w_InfoLbl.Foreground = Brushes.Red;
					w_InfoLbl.Content = result.Msg;
					mLoadingError = true;
				}
			}
			else
				mLoadingDone = true;
		}
		// dataloader related events

	}
}
