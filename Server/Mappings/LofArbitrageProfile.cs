using AutoMapper;
using Server.Models;

namespace Server.Mappings;

/// <summary>
/// AutoMapper profile for LOF arbitrage data mappings
/// </summary>
public class LofArbitrageProfile : Profile
{
    public LofArbitrageProfile()
    {
        // No specific mapping needed as we manually construct the response
        // but this profile is required for AutoMapper configuration
        CreateMap<LofArbitrageData, LofArbitrageData>();
    }
}
