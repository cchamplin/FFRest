using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TranscodeProcessor
{
    public delegate void ProcessEventHandler(object sender, string message);

    public class ProcessWrapper : Process, IProcessAsyncOperation
    {
        private Thread captureOutputThread;
        private Thread captureErrorThread;
        ManualResetEvent endEventOut = new ManualResetEvent(false);
        ManualResetEvent endEventErr = new ManualResetEvent(false);
        bool done;
        object lockObj = new object();

        public ProcessWrapper()
        {
        }
        public bool CancelRequested { get; private set; }

        public new void Start()
        {
            CheckDisposed();
            base.Start();

            captureOutputThread = new Thread(new ThreadStart(CaptureOutput));
            captureOutputThread.Name = "Process output reader";
            captureOutputThread.IsBackground = true;
            captureOutputThread.Start();

            if (ErrorStreamChanged != null)
            {
                captureErrorThread = new Thread(new ThreadStart(CaptureError));
                captureErrorThread.Name = "Process error reader";
                captureErrorThread.IsBackground = true;
                captureErrorThread.Start();
            }
            else
            {
                endEventErr.Set();
            }
        }

        public void WaitForOutput(int milliseconds)
        {
            CheckDisposed();
            WaitForExit(milliseconds);
            endEventOut.WaitOne();
        }

        public void WaitForOutput()
        {
            WaitForOutput(-1);
        }

        private void CaptureOutput()
        {
            try
            {
                if (OutputStreamChanged != null)
                {
                    char[] buffer = new char[1024];
                    int nr;
                    while ((nr = StandardOutput.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (OutputStreamChanged != null)
                            OutputStreamChanged(this, new string(buffer, 0, nr));
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // There is no need to keep propagating the abort exception
                Thread.ResetAbort();
            }
            finally
            {
                // WORKAROUND for "Bug 410743 - wapi leak in System.Diagnostic.Process"
                // Process leaks when an exit event is registered
                if (endEventErr != null)
                    endEventErr.WaitOne();

                OnExited(this, EventArgs.Empty);

                lock (lockObj)
                {
                    //call this AFTER the exit event, or the ProcessWrapper may get disposed and abort this thread
                    if (endEventOut != null)
                        endEventOut.Set();
                }
            }
        }

        private void CaptureError()
        {
            try
            {
                char[] buffer = new char[1024];
                int nr;
                while ((nr = StandardError.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (ErrorStreamChanged != null)
                        ErrorStreamChanged(this, new string(buffer, 0, nr));
                }
            }
            finally
            {
                lock (lockObj)
                {
                    if (endEventErr != null)
                        endEventErr.Set();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            lock (lockObj)
            {
                if (endEventOut == null)
                    return;

                if (!done)
                    ((IAsyncOperation)this).Cancel();

                captureOutputThread = captureErrorThread = null;
                endEventOut.Close();
                endEventErr.Close();
                endEventOut = endEventErr = null;
            }

            // HACK: try/catch is a workaround for broken Process.Dispose implementation in Mono < 3.2.7
            // https://bugzilla.xamarin.com/show_bug.cgi?id=10883
            try
            {
                base.Dispose(disposing);
            }
            catch
            {
                if (disposing)
                    throw;
            }
        }

        void CheckDisposed()
        {
            if (endEventOut == null)
                throw new ObjectDisposedException("ProcessWrapper");
        }

        int IProcessAsyncOperation.ExitCode
        {
            get { return ExitCode; }
        }

        int IProcessAsyncOperation.ProcessId
        {
            get { return Id; }
        }

        void IAsyncOperation.Cancel()
        {
            try
            {
                if (!done)
                {
                    try
                    {
                        CancelRequested = true;
                        base.Kill();
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        void IAsyncOperation.WaitForCompleted()
        {
            WaitForOutput();
        }

        void OnExited(object sender, EventArgs args)
        {
            try
            {
                if (!HasExited)
                    WaitForExit();
            }
            catch
            {
                // Ignore
            }
            finally
            {
                lock (lockObj)
                {
                    done = true;
                    try
                    {
                        if (completedEvent != null)
                            completedEvent(this);
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }

        event OperationHandler IAsyncOperation.Completed
        {
            add
            {
                bool raiseNow = false;
                lock (lockObj)
                {
                    if (done)
                        raiseNow = true;
                    else
                        completedEvent += value;
                }
                if (raiseNow)
                    value(this);
            }
            remove
            {
                lock (lockObj)
                {
                    completedEvent -= value;
                }
            }
        }

        bool IAsyncOperation.Success
        {
            get { return done ? ExitCode == 0 : false; }
        }

        bool IAsyncOperation.SuccessWithWarnings
        {
            get { return false; }
        }

        bool IAsyncOperation.IsCompleted
        {
            get { return done; }
        }

        event OperationHandler completedEvent;

        public event ProcessEventHandler OutputStreamChanged;
        public event ProcessEventHandler ErrorStreamChanged;
    }

    public interface IProcessAsyncOperation : IAsyncOperation, IDisposable
    {
        int ExitCode { get; }

        int ProcessId { get; }
    }

    public class NullProcessAsyncOperation : NullAsyncOperation, IProcessAsyncOperation
    {
        public NullProcessAsyncOperation(bool success) : base(success, false) { }
        public int ExitCode { get { return ((IAsyncOperation)this).Success ? 0 : 1; } }
        public int ProcessId { get { return 0; } }

        void IDisposable.Dispose() { }

        public new static NullProcessAsyncOperation Success = new NullProcessAsyncOperation(true);
        public new static NullProcessAsyncOperation Failure = new NullProcessAsyncOperation(false);
    }
    public delegate void OperationHandler(IAsyncOperation op);

    public interface IAsyncOperation
    {
        void Cancel();
        void WaitForCompleted();
        bool IsCompleted { get; }
        bool Success { get; }
        bool SuccessWithWarnings { get; }

        event OperationHandler Completed;
    }
    public class NullAsyncOperation : IAsyncOperation
    {
        public static NullAsyncOperation Success = new NullAsyncOperation(true, false);
        public static NullAsyncOperation Failure = new NullAsyncOperation(false, false);

        bool success;
        bool warnings;

        protected NullAsyncOperation(bool success, bool warnings)
        {
            this.success = success;
            this.warnings = warnings;
        }

        public void Cancel()
        {
        }

        public void WaitForCompleted()
        {
        }

        public bool IsCompleted
        {
            get { return true; }
        }

        bool IAsyncOperation.Success
        {
            get { return success; }
        }

        bool IAsyncOperation.SuccessWithWarnings
        {
            get { return success && warnings; }
        }

        public event OperationHandler Completed
        {
            add { value(this); }
            remove { }
        }
    }
}
