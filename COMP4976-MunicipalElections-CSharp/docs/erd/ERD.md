# C# database ERD

The active election and administrator model in `MEMS.Data` uses SQL Server and
EF Core Code First. The diagram shows core fields; nullable images/contact
fields, framework Identity details and seed bookkeeping are summarized below.

```mermaid
erDiagram
    Municipalities ||--o{ Positions : contains
    Municipalities ||--o{ Candidates : contains
    Positions ||--o{ Candidates : contested_by
    Municipalities |o--o{ AspNetUsers : assigned_to

    Municipalities {
        int MunicipalityId PK
        string Name
        string Slug UK
        string Province
        date ElectionDate
        string Description
        string LogoFileName
    }
    Positions {
        int PositionId PK
        string Name
        int SeatsAvailable
        int MunicipalityId FK
    }
    Candidates {
        int CandidateId PK
        string Name
        string Profile
        string PhotoFileName
        string Website
        string Email
        datetime DateCreated
        int PositionId FK
        int MunicipalityId FK
    }
    AspNetUsers {
        string Id PK
        string UserName
        string FullName
        string Email
        int AdministratorType
        int MunicipalityId FK "nullable"
    }
```

## Relationships and integrity

- Each position belongs to one municipality. Each candidate belongs to one
  municipality and one position in that same municipality.
- The composite foreign key `Candidates(PositionId, MunicipalityId)` references
  `Positions(PositionId, MunicipalityId)`. SQL Server rejects a candidate linked
  to another municipality's position, even outside the UI or repository.
- Municipality slugs are unique, lowercase URL segments. Municipality
  name/province and position name/municipality combinations are also unique.
