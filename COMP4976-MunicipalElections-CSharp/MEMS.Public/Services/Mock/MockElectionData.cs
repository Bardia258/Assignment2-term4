namespace MEMS.Public.Services.Mock;

/// <summary>
/// In-memory sample data used by the mock API clients until the real MEMS.Api is merged.
/// Fictional data shaped like the backend's development seed (Burnaby, Surrey, Richmond),
/// with the same positions and seat counts. All people, websites, and emails are made up.
/// Richmond intentionally has no School Trustee candidates, to exercise empty states.
/// </summary>
internal static class MockElectionData
{
    private static readonly DateTime ElectionDate = new(2026, 10, 17);
    private static readonly DateTime NominationsOpened = new(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<MunicipalityResponse> Municipalities =
    [
        new(1, "Burnaby", "burnaby", "BC", ElectionDate,
            "Fictional classroom election dataset for Burnaby, featuring housing, sustainable transportation, and inclusive public spaces.",
            null, Revision(1)),
        new(2, "Richmond", "richmond", "BC", ElectionDate,
            "Fictional classroom election dataset for Richmond, featuring flood protection, farmland preservation, and community safety.",
            null, Revision(2)),
        new(3, "Surrey", "surrey", "BC", ElectionDate,
            "Fictional classroom election dataset for Surrey, featuring rapid transit, policing, and fast-growing neighbourhoods.",
            null, Revision(3)),
    ];

    // Same names and seat counts the backend seeds for Burnaby, Surrey, and Richmond.
    public static readonly IReadOnlyList<PositionResponse> Positions =
    [
        Position(1, "Mayor", 1, municipalityId: 1),
        Position(2, "Councillor", 8, municipalityId: 1),
        Position(3, "School Trustee", 7, municipalityId: 1),
        Position(4, "Mayor", 1, municipalityId: 2),
        Position(5, "Councillor", 8, municipalityId: 2),
        Position(6, "School Trustee", 7, municipalityId: 2),
        Position(7, "Mayor", 1, municipalityId: 3),
        Position(8, "Councillor", 8, municipalityId: 3),
        Position(9, "School Trustee", 7, municipalityId: 3),
    ];

    public static readonly IReadOnlyList<CandidateResponse> Candidates = BuildCandidates();

    private static IReadOnlyList<CandidateResponse> BuildCandidates()
    {
        var list = new List<CandidateResponse>();
        void Add(int positionId, string name, string profile, string? website = null, string? email = null)
        {
            var position = Positions.Single(p => p.Id == positionId);
            var municipality = Municipalities.Single(m => m.Id == position.MunicipalityId);
            var id = list.Count + 1;
            list.Add(new CandidateResponse(id, name, profile, PhotoUrl: null, website, email,
                NominationsOpened.AddHours(id * 7), municipality.Id, municipality.Name, position.Id,
                position.Name, municipality, position, Revision(100 + id)));
        }

        // ---- Burnaby ----
        Add(1, "Alicia Moreno",
            "Two-term councillor and former transit planner. Wants more frequent bus service on Hastings and Kingsway, and faster approvals for non-market housing near SkyTrain stations.\n\nGrew up in Edmonds and still coaches youth soccer there.",
            "https://alicia-moreno.example.org", "hello@alicia-moreno.example.org");
        Add(1, "Daniel Cho",
            "Small-business owner running on keeping property taxes predictable, reopening the Metrotown library branch on Sundays, and publishing every council vote in plain language online.",
            email: "daniel.cho@example.com");
        Add(1, "Priya Natarajan",
            "Climate researcher and community-garden organizer. Her platform focuses on tree canopy targets, protected bike lanes, and a citywide heat-response plan for seniors.",
            "https://priya-for-burnaby.example.org");

        Add(2, "Marcus Bell", "Firefighter for 18 years. Running to fund more community safety officers and improve emergency response times in North Burnaby.");
        Add(2, "Fatima Haddad", "Settlement worker who helps newcomer families find housing and work. Wants more multilingual city services and affordable child care spaces.",
            email: "fatima.haddad@example.com");
        Add(2, "Kevin O'Neill", "Retired civil engineer focused on fixing ageing water mains and making sure new towers come with parks and schools.");
        Add(2, "Sofia Lindqvist", "Arts administrator who wants to turn empty storefronts into low-cost studios and expand free evening programs at community centres.",
            "https://sofia-lindqvist.example.org");
        Add(2, "Tomás Reyes", "Renter and tenant-rights advocate pushing for stronger renoviction protections and a public rental registry.");
        Add(2, "Grace Wong", "Accountant running on transparent budgets, a citizen budget panel, and quarterly online town halls.");

        Add(3, "Hannah Brooks", "Parent of three and PAC chair. Priorities: seismic upgrades, smaller class sizes, and safer walking routes to school.");
        Add(3, "Arjun Mehta", "High-school math teacher on leave. Wants more counsellors and mental-health supports in every secondary school.",
            email: "arjun.mehta@example.com");
        Add(3, "Leila Farah", "Speech-language pathologist advocating for earlier screening and better support for students with diverse learning needs.");
        Add(3, "Owen Clarke", "Youth sports coach focused on opening school gyms and fields to the community after hours.");

        // ---- Richmond (no School Trustee candidates yet) ----
        Add(4, "Helen Park",
            "Current councillor and former emergency-management coordinator. Running on dike upgrades, a long-term flood strategy, and protecting farmland from speculation.",
            "https://helen-park.example.org", "team@helen-park.example.org");
        Add(4, "Raj Sandhu",
            "Farmer and agricultural-land advocate who wants to keep the ALR for food production and bring a year-round farmers' market to the city centre.");

        Add(5, "Elaine Fraser", "Retired nurse focused on seniors' housing, accessible transit stops, and more benches along the dyke trails.");
        Add(5, "Michael Tan", "Software developer who wants online permits, open data, and a 311 app for reporting potholes and graffiti.",
            "https://michaeltan.example.org");
        Add(5, "Natalie Ruiz", "Restaurant owner running on support for small business and fewer empty storefronts on No. 3 Road.");
        Add(5, "Samuel Osei", "Community-safety volunteer pushing for better street lighting and more neighbourhood policing.");

        // ---- Surrey ----
        Add(7, "Jasmine Gill",
            "Urban planner and transit advocate. Wants SkyTrain to Langley finished on schedule, frequent bus service on 152 Street, and complete neighbourhoods in Cloverdale and Clayton.",
            "https://jasmine-gill.example.org", "contact@jasmine-gill.example.org");
        Add(7, "Robert Lawson",
            "Retired police superintendent running on community policing, youth programs, and a clear plan for public-safety funding.");

        Add(8, "Amandeep Dhillon", "Pharmacist and Newton business-association member. Priorities: safer streets and faster business licensing.");
        Add(8, "Chloe Martin", "Environmental educator who wants to protect the Serpentine and Nicomekl watersheds and plant 50,000 street trees.",
            email: "chloe.martin@example.com");
        Add(8, "Ibrahim Yusuf", "Housing co-op board member pushing for more family-sized rental homes and fair rezoning near transit.");
        Add(8, "Megan Taylor", "Paramedic focused on emergency-room wait times, overdose prevention, and better ambulance coordination with the province.");
        Add(8, "Victor Nguyen", "Contractor who wants simpler permits for secondary suites and laneway homes.");

        Add(9, "Rachel Adams", "Former teacher-librarian running to end portable classrooms and speed up new school construction in fast-growing areas.");
        Add(9, "Gurpreet Kaur", "Parent advocate for expanded before- and after-school care and more school meal programs.");
        Add(9, "Liam Douglas", "University student and former student-council president who wants a student voice on the school board.",
            "https://liam-douglas.example.org");

        return list;
    }

    private static PositionResponse Position(int id, string name, int seats, int municipalityId) =>
        new(id, name, seats, municipalityId, Municipalities.Single(m => m.Id == municipalityId).Name, Revision(10 + id));

    /// <summary>Fake 32-character lowercase hex revision, matching the real API's format.</summary>
    private static string Revision(int seed) => seed.ToString("x32");
}
