using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BFInterpreter.Views
{
	public partial class MainView: UserControl
	{
		public MainView()
		{
			InitializeComponent();
			//progress = new Progress<char>(value =>
			//{
			//	OutputBox.Text += value;
			//	OutputScrollViewer.ScrollToEnd();
			//});
		}

		readonly ConcurrentQueue<char> stdin = new();
		readonly AutoResetEvent waitHandle = new(false);
		//readonly IProgress<char> progress = new Progress<char>();
		CancellationTokenSource cancelRun = new();
		bool isRunning = false;

		void SendInput()
		{
			InputTextBox.Text ??= "";
			foreach (char c in InputTextBox.Text)
			{
				stdin.Enqueue(c);
			}
			OutputBox.Text += InputTextBox.Text;
			OutputScrollViewer.ScrollToEnd();
			InputTextBox.Text = "";
			waitHandle.Set();
		}

		async void RunButton_Click(object sender, RoutedEventArgs e)
		{
			if (!isRunning)
			{
				RunButton.Content = "Stop";
				isRunning = true;
				cancelRun = new();
				CancellationToken cancellationToken = cancelRun.Token;
				CodeTextBox.Text ??= "";
				string codeText = CodeTextBox.Text;
				await Task.Run(() =>
				{
					InterpretBF(codeText, /*progress,*/ cancellationToken);
					Dispatcher.UIThread.Invoke(() =>
					{
						RunButton.Content = "Run";
						isRunning = false;
					});
				});
			}
			else
			{
				cancelRun.Cancel();
				waitHandle.Set();
			}
		}

		void ClearButton_Click(object sender, RoutedEventArgs e)
		{
			CodeTextBox.Text = "";
		}

		void SendButton_Click(object sender, RoutedEventArgs e)
		{
			if (isRunning)
				SendInput();
		}

		void ClearOutputButton_Click(object sender, RoutedEventArgs e)
		{
			OutputBox.Text = "";
		}

		public void InterpretBF(string code, /*IProgress<char> output,*/ CancellationToken cancellationToken)
		{
			try
			{
				Dictionary<int, int> jumps = GetAllJumps(code);
				List<byte> m = [0];
				int i = 0;
				for (int j = 0; j < code.Length; j++)
				{
					if (cancellationToken.IsCancellationRequested)
					{
						stdin.Clear();
						return;
					}
					char c = code[j];
					switch (c)
					{
						case '+':
							m[i]++;
							break;
						case '-':
							m[i]--;
							break;
						case '>':
							i++;
							if (i >= m.Count)
								m.Add(0);
							break;
						case '<':
							i--;
							break;
						case '.':
							Dispatcher.UIThread.Invoke(() =>
							{
								OutputBox.Text += Convert.ToChar(m[i]);
								OutputScrollViewer.ScrollToEnd();
							});
							//output.Report(Convert.ToChar(m[i]));
							break;
						case ',':
							char firstChar;
							while (!stdin.TryDequeue(out firstChar) || cancellationToken.IsCancellationRequested)
							{
								if (cancellationToken.IsCancellationRequested)
								{
									stdin.Clear();
									return;
								}
								waitHandle.WaitOne();
							}
							m[i] = (byte) firstChar;
							break;
						case '[':
							if (m[i] == (char) 0)
							{
								j = jumps[j];
							}
							break;
						case ']':
							if (m[i] != (char) 0)
							{
								j = jumps[j];
							}
							break;
					}
				}
			}
			catch (Exception ex)
			{
				Dispatcher.UIThread.Invoke(() => { OutputBox.Text += ex.Message; });
				//foreach (char exC in $"Error: {ex.Message.ToString()}")
				//progress.Report(exC);
				return;
			}
		}

		static Dictionary<int, int> GetAllJumps(string code)
		{
			// 1st val is location of char; 2nd val is location it jumps to
			Dictionary<int, int> jumps = [];
			for (int i = 0; i < code.Length; i++)
			{
				if (code[i] == '[')
				{
					int j = i;
					int loops = 1;
					while (code[j] != ']' || loops != 0)
					{
						j++;
						if (code[j] == '[')
							loops++;
						else if (code[j] == ']')
							loops--;
					}
					jumps.Add(j, i);
					jumps.Add(i, j);
				}
			}
			return jumps;
		}
	}
}