# Municipal Elections

This repository contains both the COMP4976 C# project and the COMP4977 iOS project.

- **COMP4976:** [C# setup](COMP4976-MunicipalElections-CSharp/docs/README.md) · [Architecture](COMP4976-MunicipalElections-CSharp/docs/architecture/ARCHITECTURE.md) · [ERD](COMP4976-MunicipalElections-CSharp/docs/erd/ERD.md) · [API reference](COMP4976-MunicipalElections-CSharp/docs/api/API-ENDPOINTS.md).
- **COMP4977:** [Architecture](COMP4977-MunicipalElections-iOS/docs/architecture/ARCHITECTURE.md) · [SQLite/Firestore model](COMP4977-MunicipalElections-iOS/docs/erd/ERD.md) · [API reference](COMP4977-MunicipalElections-iOS/docs/api/API-ENDPOINTS.md).

Assignment 2 running and hosting:

- **COMP4976 runs locally.** Install the .NET 10 SDK and start Docker Desktop with Linux containers. Open `COMP4976-MunicipalElections-CSharp/MEMS.sln`, choose `MEMS.AppHost` as the startup project, and run its `https` profile. Aspire starts SQL Server, the API, Admin, and Public sites; migrations and development accounts are prepared automatically. The local Admin accounts use `P@$$w0rd` to log in. Accept the development HTTPS certificate prompt if shown. This solution does not use Azure deployment.
- **COMP4977 uses a hosted backend.** Open `COMP4977-MunicipalElections-iOS/MunicipalElections.slnx` for the SQLite/JWT backend. Only `MunicipalElections.Web` is deployed to the Azure Web App. Docker Desktop and the MEMS SQL Server container are not required. The SwiftUI iOS frontend is located in `COMP4977-MunicipalElections-iOS/MunicipalElections` and uses Firebase Authentication and Firestore for user profiles and saved selections.

Current iOS backend address: [Azure service](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/). Deployment, migrations and seeding have been completed; live directory reads returned the expected sample data.

API: [View municipalities](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/api/municipalities). The iOS app uses the same Azure address with `/api/...` routes.

Developed by **Bardia Bashiri** and **Omar Mohamad**.

## Contributions

- **Bardia Bashiri:** Responsible for backend development, including the C# API, database, admin functionality, and Azure deployment.
- **Omar Mohamad:** Responsible for frontend development, including the SwiftUI iOS app and the public-facing C# website.

