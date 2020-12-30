﻿namespace Unosquare.FFplaySharp.Primitives
{
    using FFmpeg.AutoGen;
    using System;
    using System.Threading;
    using Unosquare.FFplaySharp.Components;

    public unsafe sealed class PacketQueue : ISerialProvider, IDisposable
    {
        private readonly object SyncLock = new();
        private readonly AutoResetEvent IsAvailableEvent = new(false);
        private bool m_IsClosed = true; // starts in a blocked state
        private PacketHolder First;
        private PacketHolder Last;

        private int m_Count;
        private int m_Size;
        private long m_DurationUnits;
        private int m_Serial;

        public PacketQueue(MediaComponent component)
        {
            Component = component;
        }

        public MediaComponent Component { get; }

        public int Count
        {
            get { lock (SyncLock) return m_Count; }
            private set { lock (SyncLock) m_Count = value; }
        }

        public int Size
        {
            get { lock (SyncLock) return m_Size; }
            private set { lock (SyncLock) m_Size = value; }
        }

        public long DurationUnits
        {
            get { lock (SyncLock) return m_DurationUnits; }
            private set { lock (SyncLock) m_DurationUnits = value; }
        }

        public int Serial
        {
            get { lock (SyncLock) return m_Serial; }
            private set { lock (SyncLock) m_Serial = value; }
        }

        public bool IsClosed
        {
            get { lock (SyncLock) return m_IsClosed; }
        }

        public bool PutFlush() => Put(PacketHolder.CreateFlushPacket());

        public bool PutNull()
        {
            var packet = ffmpeg.av_packet_alloc();
            packet->data = null;
            packet->size = 0;
            packet->stream_index = Component.StreamIndex;
            return Put(packet);
        }

        public bool Put(AVPacket* packetPtr)
        {
            var result = true;
            lock (SyncLock)
            {
                var newPacket = new PacketHolder(packetPtr) { Next = null };

                if (m_IsClosed)
                {
                    result = false;
                }
                else
                {
                    if (newPacket.IsFlushPacket)
                        Serial++;

                    newPacket.Serial = Serial;

                    if (Last == null)
                        First = newPacket;
                    else
                        Last.Next = newPacket;

                    Last = newPacket;
                    Count++;
                    Size += newPacket.PacketPtr->size + PacketHolder.PacketStructureSize;
                    DurationUnits += newPacket.PacketPtr->duration;
                    IsAvailableEvent.Set();
                }

                if (!result)
                    newPacket.Dispose();
            }

            return result;
        }

        public void Clear()
        {
            lock (SyncLock)
            {
                for (var currentPacket = First; currentPacket != null; currentPacket = currentPacket?.Next)
                    currentPacket?.Dispose();

                Last = null;
                First = null;
                Count = 0;
                Size = 0;
                DurationUnits = 0;
            }
        }

        public void Dispose()
        {
            lock (SyncLock)
            {
                Close();
                Clear();
            }

            IsAvailableEvent.Set();
            IsAvailableEvent.Dispose();
        }

        public void Close()
        {
            lock (SyncLock)
                m_IsClosed = true;

            IsAvailableEvent.Set();
        }

        public void Open()
        {
            lock (SyncLock)
            {
                m_IsClosed = false;
                PutFlush();
            }
        }

        public PacketHolder Get(bool block)
        {
            while (true)
            {
                if (IsClosed)
                    return null;

                lock (SyncLock)
                {
                    var item = First;
                    if (item != null)
                    {
                        First = item.Next;
                        if (First == null)
                            Last = null;

                        Count--;
                        Size -= item.PacketPtr->size + PacketHolder.PacketStructureSize;
                        DurationUnits -= item.PacketPtr->duration;
                        return item;
                    }
                }

                if (!block)
                    return null;
                else
                    IsAvailableEvent.WaitOne(1);
            }
        }
    }
}
