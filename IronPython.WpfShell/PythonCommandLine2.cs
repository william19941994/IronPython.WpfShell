// IronPython.Hosting.PythonCommandLine
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using IronPython;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using IronPython.Runtime.Operations;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using static System.Formats.Asn1.AsnWriter;

namespace IronPython.WpfShell
{
    /// <summary>
    /// A simple Python command-line should mimic the standard python.exe
    /// 控制台的后台逻辑代码（非UI部分）
    /// </summary>
    class PythonCommandLine2 : PythonCommandLine
    {
        private new PythonContext Language
        {
            get
            {
                return (PythonContext)base.Language;
            }
        }
        private PythonContext PythonContext => Language;
        private int GetEffectiveExitCode(SystemExitException/*!*/ e)
        {
            object nonIntegerCode;
            int exitCode = e.GetExitCode(out nonIntegerCode);
            if (nonIntegerCode != null)
            {
                Console.WriteLine(nonIntegerCode.ToString(), Style.Error);
            }
            return exitCode;
        }
        public class BeforeRunEventArgs : EventArgs
        {
            public ScriptEngine Engine { get; set; }
            public ScriptScope Scope { get; set; }

        }
        public event EventHandler<BeforeRunEventArgs> BeforeRun;
        protected override int RunInteractive()
        {
            if (Scope == null)
            {
                Scope = CreateScope();
                //上面创建了之后，ScriptScope也可以用了。
                //耗时一天，找Scope转ScriptScope的方法，后来发现他们是用的同一个数据。
            }
            if(BeforeRun != null)
            {
                //var context = ((PythonScopeExtension)Scope.GetExtension(Language.ContextId)).ModuleContext.GlobalContext;
                //PythonScopeExtension是internal的。
                BeforeRun(this,new BeforeRunEventArgs() {Engine=Engine, Scope= ScriptScope});
            }
            return base.RunInteractive();
        }
        protected override int? TryInteractiveAction()
        {
            try
            {
                try
                {
                    return TryInteractiveActionWorker();
                }
                finally
                {
                    PythonOps.ClearCurrentException();
                }
            }
            catch (SystemExitException e)
            {
                return GetEffectiveExitCode(e);
            }
        }

        /// <summary>
        /// Attempts to run a single interaction and handle any language-specific
        /// exceptions.  Base classes can override this and call the base implementation
        /// surrounded with their own exception handling.
        ///
        /// Returns null if successful and execution should continue, or an exit code.
        /// </summary>
        private int? TryInteractiveActionWorker()
        {
            int? result = null;
            try
            {
                result = RunOneInteraction();
                return result;
            }
            catch (ThreadAbortException ex)
            {
                KeyboardInterruptException ex2 = ex.ExceptionState as KeyboardInterruptException;
                if (ex2 != null)
                {
                    base.Console.WriteLine(Language.FormatException(ex), Style.Error);
                    Thread.ResetAbort();
                    return result;
                }
                return result;
            }
        }

        /// <summary>
        /// Parses a single interactive command and executes it.  
        ///
        /// Returns null if successful and execution should continue, or the appropiate exit code.
        /// </summary>
        private int? RunOneInteraction()
        {
            bool continueInteraction;
            string text = ReadStatement(out continueInteraction);
            if (!continueInteraction)
            {
                PythonContext.DispatchCommand(null);
                return 0;
            }
            if (string.IsNullOrEmpty(text))
            {
                base.Console.Write(string.Empty, Style.Out);
                return null;
            }

            //单独处理run -i xxx.py
            bool RunMagic = false;
            string ShortFileName = "<stdin>";
            if (text.Trim(' ','\t').StartsWith("run -i"))
            {
                RunMagic = true;
                string FileName = text.Trim(' ', '\t').Substring("run -i".Length).Trim(' ', '"', '\r', '\n', '\t');
                ShortFileName = System.IO.Path.GetFileName(FileName);
                text = System.IO.File.ReadAllText(FileName);

                FileName = System.IO.Path.GetFullPath(FileName);
                var curDir = System.IO.Path.GetDirectoryName(FileName);
                //os.chdir(r'PythonScript\Credo410Python\TestScript')
                Directory.SetCurrentDirectory(curDir); //经过查看源码，IronPython里面os.chdir的代码复制过来了。对本系统影响未知，先打印个日志。
                base.Console.Write("切换当前目录"+curDir+"\n", Style.Warning);
            }

            SourceUnit su = Language.CreateSnippet(text, ShortFileName, RunMagic? SourceCodeKind.File : SourceCodeKind.InteractiveCode);
            PythonCompilerOptions pco = (PythonCompilerOptions)Language.GetCompilerOptions(base.Scope);
            pco.Module |= ModuleOptions.ExecOrEvalCode;



            Action command = delegate
            {
                try
                {
#if true //模式1运行，直接在scope上跑
                    var x1 = su.Compile(pco, ErrorSink);
                    var x2 = x1.Run(base.Scope);
#endif

#if false //模式2运行
                    var x3=su.Execute(base.Scope);
                    var x31 = (x1.LanguageContext as PythonContext)?.BuiltinModuleDict;
                    if (x31?.ContainsKey("_") ?? false)
                    {
                        var x5 = x31["_"].ToString(); //最后一次的返回值
                    }
#endif
#if false //查看并补丁sys.stdout变量，已经移动到Console的创建的时候了。
                    //var sys_dict_stdout = this.PythonContext.SystemState.Get__dict__()["stdout"];
                    //if (sys_dict_stdout != this.Console.Output)
                    //    this.PythonContext.SystemState.Get__dict__()["stdout"] = this.Console.Output;
#endif

#if false //模式3运行，
                    var src2 = Engine.CreateScriptSourceFromString(text);
                    var x4 = src2.Execute(base.ScriptScope);
                    //// 尝试获取最后一行的执行结果（假设结果在变量 "_" 中）并且会一直保存。。。。
                    //object lastLineResult = null;
                    //if (base.ScriptScope.TryGetVariable("_", out var result))
                    //{
                    //    lastLineResult = result;
                    //}
#endif
                }
                catch (Exception ex2)
                {
                    if (ex2 is SystemExitException)
                    {
                        throw;
                    }
                    UnhandledException(ex2);
                }
            };
            try
            {
                PythonContext.DispatchCommand(command);
            }
            catch (SystemExitException ex)
            {
                object otherCode;
                return ex.GetExitCode(out otherCode);
            }
            return null;
        }

        public void SetScopeVariable(string Name, object Value)
        {
            if(Scope==null)
                Scope = CreateScope();
            var x = Scope.Storage;
        }
    }
}
