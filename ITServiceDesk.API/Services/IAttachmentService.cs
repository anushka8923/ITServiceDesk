using ITServiceDesk.API.DTOs;
using Microsoft.AspNetCore.Http;

namespace ITServiceDesk.API.Services;

public interface IAttachmentService
{
    Task<AttachmentResponseDto> UploadAttachmentAsync(
        int ticketId,
        IFormFile file,
        string userId);

    Task<IEnumerable<AttachmentResponseDto>> GetAttachmentsAsync(
        int ticketId);

    Task<(byte[] Content, string ContentType, string FileName)?>
        GetAttachmentFileAsync(int ticketId, int attachmentId);

    Task DeleteFilesIfUnreferencedAsync(IEnumerable<string> filePaths);
}
