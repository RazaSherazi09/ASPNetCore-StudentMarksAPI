using MarksApi.Dtos.Auth;

namespace MarksApi.Services;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
}

