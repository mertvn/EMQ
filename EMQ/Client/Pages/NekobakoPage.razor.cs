using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Core;
using EMQ.Shared.Core.SharedDbEntities;
using Microsoft.AspNetCore.Components.Forms;

namespace EMQ.Client.Pages;

public partial class NekobakoPage
{
    public sealed class FileBatch
    {
        public Guid Id { get; init; }
        public bool IsActive { get; set; }
    }

    public sealed class UploadItem
    {
        public Guid Id { get; init; }
        public FileBatch Batch { get; init; } = new();
        public string Name { get; init; } = "";
        public long Size { get; init; }
        public UploadStatus Status { get; set; }
        public string? Error { get; set; }
    }

    public enum UploadStatus
    {
        Waiting,
        Reading,
        Uploading,
        Complete,
        Failed
    }

    public string SelectedTabNekobako { get; set; } = "TabUpload";

    private IQueryable<UserNekobako>? UserNekobakos { get; set; }

    private IQueryable<UserNekobako>? OurNekobakos { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await _clientUtils.TryRestoreSession();
        if (!AuthStuff.HasPermission(ClientState.Session, PermissionKind.User))
        {
            return;
        }

        await ClientUtils.SendPong(_navigation.Uri.LastSegment());
        AddActiveInput();

        HttpResponseMessage resListUserFiles = await _client.PostAsJsonAsync("Nekobako/ListUserFiles", "");
        if (resListUserFiles.IsSuccessStatusCode)
        {
            UserNekobakos = (await resListUserFiles.Content.ReadFromJsonAsync<List<UserNekobako>>())!.AsQueryable();
        }

        if (ClientUtils.HasAdminPerms())
        {
            HttpResponseMessage resListAllFiles = await _client.PostAsJsonAsync("Nekobako/ListAllFiles", "");
            if (resListAllFiles.IsSuccessStatusCode)
            {
                OurNekobakos = (await resListAllFiles.Content.ReadFromJsonAsync<List<UserNekobako>>())!.AsQueryable();
            }
        }
    }

    private Task FilesSelectedAsync(FileBatch batch, InputFileChangeEventArgs e)
    {
        // Immediately retire this input and create a fresh one. This lets the user drop another batch while files from the current
        // batch are still being read/uploaded. The old InputFile remains in the DOM so its IBrowserFile instances stay valid.
        batch.IsActive = false;
        AddActiveInput();

        IReadOnlyList<IBrowserFile> files;
        try
        {
            files = e.GetMultipleFiles(78);
        }
        catch (Exception ex)
        {
            ClientState.NekobakoUploads.Add(new UploadItem
            {
                Id = Guid.NewGuid(),
                Batch = batch,
                Name = "File selection",
                Size = 0,
                Status = UploadStatus.Failed,
                Error = ex.Message
            });

            return Task.CompletedTask;
        }

        foreach (var file in files)
        {
            var item = new UploadItem
            {
                Id = Guid.NewGuid(),
                Batch = batch,
                Name = file.Name,
                Size = file.Size,
                Status = UploadStatus.Waiting
            };

            ClientState.NekobakoUploads.Add(item);
            _ = ProcessFileAsync(file, item);
        }

        return Task.CompletedTask;
    }

    private async Task ProcessFileAsync(IBrowserFile file, UploadItem item)
    {
        await ClientState.NekobakoUploadSlots.WaitAsync();
        try
        {
            if (file.Size > UploadConstants.NekobakoMaxFilesizeBytes)
            {
                throw new InvalidOperationException(
                    $"The file exceeds the {FormatSize(UploadConstants.NekobakoMaxFilesizeBytes)} limit.");
            }

            item.Status = UploadStatus.Reading;
            await InvokeAsync(StateHasChanged);

            byte[] bytes = new byte[checked((int)file.Size)];
            await using (var source = file.OpenReadStream(UploadConstants.NekobakoMaxFilesizeBytes))
            {
                await source.ReadExactlyAsync(bytes);
            }

            item.Status = UploadStatus.Uploading;
            await InvokeAsync(StateHasChanged);

            using var form = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(bytes);
            if (!string.IsNullOrWhiteSpace(file.ContentType) &&
                MediaTypeHeaderValue.TryParse(file.ContentType, out var mediaType))
            {
                fileContent.Headers.ContentType = mediaType;
            }

            form.Add(fileContent, "file", file.Name);
            using var response = await _client.PostAsync("Nekobako/UploadFile", form);
            item.Status = response.IsSuccessStatusCode ? UploadStatus.Complete : UploadStatus.Failed;
            item.Error = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            item.Status = UploadStatus.Failed;
            item.Error = ex.Message;
        }
        finally
        {
            ClientState.NekobakoUploadSlots.Release();
            await InvokeAsync(() =>
            {
                RemoveBatchIfFinished(item.Batch);
                StateHasChanged();
            });
        }
    }

    private void AddActiveInput()
    {
        foreach (var batch in ClientState.NekobakoBatches)
        {
            batch.IsActive = false;
        }

        ClientState.NekobakoBatches.Add(new FileBatch { Id = Guid.NewGuid(), IsActive = true });
    }

    private void RemoveBatchIfFinished(FileBatch batch)
    {
        if (batch.IsActive)
            return;

        var batchItems = ClientState.NekobakoUploads.Where(x => ReferenceEquals(x.Batch, batch)).ToList();
        if (batchItems.Count == 0 ||
            batchItems.All(x => x.Status is UploadStatus.Complete or UploadStatus.Failed))
        {
            ClientState.NekobakoBatches.Remove(batch);
        }
    }

    private void ClearFinished()
    {
        ClientState.NekobakoUploads.RemoveAll(x => x.Status is UploadStatus.Complete or UploadStatus.Failed);
        foreach (var batch in ClientState.NekobakoBatches.ToList())
        {
            RemoveBatchIfFinished(batch);
        }
    }

    private static string GetStatusClass(UploadStatus status) =>
        status switch
        {
            UploadStatus.Complete => "status-complete",
            UploadStatus.Failed => "status-failed",
            _ => ""
        };

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
            < 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d:0.#} MB",
            _ => $"{bytes / 1024d / 1024d / 1024d:0.#} GB"
        };
    }
}
