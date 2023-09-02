using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Utils;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using IronPython.Runtime;
using System.Numerics;
using IronPython.Runtime.Binding;

namespace IronPython.WpfShell
{
    /// <summary>
    /// an interface for sys.out of IronPython, used by print
    /// if we redirect sys.out output, have to impliment this interface.
    /// </summary>
    public interface iobase2
    {
        void flush(CodeContext/*!*/ context);
        BigInteger write(CodeContext/*!*/ context, object buf);
    }
    /// <summary>
    /// ConsoleControl.xaml 的交互逻辑
    /// 1 界面带颜色的显示；
    /// 2 当前命令行的编辑，
    /// 2.1 tab补全，
    /// 2.2 上箭头选择上一条指令。
    /// 代码复制自 IronPython的内置Console。
    /// </summary>
    [PythonType]
    public partial class WpfShellControl : UserControl, IConsole, iobase2, IDisposable, IDynamicMetaObjectProvider, IPythonExpandable
    {
        private TextWriter _output;
        private TextWriter _errorOutput;
        private Brush _promptColor;
        private Brush _outColor;
        private Brush _errorColor;
        private Brush _warningColor;
        private int Console = 0; //防止继续使用Console类


#region Nested types: EditMode, History, SuperConsoleOptions, Cursor

        /// <summary>
        /// Keybindings and cursor movement style.
        /// </summary>
        public enum EditMode
        {
            Windows,
            Emacs,
        }

        /// <summary>
        /// Class managing the command history.
        /// </summary>
        class History
        {
            protected List<string> _list = new List<string>();
            private int _current;
            private bool _increment;         // increment on Next()

            public string Current
            {
                get
                {
                    return _current >= 0 && _current < _list.Count ? _list[_current] : String.Empty;
                }
            }

            public void Add(string line, bool setCurrentAsLast)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    int oldCount = _list.Count;
                    _list.Add(line);
                    if (setCurrentAsLast || _current == oldCount)
                    {
                        _current = _list.Count;
                    }
                    else
                    {
                        _current++;
                    }
                    // Do not increment on the immediately following Next()
                    _increment = false;
                }
            }

            public string Previous()
            {
                if (_current > 0)
                {
                    _current--;
                    _increment = true;
                }
                return Current;
            }

            public string Next()
            {
                if (_current + 1 < _list.Count)
                {
                    if (_increment) _current++;
                    _increment = true;
                }
                return Current;
            }
        }

        /// <summary>
        /// 根据输入的前缀，获取可选项，tab补齐的时候使用。
        /// </summary>
        class TabInputOptions
        {
            private List<string> _list = new List<string>();
            private int _current;
            public string PromoptString { get; set; } = string.Empty;

            public int Count
            {
                get
                {
                    return _list.Count;
                }
            }

            private string Current
            {
                get
                {
                    return _current >= 0 && _current < _list.Count ? _list[_current] : String.Empty;
                }
            }

            public void Clear()
            {
                _list.Clear();
                _current = -1;
            }

            public void Add(string line)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    _list.Add(line);
                }
            }

            public string Previous()
            {
                if (_list.Count > 0)
                {
                    _current = ((_current - 1) + _list.Count) % _list.Count;
                }
                return Current;
            }

            public string Next()
            {
                if (_list.Count > 0)
                {
                    _current = (_current + 1) % _list.Count;
                }
                return Current;
            }

            public string Root { get; set; }
        }
#if false
        /// <summary>
        /// Cursor position management
        /// </summary>
        struct Cursor
        {
            /// <summary>
            /// Beginning position of the cursor - top coordinate.
            /// </summary>
            private int _anchorTop;
            /// <summary>
            /// Beginning position of the cursor - left coordinate.
            /// </summary>
            private int _anchorLeft;

            public void Anchor()
            {
                _anchorTop = Console.CursorTop;
                _anchorLeft = Console.CursorLeft;
            }

            public void Reset()
            {
                Console.CursorTop = _anchorTop;
                Console.CursorLeft = _anchorLeft;
            }

            public void Place(int index)
            {
                Console.CursorLeft = (_anchorLeft + index) % Console.BufferWidth;
                int cursorTop = _anchorTop + (_anchorLeft + index) / Console.BufferWidth;
                if (cursorTop >= Console.BufferHeight)
                {
                    _anchorTop -= cursorTop - Console.BufferHeight + 1;
                    cursorTop = Console.BufferHeight - 1;
                }
                Console.CursorTop = cursorTop;
            }

            public static void Move(int delta)
            {
                int position = Console.CursorTop * Console.BufferWidth + Console.CursorLeft + delta;

                Console.CursorLeft = position % Console.BufferWidth;
                Console.CursorTop = position / Console.BufferWidth;
            }
        }
#endif
#endregion
        /// <summary>
        /// The number of white-spaces displayed for the auto-indenation of the current line
        /// </summary>
        private int _autoIndentSize;


        /// <summary>
        /// Command history
        /// </summary>
        private History _history = new History();

        /// <summary>
        /// Tab options available in current context
        /// </summary>
        private TabInputOptions _options = new TabInputOptions();

        /// <summary>
        /// The current edit mode of the console.
        /// </summary>
        private EditMode _editMode;

        public TextWriter Output
        {
            get
            {
                return _output;
            }
            set
            {
                ContractUtils.RequiresNotNull(value, "value");
                _output = value;
            }
        }

        public TextWriter ErrorOutput
        {
            get
            {
                return _errorOutput;
            }
            set
            {
                ContractUtils.RequiresNotNull(value, "value");
                _errorOutput = value;
            }
        }

        protected AutoResetEvent CtrlCEvent { get; set; }

        protected Thread CreatingThread { get; set; }

        //public ConsoleCancelEventHandler ConsoleCancelEventHandler { get; set; }
        private StringBuilder outStringBuilder = new StringBuilder();
        private StringWriter outStringWriter;
        public WpfShellControl() : this(true, true)
        {

        }
        public WpfShellControl(bool colorful, bool darkColorMode)
        {
            InitializeComponent();

            outStringWriter = new StringWriter(outStringBuilder);
            _output = outStringWriter; //Console.Out;// 
            _errorOutput = outStringWriter; //Console.Error;// 
            CreatingThread = Thread.CurrentThread;
            //ConsoleCancelEventHandler = delegate (object sender, ConsoleCancelEventArgs e)
            //{
            //    if (e.SpecialKey == ConsoleSpecialKey.ControlC)
            //    {
            //        e.Cancel = true;
            //        CtrlCEvent.Set();
            //        CreatingThread.Abort(new KeyboardInterruptException(""));
            //    }
            //};
            //Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            //{
            //    if (ConsoleCancelEventHandler != null)
            //    {
            //        ConsoleCancelEventHandler(sender, e);
            //    }
            //};
            CtrlCEvent = new AutoResetEvent(initialState: false);
            SetupColors(new ColorConfig());

            _editMode = Environment.OSVersion.Platform == PlatformID.Unix ? EditMode.Emacs : EditMode.Windows;

            //Console.SetOut(outStringWriter);   //把所有发往控制台的都显示一下。
            //Console.SetError(outStringWriter);
            //Console.SetWindowSize(120, 25);
        }
        /// <summary>
        /// 设置颜色主题
        /// </summary>
        /// <param name="cfg"></param>
        public void SetupColors(ColorConfig cfg)
        {
            _promptColor = cfg.PromptColor;
            txtConsole.Background = cfg.Background;
            txtConsole.Foreground = cfg.Foreground;
            //_outColor = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            _outColor = cfg.OutColor;
            _errorColor = cfg.ErrorColor;
            _warningColor = cfg.WarningColor;
        }
        protected void WriteColor(TextWriter output, string str, Brush c)
        {
            //ConsoleColor foregroundColor = Console.ForegroundColor;
            //Console.ForegroundColor = c;
            //output.Write(str);
            //output.Flush();
            //Console.ForegroundColor = foregroundColor;

            txtConsole.Dispatcher.BeginInvoke((Action)delegate ()
            {
                WriteColorInternal(output, str, c);
            });
        }
        private void WriteColorInternal(TextWriter output, string str, Brush c)
        {
            if (txtConsole.Document.Blocks.LastBlock == null)
                txtConsole.Document.Blocks.Add(new Paragraph());
            Paragraph p = txtConsole.Document.Blocks.LastBlock as Paragraph; //本行占30%的cpu
            Run r = new Run(str);
            r.Foreground = c;
            p.Inlines.Add(r);  //压力测试下本行占30%的cpu
            txtConsole.ScrollToEnd(); //压力测试的时候，本行需要额外刷一下消息循环才生效。
        }
        string strInputBuffer = null;
        public virtual string ReadLine(int autoIndentSize)
        {
            Write("".PadLeft(autoIndentSize), Microsoft.Scripting.Hosting.Shell.Style.Prompt);
            while (strInputBuffer == null)
            {
                System.Threading.Thread.Sleep(200);
                if (outStringBuilder.Length > 0)
                {
                    string txt = outStringBuilder.ToString();
                    outStringBuilder.Clear();

                    Write(txt + Environment.NewLine, Microsoft.Scripting.Hosting.Shell.Style.Out);
                }
                //因为PythonConsoleHost类的初始化时候指向了另外一个控制台...
                //也可以用 https://www.codenong.com/2089998/ 的方法  engine.Runtime.IO.SetOutput
                if (_output != outStringWriter)
                {
                    _output = outStringWriter;
                    _errorOutput = outStringWriter;
                }
            }
            string text = strInputBuffer;// Console.In.ReadLine();
            strInputBuffer = null;
            if (text == null)
            {
                //ironPython的作者说这里有个竞争，ctrl+c结束了正在执行的后，这里等一下再返回。
                if (CtrlCEvent != null && CtrlCEvent.WaitOne(100, exitContext: false))
                {
                    return "";
                }
                return null;
            }

            text = "".PadLeft(autoIndentSize) + text;
            Write(text + Environment.NewLine, Microsoft.Scripting.Hosting.Shell.Style.Out);
            return text;
        }

        public virtual void Write(string text, Microsoft.Scripting.Hosting.Shell.Style style)
        {
            switch (style)
            {
                case Microsoft.Scripting.Hosting.Shell.Style.Prompt:
                    WriteColor(_output, text, _promptColor);
                    break;
                case Microsoft.Scripting.Hosting.Shell.Style.Out:
                    WriteColor(_output, text, _outColor);
                    break;
                case Microsoft.Scripting.Hosting.Shell.Style.Error:
                    WriteColor(_errorOutput, text, _errorColor);
                    break;
                case Microsoft.Scripting.Hosting.Shell.Style.Warning:
                    WriteColor(_errorOutput, text, _warningColor);
                    break;
            }
        }
        private void WriteErrorMessage(string text)
        {
            this.Write(text, Microsoft.Scripting.Hosting.Shell.Style.Error);
        }

        public void WriteLine(string text, Microsoft.Scripting.Hosting.Shell.Style style)
        {
            Write(text + Environment.NewLine, style);
        }

        public void WriteLine()
        {
            Write(Environment.NewLine, Microsoft.Scripting.Hosting.Shell.Style.Out);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        private bool GetOptions()
        {
            _options.Clear();
            cmbTabOptions.Items.Clear();
            string _input = txtInput.Text;
            int len;
            for (len = _input.Length; len > 0; len--)
            {
                char c = _input[len - 1];
                if (Char.IsLetterOrDigit(c))
                {
                    continue;
                }
                else if (c == '.' || c == '_')
                {
                    continue;
                }
                else
                {
                    break;
                }
            }
            string name = _input.Substring(len, _input.Length - len);
            if (name.Trim().Length > 0)
            {
                int lastDot = name.LastIndexOf('.');
                string attr, pref, root;
                if (lastDot < 0)
                {
                    attr = String.Empty;
                    pref = name;
                    root = _input.Substring(0, len);
                }
                else
                {
                    attr = name.Substring(0, lastDot);
                    pref = name.Substring(lastDot + 1);
                    root = _input.Substring(0, len + lastDot + 1);
                }

                try
                {
                    IList<string> result = null;
                    if (String.IsNullOrEmpty(attr))
                    {
                        result = cmdLine.GetGlobals(name);
                    }
                    else
                    {
                        result = cmdLine.GetMemberNames(attr);
                    }

                    _options.Root = root;
                    _options.PromoptString = _input;
                    if(result!=null)
                    {
                        foreach (string option in result)
                        {
                            if (option.StartsWith(pref, StringComparison.CurrentCultureIgnoreCase))
                            {
                                _options.Add(option);
                                cmbTabOptions.Items.Add(option);
                            }
                        }
                    }
                }
                catch
                {
                    _options.Clear();
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        // Check if the user is backspacing the auto-indentation. In that case, we go back all the way to
        // the previous indentation level.
        // Return true if we did backspace the auto-indenation.
        private bool BackspaceAutoIndentation()
        {
            string _input = txtInput.Text;
            if (_input.Length == 0 || _input.Length > _autoIndentSize) return false;

            // Is the auto-indentation all white space, or has the user since edited the auto-indentation?
            for (int i = 0; i < _input.Length; i++)
            {
                if (_input[i] != ' ') return false;
            }

            // Calculate the previous indentation level
            //!!! int newLength = ((input.Length - 1) / ConsoleOptions.AutoIndentSize) * ConsoleOptions.AutoIndentSize;            
            int newLength = _input.Length - 4;

            int backspaceSize = _input.Length - newLength;
            _input.Remove(newLength, backspaceSize);
            txtInput.Text = _input;
            return true;
        }
        private void OnBackspace(ConsoleModifiers keyModifiers) {
            if (BackspaceAutoIndentation()) return;
            string _input = txtInput.Text;
            //if (_input.Length > 0 && _current > 0) {
            //    int last = _current;
            //    if ((keyModifiers & ConsoleModifiers.Alt) != 0) {
            //        MovePrevWordStart();
            //    } else {
            //        _current--;
            //    }
            //    _input.Remove(_current, last - _current);
            //    Render();
            //}
        }

        private void OnDelete() {
            string _input = txtInput.Text;
            //if (_input.Length > 0 && _current < _input.Length) {
            //    _input.Remove(_current, 1);
            //    Render();
            //}
        }

        private void Insert(ConsoleKeyInfo key) {
            char c;
            string _input = txtInput.Text;
            //if (key.Key == ConsoleKey.F6) {
            //    Debug.Assert(FinalLineText.Length == 1);

            //    c = FinalLineText[0];
            //} else {
            //    c = key.KeyChar;
            //}
            //Insert(c);
        }

        private void Insert(char c) {
            //if (_current == _input.Length) {
            //    if (Char.IsControl(c)) {
            //        string s = MapCharacter(c);
            //        _current++;
            //        _input.Append(c);
            //        Output.Write(s);
            //        _rendered += s.Length;
            //    } else {
            //        _current++;
            //        _input.Append(c);
            //        Output.Write(c);
            //        _rendered++;
            //    }
            //} else {
            //    _input.Insert(_current, c);
            //    _current++;
            //    Render();
            //}
        }
        private void DeleteTillEnd() {
            //if (_input.Length > 0 && _current < _input.Length) {
            //    _input.Remove(_current, _input.Length - _current);
            //    Render();
            //}
        }

        private void DeleteFromStart() {
            //if (_input.Length > 0 && _current > 0) {
            //    _input.Remove(0, _current);
            //    _current = 0;
            //    Render();
            //}
        }

        private static string MapCharacter(char c) {
            if (c == 13) return "\r\n";
            if (c <= 26) return "^" + ((char)(c + 'A' - 1)).ToString();

            return "^?";
        }

        private static int GetCharacterSize(char c) {
            if (Char.IsControl(c)) {
                return MapCharacter(c).Length;
            } else {
                return 1;
            }
        }

        private void Render() {
            //_cursor.Reset();
            //StringBuilder output = new StringBuilder();
            //int position = -1;
            //for (int i = 0; i < _input.Length; i++) {
            //    if (i == _current) {
            //        position = output.Length;
            //    }
            //    char c = _input[i];
            //    if (Char.IsControl(c)) {
            //        output.Append(MapCharacter(c));
            //    } else {
            //        output.Append(c);
            //    }
            //}

            //if (_current == _input.Length) {
            //    position = output.Length;
            //}

            //string text = output.ToString();
            //Output.Write(text);

            //if (text.Length < _rendered) {
            //    Output.Write(new String(' ', _rendered - text.Length));
            //}
            //_rendered = text.Length;
            //_cursor.Place(position);
        }


        private bool IsSeparator(char ch)
        {
            return _editMode switch
            {
                EditMode.Emacs => !Char.IsLetterOrDigit(ch),
                _ => Char.IsWhiteSpace(ch)
            };
        }
        private const int TabSize = 4;
        private void InsertTab() {
            int addCount = TabSize - (txtInput.Text.Length % TabSize);
            txtInput.Text += new string(' ', addCount);
        }


        public void Dispose()
        {
            CtrlCEvent?.Close();
            GC.SuppressFinalize(this);
        }


        bool inputChanged = false;
        bool optionsObsolete = false;
        private void txtInput_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.ImeProcessed || e.Key == Key.LeftShift)
                return;

            inputChanged = true; //对于上下左右箭头的，在下面单独改回来。
            optionsObsolete |= e.Key != Key.Tab;//按其它按键都会导致补全列表失效。
            switch (e.Key)
            {
                case Key.Back:
                    {
                        if (e.SystemKey == Key.LeftAlt)
                        {
                            //删除到上个单词的开始
                        }
                        else
                        {
                            //删除一个字符
                        }
                    }
                    break;
                case Key.Delete:
                    break;
                case Key.Enter:
                    {
                        DoCommand(txtInput.Text);
                        if (txtInput.Text.Trim().Length > 0)
                            _history.Add(txtInput.Text.Trim(), inputChanged);
                        txtInput.Clear();
                        break;
                    }
                case Key.Tab:
                    {
                        if(txtInput.Text.EndsWith('\t'))
                            txtInput.Text= txtInput.Text.Substring(0,txtInput.Text.Length - 1);
                        bool prefix = false;
                        if (optionsObsolete)
                        {
                            prefix = GetOptions();
                            optionsObsolete = false;
                        }

                        // Displays the next option in the option list,
                        // or beeps if no options available for current input prefix.
                        // If no input prefix, simply print tab.
                        DisplayNextOption(e, prefix);
                        optionsObsolete = false;
                        e.Handled = true;//抑制后续的事件。
                        break;
                    }
                case Key.Up:
                    txtInput.Text = _history.Previous();
                    txtInput.SelectionStart = txtInput.Text.Length;
                    inputChanged = false;
                    break;
                case Key.Down:
                    txtInput.Text = _history.Next();
                    txtInput.SelectionStart = txtInput.Text.Length;
                    inputChanged = false;
                    break;
                case Key.Left:
                    inputChanged = false;
                    break;
                case Key.Right:
                    inputChanged = false;
                    break;
                case Key.Escape:
                    txtInput.Text = string.Empty;
                    break;
                case Key.Home:
                    inputChanged = false;
                    break;
                case Key.End:
                    inputChanged = false;
                    break;
                case Key.LeftShift:
                    inputChanged = false;
                    break;
                default:
                    //EMACS的快捷键暂时没已处理。
                    break;
            }
        }
        /// <summary>
        /// Displays the next option in the option list,
        /// or beeps if no options available for current input prefix.
        /// If no input prefix, simply print tab.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="prefix"></param>
        private void DisplayNextOption(KeyEventArgs key, bool prefix)
        {
            if (_options.Count > 0)
            {
                //(key.SystemKey== Key.LeftShift)
                bool shift = Keyboard.IsKeyDown(Key.LeftShift);// || Keyboard.IsKeyDown(Key.RightShift);
                string part = shift ? _options.Previous() : _options.Next();
                cmbTabOptions.Text=part;
                txtInput.Text = _options.Root + part;
                txtInput.SelectionStart = _options.PromoptString.Length;
                txtInput.SelectionLength = 0;
            }
            else
            {
                if (prefix)
                {
                    System.Media.SystemSounds.Beep.Play();
                }
                else
                {
                    InsertTab();
                    txtInput.SelectionStart = txtInput.Text.Length;
                }
            }
        }

        private void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            DoCommand(txtInput.Text);
            if (txtInput.Text.Trim().Length > 0)
                _history.Add(txtInput.Text.Trim(), inputChanged);
            txtInput.Clear();
        }
        /// <summary>
        /// 执行一条命令
        /// </summary>
        /// <param name="cmd"></param>
        public void DoCommand(string cmd)
        {
            strInputBuffer = cmd;
        }
        #region sys.out的输出接口redirect
        public DynamicMetaObject GetMetaObject(System.Linq.Expressions.Expression parameter)
        {
            return new MetaExpandable<WpfShellControl>(parameter, this);
        }
        public void flush(CodeContext context)
        {
        }

        public BigInteger write(CodeContext context, object buf)
        {
            WriteColor(_output, buf.ToString(), _outColor);
            return 0;
        }

        private PythonDictionary _dict;
        public IDictionary<object, object> EnsureCustomAttributes()
        {
            if (_dict is null) _dict = new PythonDictionary();
            return _dict;
        }
        public IDictionary<object, object> CustomAttributes => _dict;

        private CodeContext _code_context = null;
        public CodeContext Context
        {
            get { return _code_context; }
        }
        public void SetCc(CodeContext cc)
        {
            this._code_context = cc;
        }
        #endregion
        private void btnAllGlobal_Click(object sender, RoutedEventArgs e)
        {
            var lst = cmdLine.GetGlobals(string.Empty);// _commandLine.GetGlobals(name);
            cmbTabOptions.Items.Clear();
            foreach (var s in lst)
            {
                cmbTabOptions.Items.Add(s);
            }
        }
        private PythonCommandLine2 cmdLine = null;
        private RunParameterType RunConfig1;
        public void Start(RunParameterType RunConfig)
        {
            this.IsEnabled = true;
            RunConfig1 = RunConfig;
            if(!string.IsNullOrWhiteSpace(RunConfig.RunPyFile))
                this.txtInput.Text = "run -i " + RunConfig.RunPyFile;
            if (Environment.GetEnvironmentVariable("TERM") == null)
            {
                Environment.SetEnvironmentVariable("TERM", "dumb");
            }
            cmdLine= new PythonCommandLine2();
            cmdLine.BeforeRun += CmdLine_BeforeRun;
            var host = new PythonConsoleHost(this, cmdLine, true);

            try
            {
                Task.Run(() =>
                {
                    host.Run(new string[] { }); //Config.DefaultFileName
                });
            }
            catch (IronPython.Runtime.Exceptions.ImportException ex)
            {
                WriteErrorMessage(ex.Message);
#if false
                // 获取IronPython的错误信息
                var errorInfo = ex.GetEngineException();

                // 输出错误信息
                Console.WriteLine($"IronPython Error: {errorInfo.Message}");
                Console.WriteLine($"File: {errorInfo.SourcePath}");
                Console.WriteLine($"Line: {errorInfo.Line}");
                Console.WriteLine($"Column: {errorInfo.Column}");
                Console.WriteLine($"Traceback: {errorInfo.Traceback}");
#endif
            }
            catch (Microsoft.Scripting.SyntaxErrorException errorInfo)
            {
                //编译语法错误的提示信息
                WriteErrorMessage($"IronPython Error: {errorInfo.Message}");
                WriteErrorMessage($"File: {errorInfo.SourcePath}");
                WriteErrorMessage($"Line: {errorInfo.Line}");
                WriteErrorMessage($"Column: {errorInfo.Column}");
                WriteErrorMessage($"Traceback: {errorInfo.StackTrace}");
            }
            catch (Exception ex)
            {
                var msg = host.Engine.GetService<Microsoft.Scripting.Hosting.ExceptionOperations>()
                    .FormatException(ex);
                WriteErrorMessage(msg);
                throw;
            }
        }

        private void CmdLine_BeforeRun(object sender, PythonCommandLine2.BeforeRunEventArgs e)
        {
            var cmd = sender as PythonCommandLine2;

            if (true)
            {
                var path = e.Engine.GetSearchPaths();
                List<string> pathList = new System.Collections.Generic.List<string>();
                pathList.AddRange(path);
                if(RunConfig1.SearchPathes != null && RunConfig1.SearchPathes.Count != 0)
                    pathList.AddRange(RunConfig1.SearchPathes);
                // lib directory to lib.zip：
                //   1: too many files to one file to small the size.
                //   2: the os.chdir() modifies the current Directory of c# run env, so to full path.
                string libPath = System.IO.Path.GetFullPath("lib.zip");
                if (!System.IO.File.Exists(libPath))
                    this.Write(libPath, Microsoft.Scripting.Hosting.Shell.Style.Warning);
                else
                    pathList.Add(libPath);
                e.Engine.SetSearchPaths(pathList);
            }

            //var scope = (Microsoft.Scripting.Runtime.ObjectDictionaryExpando)e.Scope;
            if(RunConfig1.GlobalVariables!=null)
            {
                foreach (var variable in RunConfig1.GlobalVariables)
                    e.Scope.SetVariable(variable.Key, variable.Value);
            }
        }
    }
}
