
using System.ComponentModel.DataAnnotations;

namespace ChongReanProject.Dto;


public class LoginCredentialRequest
{
    [Required]
    public string email { get; set; } = string.Empty;

    [Required]
    public string password { get; set; } = string.Empty;
}