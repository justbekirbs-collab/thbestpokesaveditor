namespace PokeSaveEditor.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using PokeSaveEditor.Core.Interfaces;
using PokeSaveEditor.Infrastructure.Factories;
using PokeSaveEditor.Infrastructure.Parsers.Gen3;
using PokeSaveEditor.Infrastructure.Parsers.PKHeX;

/// <summary>Registers Infrastructure layer services with the DI container.</summary>
public static class DependencyInjection
{
    /// <summary>Adds all infrastructure services to the service collection.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IChecksumCalculator, Gen3ChecksumCalculator>();
        services.AddSingleton<ISaveFileParser, PKHeXSaveFileParser>();
        services.AddSingleton<ISaveFileParser, Gen3SaveFileParser>();
        services.AddSingleton<ISaveFileParserFactory, SaveFileParserFactory>();
        return services;
    }
}
