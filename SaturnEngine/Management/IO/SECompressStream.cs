namespace SaturnEngine.Management.IO;
//LZ4实现
public class SECompressStream : Stream
{
    
    Stream lrs;
    private long leng = 0;//解压缩后大小
    private long posi = 0;
    public override bool CanRead => lrs.CanRead;

    public override bool CanSeek => lrs.CanSeek;

    public override bool CanWrite => lrs.CanWrite;

    public override long Length => leng;

    public override long Position { get => posi; set => posi = value; }

    public SECompressStream(Stream lrs)
    {
        this.lrs = lrs;
    }
    
    public override void Flush()
    {
        lrs.Flush();
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        return lre.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return lre.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        lre.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lre.Write(buffer, offset, count);
    }
    public byte[] Read(int cou,long off = -1)
    {
        return lre.Read(cou,off);
    }
}