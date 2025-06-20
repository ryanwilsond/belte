using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Buckle.Utilities;

internal sealed class ReadOnlyUnmanagedMemoryStream : Stream {
    private readonly object _memoryOwner;
    private readonly IntPtr _data;
    private readonly int _length;
    private int _position;

    internal ReadOnlyUnmanagedMemoryStream(object memoryOwner, IntPtr data, int length) {
        _memoryOwner = memoryOwner;
        _data = data;
        _length = length;
    }

    public override unsafe int ReadByte() {
        if (_position == _length)
            return -1;

        return ((byte*)_data)[_position++];
    }

    public override int Read(byte[] buffer, int offset, int count) {
        var bytesRead = Math.Min(count, _length - _position);
        Marshal.Copy(_data + _position, buffer, offset, bytesRead);
        _position += bytesRead;
        return bytesRead;
    }

    public override void Flush() { }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position {
        get {
            return _position;
        }

        set {
            Seek(value, SeekOrigin.Begin);
        }
    }

    public override long Seek(long offset, SeekOrigin origin) {
        long target;

        try {
            target = origin switch {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(offset + _position),
                SeekOrigin.End => checked(offset + _length),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
        } catch (OverflowException) {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (target < 0 || target >= _length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _position = (int)target;
        return target;
    }

    public override void SetLength(long value) {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) {
        throw new NotSupportedException();
    }
}
