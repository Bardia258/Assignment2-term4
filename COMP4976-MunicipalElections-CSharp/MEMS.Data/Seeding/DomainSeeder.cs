using Microsoft.EntityFrameworkCore;
using MEMS.Data.Entities;

namespace MEMS.Data.Seeding;

internal static class DomainSeeder
{
    // Called by the opt-in development seeder. Stable keys survive administrator
    // edits to display names, biographies, and relationships. Existing rows are never updated.
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var municipalities = new[]
        {
            new Municipality
            {
                SeedKey = "demo:pitt-meadows", Slug = "pitt-meadows", Name = "Pitt Meadows", Province = "BC",
                ElectionDate = new DateTime(2026, 10, 17),
                Description = "Fictional classroom election dataset for Pitt Meadows, featuring community planning, parks, and local services."
            },
            new Municipality
            {
                SeedKey = "demo:maple-ridge", Slug = "maple-ridge", Name = "Maple Ridge", Province = "BC",
                ElectionDate = new DateTime(2026, 10, 17),
                Description = "Fictional classroom election dataset for Maple Ridge, featuring neighbourhood connections, recreation, and education."
            },
            new Municipality
            {
                SeedKey = "demo:burnaby", Slug = "burnaby", Name = "Burnaby", Province = "BC",
                ElectionDate = new DateTime(2026, 10, 17),
                Description = "Fictional classroom election dataset for Burnaby, featuring housing, sustainable transportation, and inclusive public spaces."
            },
            new Municipality
            {
                SeedKey = "demo:surrey", Slug = "surrey", Name = "Surrey", Province = "BC",
                ElectionDate = new DateTime(2026, 10, 17),
                Description = "Fictional classroom election dataset for Surrey."
            },
            new Municipality
            {
                SeedKey = "demo:richmond", Slug = "richmond", Name = "Richmond", Province = "BC",
                ElectionDate = new DateTime(2026, 10, 17),
                Description = "Fictional classroom election dataset for Richmond."
            }
        };

        foreach (var sample in municipalities)
        {
            var municipality = await db.Municipalities.SingleOrDefaultAsync(
                m => m.SeedKey == sample.SeedKey, cancellationToken);
            if (municipality is null)
            {
                if (await db.Municipalities.AnyAsync(m => m.Slug == sample.Slug
                    || (m.Name == sample.Name && m.Province == sample.Province), cancellationToken))
                    throw new InvalidOperationException($"Existing municipality conflicts with seed '{sample.SeedKey}'. No records were changed.");
                municipality = sample;
                db.Municipalities.Add(municipality);
                await db.SaveChangesAsync(cancellationToken);
            }

            var seats = sample.Slug is "burnaby" or "surrey" or "richmond"
                ? new[] { 1, 8, 7 } : new[] { 1, 6, sample.Slug == "pitt-meadows" ? 2 : 5 };
            var names = new[] { "Mayor", "Councillor", "School Trustee" };
            var positionKeys = new[] { "mayor", "councillor", "school-trustee" };

            for (var index = 0; index < names.Length; index++)
            {
                var key = $"{sample.SeedKey}:{positionKeys[index]}";
                if (!await db.Positions.AnyAsync(p => p.SeedKey == key, cancellationToken))
                {
                    var name = names[index];
                    if (await db.Positions.AnyAsync(p => p.MunicipalityId == municipality.MunicipalityId && p.Name == name, cancellationToken))
                        throw new InvalidOperationException($"Existing position conflicts with seed '{key}'. No records were changed.");
                    db.Positions.Add(new Position
                    {
                        SeedKey = key, Name = names[index], SeatsAvailable = seats[index],
                        MunicipalityId = municipality.MunicipalityId
                    });
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var candidates = new[]
        {
            ("pitt-meadows", "mayor", "avery-brookfield", "Avery Brookfield", "A fictional community volunteer proposing accessible town halls and improvements to neighbourhood walking routes."),
            ("pitt-meadows", "councillor", "morgan-cedar", "Morgan Cedar", "A fictional recreation coordinator advocating for affordable youth activities and well-maintained community parks."),
            ("pitt-meadows", "councillor", "rowan-fairhaven", "Rowan Fairhaven", "A fictional small-business mentor interested in local shopping areas and transparent municipal budgeting."),
            ("pitt-meadows", "school-trustee", "jamie-willowby", "Jamie Willowby", "A fictional parent volunteer focusing on student wellbeing, accessible learning resources, and safe school journeys."),
            ("maple-ridge", "mayor", "casey-alderwood", "Casey Alderwood", "A fictional neighbourhood organizer proposing better public engagement and long-term recreation planning."),
            ("maple-ridge", "councillor", "taylor-meadowcroft", "Taylor Meadowcroft", "A fictional environmental educator supporting urban tree planting and connected walking trails."),
            ("maple-ridge", "councillor", "quinn-mapleford", "Quinn Mapleford", "A fictional accessibility advocate interested in inclusive civic facilities and reliable local transportation."),
            ("maple-ridge", "school-trustee", "alex-riverstone", "Alex Riverstone", "A fictional library volunteer proposing stronger reading programs and family participation in school planning."),
            ("burnaby", "mayor", "reese-hazelbrook", "Reese Hazelbrook", "A fictional community planner advocating for diverse housing choices and welcoming public gathering spaces."),
            ("burnaby", "councillor", "drew-fernvale", "Drew Fernvale", "A fictional cycling instructor interested in safer intersections and convenient connections to transit."),
            ("burnaby", "councillor", "sage-brambleton", "Sage Brambleton", "A fictional arts volunteer supporting neighbourhood cultural events and accessible recreation programs."),
            ("burnaby", "school-trustee", "parker-elmgrove", "Parker Elmgrove", "A fictional science-club mentor focusing on inclusive classrooms and practical learning opportunities."),
            ("burnaby", "mayor", "alex-cedar", "Alex Cedar", "A fictional advocate for accessible neighbourhood services."),
            ("burnaby", "councillor", "jordan-ash", "Jordan Ash", "A fictional volunteer supporting local recreation."),
            ("burnaby", "councillor", "river-birch", "River Birch", "A fictional organizer supporting community arts."),
            ("burnaby", "school-trustee", "sky-elm", "Sky Elm", "A fictional mentor supporting inclusive learning."),
            ("surrey", "mayor", "avery-pine", "Avery Pine", "A fictional community planner supporting neighbourhood engagement."),
            ("surrey", "mayor", "casey-spruce", "Casey Spruce", "A fictional advocate for accessible municipal services."),
            ("surrey", "councillor", "drew-willow", "Drew Willow", "A fictional organizer supporting parks and recreation."),
            ("surrey", "councillor", "ellis-oak", "Ellis Oak", "A fictional volunteer supporting public spaces."),
            ("surrey", "councillor", "frankie-fir", "Frankie Fir", "A fictional advocate for safe walking routes."),
            ("surrey", "school-trustee", "harper-alder", "Harper Alder", "A fictional parent volunteer supporting student wellbeing."),
            ("surrey", "school-trustee", "indigo-maple", "Indigo Maple", "A fictional mentor supporting science programs."),
            ("surrey", "school-trustee", "jules-poplar", "Jules Poplar", "A fictional library volunteer supporting reading programs."),
            ("richmond", "mayor", "kai-hemlock", "Kai Hemlock", "A fictional planner supporting community participation."),
            ("richmond", "mayor", "lane-cypress", "Lane Cypress", "A fictional volunteer supporting accessible civic facilities."),
            ("richmond", "councillor", "micah-hazel", "Micah Hazel", "A fictional advocate for connected walking trails."),
            ("richmond", "councillor", "noel-cedar", "Noel Cedar", "A fictional organizer supporting local businesses."),
            ("richmond", "councillor", "oakley-beech", "Oakley Beech", "A fictional volunteer supporting community arts."),
            ("richmond", "school-trustee", "peyton-larch", "Peyton Larch", "A fictional mentor supporting inclusive classrooms."),
            ("richmond", "school-trustee", "quinn-redwood", "Quinn Redwood", "A fictional parent volunteer supporting school journeys."),
            ("richmond", "school-trustee", "robin-sycamore", "Robin Sycamore", "A fictional tutor supporting practical learning.")
        };

        foreach (var (municipalityKey, positionKey, candidateKey, name, profile) in candidates)
        {
            var key = $"demo:{candidateKey}";
            if (await db.Candidates.AnyAsync(c => c.SeedKey == key, cancellationToken))
            {
                continue;
            }

            var fullPositionKey = $"demo:{municipalityKey}:{positionKey}";
            var position = await db.Positions.SingleAsync(p => p.SeedKey == fullPositionKey, cancellationToken);
            var normalizedName = name.ToUpperInvariant();
            if (await db.Candidates.AnyAsync(c => c.MunicipalityId == position.MunicipalityId
                && c.Name.Trim().ToUpper() == normalizedName, cancellationToken))
                throw new InvalidOperationException($"Existing candidate conflicts with seed '{key}'. No records were changed.");
            db.Candidates.Add(new Candidate
            {
                SeedKey = key, Name = name, Profile = profile,
                PositionId = position.PositionId, MunicipalityId = position.MunicipalityId,
                DateCreated = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                Website = $"https://example.com/candidates/{candidateKey}",
                Email = $"{candidateKey}@example.com"
            });
        }

        await db.SaveChangesAsync(cancellationToken);

    }
}

