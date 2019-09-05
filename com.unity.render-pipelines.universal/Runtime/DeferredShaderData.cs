using System;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace UnityEngine.Rendering.Universal
{
    struct Vector4UInt
    {
        public uint x;
        public uint y;
        public uint z;
        public uint w;

        public Vector4UInt(uint _x, uint _y, uint _z, uint _w)
        {
            x = _x;
            y = _y;
            z = _z;
            w = _w;
        }
    };


    class DeferredShaderData : IDisposable
    {
        static DeferredShaderData m_Instance = null;

        /// Precomputed tiles.
        NativeArray<PreTile> m_PreTiles;
        // Store tileData for drawing instanced tiles.
        ComputeBuffer[,] m_TileDataBuffers = null;
        // Store point lights data for a draw call.
        ComputeBuffer[,] m_PointLightBuffers = null;
        // Store lists of lights. Each tile has a list of lights, which start address is given by m_TileRelLightBuffer.
        // The data stored is a relative light index, which is an index into m_PointLightBuffer. 
        ComputeBuffer[,] m_RelLightIndexBuffers = null;

        int m_TileDataBuffer_UsedCount = 0;
        int m_PointLightBuffer_UsedCount = 0;
        int m_RelLightIndexBuffer_UsedCount = 0;

        int m_FrameLatency = 4;
        int m_FrameIndex = 0;

        DeferredShaderData()
        {
            // TODO: make it a vector
            m_TileDataBuffers = new ComputeBuffer[m_FrameLatency, 32]; 
            m_PointLightBuffers = new ComputeBuffer[m_FrameLatency,32];
            m_RelLightIndexBuffers = new ComputeBuffer[m_FrameLatency,32];

            m_TileDataBuffer_UsedCount = 0;
            m_PointLightBuffer_UsedCount = 0;
            m_RelLightIndexBuffer_UsedCount = 0;
        }

        internal static DeferredShaderData instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = new DeferredShaderData();

                return m_Instance;
            }
        }

        public void Dispose()
        {
            DisposeNativeArray(ref m_PreTiles);
            DisposeBuffers(m_TileDataBuffers);
            DisposeBuffers(m_PointLightBuffers);
            DisposeBuffers(m_RelLightIndexBuffers);
        }

        internal void ResetBuffers()
        {
            m_TileDataBuffer_UsedCount = 0;
            m_PointLightBuffer_UsedCount = 0;
            m_RelLightIndexBuffer_UsedCount = 0;
            m_FrameIndex = (m_FrameIndex + 1) % m_FrameLatency;
        }

        internal NativeArray<PreTile> GetPreTiles(int count)
        {
            return GetOrUpdateNativeArray<PreTile>(ref m_PreTiles, count);
        }

        internal ComputeBuffer ReserveTileDataBuffer(int count)
        {
            return GetOrUpdateBuffer<Vector4UInt>(m_TileDataBuffers, count, ComputeBufferType.Constant, m_TileDataBuffer_UsedCount++);
        }

        internal ComputeBuffer ReservePointLightBuffer(int count)
        {
#if UNITY_SUPPORT_STRUCT_IN_CBUFFER
            return GetOrUpdateBuffer<PointLightData>(m_PointLightBuffers, count, ComputeBufferType.Constant, m_PointLightBuffer_UsedCount++);
#else
            int sizeof_PointLightData = System.Runtime.InteropServices.Marshal.SizeOf(typeof(PointLightData));
            int vec4Count = sizeof_PointLightData / 16;
            return GetOrUpdateBuffer<Vector4UInt>(m_PointLightBuffers, count * vec4Count, ComputeBufferType.Constant, m_PointLightBuffer_UsedCount++);
#endif
        }

        internal ComputeBuffer ReserveRelLightIndexBuffer(int count)
        {
            return GetOrUpdateBuffer<Vector4UInt>(m_RelLightIndexBuffers, (count + 3) / 4, ComputeBufferType.Constant, m_RelLightIndexBuffer_UsedCount++);
        }

        NativeArray<T> GetOrUpdateNativeArray<T>(ref NativeArray<T> nativeArray, int count) where T : struct
        {
            if (!nativeArray.IsCreated)
            {
                nativeArray = new NativeArray<T>(count, Allocator.Persistent);
            }
            else if (count > nativeArray.Length)
            {
                nativeArray.Dispose();
                nativeArray = new NativeArray<T>(count, Allocator.Persistent);
            }

            return nativeArray;
        }

        void DisposeNativeArray<T>(ref NativeArray<T> nativeArray) where T : struct
        {
            if (nativeArray.IsCreated)
                nativeArray.Dispose();
        }

        ComputeBuffer GetOrUpdateBuffer<T>(ComputeBuffer[,] buffers, int count, ComputeBufferType type, int index) where T : struct
        {
            if (buffers[m_FrameIndex,index] == null)
            {
                buffers[m_FrameIndex, index] = new ComputeBuffer(count, Marshal.SizeOf<T>(), type, ComputeBufferMode.Immutable);
            }
            else if (count > buffers[m_FrameIndex, index].count)
            {
                buffers[m_FrameIndex, index].Dispose();
                buffers[m_FrameIndex, index] = new ComputeBuffer(count, Marshal.SizeOf<T>(), type, ComputeBufferMode.Immutable);
            }

            return buffers[m_FrameIndex, index];
        }

        void DisposeBuffers(ComputeBuffer[,] buffers)
        {
            for (int i = 0; i < buffers.GetLength(0); ++i)
            {
                for (int j = 0; j < buffers.GetLength(1); ++j)
                {

                    if (buffers[i, j] != null)
                    {
                        buffers[i, j].Dispose();
                        buffers[i, j] = null;
                    }
                }
            }
        }
    }
}
