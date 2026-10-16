
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class GamHealthService
{
    private readonly GamService _gamService;

    public GamHealthService()
    {
        _gamService = new GamService();
    }

    public async Task<GamResult> CheckAsync()
    {
        return await _gamService.CheckGamHealthAsync();
    }
}
