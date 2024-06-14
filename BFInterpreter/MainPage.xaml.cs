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
        List<int> labels = [];
        byte[] m = new byte[30000];
        Array.Fill(m, (byte) 0);
        int i = 0;
        for (int j = 0; j < code.Length; j++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                stdin.Clear();
                return;
            }
            char c = code[j];
            try
            {
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
                        if (m[i] != (char) 0)
                        {
                            labels.Add(j);
                        }
                        else
                        {
                            SeekEndOfLoop(ref j, code);
                        }
                        break;
                    case ']':
                        if (m[i] != (char) 0)
                        {
                            j = labels.Last();
                        }
                        else
                        {
                            labels.Remove(labels.Last());
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                foreach (char exC in ex.Message.ToString())
                    progress.Report(exC);
            }
        }
    }
    static void SeekEndOfLoop(ref int j, string code)
    {
        char c = code[j];
        int outOfScopeNum = 1;
        while (c != ']' || outOfScopeNum != 0)
        {
            j++;
            c = code[j];
            if (c == '[')
            {
                outOfScopeNum++;
            }
            else if (c == ']' && outOfScopeNum != 0)
            {
                outOfScopeNum--;
            }
        }
    }

}
