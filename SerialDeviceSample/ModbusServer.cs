using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace SerialDeviceSample
{
    public class ModbusServer
    {
        public enum ModbusServerStatus
        {
            Opened, Opening, Closed, None, Initialled
        }

        private ModbusServerStatus status = ModbusServerStatus.None;
        public ModbusServerStatus Status
        {
            get { return status; }
            set { status = value; }
        }

        SerialDevice serialPort = null;
        DataWriter dataWrite = null;
        DataReader dataReader = null;
        CancellationTokenSource readCancellationTokenSource;
        IBuffer readBuffer;

        const double WRITE_TIMEOUT = 10;
        const double READ_TIMEOUT = 10;
        const uint BAUD_RATE = 9600;
        const SerialParity SERIAL_PARITY = SerialParity.None;
        const SerialStopBitCount SERIAL_STOP_BIT_COUNT = SerialStopBitCount.One;
        const ushort DATA_BITS = 8;
        const SerialHandshake SERIAL_HANDSHAKE = SerialHandshake.None;

        string portName;

        public ModbusServer(string portName)
        {
            this.portName = portName;
            Status = ModbusServerStatus.Initialled;
        }

        public async Task Open()
        {
            if (Status == ModbusServerStatus.Initialled)
            {
                try
                {
                    Status = ModbusServerStatus.Opening;
                    string aqs = SerialDevice.GetDeviceSelector(portName);
                    var dis = await DeviceInformation.FindAllAsync(aqs);
                    serialPort = await SerialDevice.FromIdAsync(dis[0].Id);

                    serialPort.WriteTimeout = TimeSpan.FromMilliseconds(WRITE_TIMEOUT);
                    serialPort.ReadTimeout = TimeSpan.FromMilliseconds(READ_TIMEOUT);
                    serialPort.BaudRate = BAUD_RATE;
                    serialPort.Parity = SERIAL_PARITY;
                    serialPort.StopBits = SERIAL_STOP_BIT_COUNT;
                    serialPort.DataBits = DATA_BITS;
                    serialPort.Handshake = SERIAL_HANDSHAKE;

                    readCancellationTokenSource = new CancellationTokenSource();

                    listen(1024);
                    Status = ModbusServerStatus.Opened;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        public async Task Open(double writeTimeout, double readTimeout)
        {
            await Open();
            serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writeTimeout);
            serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readTimeout);
        }

        public void Clear()
        {
            if (readBuffer != null) readBuffer = null;
        }

        private async void listen(uint bufferLength)
        {
            try
            {
                if (serialPort != null)
                {
                    dataReader = new DataReader(serialPort.InputStream);

                    while (true)
                    {
                        await readAsync(bufferLength, readCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    if (serialPort != null)
                    {
                        serialPort.Dispose();
                    }
                    serialPort = null;
                }
            }
            finally
            {
                if (dataReader != null)
                {
                    dataReader.DetachStream();
                    dataReader = null;
                }
            }
        }

        private async Task readAsync(uint readBufferLength, CancellationToken cancellationToken)
        {
            try
            {
                Task<uint> loadAsyncTask;

                cancellationToken.ThrowIfCancellationRequested();

                dataReader.InputStreamOptions = InputStreamOptions.Partial;

                loadAsyncTask = dataReader.LoadAsync(readBufferLength).AsTask(cancellationToken);

                uint bytesRead = await loadAsyncTask;
                if (bytesRead > 0)
                {
                    readBuffer = dataReader.ReadBuffer(bytesRead);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<IBuffer> Receive()
        {
            do
            {
                await Task.Delay(5);
            }
            while (readBuffer == null);

            return readBuffer;
        }

        public async Task Send(IBuffer buffer)
        {
            try
            {
                if (serialPort != null)
                {
                    dataWrite = new DataWriter(serialPort.OutputStream);
                    await writeAsync(buffer);
                }
                else
                {
                    throw new Exception("Establish a connection before send buffer please");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (dataWrite != null)
                {
                    dataWrite.DetachStream();
                    dataWrite = null;
                }
            }
        }

        private async Task writeAsync(IBuffer buffer)
        {
            Task<uint> storeAsyncTask;

            if (buffer.Length != 0)
            {
                dataWrite.WriteBuffer(buffer);

                storeAsyncTask = dataWrite.StoreAsync().AsTask();
                uint bytesWritten = await storeAsyncTask;
                if (bytesWritten == 0)
                {
                    throw new Exception("writeAsync : Failure to send message");
                }
            }
            else
            {
                throw new Exception("writeAsync : Failure to establish a buffer");
            }
        }

        public void Cancel()
        {
            if (readCancellationTokenSource != null)
            {
                if (!readCancellationTokenSource.IsCancellationRequested)
                {
                    readCancellationTokenSource.Cancel();
                }
            }
            Status = ModbusServerStatus.Closed;
        }

        public void Close()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;
            Status = ModbusServerStatus.None;
        }

        public void SetWritingTimeout(double writingTimeout)
        {
            if (Status == ModbusServerStatus.Opened)
                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(writingTimeout);
        }

        public void SetReadingTimeout(double readingTimeout)
        {
            if (Status == ModbusServerStatus.Opened)
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(readingTimeout);
        }
    }
}
