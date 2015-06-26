using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace TranscodeProcessor
{
    /// <summary>
    /// Container class for trancoding presets
    /// Presets are saved between restarts in a file calls presets.json
    /// </summary>
    public class Presets
    {
        protected ILog log = LogManager.GetCurrentClassLogger();
        protected ConcurrentDictionary<string, string> presets;
        protected Task presetSaveTask;
        protected CancellationTokenSource cancelToken;
        protected bool saving = false;
        public Presets()
        {
            presets = new ConcurrentDictionary<string,string>();

            // Check if we have an existing preset file
            if (File.Exists("presets.json"))
            {
                FastSerialize.Serializer serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));
                StreamReader sr = new StreamReader(File.Open("presets.json", FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                string data = sr.ReadToEnd();
                sr.Close();

                Dictionary<string, string> saved = serializer.Deserialize<Dictionary<string, string>>(data);
                foreach (var val in saved)
                {
                    presets.TryAdd(val.Key, val.Value);
                }
            }
            cancelToken = new CancellationTokenSource();
            // Start a background task to save the preset file on a regular basis
            presetSaveTask = TaskExecutorFactory.Begin(SaveAll, 5000, 100,Timeout.Infinite,-1,cancelToken.Token);
        }
        protected void SaveAll()
        {
            try {

            saving = true;
            FastSerialize.Serializer serializer = new FastSerialize.Serializer(typeof(FastSerialize.JsonSerializerGeneric));
            var data = serializer.Serialize(presets);
            StreamWriter sw = new StreamWriter(File.Open("presets.json", FileMode.Create, FileAccess.Write, FileShare.Read));
            using (sw)
            {
                sw.Write(data);
            }
            saving = false;
            }
            catch (OperationCanceledException)
            {
                log.Debug("Shutting down presets");
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down presets");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down presets");
            }
            catch (Exception e)
            {
                log.Error("Exception in prests file saving occured", e);
            }
            finally
            {
                saving = false;
            }
        }
        public void Shutdown()
        {
            try
            {
                // Attempt to gracefully shutdown
                cancelToken.Cancel(true);
                int cnt = 0;
                while (saving == true && cnt < 20)
                {
                    Thread.Sleep(15);
                    cnt++;
                }
            }
            catch (ThreadInterruptedException)
            {
                log.Debug("Shutting down stats");
            }
            catch (ThreadAbortException)
            {
                log.Debug("Shutting down starts");
            }
            catch (Exception e)
            {
                log.Error("Exception in data stream occured", e);
            }
            finally
            {
            }
        }
        public void Add(string presetName, string preset)
        {
            presets.AddOrUpdate(presetName, preset, (key, oldValue) => preset);            
        }
        public void Remove(string presetName)
        {
            string existing;
            presets.TryRemove(presetName, out existing);
        }
        public string Get(string presetName)
        {
            string existing;
            if (presets.TryGetValue(presetName, out existing))
            {
                return existing;
            }
            return null;
        }
        public IDictionary<string,string> GetAll()
        {
            return presets;
        }

    }
}
