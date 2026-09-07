
using System.ComponentModel.DataAnnotations;

namespace ChongReanProject.Dto;


public class ApiBaseResponse<T>
{
    public int error_code { get; set; } = 0;
    public ErrorResponse? error_response { get; set; } = null;
    public T data { get; set; }
}

public class ErrorResponse
{
    public string type { get; set; }
    [Required]
    public string title { get; set; }

    [Required]
    public int status { get; set; }

    public string detail { get; set; }

    public string instance { get; set; }
    public string traceId { get; set; }


}