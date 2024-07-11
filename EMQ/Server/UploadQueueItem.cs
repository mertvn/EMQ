using System;
using System.IO;
using EMQ.Client;
using EMQ.Shared.Auth.Entities.Concrete;
using EMQ.Shared.Quiz.Entities.Concrete;
using Microsoft.AspNetCore.Http;
using MimeKit;

namespace EMQ.Server;

public class MyFormFile
{
    public MyFormFile(long length, string contentType, string fileName, FileStream fileStream, string tempFsPath)
    {
        Length = length;
        ContentType = contentType;
        FileName = fileName;
        FileStream = fileStream;
        TempFsPath = tempFsPath;
    }

    public long Length { get; set; }

    public string ContentType { get; set; }

    public string FileName { get; set; }

    public FileStream FileStream { get; set; }

    public string TempFsPath { get; set; }
}

public class UploadQueueItem
{
    public UploadQueueItem(string id, Song song, MyFormFile myFormFile, UploadResult uploadResult, Session session,
        HttpRequest request, UploadOptions uploadOptions)
    {
        Id = id;
        Song = song;
        MyFormFile = myFormFile;
        UploadResult = uploadResult;
        Session = session;
        Request = request;
        UploadOptions = uploadOptions;
    }

    public string Id { get; }

    public Song Song { get; }

    public MyFormFile MyFormFile { get; }

    public UploadResult UploadResult { get; }

    public Session Session { get; }

    public HttpRequest Request { get; }

    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public UploadOptions UploadOptions { get; }
}
