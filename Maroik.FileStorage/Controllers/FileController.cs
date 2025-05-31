using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Diagnostics;
using Maroik.FileStorage.Contracts;
using ICSharpCode.SharpZipLib.Zip;

namespace Maroik.FileStorage.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FileController : ControllerBase
    {
        private readonly IClamavRepository _clamavRepository;

        public FileController(IClamavRepository clamavRepository)
        {
            _clamavRepository = clamavRepository;
        }
        
        [AllowAnonymous]
        [HttpPost("upload")]
        public async Task<IActionResult> UploadAsync([FromForm] IFormFile file, [FromForm] string filePath)
        {
            try
            {
                if (file is not { Length: > 0 })
                    return BadRequest("Invalid file.");

                var fileName = Path.GetFileName(file.FileName);
                var safeDirectory = Path.GetFullPath(filePath);
                var safeFilePath = Path.Combine(safeDirectory, fileName);
                
                // extension check
                var allowedExt = new[] { ".zip", ".jpg", ".jpeg", ".png" };
                if (!allowedExt.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                    return BadRequest("Unsupported file extension.");

                // file size check
                if (file.Length > 10 * 1024 * 1024) // 10MB
                    return BadRequest("File too large.");

                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                
                bool IsAllowedFileType(Stream stream, string tempFileName)
                {
                    stream.Position = 0;
                    var buffer = new byte[8];
                    _ = stream.Read(buffer, 0, buffer.Length);
                    stream.Position = 0;

                    var ext = Path.GetExtension(tempFileName).ToLowerInvariant();

                    return ext switch
                    {
                        ".zip" => buffer.Take(4).SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }),
                        ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8, // JPEG
                        ".png" => buffer.Take(8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                        _ => false
                    };
                }

                // 파일 종류별 Magic Header 검사
                if (!IsAllowedFileType(memoryStream, fileName))
                    return BadRequest("Invalid or unsupported file format.");
                
                // ClamAV 바이러스 검사
                if (!await _clamavRepository.ScanWithClamavAsync(memoryStream))
                    return BadRequest("File may be infected with a virus.");

                // .zip 확장자인 경우 암호화 여부 검사
                if (Path.GetExtension(fileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // 스트림 위치를 리셋
                    memoryStream.Position = 0;
                    
                    // 스트림 복사본 만들기 (메모리 사용량 증가 주의)
                    using var msCopy = new MemoryStream();
                    await memoryStream.CopyToAsync(msCopy);
                    msCopy.Position = 0;
                    
                    // 이제 zip 검사
                    using var zipFile = new ZipFile(msCopy);
                    foreach (ZipEntry entry in zipFile)
                    {
                        if (entry.IsCrypted)
                            return BadRequest("Encrypted ZIP files are not allowed.");
                    }
                }
                
                if (!Directory.Exists(safeDirectory))
                    Directory.CreateDirectory(safeDirectory);

                // 저장
        #pragma warning disable SCS0018
                await using var stream = new FileStream(safeFilePath, FileMode.Create);
        #pragma warning restore SCS0018
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(stream);
                await stream.FlushAsync();

                // 실행 권한 제거 (chmod 644)
                var chmod = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
        #pragma warning disable SCS0001
                    Arguments = $"644 \"{safeFilePath}\"",
        #pragma warning restore SCS0001
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var chmodProcess = Process.Start(chmod);
                await chmodProcess!.WaitForExitAsync();

                return Ok("File uploaded successfully.");
            }
            catch
            {
                return BadRequest("File upload failed.");
            }
        }

        [AllowAnonymous]
        [HttpPost("download")]
        public async Task<IActionResult> DownloadAsync([FromForm] string filePath)
        {
            try
            {
                // 파일 이름에서 디렉터리 경로 제거 (보안)
                var sanitizedFileName = Path.GetFileName(filePath);
                filePath = Path.Combine(Path.GetDirectoryName(filePath ?? "") ?? string.Empty, sanitizedFileName);

                // 파일이 존재하는지 확인
                if (!System.IO.File.Exists(filePath)) return NotFound("File not found");
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found.
                var fileData = await System.IO.File.ReadAllBytesAsync(filePath);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found.

                // 파일 확장자에 맞는 Content-Type을 가져오기 위한 FileExtensionContentTypeProvider 사용
                var provider = new FileExtensionContentTypeProvider();

                // contentType 설정 (없을 경우 기본값 "application/octet-stream" 설정)
                if (!provider.TryGetContentType(sanitizedFileName, out var contentType))
                {
                    contentType = "application/octet-stream";
                }

                return File(fileData, contentType, sanitizedFileName);

            }
            catch
            {
                return BadRequest("Invalid file");
            }
        }

        [AllowAnonymous]
        [HttpPost("delete")]
        public IActionResult Delete([FromForm] string filePath)
        {
            // 파일 이름에서 디렉터리 경로 제거 (보안)
            var sanitizedFileName = Path.GetFileName(filePath);
            filePath = Path.Combine(Path.GetDirectoryName(filePath ?? "") ?? string.Empty, sanitizedFileName);

            if (!System.IO.File.Exists(filePath)) return NotFound();
#pragma warning disable SCS0018 // Potential Path Traversal vulnerability was found.
            System.IO.File.Delete(filePath);
#pragma warning restore SCS0018 // Potential Path Traversal vulnerability was found.
            return Ok();

        }
    }
}