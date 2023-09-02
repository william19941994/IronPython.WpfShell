using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronPython.WpfShell.Test
{
    public class MyNumPyWrapper
    {
        private NumSharp.Shape conv(IronPython.Runtime.PythonTuple tup)
        {
            var arr = tup.ToArray();
            return new NumSharp.Shape((int)arr[0], (int)arr[1]);
        }
        public NumSharp.NDArray full(IronPython.Runtime.PythonTuple shape,ValueType fillValue)
        {
            return NumSharp.np.full(conv(shape), fillValue);
        }
        public NumSharp.NDArray empty(IronPython.Runtime.PythonTuple shape,Type dtype)
        {
            return NumSharp.np.empty(conv(shape), dtype);
        }
        public NumSharp.NDArray transpose(NumSharp.NDArray vec)
        {
            return NumSharp.np.transpose(vec);
        }
        public NumSharp.NDArray reshape(NumSharp.NDArray vec, IronPython.Runtime.PythonTuple shape)
        {
            return NumSharp.np.reshape(vec, conv(shape));
        }
        public NumSharp.NDArray ndarray(IronPython.Runtime.PythonTuple shape, Type dtype)
        {
            return NumSharp.np.ndarray(conv(shape), dtype);
        }
        public NumSharp.NDArray zeros(IronPython.Runtime.PythonTuple shape, Type dtype)
        {
            return NumSharp.np.zeros(conv(shape), dtype);
        }
        public NumSharp.NDArray sum(NumSharp.NDArray a)
        {
            return NumSharp.np.sum(a);
        }
        public NumSharp.NDArray int16(NumSharp.NDArray a)
        {
            var b= a.copy();
            return b;
        }
        public double mean(NumSharp.NDArray arr)
        {
            return NumSharp.np.mean(arr);
        }
        public int[] shape(NumSharp.NDArray nd)
        {
            return nd.shape;
        }
        public NumSharp.NDArray array(NumSharp.NDArray nd, IronPython.Runtime.PythonTuple shape)
        {
            return NumSharp.np.reshape(nd, conv(shape));
        }
        public NumSharp.NDArray concatenate(NumSharp.NDArray[] arr)
        {
            return NumSharp.np.concatenate(arr);
        }
        public NumSharp.NDArray arange(double start,double stop,double step=1)
        {
            return NumSharp.np.arange(start,stop,step);
        }
        public NumSharp.NDArray abs(NumSharp.NDArray absValue )
        {
            return NumSharp.np.abs(absValue);
        }
        public NumSharp.NDArray loadtxt(string path)
        {
            return NumSharp.np.load(path);
        }
        public NumSharp.NDArray log10(NumSharp.NDArray nd )
        {
            return NumSharp.np.log10(nd);
        }
    }
}
