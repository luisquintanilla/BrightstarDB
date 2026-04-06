using System;
using System.IO;
using System.Text;

namespace BrightstarDB.Storage
{
    internal class SerializationUtils
    {
        public static int WriteString(BinaryWriter dataStream, string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            var count = WriteVarint(dataStream, (ulong)bytes.Length);
            dataStream.Write(bytes);
            return count;
        }

        public static string ReadString(BinaryReader dataStream)
        {
            var byteCount = (int)ReadVarint(dataStream);
            return Encoding.UTF8.GetString(dataStream.ReadBytes(byteCount), 0, byteCount);
        }

        /// <summary>
        /// Writes a protobuf varint onto the stream and returns the number of bytes used.
        /// </summary>
        /// <param name="dataStream">Stream to write to</param>
        /// <param name="value">value to serialize</param>
        /// <returns>Number of bytes written</returns>
        public static int WriteVarint(BinaryWriter dataStream, ulong value)
        {
            var count = 0;
#if NET10_0_OR_GREATER
            Span<byte> buffer = stackalloc byte[10];
#else
            var buffer = new byte[10];
#endif
            do
            {
                buffer[count] = (byte)((value & 0x7F) | 0x80);
                count++;
            } while ((value >>= 7) != 0);

            buffer[count - 1] &= 0x7F;
#if NET10_0_OR_GREATER
            dataStream.Write(buffer[..count]);
#else
            dataStream.Write(buffer, 0, count);
#endif

            return count;
        }

        /// <summary>
        /// Read a ulong varint from the stream provided. 
        /// </summary>
        /// <param name="dataStream">datastream containing the varint</param>
        /// <returns>The value as a ulong</returns>
        public static ulong ReadVarint(BinaryReader dataStream)
        {
            // int readPos = 0;
            ulong value = dataStream.ReadByte(); // ioBuffer[readPos++];
            if ((value & 0x80) == 0) return value;
            value &= 0x7F;

            ulong chunk = dataStream.ReadByte(); // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 7;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 14;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  //ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 21;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 28;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 35;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 42;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 49;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos++];
            value |= (chunk & 0x7F) << 56;
            if ((chunk & 0x80) == 0) return value;

            chunk = dataStream.ReadByte();  // ioBuffer[readPos];
            value |= chunk << 63; // can only use 1 bit from this chunk

            // if ((chunk & ~(ulong)0x01) != 0) throw AddErrorData(new OverflowException(), this);
            return value;
        }

    }
}
