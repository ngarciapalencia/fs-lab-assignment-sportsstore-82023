# SportsStore Modernisation Project

## Overview
Modernised SportsStore application upgraded from .NET 6 to .NET 10, with Serilog logging, Stripe payment integration, and GitHub Actions CI.

## Upgrade to .NET 10
- Updated target framework to net10.0
- Updated NuGet packages
- Resolved compatibility issues
- Verified build and tests

## Logging Setup
- Integrated Serilog
- Console sink enabled
- Rolling file sink enabled
- Logging added for startup, checkout flow, order creation, and exceptions

## Stripe Configuration
- Implemented Stripe test payment workflow
- Keys are loaded from configuration/environment variables
- No secrets are committed to source control

## CI Pipeline
- GitHub Actions runs on push to main and pull requests
- Restores dependencies
- Builds the solution
- Runs automated tests

## How to Run Locally
1. Clone the repository
2. Restore dependencies
3. Configure Stripe test keys
4. Run the application

## Testing
Run:
```bash
dotnet test
