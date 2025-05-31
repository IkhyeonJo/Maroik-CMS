using Maroik.WebSite.Contracts;
using System.Net;

public class FileRepository : IFileRepository
{
    private readonly string _fileStorageDomain = "Maroik.FileStorage";

    public async Task<byte[]> DownloadAsync(string filePath)
    {
        using HttpClient httpClient = new();
        using MultipartFormDataContent content = [];
        content.Add(new StringContent(filePath), "filePath");

        HttpResponseMessage response = await httpClient.PostAsync($"http://{_fileStorageDomain}/api/File/download", content);

        _ = response.EnsureSuccessStatusCode(); // 성공 여부 확인
        return await response.Content.ReadAsByteArrayAsync(); // 파일 데이터 반환
    }

    public async Task<bool> UploadAsync(IFormFile formFile, string filePath)
    {
        try
        {
            // 1. 파일 이름을 안전하게 처리하여 경로 탐색 취약성 방지
            string sanitizedFileName = Path.GetFileName(filePath); // 사용자가 입력한 파일 이름에서 디렉토리 경로 제거

            // 2. 파일을 메모리 스트림에 저장하고 외부 서버로 업로드
            if (formFile != null && formFile.Length > 0)
            {
                using MemoryStream memoryStream = new();
                // IFormFile 데이터를 메모리 스트림에 복사
                await formFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0; // 스트림의 시작 위치로 돌아가기

                // HttpClient로 외부 파일 서버에 파일 업로드
                using HttpClient httpClient = new();
                using MultipartFormDataContent content = [];

                // 메모리 스트림을 ByteArrayContent로 변환하여 업로드 준비
                StreamContent fileContent = new(memoryStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(formFile.ContentType);

                // MultipartFormDataContent에 파일 추가
                content.Add(fileContent, "file", sanitizedFileName);

                // 추가 파라미터 추가
                content.Add(new StringContent(Path.GetDirectoryName(filePath ?? "")), "filePath");

                HttpResponseMessage response = await httpClient.PostAsync($"http://{_fileStorageDomain}/api/File/upload", content);
                _ = response.EnsureSuccessStatusCode(); // 상태 코드 확인

                return response.StatusCode == HttpStatusCode.OK;
            }
            else
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task DeleteAsync(string filePath)
    {
        using HttpClient httpClient = new();
        using MultipartFormDataContent content = [];
        content.Add(new StringContent(filePath), "filePath");

        HttpResponseMessage response = await httpClient.PostAsync($"http://{_fileStorageDomain}/api/File/delete", content);

        _ = response.EnsureSuccessStatusCode();
    }
}