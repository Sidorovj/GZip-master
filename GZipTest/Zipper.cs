using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    enum ZipMode
    {
        Hold,
        Compressing,
        Decompressing
    }

    public class Zipper:IZipper
    {
        ZipMode zipMode = ZipMode.Hold;
         bool _cancelled = false;
         bool _success = false;
        string sourceFile;
        string destinationFile;
         static int _threadsCount = Environment.ProcessorCount;

         int blockSize = 1024*1024;
        int counterForDecompress = 0;
         QueueManager _queueReader = new QueueManager();
         QueueManager _queueWriter = new QueueManager();
         ManualResetEvent[] doneEvents = new ManualResetEvent[_threadsCount];

        public int Compress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Compressing;
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;
            Launch();
            return 1;
        }
        public int Decompress(string sourceFile, string destinationFile)
        {
            zipMode = ZipMode.Decompressing;
            this.sourceFile = sourceFile;
            this.destinationFile = destinationFile;
            Launch();
            return 1;
        }

        public bool GetCommandResult()
        {
            return !_cancelled && _success;
        }

        public void Cancel()
        {
            _cancelled = true;
        }
        

        private void Launch()
        {
            if (zipMode == ZipMode.Hold)
                throw new Exception("Please select the command to execute");
            Console.WriteLine("{0}...\n", zipMode);

            Thread _reader = new Thread(new ThreadStart(Read));

            _reader.Start();

            for (int i = 0; i < _threadsCount; i++)
            {
                doneEvents[i] = new ManualResetEvent(false);
                if (zipMode == ZipMode.Compressing)
                    ThreadPool.QueueUserWorkItem(CompressExecute, i);
                else
                    ThreadPool.QueueUserWorkItem(DecompressExecute, i);
                
            }

            Thread _writer = new Thread(new ThreadStart(Write));
            _writer.Start();

            WaitHandle.WaitAll(doneEvents);

            if (!_cancelled)
            {
                Console.WriteLine("\nOperation succesfull");
                _success = true;
            }
        }


        private void CompressExecute(object i)
        {
            try
            {
                while (true && !_cancelled)
                {
                    ByteBlock _block = _queueReader.Dequeue();

                    if (_block == null)
                        return;

                    using (MemoryStream _memoryStream = new MemoryStream())
                    {
                        using (GZipStream cs = new GZipStream(_memoryStream, CompressionMode.Compress))
                        {
                            cs.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }
                        
                        byte[] compressedData = _memoryStream.ToArray();
                        ByteBlock _out = new ByteBlock(_block.Id, compressedData);
                        _queueWriter.EnqueueForWriting(_out);
                    }
                    ManualResetEvent doneEvent = doneEvents[(int)i];
                    doneEvent.Set();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}", i, ex.Message);
                _cancelled = true;
            }

        }
        private void DecompressExecute(object i)
        {
            try
            {
                while (true && !_cancelled)
                {
                    ByteBlock _block = _queueReader.Dequeue();
                    if (_block == null)
                        return;

                    using (MemoryStream ms = new MemoryStream(_block.CompressedBuffer))
                    {
                        using (GZipStream _gz = new GZipStream(ms, CompressionMode.Decompress))
                        {
                            _gz.Read(_block.Buffer, 0, _block.Buffer.Length);
                            byte[] decompressedData = _block.Buffer.ToArray();
                            ByteBlock block = new ByteBlock(_block.Id, decompressedData);
                            _queueWriter.EnqueueForWriting(block);
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                Console.WriteLine("Error in thread number {0}. \r\n Error description: {1}", i, ex.Message);
                _cancelled = true;
            }
        }

        private void Read()
        {
            try
            {
                if (zipMode == ZipMode.Compressing)
                    using (FileStream _fileToBeCompressed = new FileStream(sourceFile, FileMode.Open))
                    {

                        int bytesCount;
                        byte[] lastBuffer;

                        while (_fileToBeCompressed.Position < _fileToBeCompressed.Length && !_cancelled)
                        {
                            if (_fileToBeCompressed.Length - _fileToBeCompressed.Position <= blockSize)
                            {
                                bytesCount = (int)(_fileToBeCompressed.Length - _fileToBeCompressed.Position);
                            }
                            else
                            {
                                bytesCount = blockSize;
                            }

                            lastBuffer = new byte[bytesCount];
                            _fileToBeCompressed.Read(lastBuffer, 0, bytesCount);
                            _queueReader.EnqueueForCompressing(lastBuffer);
                            ConsoleProgressBar.DrawTextProgressBar(_fileToBeCompressed.Position, _fileToBeCompressed.Length);
                        }

                    }
                else
                    using (FileStream _compressedFile = new FileStream(sourceFile, FileMode.Open))
                    {
                        while (_compressedFile.Position < _compressedFile.Length && !_cancelled)
                        {
                            byte[] lengthBuffer = new byte[8];
                            _compressedFile.Read(lengthBuffer, 0, lengthBuffer.Length);
                            int blockLength = BitConverter.ToInt32(lengthBuffer, 4);
                            byte[] compressedData = new byte[blockLength];
                            lengthBuffer.CopyTo(compressedData, 0);

                            _compressedFile.Read(compressedData, 8, blockLength - 8);
                            int _dataSize = BitConverter.ToInt32(compressedData, blockLength - 4);
                            byte[] lastBuffer = new byte[_dataSize];

                            ByteBlock _block = new ByteBlock(counterForDecompress, lastBuffer, compressedData);
                            _queueReader.EnqueueForWriting(_block);
                            counterForDecompress++;
                            ConsoleProgressBar.DrawTextProgressBar(_compressedFile.Position, _compressedFile.Length);
                        }
                        _queueReader.Stop();
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _cancelled = true;
            }
        }

        private void Write()
        {
            try
            {
                if (ZipMode.Compressing == zipMode)
                    using (FileStream _fileCompressed = new FileStream(destinationFile + ".gz", FileMode.Append))
                    {
                        while (true && !_cancelled)
                        {
                            ByteBlock _block = _queueWriter.Dequeue();
                            if (_block == null)
                                return;

                            BitConverter.GetBytes(_block.Buffer.Length).CopyTo(_block.Buffer, 4);
                            _fileCompressed.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }
                    }
                else
                    using (FileStream _decompressedFile = new FileStream(sourceFile.Remove(sourceFile.Length - 3), FileMode.Append))
                    {
                        while (true && !_cancelled)
                        {
                            ByteBlock _block = _queueWriter.Dequeue();
                            if (_block == null)
                                return;


                            _decompressedFile.Write(_block.Buffer, 0, _block.Buffer.Length);
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                _cancelled = true;
            }
        }
    }
}
