using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace IronPython.WpfShell
{
    public class RunParameterType
    {
        /// <summary>
        /// 搜索路径
        /// </summary>
        public List<string> SearchPathes { get; set; }
        /// <summary>
        /// 默认运行的py文件
        /// 我的需求是先运行一个通用库在内的加载文件，然后再输入几个指令调试。
        /// </summary>
        public string RunPyFile { get; set; }
        /// <summary>
        /// 默认的全局变量，
        /// c#的变量可以直接在这里注入。
        /// </summary>
        public Dictionary<string,object> GlobalVariables { get; set; }

    }
    public class ColorConfig
    {
        ////配色来自于 https://learn.microsoft.com/zh-cn/windows/terminal/customize-settings/color-schemes
        public Brush PromptColor { get; set; } = new SolidColorBrush(Color.FromRgb(0x16, 0xC6, 0x0C));
        public Brush Background { get; set; } = new SolidColorBrush(Color.FromRgb(1, 36, 86));
        public Brush Foreground { get; set; } = new SolidColorBrush(Color.FromRgb(0x3B, 0x78, 0xFF));

        public Brush OutColor { get; set; } = Brushes.White;  
        public Brush ErrorColor { get; set; } = new SolidColorBrush(Color.FromRgb(0xE7, 0x48, 0x56));
        public Brush WarningColor { get; set; } = new SolidColorBrush(Color.FromRgb(0xF9, 0xF1, 0xA5));
    }
}
