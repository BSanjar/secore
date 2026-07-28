using Microsoft.EntityFrameworkCore;

using WebApplication1.Models.DBModels;



namespace WebApplication1.Services;



public static class NotificationRecipientModes

{

    public const string All = "all";

    public const string Manual = "manual";



    public static readonly IReadOnlyList<(string Value, string Label)> MedclinicAudienceOptions = new List<(string Value, string Label)>

    {

        (All, "Все пациенты"),

        (Manual, "Выбор вручную")

    };



    public static bool IsManual(string? mode) =>

        string.Equals(mode, Manual, StringComparison.OrdinalIgnoreCase);



    public static bool IsAll(string? mode) =>

        string.IsNullOrWhiteSpace(mode) ||

        string.Equals(mode, All, StringComparison.OrdinalIgnoreCase);

}



public static class NotificationGenderFilters

{

    public const string Any = "";

    public const string Male = "male";

    public const string Female = "female";



    public static readonly IReadOnlyList<(string Value, string Label)> Options = new List<(string Value, string Label)>

    {

        (Any, "Любой"),

        (Male, "Мужчины"),

        (Female, "Женщины")

    };

}



public static class NotificationAgeFilters

{

    public const string Any = "";

    public const string Under18 = "under18";

    public const string Age18To30 = "18_30";

    public const string Under30 = "under30";

    public const string Age30Plus = "30plus";

    public const string Age40Plus = "40plus";



    public static readonly IReadOnlyList<(string Value, string Label)> Options = new List<(string Value, string Label)>

    {

        (Any, "Любой"),

        (Under18, "Младше 18 лет"),

        (Age18To30, "18–30 лет"),

        (Under30, "Младше 30 лет"),

        (Age30Plus, "30 лет и старше"),

        (Age40Plus, "40 лет и старше")

    };

}



public sealed class ClientDemographics
{
    public string? Gender { get; init; }

    public string? GenderRaw { get; init; }

    public string? BirthDate { get; init; }

    public int? Age { get; init; }
}



public sealed class NotificationRecipientResolver

{

    private readonly AppDbContext _db;



    public NotificationRecipientResolver(AppDbContext db)

    {

        _db = db;

    }



    public async Task<(string? GenderFieldId, string? BirthFieldId)> GetDemographicFieldIdsAsync(

        string organizationId,

        CancellationToken cancellationToken)

    {

        var fields = await _db.OrganizationFields

            .AsNoTracking()

            .Where(f => f.Organization == organizationId && (f.Isdeleted == null || f.Isdeleted == 0))

            .Select(f => new { f.Id, f.FieldName, f.FieldType })

            .ToListAsync(cancellationToken);



        var genderField = fields.FirstOrDefault(f =>

            !string.IsNullOrWhiteSpace(f.FieldName) &&

            f.FieldName.Contains("пол", StringComparison.OrdinalIgnoreCase) &&

            !f.FieldName.Contains("кров", StringComparison.OrdinalIgnoreCase));



        var birthField = fields.FirstOrDefault(f =>

            f.FieldType == "datetime" &&

            !string.IsNullOrWhiteSpace(f.FieldName) &&

            (f.FieldName.Contains("рожд", StringComparison.OrdinalIgnoreCase) ||

             f.FieldName.Contains("birth", StringComparison.OrdinalIgnoreCase)));



        return (genderField?.Id, birthField?.Id);

    }



    public async Task SaveClientDemographicsAsync(

        string organizationId,

        string clientId,

        string? gender,

        string? birthDate,

        CancellationToken cancellationToken)

    {

        var (genderFieldId, birthFieldId) = await GetDemographicFieldIdsAsync(organizationId, cancellationToken);



        if (genderFieldId != null && !string.IsNullOrWhiteSpace(gender))

        {

            await UpsertFieldValueAsync(clientId, genderFieldId, NormalizeGenderStorageValue(gender), cancellationToken);

        }



        if (birthFieldId != null && !string.IsNullOrWhiteSpace(birthDate) &&

            DateTime.TryParse(birthDate, out var parsedBirth))

        {

            await UpsertFieldValueAsync(

                clientId,

                birthFieldId,

                parsedBirth.ToString("yyyy-MM-dd"),

                cancellationToken);

        }

    }



    public async Task<Dictionary<string, ClientDemographics>> LoadDemographicsAsync(

        string organizationId,

        IEnumerable<string> clientIds,

        CancellationToken cancellationToken)

    {

        var ids = clientIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();

        var result = ids.ToDictionary(id => id, _ => new ClientDemographics());



        if (ids.Count == 0)

            return result;



        var (genderFieldId, birthFieldId) = await GetDemographicFieldIdsAsync(organizationId, cancellationToken);

        if (genderFieldId == null && birthFieldId == null)

            return result;



        var fieldIds = new[] { genderFieldId, birthFieldId }.Where(x => x != null).Cast<string>().ToList();

        if (fieldIds.Count == 0)

            return result;



        var values = await _db.OrganizationClientsAdditionalFields

            .AsNoTracking()

            .Where(x => x.OrganizationClient != null &&

                        ids.Contains(x.OrganizationClient) &&

                        x.Field != null &&

                        fieldIds.Contains(x.Field))

            .Select(x => new { ClientId = x.OrganizationClient!, FieldId = x.Field!, Value = x.Value })

            .ToListAsync(cancellationToken);



        var today = DateTime.Today;

        foreach (var clientId in ids)

        {

            var genderValue = genderFieldId == null

                ? null

                : values.FirstOrDefault(x => x.ClientId == clientId && x.FieldId == genderFieldId)?.Value;

            var birthValue = birthFieldId == null

                ? null

                : values.FirstOrDefault(x => x.ClientId == clientId && x.FieldId == birthFieldId)?.Value;



            result[clientId] = new ClientDemographics
            {
                Gender = NormalizeGender(genderValue),
                GenderRaw = genderValue?.Trim(),
                BirthDate = TryParseBirthDate(birthValue, out var birthDate)
                    ? birthDate.ToString("yyyy-MM-dd")
                    : birthValue?.Trim(),
                Age = TryParseBirthDate(birthValue, out birthDate) ? CalculateAge(birthDate, today) : null
            };

        }



        return result;

    }



    public async Task<List<OrganizationClient>> ResolveAsync(

        string organizationId,

        string? recipientMode,

        string? genderFilter,

        string? ageFilter,

        IReadOnlyList<string> selectedClientIds,

        IReadOnlyList<string> selectedGroupIds,

        bool includeGroups,

        CancellationToken cancellationToken)

    {

        var baseQuery = _db.OrganizationClients

            .AsNoTracking()

            .Where(c => c.Organization == organizationId && c.ClientStatus == 1);



        List<OrganizationClient> clients;



        if (NotificationRecipientModes.IsManual(recipientMode))

        {

            if (includeGroups && selectedGroupIds.Count > 0)

            {

                clients = await baseQuery

                    .Where(c =>

                        selectedClientIds.Contains(c.Id) ||

                        (c.OrgClientGroupId != null && selectedGroupIds.Contains(c.OrgClientGroupId)))

                    .ToListAsync(cancellationToken);

            }

            else

            {

                clients = await baseQuery

                    .Where(c => selectedClientIds.Contains(c.Id))

                    .ToListAsync(cancellationToken);

            }

        }

        else

        {

            clients = await baseQuery.ToListAsync(cancellationToken);

        }



        genderFilter = genderFilter?.Trim() ?? "";

        ageFilter = ageFilter?.Trim() ?? "";



        if (string.IsNullOrEmpty(genderFilter) && string.IsNullOrEmpty(ageFilter))

            return clients;



        var demographics = await LoadDemographicsAsync(

            organizationId,

            clients.Select(c => c.Id),

            cancellationToken);



        return clients

            .Where(c =>

            {

                var demo = demographics.GetValueOrDefault(c.Id);

                return MatchesGenderFilter(demo, genderFilter) && MatchesAgeFilter(demo, ageFilter);

            })

            .ToList();

    }



    public static bool MatchesGenderFilter(ClientDemographics? demographics, string? filter)

    {

        if (string.IsNullOrWhiteSpace(filter))

            return true;



        demographics ??= new ClientDemographics();



        return filter switch

        {

            NotificationGenderFilters.Male => demographics.Gender == "male",

            NotificationGenderFilters.Female => demographics.Gender == "female",

            _ => true

        };

    }



    public static bool MatchesAgeFilter(ClientDemographics? demographics, string? filter)

    {

        if (string.IsNullOrWhiteSpace(filter))

            return true;



        demographics ??= new ClientDemographics();

        if (demographics.Age == null)

            return false;



        return filter switch

        {

            NotificationAgeFilters.Under18 => demographics.Age is < 18,

            NotificationAgeFilters.Age18To30 => demographics.Age is >= 18 and <= 30,

            NotificationAgeFilters.Under30 => demographics.Age is < 30,

            NotificationAgeFilters.Age30Plus => demographics.Age is >= 30,

            NotificationAgeFilters.Age40Plus => demographics.Age is >= 40,

            _ => true

        };

    }



    public static string? FormatGenderDisplay(string? normalizedGender, string? rawValue = null)
    {
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var raw = rawValue.Trim();
            if (raw is "М" or "M")
                return "Мужской";
            if (raw is "Ж" or "F")
                return "Женский";
        }

        return normalizedGender switch
        {
            NotificationGenderFilters.Male => "Мужской",
            NotificationGenderFilters.Female => "Женский",
            _ => null
        };
    }

    public static string? NormalizeGender(string? value)

    {

        if (string.IsNullOrWhiteSpace(value))

            return null;



        var normalized = value.Trim().ToLowerInvariant();

        if (normalized is "м" or "male" or "m" or "муж" or "мужской")

            return "male";

        if (normalized is "ж" or "female" or "f" or "жен" or "женский")

            return "female";

        if (normalized.StartsWith("муж", StringComparison.Ordinal))

            return "male";

        if (normalized.StartsWith("жен", StringComparison.Ordinal))

            return "female";



        return null;

    }



    private static string NormalizeGenderStorageValue(string gender)

    {

        var normalized = gender.Trim().ToUpperInvariant();

        if (normalized is "M" or "MALE" or "МУЖ" or "МУЖСКОЙ")

            return "М";

        if (normalized is "F" or "FEMALE" or "ЖЕН" or "ЖЕНСКИЙ")

            return "Ж";

        return gender.Trim();

    }



    private async Task UpsertFieldValueAsync(

        string clientId,

        string fieldId,

        string value,

        CancellationToken cancellationToken)

    {

        var existing = await _db.OrganizationClientsAdditionalFields

            .FirstOrDefaultAsync(

                x => x.OrganizationClient == clientId && x.Field == fieldId,

                cancellationToken);



        if (existing != null)

        {

            existing.Value = value;

            return;

        }



        _db.OrganizationClientsAdditionalFields.Add(new OrganizationClientsAdditionalField

        {

            Id = Guid.NewGuid().ToString(),

            OrganizationClient = clientId,

            Field = fieldId,

            Value = value

        });

    }



    private static bool TryParseBirthDate(string? value, out DateTime birthDate)

    {

        birthDate = default;

        if (string.IsNullOrWhiteSpace(value))

            return false;



        if (DateTime.TryParse(value, out birthDate))

            return birthDate.Year > 1900;



        return false;

    }



    private static int CalculateAge(DateTime birthDate, DateTime today)

    {

        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age))

            age--;

        return age;

    }

}


