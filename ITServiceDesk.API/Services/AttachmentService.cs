using ITServiceDesk.API.Data;
using ITServiceDesk.API.DTOs;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ITServiceDesk.API.Services;

public class AttachmentService : IAttachmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AttachmentService>? _logger;

    private const int MaxFileSizeBytes = 5 * 1024 * 1024;
    private const long MaxContentTypesXmlBytes = 1024 * 1024;
    private const long MaxDocumentXmlBytes = 16 * 1024 * 1024;
    private const long MaxPngDecodedBytes = 64 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"
    };

    public AttachmentService(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        ILogger<AttachmentService>? logger = null)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<AttachmentResponseDto> UploadAttachmentAsync(
        int ticketId,
        IFormFile file,
        string userId)
    {
        var ticket = await _context.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId);

        if (ticket == null)
            throw new ArgumentException("Ticket not found.");

        if (file == null)
            throw new ArgumentException("Please select a file.");

        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException("File type is not allowed.");

        var uploadsFolder = Path.Combine(
            _environment.ContentRootPath,
            "Uploads");

        Directory.CreateDirectory(uploadsFolder);

        var storedFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(uploadsFolder, storedFileName);
        var relativePath = Path.Combine("Uploads", storedFileName);
        var fileCreated = false;

        try
        {
            using var input = file.OpenReadStream();
            using var stream = new FileStream(
                fullPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);
            fileCreated = true;
            var bytesCopied = await CopyWithSizeLimitAsync(input, stream);
            if (bytesCopied == 0)
                throw new ArgumentException("Please select a file.");
        }
        catch
        {
            if (fileCreated)
                TryDeleteFile(fullPath, "partial upload");

            throw;
        }

        if (!HasValidContent(fullPath, extension))
        {
            TryDeleteFile(fullPath, "invalid upload");
            throw new ArgumentException("File content does not match its file type.");
        }

        var attachment = new Attachment
        {
            FileName = Path.GetFileName(file.FileName),
            FilePath = relativePath,
            ContentType = GetContentType(extension),
            UploadedAt = DateTime.UtcNow,
            TicketId = ticketId,
            UploadedByUserId = userId
        };

        _context.Attachments.Add(attachment);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch
        {
            try
            {
                _context.Entry(attachment).State = EntityState.Detached;
                var isReferenced = await _context.Attachments
                    .AsNoTracking()
                    .AnyAsync(a => a.FilePath == relativePath);

                if (!isReferenced)
                    TryDeleteFile(fullPath, "failed attachment metadata save");
            }
            catch (Exception cleanupException)
            {
                _logger?.LogError(
                    cleanupException,
                    "Could not verify or clean up uploaded file {FilePath} after metadata persistence failed.",
                    relativePath);
            }

            throw;
        }

        return new AttachmentResponseDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            UploadedAt = attachment.UploadedAt,
            TicketId = attachment.TicketId,
            UploadedByUserId = attachment.UploadedByUserId
        };
    }

    private static async Task<long> CopyWithSizeLimitAsync(
        Stream source,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long totalBytes = 0;

        try
        {
            while (true)
            {
                var remainingWithOverflowByte = MaxFileSizeBytes - totalBytes + 1;
                var bytesToRead = (int)Math.Min(buffer.Length, remainingWithOverflowByte);
                var read = await source.ReadAsync(
                    buffer.AsMemory(0, bytesToRead),
                    cancellationToken);

                if (read == 0)
                    return totalBytes;

                if (totalBytes + read > MaxFileSizeBytes)
                    throw new ArgumentException("File size cannot exceed 5 MB.");

                await destination.WriteAsync(
                    buffer.AsMemory(0, read),
                    cancellationToken);
                totalBytes += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async Task<IEnumerable<AttachmentResponseDto>>
        GetAttachmentsAsync(int ticketId)
    {
        return await _context.Attachments
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new AttachmentResponseDto
            {
                Id = a.Id,
                FileName = a.FileName,
                ContentType = a.ContentType,
                UploadedAt = a.UploadedAt,
                TicketId = a.TicketId,
                UploadedByUserId = a.UploadedByUserId
            })
            .ToListAsync();
    }

    public async Task<(byte[] Content, string ContentType, string FileName)?>
        GetAttachmentFileAsync(int ticketId, int attachmentId)
    {
        var attachment = await _context.Attachments
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.TicketId == ticketId);

        if (attachment == null)
            return null;

        var uploadsRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "Uploads"));

        var fullPath = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, attachment.FilePath));

        // Prevent access outside the Uploads folder.
        if (!fullPath.StartsWith(
            uploadsRoot + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(fullPath))
            return null;

        var content = await File.ReadAllBytesAsync(fullPath);

        return (
            content,
            string.IsNullOrWhiteSpace(attachment.ContentType)
                ? "application/octet-stream"
                : attachment.ContentType,
            Path.GetFileName(attachment.FileName)
        );
    }

    public async Task DeleteFilesIfUnreferencedAsync(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var isReferenced = await _context.Attachments
                    .AsNoTracking()
                    .AnyAsync(a => a.FilePath == filePath);

                if (isReferenced)
                    continue;

                if (!TryResolveUploadPath(filePath, out var fullPath))
                {
                    _logger?.LogWarning(
                        "Skipped attachment cleanup for an unsafe path: {FilePath}",
                        filePath);
                    continue;
                }

                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception exception)
            {
                // Ticket deletion has already committed; cleanup failures must not
                // turn that successful deletion into a failed response.
                _logger?.LogError(
                    exception,
                    "Could not clean up unreferenced attachment file {FilePath}.",
                    filePath);
            }
        }
    }

    private static bool HasValidContent(string filePath, string extension)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return extension switch
            {
                ".jpg" or ".jpeg" => IsStructurallyPlausibleJpeg(stream),
                ".png" => IsStructurallyPlausiblePng(stream),
                ".pdf" => IsStructurallyPlausiblePdf(stream),
                ".doc" => HasPlausibleCompoundFileHeader(stream),
                ".docx" => IsWordDocumentPackage(stream),
                _ => false
            };
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or UnauthorizedAccessException or
                XmlException or ArgumentException or OverflowException)
        {
            return false;
        }
    }

    private static bool IsStructurallyPlausibleJpeg(Stream stream)
    {
        if (stream.Length < 16 || stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
            return false;

        var hasFrame = false;

        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xFF)
                return false;

            int marker;
            do
            {
                marker = stream.ReadByte();
            } while (marker == 0xFF);

            if (marker == 0x01)
                continue;

            if (marker < 0 || marker == 0x00 || marker == 0xD8 || marker == 0xD9 ||
                marker is >= 0xD0 and <= 0xD7)
            {
                return false;
            }

            var lengthHigh = stream.ReadByte();
            var lengthLow = stream.ReadByte();
            if (lengthHigh < 0 || lengthLow < 0)
                return false;

            var segmentLength = (lengthHigh << 8) | lengthLow;
            var segmentEnd = stream.Position + segmentLength - 2;
            if (segmentLength < 2 || segmentEnd > stream.Length)
                return false;

            if (IsJpegStartOfFrame(marker))
            {
                if (segmentLength < 11)
                    return false;

                var precision = stream.ReadByte();
                var height = ReadBigEndianUInt16(stream);
                var width = ReadBigEndianUInt16(stream);
                var components = stream.ReadByte();
                if (precision <= 0 || height == 0 || width == 0 ||
                    components is < 1 or > 4 || segmentLength != 8 + (3 * components))
                {
                    return false;
                }

                hasFrame = true;
                stream.Position = segmentEnd;
                continue;
            }

            if (marker == 0xDA)
            {
                var componentCount = stream.ReadByte();
                if (!hasFrame || componentCount is < 1 or > 4 ||
                    segmentLength != 6 + (2 * componentCount))
                {
                    return false;
                }

                stream.Position = segmentEnd;
                if (stream.Length - stream.Position < 3)
                    return false;

                stream.Position = stream.Length - 2;
                return stream.ReadByte() == 0xFF && stream.ReadByte() == 0xD9;
            }

            stream.Position = segmentEnd;
        }

        return false;
    }

    private static bool IsJpegStartOfFrame(int marker) => marker is
        0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or
        0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;

    private static ushort ReadBigEndianUInt16(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        if (high < 0 || low < 0)
            return 0;

        return (ushort)((high << 8) | low);
    }

    private static bool IsStructurallyPlausiblePng(FileStream stream)
    {
        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return false;
        }

        var hasHeader = false;
        var hasPalette = false;
        var hasImageData = false;
        var hasNonEmptyImageData = false;
        var imageDataEnded = false;
        byte colorType = 0xFF;
        byte bitDepth = 0;
        byte interlaceMethod = 0;
        uint imageWidth = 0;
        uint imageHeight = 0;
        var idatChunks = new List<(long Offset, int Length)>();
        var chunkType = new byte[4];
        var lengthBytes = new byte[4];
        var crcBuffer = new byte[81920];

        while (stream.Position < stream.Length)
        {
            if (stream.Length - stream.Position < 12)
                return false;

            stream.ReadExactly(lengthBytes);
            var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
            stream.ReadExactly(chunkType);

            if (length > stream.Length - stream.Position - 4)
                return false;

            var chunkName = Encoding.ASCII.GetString(chunkType);
            var isHeader = chunkName == "IHDR";
            var isPalette = chunkName == "PLTE";
            var isData = chunkName == "IDAT";
            var isEnd = chunkName == "IEND";

            if ((!hasHeader && !isHeader) || (isHeader && (hasHeader || length != 13)) ||
                (isPalette && (!hasHeader || hasPalette || hasImageData || length == 0 ||
                    length > 768 || length % 3 != 0 || colorType is 0 or 4)) ||
                (isData && (imageDataEnded || colorType == 3 && !hasPalette)) ||
                (isEnd && (!hasImageData || !hasNonEmptyImageData || length != 0)))
            {
                return false;
            }

            if (!isHeader && !isData && !isEnd && char.IsUpper(chunkName[0]))
                return false;

            byte[]? headerData = null;
            if (isHeader)
            {
                headerData = new byte[13];
                stream.ReadExactly(headerData);

                var width = BinaryPrimitives.ReadUInt32BigEndian(headerData.AsSpan(0, 4));
                var height = BinaryPrimitives.ReadUInt32BigEndian(headerData.AsSpan(4, 4));
                imageWidth = width;
                imageHeight = height;
                bitDepth = headerData[8];
                colorType = headerData[9];
                interlaceMethod = headerData[12];
                var validBitDepth = colorType switch
                {
                    0 => bitDepth is 1 or 2 or 4 or 8 or 16,
                    2 => bitDepth is 8 or 16,
                    3 => bitDepth is 1 or 2 or 4 or 8,
                    4 or 6 => bitDepth is 8 or 16,
                    _ => false
                };

                if (width == 0 || height == 0 || !validBitDepth ||
                    headerData[10] != 0 || headerData[11] != 0 || headerData[12] > 1)
                {
                    return false;
                }

                hasHeader = true;
            }
            else if (isPalette)
            {
                hasPalette = true;
            }
            else if (isData)
            {
                hasImageData = true;
                hasNonEmptyImageData |= length > 0;
                if (length > 0)
                {
                    if (idatChunks.Count >= 4096)
                        return false;
                    idatChunks.Add((stream.Position, checked((int)length)));
                }
            }
            else if (hasImageData)
            {
                imageDataEnded = true;
            }

            if (!VerifyPngChunkCrc(stream, chunkType, length, headerData, crcBuffer))
                return false;

            if (isEnd)
            {
                return stream.Position == stream.Length &&
                    ValidatePngImageData(
                        stream,
                        idatChunks,
                        imageWidth,
                        imageHeight,
                        bitDepth,
                        colorType,
                        interlaceMethod);
            }
        }

        return false;
    }

    private static bool ValidatePngImageData(
        FileStream stream,
        IReadOnlyList<(long Offset, int Length)> idatChunks,
        uint width,
        uint height,
        byte bitDepth,
        byte colorType,
        byte interlaceMethod)
    {
        var channels = colorType switch
        {
            0 or 3 => 1,
            2 => 3,
            4 => 2,
            6 => 4,
            _ => 0
        };
        if (channels == 0)
            return false;

        var bitsPerPixel = (long)channels * bitDepth;
        var rows = new List<(long RowBytes, long Count)>();
        long decodedSize = 0;

        bool AddPass(uint xStart, uint yStart, uint xStep, uint yStep)
        {
            var passWidth = width <= xStart ? 0 :
                (uint)(((ulong)width - xStart + xStep - 1) / xStep);
            var passHeight = height <= yStart ? 0 :
                (uint)(((ulong)height - yStart + yStep - 1) / yStep);
            if (passWidth == 0 || passHeight == 0)
                return true;

            var rowBytes = checked(((long)passWidth * bitsPerPixel + 7) / 8);
            decodedSize = checked(decodedSize + checked((rowBytes + 1) * passHeight));
            if (decodedSize > MaxPngDecodedBytes)
                return false;

            rows.Add((rowBytes, passHeight));
            return true;
        }

        if (interlaceMethod == 0)
        {
            if (!AddPass(0, 0, 1, 1))
                return false;
        }
        else if (interlaceMethod == 1)
        {
            var passes = new (uint X, uint Y, uint Dx, uint Dy)[]
            {
                (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8),
                (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)
            };
            foreach (var pass in passes)
            {
                if (!AddPass(pass.X, pass.Y, pass.Dx, pass.Dy))
                    return false;
            }
        }
        else
        {
            return false;
        }

        using var idatStream = new PngIdatReadStream(stream, idatChunks);
        using var zlib = new ZLibStream(idatStream, CompressionMode.Decompress, leaveOpen: true);
        var rowBuffer = new byte[81920];

        foreach (var (rowBytes, rowCount) in rows)
        {
            for (long row = 0; row < rowCount; row++)
            {
                var filter = zlib.ReadByte();
                if (filter is < 0 or > 4)
                    return false;

                var remaining = rowBytes;
                while (remaining > 0)
                {
                    var read = zlib.Read(rowBuffer, 0, (int)Math.Min(rowBuffer.Length, remaining));
                    if (read == 0)
                        return false;
                    remaining -= read;
                }
            }
        }

        return zlib.ReadByte() == -1;
    }

    private sealed class PngIdatReadStream(
        FileStream source,
        IReadOnlyList<(long Offset, int Length)> chunks) : Stream
    {
        private int _chunkIndex;
        private int _chunkBytesRead;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => chunks.Sum(chunk => (long)chunk.Length);
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            while (_chunkIndex < chunks.Count)
            {
                var chunk = chunks[_chunkIndex];
                if (_chunkBytesRead == chunk.Length)
                {
                    _chunkIndex++;
                    _chunkBytesRead = 0;
                    continue;
                }

                var count = Math.Min(buffer.Length, chunk.Length - _chunkBytesRead);
                source.Position = chunk.Offset + _chunkBytesRead;
                var read = source.Read(buffer[..count]);
                if (read == 0)
                    throw new EndOfStreamException("Truncated PNG image data.");

                _chunkBytesRead += read;
                return read;
            }

            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static bool VerifyPngChunkCrc(
        Stream stream,
        byte[] chunkType,
        uint dataLength,
        byte[]? alreadyReadData,
        byte[] buffer)
    {
        var crc = uint.MaxValue;
        crc = UpdateCrc32(crc, chunkType);

        if (alreadyReadData != null)
        {
            crc = UpdateCrc32(crc, alreadyReadData);
        }
        else
        {
            var remaining = (long)dataLength;
            while (remaining > 0)
            {
                var readSize = (int)Math.Min(buffer.Length, remaining);
                var read = stream.Read(buffer, 0, readSize);
                if (read == 0)
                    return false;

                crc = UpdateCrc32(crc, buffer.AsSpan(0, read));
                remaining -= read;
            }
        }

        Span<byte> crcBytes = stackalloc byte[4];
        stream.ReadExactly(crcBytes);
        return ~crc == BinaryPrimitives.ReadUInt32BigEndian(crcBytes);
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }

    private static bool IsStructurallyPlausiblePdf(Stream stream)
    {
        if (stream.Length < 32)
            return false;

        Span<byte> header = stackalloc byte[8];
        stream.ReadExactly(header);
        if (!header[..5].SequenceEqual("%PDF-"u8) ||
            !char.IsDigit((char)header[5]) || header[6] != (byte)'.' ||
            !char.IsDigit((char)header[7]))
        {
            return false;
        }

        var tailLength = (int)Math.Min(1024, stream.Length);
        var tail = new byte[tailLength];
        stream.Position = stream.Length - tailLength;
        stream.ReadExactly(tail);
        var tailText = Encoding.ASCII.GetString(tail);
        var eofIndex = tailText.LastIndexOf("%%EOF", StringComparison.Ordinal);
        if (eofIndex < 0 || tailText[(eofIndex + 5)..].Any(character => !char.IsWhiteSpace(character)))
            return false;

        var startXrefIndex = tailText.LastIndexOf("startxref", eofIndex, StringComparison.Ordinal);
        if (startXrefIndex < 0)
            return false;

        var numberStart = startXrefIndex + "startxref".Length;
        while (numberStart < eofIndex && char.IsWhiteSpace(tailText[numberStart]))
            numberStart++;
        var numberEnd = numberStart;
        while (numberEnd < eofIndex && char.IsDigit(tailText[numberEnd]))
            numberEnd++;

        if (numberEnd == numberStart ||
            !long.TryParse(tailText[numberStart..numberEnd], out var xrefOffset) ||
            xrefOffset < header.Length || xrefOffset >= stream.Length)
        {
            return false;
        }

        stream.Position = xrefOffset;
        var probeLength = (int)Math.Min(256, stream.Length - xrefOffset);
        var probeBytes = new byte[probeLength];
        stream.ReadExactly(probeBytes);
        var probe = Encoding.ASCII.GetString(probeBytes).TrimStart();

        return probe.StartsWith("xref", StringComparison.Ordinal) ||
            Regex.IsMatch(
                probe,
                @"\A\d+\s+\d+\s+obj\b[\s\S]{0,200}?/Type\s*/XRef\b",
                RegexOptions.CultureInvariant);
    }

    private static bool HasPlausibleCompoundFileHeader(Stream stream)
    {
        if (stream.Length < 1024)
            return false;

        Span<byte> header = stackalloc byte[512];
        stream.ReadExactly(header);
        ReadOnlySpan<byte> signature = new byte[]
            { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

        var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(header[26..28]);
        var sectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header[30..32]);
        var miniSectorShift = BinaryPrimitives.ReadUInt16LittleEndian(header[32..34]);
        var fatSectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[44..48]);
        var firstDirectorySector = BinaryPrimitives.ReadUInt32LittleEndian(header[48..52]);
        var directorySectorCount = BinaryPrimitives.ReadUInt32LittleEndian(header[40..44]);
        var firstFatSector = BinaryPrimitives.ReadUInt32LittleEndian(header[76..80]);
        var sectorSize = majorVersion == 4 ? 4096L : 512L;
        var sectorCount = (stream.Length - 512) / sectorSize;

        return header[..8].SequenceEqual(signature) &&
            header[28] == 0xFE && header[29] == 0xFF &&
            (majorVersion == 3 && sectorShift == 9 || majorVersion == 4 && sectorShift == 12) &&
            miniSectorShift == 6 && header[34..40].IndexOfAnyExcept((byte)0) < 0 &&
            stream.Length == 512 + (sectorCount * sectorSize) &&
            fatSectorCount > 0 && fatSectorCount <= sectorCount &&
            firstDirectorySector < sectorCount && firstFatSector < sectorCount &&
            firstDirectorySector != uint.MaxValue && firstFatSector != uint.MaxValue &&
            (majorVersion != 3 || directorySectorCount == 0);
    }

    private static bool IsWordDocumentPackage(FileStream stream)
    {
        Span<byte> signature = stackalloc byte[4];
        stream.ReadExactly(signature);
        if (signature[0] != 0x50 || signature[1] != 0x4B ||
            signature[2] != 0x03 || signature[3] != 0x04)
        {
            return false;
        }

        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var contentTypeEntries = archive.Entries
            .Where(entry => entry.FullName == "[Content_Types].xml")
            .ToList();
        var documentEntries = archive.Entries
            .Where(entry => entry.FullName == "word/document.xml")
            .ToList();

        return contentTypeEntries.Count == 1 && documentEntries.Count == 1 &&
            contentTypeEntries[0].Length <= MaxContentTypesXmlBytes &&
            documentEntries[0].Length <= MaxDocumentXmlBytes &&
            HasValidContentTypesXml(contentTypeEntries[0]) &&
            HasValidWordDocumentXml(documentEntries[0]);
    }

    private static bool HasValidContentTypesXml(ZipArchiveEntry entry)
    {
        const string contentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
        const string wordDocumentContentType =
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";
        var foundWordDocumentType = false;

        using var input = entry.Open();
        using var reader = XmlReader.Create(input, CreateSafeXmlSettings(MaxContentTypesXmlBytes));
        if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "Types" ||
            reader.NamespaceURI != contentTypesNamespace)
        {
            return false;
        }

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Override" &&
                reader.NamespaceURI == contentTypesNamespace &&
                reader.GetAttribute("PartName") == "/word/document.xml" &&
                reader.GetAttribute("ContentType") == wordDocumentContentType)
            {
                foundWordDocumentType = true;
            }
        }

        return foundWordDocumentType;
    }

    private static bool HasValidWordDocumentXml(ZipArchiveEntry entry)
    {
        const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var hasDocumentRoot = false;
        var hasBody = false;

        using var input = entry.Open();
        using var reader = XmlReader.Create(input, CreateSafeXmlSettings(MaxDocumentXmlBytes));
        if (reader.MoveToContent() != XmlNodeType.Element || reader.LocalName != "document" ||
            reader.NamespaceURI != wordNamespace)
        {
            return false;
        }

        hasDocumentRoot = true;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "body" &&
                reader.NamespaceURI == wordNamespace)
            {
                hasBody = true;
            }
        }

        return hasDocumentRoot && hasBody;
    }

    private static XmlReaderSettings CreateSafeXmlSettings(long maxCharacters) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = maxCharacters,
        MaxCharactersFromEntities = 0,
        IgnoreComments = true
    };

    private static string GetContentType(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };

    private bool TryResolveUploadPath(string storedPath, out string fullPath)
    {
        fullPath = string.Empty;
        var uploadsRoot = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, "Uploads"));
        var uploadsDirectory = new DirectoryInfo(uploadsRoot);
        if (uploadsDirectory.Exists &&
            (uploadsDirectory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return false;
        }

        var candidate = Path.GetFullPath(
            Path.Combine(_environment.ContentRootPath, storedPath));
        var rootPrefix = Path.EndsInDirectorySeparator(uploadsRoot)
            ? uploadsRoot
            : uploadsRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootPrefix, comparison))
            return false;

        // Upload storage is flat; reject nested paths so a symlinked subdirectory
        // cannot redirect cleanup outside the configured Uploads directory.
        var relativeToUploads = Path.GetRelativePath(uploadsRoot, candidate);
        if (relativeToUploads == "." || Path.IsPathRooted(relativeToUploads) ||
            relativeToUploads == ".." ||
            relativeToUploads.StartsWith(".." + Path.DirectorySeparatorChar, comparison) ||
            relativeToUploads.Contains(Path.DirectorySeparatorChar) ||
            relativeToUploads.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private void TryDeleteFile(string path, string reason)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Could not delete file {FilePath} after {Reason}.", path, reason);
        }
    }
}
