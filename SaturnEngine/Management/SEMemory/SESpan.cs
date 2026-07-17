using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

namespace SaturnEngine.Management.SEMemory
{
    public unsafe ref struct SESpan<T>
    {
        long co;
        long sizeperT;
        long size;
        SEMemoryStream sms;

        public SESpan(long count)
        {
            sizeperT = sizeof(T);
            co = count;
            size = co * sizeperT;
            sms = new SEMemoryStream(size);
        }
    }
}
