using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GZipTest
{
    public class Compressor : Zipper
    {
        public Compressor(string sourceFile, string destinationFile) : base(sourceFile, destinationFile)
        {
            zipMode = ZipMode.Compressing;
        }

        public override bool Execute()
        {
            return ExecuteOperation(CompressExecute);
        }

        /// <summary>
        /// Считывание файла
        /// </summary>
        protected override void ReadFromFile()
        {
            Debug.WriteLine("Read start");
            try
            {
                using (FileStream _sourceFile = new FileStream(sourceFile, FileMode.Open))
                {
                    int _bytesCount;
                    byte[] _lastBuffer;
                    totalBlockCount = (long)Math.Ceiling((decimal)(_sourceFile.Length * 1.0 / blockSize));
                    ConsoleProgressBar.SetFileSize(_sourceFile.Length);

                    while (_sourceFile.Position < _sourceFile.Length && !cancelled)
                    {
                        if (_sourceFile.Length - _sourceFile.Position <= blockSize)
                        {
                            _bytesCount = (int)(_sourceFile.Length - _sourceFile.Position);
                        }
                        else
                        {
                            _bytesCount = blockSize;
                        }
                        _lastBuffer = new byte[_bytesCount];
                        _sourceFile.Read(_lastBuffer, 0, _bytesCount);
                        queueReader.EnqueueForCompressing(_lastBuffer);
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
                queueReader.Stop();
                Debug.WriteLine("Read end");
            }
        }

        /// <summary>
        /// Выполнение сжатия
        /// </summary>
        /// <param name="i"></param>
        private void CompressExecute(object i)
        {
            Debug.WriteLine("Thrd {0} start", i);
            try
            {
                while (true && !cancelled)
                {
                    ByteBlock _block = queueReader.Dequeue();

                    if (_block == null)
                    {
                        return;
                    }

                    using (MemoryStream _memoryStream = new MemoryStream())
                    {
                        using (GZipStream _cs = new GZipStream(_memoryStream, CompressionMode.Compress))
                        {
                            _cs.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }

                        byte[] _compressedData = _memoryStream.ToArray();
                        ByteBlock _out = new ByteBlock(_block.Id, _compressedData);
                        queueWriter.EnqueueForWriting(_out);
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}\r\nStackTrace: {2}", i, ex.Message, ex.StackTrace);
                cancelled = true;
            }
            finally
            {
                Debug.WriteLine("Thrd {0} end", i);
                ManualResetEvent _doneEvent = doneEvents[(int)i];
                _doneEvent.Set();
            }
        }
    }
}
