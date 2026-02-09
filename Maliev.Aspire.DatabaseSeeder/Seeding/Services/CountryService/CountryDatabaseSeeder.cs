using Maliev.CountryService.Data;
using Maliev.CountryService.Data.Entities;
using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.CountryService;

public class CountryDatabaseSeeder : DatabaseSeeder<CountryDbContext>
{
    public CountryDatabaseSeeder(CountryDbContext context, ILogger<CountryDatabaseSeeder> logger) 
        : base(context, logger)
    {
    }

    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await HasDataAsync<Country>(cancellationToken))
        {
            Logger.LogInformation("Country database already contains data. Skipping seeding.");
            return;
        }

        Logger.LogInformation("Seeding countries...");

        var countries = new List<Country>
        {
            new Country
            {
                Iso2 = "TH", Iso3 = "THA", Name = "Thailand", OfficialName = "Kingdom of Thailand",
                Region = "Asia", Subregion = "South-Eastern Asia", Population = 69000000, AreaKm2 = 513120,
                Timezones = "[\"Asia/Bangkok\"]",
                Borders = "[\"MMR\",\"KHM\",\"LAO\",\"MYS\"]",
                CallingCodes = "[\"66\"]",
                TopLevelDomains = "[\".th\"]",
                Currencies = "{\"THB\":{\"name\":\"Thai baht\",\"symbol\":\"฿\"}}",
                Languages = "{\"tha\":\"Thai\"}",
                Translations = "{\"ara\":\"تايلاند\",\"ces\":\"Thajsko\",\"cym\":\"Thailand\",\"deu\":\"Thailand\",\"est\":\"Tai\",\"fin\":\"Thaimaa\",\"fra\":\"Thaïlande\",\"hrv\":\"Tajland\",\"hun\":\"Thaiföld\",\"ita\":\"Thailandia\",\"jpn\":\"タイ\",\"kor\":\"태국\",\"nld\":\"Thailand\",\"per\":\"تایلند\",\"pol\":\"Tajlandia\",\"por\":\"Tailândia\",\"rus\":\"Таиланд\",\"slk\":\"Thajsko\",\"spa\":\"Tailandia\",\"swe\":\"Thailand\",\"urd\":\"تھائی لینڈ\",\"zho\":\"泰国\"}",
                Flags = "{\"png\":\"https://flagcdn.com/w320/th.png\",\"svg\":\"https://flagcdn.com/th.svg\"}",
                Independent = true, UnMember = true, Landlocked = false, IsActive = true, CreatedBy = "seeder", UpdatedBy = "seeder"
            },
            new Country
            {
                Iso2 = "US", Iso3 = "USA", Name = "United States", OfficialName = "United States of America",
                Region = "Americas", Subregion = "North America", Population = 330000000, AreaKm2 = 9833520,
                Timezones = "[\"America/New_York\"]",
                Borders = "[\"CAN\",\"MEX\"]",
                CallingCodes = "[\"1\"]",
                TopLevelDomains = "[\".us\"]",
                Currencies = "{\"USD\":{\"name\":\"United States dollar\",\"symbol\":\"$\"}}",
                Languages = "{\"eng\":\"English\"}",
                Translations = "{\"ara\":\"الولايات المتحدة\",\"ces\":\"Spojené státy\",\"cym\":\"Unol Daleithiau\",\"deu\":\"Vereinigte Staaten\",\"est\":\"Ühendriigid\",\"fin\":\"Yhdysvallat\",\"fra\":\"États-Unis\",\"hrv\":\"Sjedinjene Američke Države\",\"hun\":\"Egyesült Államok\",\"ita\":\"Stati Uniti d'America\",\"jpn\":\"アメリカ合衆国\",\"kor\":\"미국\",\"nld\":\"Verenigd Koninkrijk\",\"per\":\"ایالات متحده آمریکا\",\"pol\":\"Zjednoczone Królestwo\",\"por\":\"Reino Unido\",\"rus\":\"Великобритания\",\"slk\":\"Spojené kráľovstvo\",\"spa\":\"Reino Unido\",\"swe\":\"Storbritannien\",\"urd\":\"برطانیہ\",\"zho\":\"英国\"}",
                Flags = "{\"png\":\"https://flagcdn.com/w320/us.png\",\"svg\":\"https://flagcdn.com/us.svg\"}",
                Independent = true, UnMember = true, Landlocked = false, IsActive = true, CreatedBy = "seeder", UpdatedBy = "seeder"
            }
        };

        Context.Countries.AddRange(countries);
        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation("Successfully seeded {Count} countries.", countries.Count);
    }
}
