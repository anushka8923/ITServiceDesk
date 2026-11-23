
using ITServiceDesk.API.Data;
using ITServiceDesk.API.Services;
using ITServiceDesk.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Moq;

namespace ITServiceDesk.Tests;

public class AttachmentServiceTests
{
    private sealed class TestApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public Exception? NextSaveFailure { get; set; }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            if (NextSaveFailure is { } exception)
            {
                NextSaveFailure = null;
                return Task.FromException<int>(exception);
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FailingReadStream(byte[] initialBytes, Exception failure) : MemoryStream(initialBytes)
    {
        private bool _returnedInitialBytes;

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_returnedInitialBytes)
                throw failure;

            _returnedInitialBytes = true;
            return base.ReadAsync(buffer, cancellationToken);
        }
    }

    private static TestApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new TestApplicationDbContext(options);

        context.Categories.Add(new Category
        {
            Id = 1,
            Name = "Hardware",
            IsActive = true
        });

        context.Priorities.Add(new Priority
        {
            Id = 1,
            Name = "Medium",
            IsActive = true
        });

        context.Tickets.Add(new Ticket
        {
            Id = 1,
            Title = "Test Ticket",
            Description = "This is a test ticket description.",
            CategoryId = 1,
            PriorityId = 1,
            CreatedByUserId = "user1"
        });

        context.SaveChanges();
        return context;
    }

    private static AttachmentService CreateService(
        ApplicationDbContext context,
        string folder)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.Setup(e => e.ContentRootPath).Returns(folder);

        return new AttachmentService(context, environment.Object);
    }

    private static IFormFile CreateFile(
        string fileName = "test.pdf",
        byte[]? content = null,
        string contentType = "application/pdf")
    {
        content ??= CreateValidContent(Path.GetExtension(fileName));

        var stream = new MemoryStream(content);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] CreateValidContent(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01,
                0x01, 0x01, 0x11, 0x00, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
                0x00, 0x00, 0x3F, 0x00, 0x00, 0xFF, 0xD9
            },
            ".png" => CreatePngContent(),
            ".pdf" => CreatePdfContent(),
            ".doc" => CreateCompoundFileHeader(),
            ".docx" => CreateDocxContent(),
            _ => throw new ArgumentException("Unsupported fixture extension.", nameof(extension))
        };
    }

    private static byte[] CreateDocxContent(
        string? contentTypesXml = null,
        string? documentXml = null)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var contentTypes = new StreamWriter(
                archive.CreateEntry("[Content_Types].xml").Open()))
                contentTypes.Write(contentTypesXml ??
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Override PartName=\"/word/document.xml\" " +
                    "ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>" +
                    "</Types>");

            using (var document = new StreamWriter(
                archive.CreateEntry("word/document.xml").Open()))
                document.Write(documentXml ??
                    "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                    "<w:body/></w:document>");
        }

        return output.ToArray();
    }

    private static byte[] CreateZipWithoutWordDocument()
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        using (var contentTypes = new StreamWriter(
            archive.CreateEntry("[Content_Types].xml").Open()))
        {
            contentTypes.Write("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"></Types>");
        }

        return output.ToArray();
    }

    private static byte[] CreatePdfContent(int targetLength = 0)
    {
        const string header = "%PDF-1.7\n";
        const string objects = "1 0 obj\n<< /Type /Catalog >>\nendobj\n";
        const string xrefTemplate = "xref\n0 2\n0000000000 65535 f \n0000000000 00000 n \n" +
            "trailer\n<< /Size 2 /Root 1 0 R >>\nstartxref\n0000000000\n%%EOF\n";
        var paddingLength = Math.Max(0,
            targetLength - Encoding.ASCII.GetByteCount(header + objects + xrefTemplate));
        var padding = new string(' ', paddingLength);
        var objectOffset = Encoding.ASCII.GetByteCount(header + padding);
        var xrefOffset = Encoding.ASCII.GetByteCount(header + padding + objects);
        var xref = "xref\n0 2\n0000000000 65535 f \n" +
            $"{objectOffset:D10} 00000 n \n" +
            "trailer\n<< /Size 2 /Root 1 0 R >>\n" +
            $"startxref\n{xrefOffset:D10}\n%%EOF\n";

        if (targetLength > 0 && header.Length + padding.Length + objects.Length + xref.Length != targetLength)
            throw new InvalidOperationException("Could not create a PDF fixture with the requested size.");

        return Encoding.ASCII.GetBytes(header + padding + objects + xref);
    }

    private static byte[] CreateCompoundFileHeader()
    {
        var header = new byte[1536];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }
            .CopyTo(header, 0);
        header[26] = 3;
        header[28] = 0xFE;
        header[29] = 0xFF;
        header[30] = 9;
        header[32] = 6;
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(44, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(48, 4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(76, 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(512, 4), 0xFFFFFFFD);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(516, 4), 0xFFFFFFFF);
        return header;
    }

    private static byte[] CreatePngContent(byte[]? rowData = null)
    {
        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 1);
        header[8] = 8;
        header[9] = 6;
        WritePngChunk(output, "IHDR", header);

        using var imageData = new MemoryStream();
        using (var zlib = new ZLibStream(imageData, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(rowData ?? new byte[] { 0, 0, 0, 0, 0 });
        WritePngChunk(output, "IDAT", imageData.ToArray());
        WritePngChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WritePngChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = uint.MaxValue;
        crc = UpdateTestCrc32(crc, typeBytes);
        crc = UpdateTestCrc32(crc, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ~crc);
        output.Write(crcBytes);
    }

    private static uint UpdateTestCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }

    [Fact]
    public async Task UploadAttachmentAsync_ValidFile_SavesAttachment()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var service = CreateService(context, folder);
            var file = CreateFile();

            var result = await service.UploadAttachmentAsync(1, file, "user1");

            Assert.Equal("test.pdf", result.FileName);
            Assert.Equal(1, result.TicketId);
            Assert.Equal("user1", result.UploadedByUserId);
            Assert.Equal(1, await context.Attachments.CountAsync());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("test.jpg", "image/jpeg")]
    [InlineData("test.jpeg", "image/jpeg")]
    [InlineData("test.png", "image/png")]
    [InlineData("test.pdf", "application/pdf")]
    [InlineData("test.doc", "application/msword")]
    [InlineData("test.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task UploadAttachmentAsync_ValidSignatureForSupportedExtension_Succeeds(
        string fileName,
        string expectedContentType)
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var result = await CreateService(context, folder)
                .UploadAttachmentAsync(1, CreateFile(fileName), "user1");

            Assert.Equal(expectedContentType, result.ContentType);
            Assert.Equal(1, await context.Attachments.CountAsync());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_GenericClientContentType_UsesValidatedType()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            var result = await CreateService(context, folder)
                .UploadAttachmentAsync(
                    1,
                    CreateFile(contentType: "application/octet-stream"),
                    "user1");

            Assert.Equal("application/pdf", result.ContentType);
            Assert.Equal("application/pdf", (await context.Attachments.SingleAsync()).ContentType);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("test.png", "pdf")]
    [InlineData("test.docx", "malformed-docx")]
    [InlineData("test.docx", "zip-without-word-document")]
    public async Task UploadAttachmentAsync_MismatchedOrMalformedContent_IsRejectedAndRemoved(
        string suppliedName,
        string fixtureKind)
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);
        var content = fixtureKind switch
        {
            "pdf" => CreateValidContent(".pdf"),
            "zip-without-word-document" => CreateZipWithoutWordDocument(),
            _ => Encoding.ASCII.GetBytes("PK not a valid DOCX package")
        };

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.UploadAttachmentAsync(
                    1,
                    CreateFile(suppliedName, content),
                    "user1"));

            Assert.Empty(await context.Attachments.ToListAsync());
            Assert.Empty(Directory.Exists(Path.Combine(folder, "Uploads"))
                ? Directory.GetFiles(Path.Combine(folder, "Uploads"))
                : Array.Empty<string>());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Theory]
    [InlineData("test.jpg", ".jpg")]
    [InlineData("test.png", ".png")]
    [InlineData("test.pdf", ".pdf")]
    [InlineData("test.doc", ".doc")]
    [InlineData("test.docx", ".docx")]
    public async Task UploadAttachmentAsync_SignatureOnlyOrTruncatedContent_IsRejected(
        string fileName,
        string extension)
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        byte[] content = extension switch
        {
            ".jpg" => new byte[] { 0xFF, 0xD8, 0xFF },
            ".png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            ".pdf" => Encoding.ASCII.GetBytes("%PDF-1.7"),
            ".doc" => new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 },
            _ => new byte[] { 0x50, 0x4B, 0x03, 0x04 }
        };

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService(context, folder)
                    .UploadAttachmentAsync(1, CreateFile(fileName, content), "user1"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_PngWithInvalidCompressedImageData_IsRejected()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService(context, folder).UploadAttachmentAsync(
                    1,
                    CreateFile("broken.png", CreatePngContent(new byte[] { 1, 2, 3 })),
                    "user1"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_DocxWithMalformedXml_IsRejected()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        const string malformedDocument =
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body></w:document>";

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService(context, folder).UploadAttachmentAsync(
                    1,
                    CreateFile("broken.docx", CreateDocxContent(documentXml: malformedDocument)),
                    "user1"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_DocxWithOversizedUncompressedDocument_IsRejected()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var oversizedDocument =
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            new string(' ', 16 * 1024 * 1024 + 1) +
            "</w:body></w:document>";

        try
        {
            var content = CreateDocxContent(documentXml: oversizedDocument);
            Assert.True(content.Length < 5 * 1024 * 1024);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService(context, folder).UploadAttachmentAsync(
                    1, CreateFile("large.docx", content), "user1"));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_InvalidTicket_ThrowsException()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAttachmentAsync(999, CreateFile(), "user1"));
    }

    [Fact]
    public async Task UploadAttachmentAsync_NullFile_ThrowsException()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAttachmentAsync(1, null!, "user1"));
    }

    [Fact]
    public async Task UploadAttachmentAsync_EmptyFile_ThrowsException()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAttachmentAsync(
                1, CreateFile(content: Array.Empty<byte>()), "user1"));
    }

    [Fact]
    public async Task UploadAttachmentAsync_FileExactly5MiB_Succeeds()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var content = new byte[5 * 1024 * 1024];
        CreatePdfContent(content.Length).CopyTo(content, 0);

        try
        {
            await CreateService(context, folder).UploadAttachmentAsync(
                1, CreateFile(content: content), "user1");
            Assert.Single(await context.Attachments.ToListAsync());
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_FileLargerThan5MB_ThrowsException()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);
        var largeContent = new byte[5 * 1024 * 1024 + 1];

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAttachmentAsync(
                1, CreateFile(content: largeContent), "user1"));
    }

    [Fact]
    public async Task UploadAttachmentAsync_InvalidExtension_ThrowsException()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAttachmentAsync(
                1,
                CreateFile("test.exe", CreateValidContent(".pdf")),
                "user1"));
    }

    [Fact]
    public async Task UploadAttachmentAsync_MetadataSaveFailure_DeletesNewFileAndRethrowsOriginal()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var expected = new InvalidOperationException("database write failed");
        context.NextSaveFailure = expected;

        try
        {
            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(context, folder)
                    .UploadAttachmentAsync(1, CreateFile(), "user1"));

            Assert.Same(expected, actual);
            Assert.Empty(await context.Attachments.ToListAsync());
            Assert.Empty(Directory.GetFiles(Path.Combine(folder, "Uploads")));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_CopyFailure_RemovesPartialFile()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var expected = new IOException("copy failed");
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns("partial.pdf");
        file.SetupGet(f => f.Length).Returns(100);
        file.SetupGet(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.OpenReadStream()).Returns(
            new FailingReadStream(new byte[] { 1, 2, 3 }, expected));

        try
        {
            var actual = await Assert.ThrowsAsync<IOException>(() =>
                CreateService(context, folder)
                    .UploadAttachmentAsync(1, file.Object, "user1"));

            Assert.Same(expected, actual);
            Assert.Empty(Directory.GetFiles(Path.Combine(folder, "Uploads")));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task UploadAttachmentAsync_ActualStreamExceedsLimitDespiteSmallReportedLength_IsRejected()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var content = new byte[5 * 1024 * 1024 + 1];
        CreateValidContent(".pdf").AsSpan(0, 8).CopyTo(content);
        var file = new Mock<IFormFile>();
        file.SetupGet(f => f.FileName).Returns("actual-large.pdf");
        file.SetupGet(f => f.Length).Returns(1);
        file.SetupGet(f => f.ContentType).Returns("application/octet-stream");
        file.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(content));

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                CreateService(context, folder)
                    .UploadAttachmentAsync(1, file.Object, "user1"));

            Assert.Empty(await context.Attachments.ToListAsync());
            Assert.Empty(Directory.GetFiles(Path.Combine(folder, "Uploads")));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task DeleteFilesIfUnreferencedAsync_TraversalPath_DoesNotDeleteOutsideUploadFolder()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(folder);
        var outsideFile = Path.Combine(folder, "outside.txt");
        await File.WriteAllTextAsync(outsideFile, "keep");

        try
        {
            await CreateService(context, folder)
                .DeleteFilesIfUnreferencedAsync(new[] { Path.Combine("Uploads", "..", "outside.txt") });
            await CreateService(context, folder)
                .DeleteFilesIfUnreferencedAsync(new[] { outsideFile });

            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task DeleteFilesIfUnreferencedAsync_UploadsRootSymlink_DoesNotDeleteLinkTargetFile()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var targetFolder = Path.Combine(folder, "outside-storage");
        var uploadsLink = Path.Combine(folder, "Uploads");
        Directory.CreateDirectory(targetFolder);
        var targetFile = Path.Combine(targetFolder, "linked.pdf");
        await File.WriteAllTextAsync(targetFile, "keep");

        try
        {
            try
            {
                Directory.CreateSymbolicLink(uploadsLink, targetFolder);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                return;
            }

            await CreateService(context, folder)
                .DeleteFilesIfUnreferencedAsync(new[] { Path.Combine("Uploads", "linked.pdf") });

            Assert.True(File.Exists(targetFile));
        }
        finally
        {
            if (Directory.Exists(uploadsLink))
                Directory.Delete(uploadsLink);
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task GetAttachmentsAsync_NoAttachments_ReturnsEmpty()
    {
        using var context = CreateContext();
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        var result = await service.GetAttachmentsAsync(1);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAttachmentsAsync_ReturnsNewestFirst()
    {
        using var context = CreateContext();

        context.Attachments.AddRange(
            new Attachment
            {
                FileName = "old.pdf",
                FilePath = "Uploads/old.pdf",
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow.AddDays(-1),
                TicketId = 1,
                UploadedByUserId = "user1"
            },
            new Attachment
            {
                FileName = "new.pdf",
                FilePath = "Uploads/new.pdf",
                ContentType = "application/pdf",
                UploadedAt = DateTime.UtcNow,
                TicketId = 1,
                UploadedByUserId = "user1"
            });

        await context.SaveChangesAsync();

        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = CreateService(context, folder);

        var result = (await service.GetAttachmentsAsync(1)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal("new.pdf", result[0].FileName);
        Assert.Equal("old.pdf", result[1].FileName);
    }
}
