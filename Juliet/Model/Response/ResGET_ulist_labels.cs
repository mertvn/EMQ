using System.Text.Json.Serialization;
using Juliet.Model.VNDBObject;

namespace Juliet.Model.Response;

public class ResGET_ulist_labels
{
    [JsonPropertyName("labels")]
    public VNDBLabel[] Labels { get; set; } = Array.Empty<VNDBLabel>();
}
