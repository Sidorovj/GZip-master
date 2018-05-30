using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace GZipTest
{
    /// <summary>
    /// Архиватор
    /// </summary>
    public  abstract class Zipper : IZipper
    {
        /// <summary>
        /// Режим работы архиватора
        /// </summary>
        protected enum ZipMode
        {
            Hold,
            Compressing,
            Decompressing
        }
        
        #region Fields and properties

        protected ZipMode zipMode = ZipMode.Hold;
        protected bool cancelled = false;
        protected string sourceFile;
        protected string destinationFile;
        protected int counterForDecompress = 0;
        protected QueueManager queueReader = new QueueManager();
        protected QueueManager queueWriter = new QueueManager();
        protected ManualResetEvent[] doneEvents = new ManualResetEvent[ThreadsCount];
        protected ManualResetEvent writeEndEvent = new ManualResetEvent(false);
        /// <summary>
        /// Для вычисления прогресс-бара
        /// </summary>
        protected long totalBlockCount = 0;
        public const int blockSize = 1024 * 1024;

        protected static int ThreadsCount { get { return Environment.ProcessorCount; } }

        #endregion

        #region Functions:abstract
        public abstract bool Execute();

        /// <summary>
        /// Считывание файла
        /// </summary>
        protected abstract void ReadFromFile();
        #endregion

        #region Functions:public
        
        public void Cancel()
        {
            cancelled = true;
        }

        #endregion

        #region Functions:private/protected


        protected Zipper(string sourceFile, string destinationFile)
        {
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;
        }
        
        /// <summary>
        /// Запуск команды
        /// </summary>
        protected bool ExecuteOperation(ParameterizedThreadStart operation)
        {
            if (zipMode == ZipMode.Hold)
                throw new Exception("Please select the command to execute");
            Console.WriteLine("{0}...", zipMode);

            Thread _reader = new Thread(new ThreadStart(ReadFromFile));
            Thread[] _workers = new Thread[ThreadsCount];

            _reader.Start();

            for (int i = 0; i < ThreadsCount; i++)
            {
                doneEvents[i] = new ManualResetEvent(false);
                _workers[i] = new Thread(operation);
                _workers[i].Start(i);
            }

            Thread _writer = new Thread(new ThreadStart(WriteToFile));
            _writer.Start();

            TimerCallback tmCallback = new TimerCallback(DrawCurrentProgress);
            Timer progressTimer = new Timer(tmCallback, 0, 0, 50);

            WaitHandle.WaitAll(doneEvents);
            queueWriter.Stop();
            WaitHandle.WaitAll(new WaitHandle[] { writeEndEvent });

            progressTimer.Dispose();
            DrawCurrentProgress(null);

            GC.Collect();
            return !cancelled;
        }

        private void DrawCurrentProgress(object obj)
        {
            if (totalBlockCount > 0)
            {
                try {
                    ConsoleProgressBar.DrawTextProgressBar(queueReader.blockId + queueWriter.blockId, totalBlockCount * 2);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }
        }
                
        /// <summary>
        /// Запись в файл 
        /// </summary>
        private void WriteToFile()
        {
            Debug.WriteLine("Write begin");
            try
            {
                using (FileStream _destFile = new FileStream(destinationFile + (zipMode == ZipMode.Compressing ? ".gz" : ""), FileMode.Append))
                {
                    while (true && !cancelled)
                    {
                        ByteBlock _block = queueWriter.Dequeue();
                        if (_block == null)
                        {
                            return;
                        }
                        if (zipMode == ZipMode.Compressing)
                            BitConverter.GetBytes(_block.Buffer.Length).CopyTo(_block.Buffer, 4);
                        _destFile.Write(_block.Buffer, 0, _block.Buffer.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                cancelled = true;
                throw;
            }
            finally
            {
                Debug.WriteLine("Write end");
                writeEndEvent.Set();
            }
        }

        #endregion
    }

}
