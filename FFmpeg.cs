using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Common.Logging;

namespace TranscodeProcessor
{
    public class FFmpeg
    {
        private static ILog log = LogManager.GetCurrentClassLogger();
        public delegate void LogData(string line);
        public static string Exec(string args, out int exitCode, LogData logger = null)
        {
            log.Debug("Executing Command: " + args);
            try
            {
                ProcessWrapper proc = new ProcessWrapper();
                proc.EnableRaisingEvents = false;
                proc.StartInfo.FileName = FFRest.config["ffmpeg-location"];
                proc.StartInfo.Arguments = args;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardInput = false;
                //proc.Start();

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                proc.OutputStreamChanged += (sender, m) =>
                {
                    if (m != null)
                    {
                        output.Append(m);
                        if (logger != null)
                            logger(m);
                    }
                };
                proc.ErrorStreamChanged += (sender, m) =>
                {
                    if (m != null)
                    {
                        error.Append(m);
                        if (logger != null)
                            logger(m);
                    }
                };

                proc.Start();
                proc.WaitForOutput();
                exitCode = proc.ExitCode;
                return error.ToString() + output.ToString();

            }
            catch (Exception ex)
            {
                log.Debug("Error executing ffmpeg: " + ex.Message);
                log.Debug("Exception: ", ex);
                exitCode = -1;
            }
            return "";
        }
        public static string ExecProbe(string args)
        {
            log.Debug("Executing Probe: " + args);
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.EnableRaisingEvents = false;
                proc.StartInfo.FileName = FFRest.config["ffprobe-location"];
                proc.StartInfo.Arguments = args;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardInput = true;
                //proc.Start();

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();

                using (AutoResetEvent outputWaitHandle = new AutoResetEvent(false))
                using (AutoResetEvent errorWaitHandle = new AutoResetEvent(false))
                {
                    proc.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            outputWaitHandle.Set();
                        }
                        else
                        {
                            output.AppendLine(e.Data);
                        }
                    };
                    proc.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data == null)
                        {
                            errorWaitHandle.Set();
                        }
                        else
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    proc.Start();

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    if (proc.WaitForExit(int.MaxValue) &&
                        outputWaitHandle.WaitOne(int.MaxValue) &&
                        errorWaitHandle.WaitOne(int.MaxValue))
                    {
                        return error.ToString() + output.ToString();
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                log.Debug("Error executing ffprobe: " + ex.Message);
                log.Debug("Exception: ", ex);

            }
            return "";
        }
       
    }
}
