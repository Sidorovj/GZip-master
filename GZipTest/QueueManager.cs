using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;


namespace GZipTest
{
    /// <summary>
    /// Массив байтов для компрессии/распаковки
    /// </summary>
    public class ByteBlock
    {
        public int Id { get; private set; }
        public byte[] Buffer { get; private set; }
        public byte[] CompressedBuffer { get; private set; }


        public ByteBlock(int id, byte[] buffer) : this(id, buffer, new byte[0])
        {

        }

        public ByteBlock(int id, byte[] buffer, byte[] compressedBuffer)
        {
            this.Id = id;
            this.Buffer = buffer;
            this.CompressedBuffer = compressedBuffer;
        }

    }

    /// <summary>
    /// Очередь для параллельной работы с массивами байтов
    /// </summary>
    public class QueueManager
    {
        private object locker = new object();
        Queue<ByteBlock> queue = new Queue<ByteBlock>();
        bool isDead = false;
        private int blockId = 0;

        /// <summary>
        /// Добавить элемент в очередь на запись в файл
        /// </summary>
        /// <param name="_block"></param>
        public void EnqueueForWriting(ByteBlock _block)
        {
            int id = _block.Id;
            lock (locker)
            {
                if (isDead)
                    throw new InvalidOperationException("Queue already stopped");

                while (id != blockId)
                {
                    Monitor.Wait(locker);
                }

                queue.Enqueue(_block);
                blockId++;
                Monitor.PulseAll(locker);
            }
        }

        /// <summary>
        /// Добавить элемент в очередь на сжатие
        /// </summary>
        /// <param name="buffer"></param>
        public void EnqueueForCompressing(byte[] buffer)
        {
            lock (locker)
            {
                if (isDead)
                    throw new InvalidOperationException("Queue already stopped");

                ByteBlock _block = new ByteBlock(blockId, buffer);
                queue.Enqueue(_block);
                blockId++;
                Monitor.PulseAll(locker);
            }
        }
        
        /// <returns>Первый элемент очереди</returns>
        public ByteBlock Dequeue()
        {
            lock (locker)
            {
                if (queue.Count == 0)
                    while (queue.Count == 0 && !isDead)
                        Monitor.Wait(locker);

                if (queue.Count == 0)
                    return null;

                return queue.Dequeue();

            }
        }

        /// <summary>
        /// Остановка очереди
        /// </summary>
        public void Stop()
        {
            lock (locker)
            {
                isDead = true;
                Monitor.PulseAll(locker);
            }
        }
    }
}