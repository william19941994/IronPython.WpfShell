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

namespace IronPython.WpfShell.Test
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string pyFileName;
        public MainWindow()
        {
            InitializeComponent();
            
            string demoFileName = "test1.py";
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var exe_dir = System.IO.Path.GetDirectoryName(exe);
            pyFileName = System.IO.Path.Combine(exe_dir, demoFileName);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (System.IO.File.Exists(pyFileName))
            {
                txtPySource.Text = System.IO.File.ReadAllText(pyFileName);
            }
            else
                txtPySource.Text = pyFileName + " file NOT found.";
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            System.IO.File.WriteAllText(pyFileName, txtPySource.Text);

            ctl1.Start(new RunParameterType()
            {
                RunPyFile= pyFileName,
                GlobalVariables=new Dictionary<string, object>()
                {
                      {"np",new MyNumPyWrapper() }
                }
            });
        }
    }
}
