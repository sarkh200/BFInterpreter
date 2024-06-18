using System.Collections.Concurrent;

namespace BFInterpreter;

public sealed partial class MainPage: Page
{
    public MainPage()
    {
        InitializeComponent();
        progress = new Progress<char>(value =>
        {
            OutputBox.Text += value;
            OutputScrollViewer.ScrollToVerticalOffset(OutputScrollViewer.ExtentHeight);
        });
    }

    readonly ConcurrentQueue<char> stdin = new();
    readonly AutoResetEvent waitHandle = new(false);
    readonly IProgress<char> progress = new Progress<char>();
    CancellationTokenSource cancelRun = new();
    bool isRunning = false;

    void SendInput()
    {
        foreach (char c in InputTextBox.Text)
        {
            stdin.Enqueue(c);
        }
        OutputBox.Text += InputTextBox.Text;
        OutputScrollViewer.ScrollToVerticalOffset(OutputScrollViewer.ExtentHeight);
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
            await Task.Run(() => InterpretBF(CodeTextBox.Text, progress, cancellationToken));
            RunButton.Content = "Run";
            isRunning = false;
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

    void InputTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            SendInput();
        }
    }
    void ClearOutputButton_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Text = "";
    }

    public void InterpretBF(string code, IProgress<char> output, CancellationToken cancellationToken)
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
                        output.Report(Convert.ToChar(m[i]));
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
            foreach (char exC in $"Error: {ex.Message.ToString()}")
                progress.Report(exC);
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
