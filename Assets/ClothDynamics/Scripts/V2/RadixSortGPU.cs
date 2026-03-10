using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using System;

namespace ClothDynamics
{
    [DefaultExecutionOrder(15200)] //When using Final IK
    public class RadixSortGPU
    {
        public ComputeBuffer buffer;
        ComputeShader counting;
        ComputeShader blockscan;
        ComputeShader globalsort;
        ComputeShader addblocksum;
        ComputeBuffer prefixsums;
        ComputeBuffer result;
        List<ComputeBuffer> blocksums;
        int numbits;
        int blocksize;
        int numblocks;

        int count_sortbits(int v)
        {
            int r = 1;
            while ((v >>= 1) != 0 ? true : false)
                r++;
            return r;
        }

        public RadixSortGPU(int _blocksize, int _numblocks)
        {
            Cleanup();
            blocksize = _blocksize;
            numblocks = _numblocks;

            //counting = (ComputeShader)Resources.Load("Shaders/Compute/V2/RadixSort/Counting", typeof(ComputeShader));
            counting = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/RadixSort/Counting");
            if (counting == null)
            {
                Debug.LogError("counting compute shader missing");
                return;
            }
            //blockscan = (ComputeShader)Resources.Load("Shaders/Compute/V2/RadixSort/BlockScan", typeof(ComputeShader));
            blockscan = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/RadixSort/BlockScan");
            if (blockscan == null)
            {
                Debug.LogError("blockscan compute shader missing");
                return;
            }
            //globalsort = (ComputeShader)Resources.Load("Shaders/Compute/V2/RadixSort/GlobalSort", typeof(ComputeShader));
            globalsort = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/RadixSort/GlobalSort");
            if (globalsort == null)
            {
                Debug.LogError("globalsort compute shader missing");
                return;
            }
            //addblocksum = (ComputeShader)Resources.Load("Shaders/Compute/V2/RadixSort/AddBlockSum", typeof(ComputeShader));
            addblocksum = GraphicsUtilities.LoadComputeShaderAt("Shaders/Compute/V2/RadixSort/AddBlockSum");
            if (addblocksum == null)
            {
                Debug.LogError("addblocksum compute shader missing");
                return;
            }

            if ((blocksize & 1) == 0 ? false : true)
                Debug.LogError("The block size for sorting has to be even.");

            //numbits = count_sortbits((int)(gridsize.x) * (int)(gridsize.y) * (int)(gridsize.z) - 1);
            numbits = count_sortbits((blocksize * numblocks) - 1) + 1;
            //Debug.Log("Radix Sort numbits: " + numbits);
            var data = new uint2[blocksize * numblocks];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = new uint2(uint.MaxValue, 0);
            }
            buffer = new ComputeBuffer(blocksize * numblocks, sizeof(int) * 2);
            buffer.SetData(data);
            prefixsums = new ComputeBuffer(blocksize * numblocks, sizeof(int));

            int numblocksums = 4 * numblocks;
            int n = Mathf.CeilToInt(Mathf.Log(((numblocksums + blocksize - 1) / blocksize) * blocksize) / Mathf.Log(blocksize));
            n++;
            blocksums = new List<ComputeBuffer>(n);

            for (int i = 0; i < n; i++)
            {
                numblocksums = ((numblocksums + blocksize - 1) / blocksize) * blocksize;
                if (numblocksums < 1)
                    numblocksums = 1;
                blocksums.Add(new ComputeBuffer(numblocksums, sizeof(int)));
                numblocksums /= blocksize;
            }

            result = new ComputeBuffer(blocksize * numblocks, sizeof(int) * 2);

            Vector4 blocksumoffsets = new Vector4(0, numblocks, numblocks * 2, numblocks * 3);
            counting.SetVector("blocksumoffsets", blocksumoffsets);
            globalsort.SetVector("blocksumoffsets", blocksumoffsets);
        }

        public void Cleanup()
        {
            buffer.ClearBuffer();
            prefixsums.ClearBuffer();
            result.ClearBuffer();
            if (blocksums != null)
            {
                for (int i = 0; i < blocksums.Count; i++)
                {
                    blocksums[i].ClearBuffer();
                }
            }
        }

        void swap(ref ComputeBuffer a, ref ComputeBuffer b)
        {
            ComputeBuffer t = a;
            a = b;
            b = t;
        }

        public void Run()
        {
            // sort bits from least to most significant
            for (int i = 0; i < (numbits + 1) >> 1; i++)
            {
                SortBits(2 * i);
                // swap the buffer objects
                swap(ref result, ref buffer);
            }
        }

        int intpow(int x, int y)
        {
            int r = 1;
            while (y != 0 ? true : false)
            {
                if ((y & 1) != 0 ? true : false)
                    r *= x;
                y >>= 1;
                x *= x;
            }
            return r;
        }

        void SortBits(int bits)
        {
            Vector4 blocksumoffsets = new Vector4(0, numblocks, numblocks * 2, numblocks * 3);
            counting.SetVector("blocksumoffsets", blocksumoffsets);
            counting.SetInt("bitshift", bits);
            counting.SetBuffer(0, "data", buffer);
            counting.SetBuffer(0, "prefixsum", prefixsums);
            counting.SetBuffer(0, "blocksum", blocksums[0]);
            counting.Dispatch(0, numblocks, 1, 1);

            int count = blocksums.Count;
            // create block sums level by level
            int numblocksums = (4 * numblocks) / blocksize;
            for (int i = 0; i < count - 1; i++)
            {
                blockscan.SetBuffer(0, "data", blocksums[i]);
                blockscan.SetBuffer(0, "blocksums", blocksums[i + 1]);
                blockscan.Dispatch(0, numblocksums > 0 ? numblocksums : 1, 1, 1);
                numblocksums /= blocksize;
            }
            // add block sums level by level (in reversed order)
            for (int i = count - 3; i >= 0; i--)
            {
                addblocksum.SetBuffer(0, "data", blocksums[i]);
                addblocksum.SetBuffer(0, "blocksums", blocksums[i + 1]);
                int temp = intpow(blocksize, i + 1);
                numblocksums = (4 * numblocks) / temp;
                addblocksum.Dispatch(0, numblocksums > 0 ? numblocksums : 1, 1, 1);
            }

            // map values to their global position in the output buffer
            globalsort.SetVector("blocksumoffsets", blocksumoffsets);
            globalsort.SetInt("bitshift", bits);
            globalsort.SetBuffer(0, "data", buffer);
            globalsort.SetBuffer(0, "prefixsum", prefixsums);
            globalsort.SetBuffer(0, "blocksum", blocksums[0]);
            globalsort.SetBuffer(0, "result", result);
            globalsort.Dispatch(0, numblocks, 1, 1);
        }

    }
}