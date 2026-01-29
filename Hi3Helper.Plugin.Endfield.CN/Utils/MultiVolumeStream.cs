using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Hi3Helper.Plugin.Endfield.CN.Utils;

/// <summary>
///     将多个分卷文件模拟为一个连续的流，避免物理合并
///     因为CollapseLauncher/SevenZipExtractor的库似乎只支持单个压缩包，所以只能进行模拟
/// </summary>
public class MultiVolumeStream : Stream
{
    private readonly List<long> _fileLengths;
    private readonly List<string> _filePaths;
    private readonly long _totalLength;

    private int _currentIndex;
    private FileStream? _currentStream;
    private long _position;

    public MultiVolumeStream(IEnumerable<string> filePaths)
    {
        _filePaths = filePaths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        _fileLengths = _filePaths.Select(p => new FileInfo(p).Length).ToList();
        _totalLength = _fileLengths.Sum();
        _position = 0;

        OpenStreamAtIndex(0);
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _totalLength;

    public override long Position
    {
        get => _position;
        set => Seek(value, SeekOrigin.Begin);
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalBytesRead = 0;

        while (count > 0)
        {
            if (_currentIndex >= _filePaths.Count) break;

            if (_currentStream!.Position >= _currentStream.Length)
                if (!OpenStreamAtIndex(_currentIndex + 1))
                    break;

            var bytesToRead = (int)Math.Min(count, _currentStream.Length - _currentStream.Position);
            var bytesRead = _currentStream.Read(buffer, offset, bytesToRead);

            if (bytesRead == 0) break;

            _position += bytesRead;
            offset += bytesRead;
            count -= bytesRead;
            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var targetPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (targetPosition < 0 || targetPosition > _totalLength)
            throw new ArgumentOutOfRangeException(nameof(offset));

        _position = targetPosition;

        // 计算目标位置在哪个文件中
        long accumulatedLength = 0;
        for (var i = 0; i < _filePaths.Count; i++)
        {
            var fileLen = _fileLengths[i];
            if (targetPosition < accumulatedLength + fileLen)
            {
                OpenStreamAtIndex(i);
                _currentStream!.Position = targetPosition - accumulatedLength;
                return _position;
            }

            accumulatedLength += fileLen;
        }

        if (targetPosition == _totalLength)
        {
            OpenStreamAtIndex(_filePaths.Count - 1);
            _currentStream!.Position = _currentStream.Length;
            return _position;
        }

        throw new IOException("Seek failed to locate position.");
    }

    private bool OpenStreamAtIndex(int index)
    {
        if (index >= _filePaths.Count) return false;

        if (_currentIndex != index || _currentStream == null)
        {
            _currentStream?.Dispose();
            _currentIndex = index;
            _currentStream = new FileStream(_filePaths[index], FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _currentStream?.Dispose();
        base.Dispose(disposing);
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}