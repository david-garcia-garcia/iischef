using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace iischef.utils
{
    /// <summary>
    /// 
    /// </summary>
    public class StreamReaderLineInput : IDisposable
    {
        private char charNewline = "\n".ToCharArray().First();
        private char charError = "\0".ToCharArray().First();

        // On "old" printers, \r sent the print head back to the start of the line
        private char charCarriage = "\r".ToCharArray().First();

        private Thread ThreadReader;
        private CancellationTokenWrapper CancellationToken;

        private Action<string, string> writer;
        private volatile bool abort = false;

        private Dictionary<string, StreamReader> InputReaders = new Dictionary<string, StreamReader>();
        private Dictionary<string, StringBuilder> OuputBuffers = new Dictionary<string, StringBuilder>();
        private Dictionary<string, Task<int>> CharReaderTasks = new Dictionary<string, Task<int>>();
        private Dictionary<string, char[]> CharReaderBuffer = new Dictionary<string, char[]>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="cancellationToken"></param>
        public StreamReaderLineInput(Action<string, string> writer, CancellationTokenWrapper cancellationToken)
        {
            this.writer = writer;
            this.CancellationToken = cancellationToken;
            this.ThreadReader = new Thread(this.Read);
        }

        public bool Reading
        {
            get
            {
                var threadState = this.ThreadReader.ThreadState;

                if (threadState == ThreadState.Running
                    || threadState == ThreadState.Background
                    || threadState == ThreadState.WaitSleepJoin)
                {
                    return true;
                }

                // Se están cortando las descompresiones a medias, creo que el bug está en le interpretación
                // de estos estados
                if (Environment.UserInteractive)
                {
                    Console.WriteLine($"Stream Reader NOT reading with thread state = {threadState}");
                }

                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void StartReading()
        {
            this.ThreadReader.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public void StopReading()
        {
            this.abort = true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="input"></param>
        public void AddReader(string name, StreamReader input)
        {
            this.OuputBuffers.Add(name, new StringBuilder());
            this.InputReaders.Add(name, input);
        }

        /// </summary>
        public void DoRead()
        {
            if (this.CancellationToken?.IsCancellationRequested == true)
            {
                this.abort = true;
                return;
            }

            foreach (var streamName in this.InputReaders.Keys)
            {
                while (this.Read(streamName, out var output))
                {
                    if (this.CancellationToken?.IsCancellationRequested == true)
                    {
                        this.abort = true;
                        return;
                    }

                    this.AddOutput(streamName, output);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void Read()
        {
            while (true && !this.abort)
            {
                this.DoRead();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private bool Read(string stream, out char output)
        {
            if (!this.CharReaderTasks.ContainsKey(stream))
            {
                this.CharReaderBuffer[stream] = new char[1];
                this.CharReaderTasks[stream] = this.InputReaders[stream].ReadAsync(this.CharReaderBuffer[stream], 0, 1);
            }

            var task = this.CharReaderTasks[stream];
            task.Wait(10);

            if (task.IsCompleted)
            {
                output = this.CharReaderBuffer[stream][0];
                this.CharReaderTasks.Remove(stream);
                this.CharReaderBuffer.Remove(stream);
                return task.Result > 0;
            }

            output = new char();
            return false;
        }

        /// <summary>
        /// Añade un output a la consola, que proviene de los streams!
        /// </summary>
        /// <param name="streamName"></param>
        /// <param name="output"></param>
        private void AddOutput(string streamName, char output)
        {
            var buffer = this.OuputBuffers[streamName];
            buffer.Append(output);

            bool send = output == this.charError || output == this.charNewline || output == this.charCarriage;

            if (send)
            {
                if (buffer.Length > 0)
                {
                    this.writer(streamName, buffer.ToString());
                    buffer.Clear();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.ThreadReader.Abort();
        }
    }
}
