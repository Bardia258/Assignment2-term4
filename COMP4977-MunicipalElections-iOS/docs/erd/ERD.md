# iOS data model

These diagrams show the main election/account data in SQLite and personal
profiles/selections in Firestore. Candidates use positions in the same
municipality; the dotted Firestore relationship represents ownership by email.

## SQLite election and account data

```mermaid
erDiagram
    Municipalities ||--o{ Positions : contains
    Municipalities ||--o{ Candidates : contains
    Positions ||--o{ Candidates : contested_by
    Municipalities |o--o{ AspNetUsers : legacy_admin_assignment

    Municipalities {
        int MunicipalityId PK
        string Name
        string Province
        datetime ElectionDate
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
        int MunicipalityId FK "nullable legacy field"
    }
```

## Firestore documents

```mermaid
erDiagram
    UserProfiles ||..o{ CandidateSelections : owns_by_email

    UserProfiles {
        string email "document ID"
        int municipalityId
        string municipalityName
        timestamp updatedAt
    }
    CandidateSelections {
        string id "email_candidateId"
        string userEmail
        int candidateId
        string candidateName
        int municipalityId
        string municipalityName
        int positionId "optional"
        string positionName
        string photoUrl "optional"
        string notes
        timestamp dateCreated
        timestamp dateModified
    }
```

