# MSIX Packaging for GlassManagement Desktop (MAUI Blazor)

- Purpose: Provide a repeatable, enterprise-friendly packaging flow for the desktop app.
- Scope: Build, sign, package, and distribute via MSIX inside the corporate network.

## Prerequisites
- Visual Studio with MAUI workload installed
- .NET 8/10 SDK (matching project requirements)
- MSIX packaging tooling installed (via Windows SDK)
- Signing certificate (PFX) for code signing

## Build and Publish (local dev)
- Open the GlassManagement.Desktop.Blazor solution in Visual Studio
- Build -> Publish -> MSIX (local) or use the provided packaging script
- Ensure the publish output contains the MAUI Blazor desktop bundle

## Packaging Script (basic workflow)
- The repository includes a basic PrepareMsix.ps1 and PackageMsix.ps1 under GlassManagement.Desktop.Blazor/Packaging
- Customize paths and signing settings as per your org conventions
- Example usage:
  - powershell -File PrepareMsix.ps1 -PublishDir "./publish" -OutputDir "./msix-output" -CertPath "C:\certs\signing.pfx" -CertPassword "<password>"
- The actual packaging to MSIX should be implemented in your CI/CD using these templates

## Distribution
- Internal enterprise distribution (Intune / SCCM) recommended
- Add update mechanism to push new MSIX packages and keep track of versions

## Notes
- This is a scaffolding guide. You should extend it with your organization’s security, signing, and distribution policies.
