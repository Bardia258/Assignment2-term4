# Municipal Elections

This repository contains both the COMP4976 C# project and the COMP4977 iOS project.

- **COMP4976:** [C# setup](COMP4976-MunicipalElections-CSharp/docs/README.md) · [Architecture](COMP4976-MunicipalElections-CSharp/docs/architecture/ARCHITECTURE.md) · [ERD](COMP4976-MunicipalElections-CSharp/docs/erd/ERD.md) · [API reference](COMP4976-MunicipalElections-CSharp/docs/api/API-ENDPOINTS.md).
- **COMP4977:** [iOS setup](COMP4977-MunicipalElections-iOS/docs/README.md) · [Architecture](COMP4977-MunicipalElections-iOS/docs/architecture/ARCHITECTURE.md) · [SQLite/Firestore model](COMP4977-MunicipalElections-iOS/docs/erd/ERD.md) · [API reference](COMP4977-MunicipalElections-iOS/docs/api/API-ENDPOINTS.md).

Assignment 2 running and hosting:

- **COMP4976 runs locally.** Install the .NET 10 SDK and start Docker Desktop with Linux containers. Open `COMP4976-MunicipalElections-CSharp/MEMS.sln`, choose `MEMS.AppHost` as the startup project, and run its `https` profile. Aspire starts SQL Server, the API, Admin, and Public sites; migrations and development accounts are prepared automatically. The five local Admin accounts use `P@$$w0rd`. Accept the development HTTPS certificate prompt if shown. This solution does not need Azure deployment.
- **COMP4977 uses a hosted backend.** Open `COMP4977-MunicipalElections-iOS/MunicipalElections.slnx` for the SQLite/JWT backend. Only `MunicipalElections.Web` is deployed to the Linux Azure Web App `assignment2-term4`, using a published Linux ZIP. The existing GitHub workflow still targets the previous Windows app. This backend does not need the MEMS SQL Server container or Docker Desktop. The SwiftUI app also uses Firebase Authentication and Firestore for its profile and saved selections.

Current iOS backend address: [Azure service](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/). Deployment, migrations and seeding have been completed; live directory reads returned the expected sample data.

API: [View municipalities](https://assignment2-term4-dbe7c6evbpajetbf.westus3-01.azurewebsites.net/api/municipalities). The iOS app uses the same Azure address with `/api/...` routes.

Developed by **Bardia Bashiri** and **Omar Mohamad**.

## Contributions

- **Bardia Bashiri:** Responsible for backend development, including the C# API, database, admin functionality, and Azure deployment.
- **Omar Mohamad:** Responsible for frontend development, including the SwiftUI iOS app and the public-facing C# website.

## Project status

The two course folders contain separate backends: C# uses SQL Server and
Identity cookies; iOS uses SQLite/JWT plus Firebase. Their databases and login
sessions are independent. See each setup guide for current functionality and
known limitations. The new Azure directory endpoints have been checked; native
iOS/Firebase runtime checks and authenticated login remain unverified on this deployment.

## Assignment 1 history

The original project used an MVC/SQLite application with a shared iOS API.
Azure deployment was a main challenge and deployment issues were resolved
during that work. For Assignment 2, the moved `MunicipalElections.Web` project
is API-only; the C# administrative/public websites are the separate MEMS solution.
