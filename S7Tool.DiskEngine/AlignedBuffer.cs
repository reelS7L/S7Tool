using System.Runtime.InteropServices;

namespace S7Tool.DiskEngine;

public sealed unsafe class AlignedBuffer : IDisposable
{
    private void* _ptr;
    public int Length { get; }

    public AlignedBuffer(int length, int alignment)
    {
        Length = length;
        _ptr = NativeMemory.AlignedAlloc((nuint)length, (nuint)alignment);
    }

    public Span<byte> Span => new(_ptr, Length);

    public void Dispose()
    {
        if (_ptr != null)
        {
            NativeMemory.AlignedFree(_ptr);
            _ptr = null;
        }
    }
}
