# SMHS-Cal

SMHS-Cal is a glass curtain wall calculation system for Chinese building standards.

## Overview

- Backend: .NET 10 ASP.NET Core Web API
- Frontend: Blazor Server and Avalonia Desktop
- Database: SQLite
- Main scope: glass, mullion, transom, connection, embed part, sealant, panel, and report generation

## Features

- Glass calculation and validation
- Aluminum mullion calculation
- Aluminum transom calculation
- Connection, embed part, and sealant calculations
- Calculation book generation
- Database-driven material and profile selection

## Quick Start

```powershell
cd src/GlassManagement.Api
dotnet run
```

API and web UI are available from the ASP.NET Core host. See `src/README.md` and `PROJECT.md` for more detail.

## Database Files

- `src/GlassManagement.Api/Data/SMHS_Region.db`
- `src/GlassManagement.Api/Data/SMHS_Glass.db`
- `src/GlassManagement.Api/Data/SMHS_Profile.db`
- `src/GlassManagement.Api/Data/SMHS_Bolt.db`
- `src/GlassManagement.Api/Data/SMHS_Material.db`
- `src/GlassManagement.Api/Data/SMHS_User.db`

## Project Status

Current progress and recent validation notes are tracked in `SMHS_PROJECT_STATUS.md`.

## Copyright

Copyright (c) 2026 ey2h.
All rights reserved.

