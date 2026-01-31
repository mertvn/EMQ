using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    public bool IsBGM { get; set; }

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    private string StatusText { get; set; } = "";

    public UploadOptions UploadOptions { get; set; } = new();

    protected override void OnParametersSet()
    {
        UploadOptions.ShouldCropSilence = !IsBGM;
    }

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

            if (file.Size > UploadConstants.MaxFilesizeBytes)
            {
                uploadResult.ErrorStr = "File is too large";
                continue;
            }

            string fileContentType = file.Name.EndsWith(".mkv") ? "video/x-matroska" : file.ContentType;
            if (string.IsNullOrWhiteSpace(fileContentType))
            {
                uploadResult.ErrorStr = "Unknown file format";
                continue;
            }

            var mediaTypeInfo = UploadConstants.ValidMediaTypes.FirstOrDefault(x => x.MimeType == fileContentType);
            if (mediaTypeInfo is null)
            {
                uploadResult.ErrorStr = $"Invalid file format: {file.ContentType}";
                continue;
            }

            if (UploadOptions.To != 0 && UploadOptions.To <= UploadOptions.Ss)
            {
                uploadResult.ErrorStr = "Start time cannot be greater than End time";
                continue;
            }

            string filename = WebUtility.HtmlEncode(file.Name);
            string tempUploadId = $"{ClientState.Session!.Player.Id};{mId.ToString()};{file.Size};{filename}";
            ClientState.UploadResults[tempUploadId] = uploadResult;
            bool success =
                await ClientUtils.SendPostFileReq(_client, uploadResult, file, mId, UploadOptions, fileContentType);
            if (success)
            {
                await Utils.WaitWhile(async () =>
                {
                    bool needToWait = true;
                    HttpResponseMessage res =
                        await _client.PostAsJsonAsync("Upload/GetUploadResult", uploadResult.UploadId);
                    if (res.IsSuccessStatusCode)
                    {
                        var content = (await res.Content.ReadFromJsonAsync<UploadResult>())!;
                        uploadResult.IsSuccess = content.IsSuccess;
                        uploadResult.FileName = content.FileName;
                        uploadResult.ResultUrl = content.ResultUrl;
                        uploadResult.ErrorStr = content.ErrorStr;
                        uploadResult.ExtractedResultUrl = content.ExtractedResultUrl;
                        uploadResult.UploadId = content.UploadId;
                        uploadResult.ChosenMatch = content.ChosenMatch;
                        ClientState.UploadResults[uploadResult.UploadId] = uploadResult;
                        StateHasChanged();

                        if (content.IsProcessing != null && !content.IsProcessing.Value)
                        {
                            needToWait = false;
                        }
                    }
                    else
                    {
                        uploadResult.ErrorStr = "Failed to fetch upload status.";
                        needToWait = false;
                    }

                    return needToWait;
                }, 5000, UploadConstants.TimeoutSeconds * 1000);
            }

            StateHasChanged();
        }
    }
}
