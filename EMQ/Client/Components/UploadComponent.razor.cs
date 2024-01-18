using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared;
using EMQ.Shared.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace EMQ.Client.Components;

public partial class UploadComponent
{
    private List<UploadResult> _uploadResults = new();

    [Parameter]
    public int mId { get; set; }

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    private string StatusText { get; set; } = "";

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        // todo? cancellation
        Console.WriteLine("OnInputFileChange");
        List<IBrowserFile> files;
        try
        {
            files = e.GetMultipleFiles(UploadConstants.MaxFilesSpecificSongUpload).ToList(); // ToList is required here
        }
        catch (InvalidOperationException)
        {
            StatusText = "Too many files selected.";
            return;
        }

        // queue files
        foreach (var file in files)
        {
            Console.WriteLine($"processing: {file.Name}");
            StatusText = "";
            if (_uploadResults.Any(x => x.FileName == file.Name))
            {
                Console.WriteLine($"not queueing file because it is already queued:{file.Name}");
                continue;
            }

            if (_uploadResults.Count >= UploadConstants.MaxFilesSpecificSongUpload)
            {
                Console.WriteLine($"not queueing file because over MaxFilesSpecificSongUpload limit: {file.Name}");
                continue;
            }

            var uploadResult = new UploadResult { FileName = file.Name };
            _uploadResults.Add(uploadResult);
            Console.WriteLine($"queued: {file.Name}");
            StateHasChanged();
        }

        // process queue
        foreach (var file in files)
        {
            var uploadResult = _uploadResults.SingleOrDefault(x => x.FileName == file.Name);
            if (uploadResult == null)
            {
                continue;
            }

            // todo check if file can be uploaded: other things
            if (file.Size > UploadConstants.MaxFilesizeBytes)
            {
                uploadResult.ErrorStr = "File is too large";
                continue;
            }

            try
            {
                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(file.OpenReadStream(UploadConstants.MaxFilesizeBytes));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                content.Add(fileContent, "\"files\"", $"{mId};{file.Name}");

                // await Task.Delay(TimeSpan.FromSeconds(1));
                var response = await _client.PostAsync("Upload/PostFile", content);
                if (response.IsSuccessStatusCode)
                {
                    var res = await response.Content.ReadFromJsonAsync<UploadResult>();
                    if (res is not null)
                    {
                        uploadResult.Uploaded = res.Uploaded;
                        uploadResult.FileName = res.FileName;
                        uploadResult.ResultUrl = res.ResultUrl;
                        uploadResult.ErrorStr = res.ErrorStr;

                        // Console.WriteLine($"set mdl.url to {uploadResult.ResultUrl}");
                        // Mdl[mId].Url = uploadResult.ResultUrl!;
                        // await MdlChanged.InvokeAsync();
                        await ParentStateHasChangedCallback!.Invoke();
                    }
                    else
                    {
                        uploadResult.ErrorStr = "UploadResult was null";
                    }
                }

                StateHasChanged();
            }
            catch (Exception ex)
            {
                uploadResult.ErrorStr = $"Client-side exception: {ex}";
            }
        }
    }
}
