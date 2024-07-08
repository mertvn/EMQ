using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using ATL;
using EMQ.Shared;
using EMQ.Shared.Core;
using EMQ.Shared.Library.Entities.Concrete.Dto.Request;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace EMQ.Client.Components;

public partial class UploadBatchComponent
{
    private List<UploadResult> _uploadResults = new();

    [Parameter]
    public Func<Task>? ParentStateHasChangedCallback { get; set; }

    private string StatusText { get; set; } = "";

    public int HaveCount { get; set; }

    [Parameter]
    public List<Song>? Songs { get; set; }

    [Parameter]
    public bool IsBGMMode { get; set; }

    private List<Task> UploadTasks { get; } = new();

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        if (UploadTasks.Any(x => !x.IsCompleted))
        {
            return;
        }

        // todo? cancellation
        Console.WriteLine("OnInputFileChange");
        UploadTasks.Clear();

        List<IBrowserFile> files;
        try
        {
            files = e.GetMultipleFiles(UploadConstants.MaxFilesBatchUpload).ToList(); // ToList is required here
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

            if (_uploadResults.Count >= UploadConstants.MaxFilesBatchUpload)
            {
                Console.WriteLine($"not queueing file because over MaxFilesBatchUpload limit: {file.Name}");
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

            if (string.IsNullOrWhiteSpace(file.ContentType))
            {
                uploadResult.ErrorStr = "Unknown file format";
                continue;
            }

            var validMediaTypes = IsBGMMode ? UploadConstants.ValidMediaTypesBgm : UploadConstants.ValidMediaTypes;
            var mediaTypeInfo = validMediaTypes.FirstOrDefault(x => x.MimeType == file.ContentType);
            if (mediaTypeInfo is null)
            {
                uploadResult.ErrorStr = $"Invalid file format: {file.ContentType}";
                continue;
            }

            try
            {
                await using var stream = file.OpenReadStream(UploadConstants.MaxFilesizeBytes);

                string title = "";
                List<string> artists = new();
                var metadataMBRecordingOrTrackIds = new List<string>();

                try
                {
                    await using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    // ms.Position = 0; // doesn't work in the browser ¯\_(ツ)_/¯, check again after .NET 9 I guess

                    Track tFile = new(ms, file.ContentType);
                    if (IsBGMMode)
                    {
                        foreach (string possibleRecordingIdName in UploadConstants.PossibleMetadataRecordingIdNames)
                        {
                            if (tFile.AdditionalFields.TryGetValue(possibleRecordingIdName,
                                    out string? possibleRecordingId))
                            {
                                if (!string.IsNullOrWhiteSpace(possibleRecordingId))
                                {
                                    metadataMBRecordingOrTrackIds.Add(possibleRecordingId);
                                }
                            }
                        }

                        // Console.WriteLine(JsonSerializer.Serialize(metadataMBRecordingOrTrackIds));
                    }
                    else
                    {
                        string? metadataTitle = tFile.Title;
                        List<string> metadataArtists = new() { tFile.Artist, tFile.AlbumArtist };

                        if (!string.IsNullOrWhiteSpace(metadataTitle))
                        {
                            title = metadataTitle;
                        }

                        artists = metadataArtists.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                    }
                }
                catch (Exception atlException)
                {
                    Console.WriteLine($"ATL exception for {file.Name}: {atlException}");
                    uploadResult.ErrorStr = "Error reading file metadata";
                    continue;
                }

                if (IsBGMMode)
                {
                    uploadResult.MBRecordingOrTrackIds = metadataMBRecordingOrTrackIds;
                    if (!metadataMBRecordingOrTrackIds.Any())
                    {
                        continue;
                    }

                    var matchingSongs = Songs!.Where(x =>
                            x.MusicBrainzRecordingGid is not null &&
                            (metadataMBRecordingOrTrackIds.Contains(x.MusicBrainzRecordingGid.Value.ToString()) ||
                             x.MusicBrainzTracks.Any(y => metadataMBRecordingOrTrackIds.Contains(y.ToString()))))
                        .DistinctBy(x => x.MusicBrainzRecordingGid).ToArray();

                    switch (matchingSongs.Length)
                    {
                        case 0:
                            break;
                        case 1:
                            HaveCount += 1;
                            uploadResult.PossibleMatches.Add(matchingSongs.First());
                            uploadResult.File = file;

                            if (!matchingSongs.First().Links.Any())
                            {
                                UploadTasks.Add(ChooseAndUpload(uploadResult, matchingSongs.First().Id));
                            }

                            break;
                        case > 1:
                            HaveCount += 1;
                            uploadResult.PossibleMatches.AddRange(matchingSongs);
                            uploadResult.File = file;
                            break;
                    }

                    StateHasChanged();
                }
                else
                {
                    uploadResult.Title = title;
                    uploadResult.Artists = artists;

                    if (!title.Any() || !artists.Any())
                    {
                        continue;
                    }

                    var mode = SongSourceSongTypeMode.Vocals;
                    var res = await _client.PostAsJsonAsync("Library/FindSongsByTitleAndArtistFuzzy",
                        new ReqFindSongsByTitleAndArtistFuzzy(new List<string> { title }, artists, mode));
                    if (res.IsSuccessStatusCode)
                    {
                        var content = await res.Content.ReadFromJsonAsync<List<Song>>();
                        if (content is not null && content.Any())
                        {
                            HaveCount += 1;
                            // Console.WriteLine(JsonSerializer.Serialize(content, Utils.Jso));
                            uploadResult.PossibleMatches.AddRange(content);
                            uploadResult.File = file;
                            StateHasChanged();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                uploadResult.ErrorStr = $"Client-side exception: {ex}";
            }
        }

        await Task.WhenAll(UploadTasks);
    }

    private async Task ChooseAndUpload(UploadResult uploadResult, int mId)
    {
        uploadResult.ChosenMatch = uploadResult.PossibleMatches.Single(x => x.Id == mId);
        StateHasChanged();
        var file = uploadResult.File!;

        ClientState.UploadResults[uploadResult.UploadId] = uploadResult;
        await ClientUtils.SendPostFileReq(_client, uploadResult, file, mId);
        var waitTask = Utils.WaitWhile(async () =>
        {
            bool needToWait = true;
            HttpResponseMessage res =
                await _client.PostAsJsonAsync("Upload/GetUploadResult", uploadResult.UploadId);
            if (res.IsSuccessStatusCode)
            {
                var content = (await res.Content.ReadFromJsonAsync<UploadResult>())!;
                uploadResult.Uploaded = content.Uploaded;
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
        }, 20000, UploadConstants.TimeoutSeconds * 1000);
        UploadTasks.Add(waitTask);

        StateHasChanged();
    }
}
